using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace NetworkTroubleshooter;

public partial class TraceroutePage : ContentPage
{
	private CancellationTokenSource _tracerouteCts;

	public TraceroutePage()
	{
		InitializeComponent();
	}

	private void OnTracerouteClicked(object sender, EventArgs e)
	{
		if (_tracerouteCts != null) // Traceroute is running, so stop it
		{
			SetStatusAsync("Stopping traceroute...");
			_tracerouteCts.Cancel();
		}
		else // Start traceroute
		{
			// --- Validate Inputs ---
			string hostname = HostnameEntry.Text?.Trim();
			if (string.IsNullOrWhiteSpace(hostname))
			{
				StatusLabel.Text = "Please enter a hostname or IP address.";
				return;
			}

			if (!int.TryParse(MaxHopsEntry.Text, out int maxHops) || maxHops <= 0 || maxHops > 128) // Added upper bound
			{
				StatusLabel.Text = "Please enter a valid max hops value (1-128).";
				return;
			}

			if (!int.TryParse(TimeoutEntry.Text, out int timeout) || timeout <= 0)
			{
				StatusLabel.Text = "Please enter a valid timeout in milliseconds (> 0).";
				return;
			}
			// -----------------------

			ResultsEditor.Text = string.Empty;
			StatusLabel.Text = string.Empty;
			SetUiState(isTracing: true);

			_tracerouteCts = new CancellationTokenSource();
			var cancellationToken = _tracerouteCts.Token;

			// Run traceroute logic in background task
			Task.Run(async () => await ExecuteTracerouteAsync(hostname, maxHops, timeout, cancellationToken), cancellationToken);
		}
	}

	private async Task ExecuteTracerouteAsync(string hostname, int maxHops, int timeout, CancellationToken cancellationToken)
	{
		IPAddress targetIpAddress = null;
		try
		{
			// Resolve hostname first
			IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostname, cancellationToken);
			targetIpAddress = addresses.FirstOrDefault();
			if (targetIpAddress == null)
			{
				await SetStatusAsync($"Could not resolve hostname: {hostname}");
				return;
			}

			await AppendResultAsync($"Tracing route to {hostname} [{targetIpAddress}] over a maximum of {maxHops} hops:\n");

			byte[] buffer = new byte[32]; // Standard ping buffer
			var stopwatch = new Stopwatch();

			using (var pingSender = new Ping())
			{
				for (int ttl = 1; ttl <= maxHops; ttl++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						await AppendResultAsync("\nTraceroute cancelled by user.");
						break;
					}

					var options = new PingOptions(ttl, true); // Don't fragment
					stopwatch.Restart();
					PingReply reply = null;
					string hopAddressStr = "*";
					string hopHostName = string.Empty;
					long roundTripTime = -1;
					
					try
					{
						reply = await pingSender.SendPingAsync(targetIpAddress, timeout, buffer, options);
						stopwatch.Stop();
						roundTripTime = stopwatch.ElapsedMilliseconds;

						if (reply.Status == IPStatus.Success)
						{
							hopAddressStr = reply.Address.ToString();
							// Try reverse DNS lookup (optional)
							try { hopHostName = (await Dns.GetHostEntryAsync(reply.Address, cancellationToken)).HostName; }
							catch { /* Ignore lookup failure */ }
							await AppendResultAsync(FormatHop(ttl, roundTripTime, hopAddressStr, hopHostName));
							await AppendResultAsync("\nTrace complete.");
							break; // Destination reached
						}
						else if (reply.Status == IPStatus.TtlExpired || reply.Status == IPStatus.TimeExceeded) // TtlExpired is obsolete, TimeExceeded used instead in some cases
						{
							 hopAddressStr = reply.Address.ToString();
							 // Try reverse DNS lookup (optional)
							try { hopHostName = (await Dns.GetHostEntryAsync(reply.Address, cancellationToken)).HostName; }
							catch { /* Ignore lookup failure */ }
							 await AppendResultAsync(FormatHop(ttl, roundTripTime, hopAddressStr, hopHostName));
						}
						else if (reply.Status == IPStatus.TimedOut)
						{
							await AppendResultAsync(FormatHop(ttl, -1, "Request timed out."));
						}
						else
						{
							await AppendResultAsync(FormatHop(ttl, -1, $"Failed: {reply.Status}"));
						}
					}
					 catch (TaskCanceledException) // Catch timeout from SendPingAsync itself or our cancellation
					 {
						 if (cancellationToken.IsCancellationRequested)
						 {
							  await AppendResultAsync("\nTraceroute cancelled by user.");
							 break;
						 }
						 else
						 {
							await AppendResultAsync(FormatHop(ttl, -1, "Request timed out."));
						 }
					 }
					catch (PingException pex) 
					{
						await AppendResultAsync(FormatHop(ttl, -1, $"Ping Error: {pex.InnerException?.Message ?? pex.Message}"));
					}
					catch (Exception ex) // Catch other potential errors
					{
						 await AppendResultAsync(FormatHop(ttl, -1, $"Error: {ex.Message}"));
						 // Consider stopping if a critical error occurs
					}
					 if (ttl == maxHops && reply?.Status != IPStatus.Success)
					 {
						 await AppendResultAsync("\nTrace incomplete (max hops reached).");
					 }
				}
			}
		}
		catch (TaskCanceledException) 
		{
			 await SetStatusAsync("Traceroute cancelled.");
		}
		catch (SocketException sockEx) when (sockEx.SocketErrorCode == SocketError.HostNotFound)
		{
			 await SetStatusAsync($"Could not resolve hostname: {hostname}");
		}
		catch (Exception ex)
		{
			await SetStatusAsync($"An error occurred: {ex.Message}");
		}
		finally
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SetUiState(isTracing: false);
				_tracerouteCts?.Dispose();
				_tracerouteCts = null;
			});
		}
	}

	private string FormatHop(int hopNumber, long rtt, string address, string hostname = null)
	{
		string rttStr = rtt < 0 ? "   *   " : $"{rtt,4} ms"; // Format RTT or show timeout
		string displayAddress = string.IsNullOrWhiteSpace(hostname) || hostname == address ? address : $"{address} [{hostname}]";
		return $"{hopNumber,2} {rttStr} {displayAddress}";
	}

	private Task AppendResultAsync(string text)
	{
		return MainThread.InvokeOnMainThreadAsync(() =>
		{
			ResultsEditor.Text += text + Environment.NewLine;
		});
	}

	private Task SetStatusAsync(string text)
	{
		return MainThread.InvokeOnMainThreadAsync(() =>
		{
			StatusLabel.Text = text;
		});
	}

	private void SetUiState(bool isTracing)
	{
		HostnameEntry.IsEnabled = !isTracing;
		MaxHopsEntry.IsEnabled = !isTracing;
		TimeoutEntry.IsEnabled = !isTracing;
		LoadingIndicator.IsVisible = isTracing;
		LoadingIndicator.IsRunning = isTracing;
		TracerouteButton.Text = isTracing ? "Stop Traceroute" : "Start Traceroute";
		if (!isTracing)
		{
			 if (!StatusLabel.Text.StartsWith("Stopping")) // Avoid clearing cancellation message
			 {
				 StatusLabel.Text = string.Empty; 
			 }
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (_tracerouteCts != null)
		{
			_tracerouteCts.Cancel();
		}
	}
} 