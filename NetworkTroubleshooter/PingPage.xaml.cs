using System;
using System.Collections.Generic;
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

public partial class PingPage : ContentPage
{
	private CancellationTokenSource _pingCts;

	public PingPage()
	{
		InitializeComponent();
	}

	private void OnPingClicked(object sender, EventArgs e)
	{
		if (_pingCts != null) // Ping is running, so stop it
		{
			SetStatusAsync("Stopping ping..."); // Use async helper
			_pingCts.Cancel();
		}
		else // Start ping
		{
			// --- Validate Inputs ---
			string hostname = HostnameEntry.Text?.Trim();
			if (string.IsNullOrWhiteSpace(hostname))
			{
				StatusLabel.Text = "Please enter a hostname or IP address.";
				return;
			}

			if (!int.TryParse(PingCountEntry.Text, out int pingCount) || pingCount <= 0)
			{
				StatusLabel.Text = "Please enter a valid number of pings (> 0).";
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
			SetUiState(isPinging: true);

			_pingCts = new CancellationTokenSource();
			var cancellationToken = _pingCts.Token;

			// Run ping logic in background task
			Task.Run(async () => await ExecutePingAsync(hostname, pingCount, timeout, cancellationToken), cancellationToken);
		}
	}

	private async Task ExecutePingAsync(string hostname, int pingCount, int timeout, CancellationToken cancellationToken)
	{
		var roundTripTimes = new List<long>();
		int packetsSent = 0;
		int packetsReceived = 0;

		try
		{
			// Resolve hostname once before starting
			string targetIp = ResolveHostname(hostname);
			await AppendResultAsync($"Pinging {hostname} [{targetIp}] with {pingCount} requests:\n");

			using (var pingSender = new Ping())
			{
				for (int i = 0; i < pingCount; i++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						await AppendResultAsync("\nPing operation cancelled by user.");
						break;
					}

					packetsSent++;
					PingReply reply = null;
					string resultLine;
					try
					{
						reply = await pingSender.SendPingAsync(hostname, timeout); // Use original hostname/IP input
						
						if (reply.Status == IPStatus.Success)
						{
							packetsReceived++;
							roundTripTimes.Add(reply.RoundtripTime);
							resultLine = $"Reply from {reply.Address}: bytes={reply.Buffer?.Length} time={reply.RoundtripTime}ms TTL={reply.Options?.Ttl}";
						}
						else
						{
							resultLine = $"Request failed: {reply.Status}";
						}
					}
					catch (PingException pex) when (pex.InnerException is SocketException sex && sex.SocketErrorCode == SocketError.HostNotFound)
					{
						 resultLine = $"Ping Error: Could not resolve host '{hostname}'.";
						 await AppendResultAsync(resultLine);
						 break; // Stop if host cannot be resolved
					}
					catch (PingException pex)
					{
						resultLine = $"Ping Error: {pex.InnerException?.Message ?? pex.Message}";
					}
					catch (SocketException sox) // Should be caught by PingException mostly, but just in case
					{
						resultLine = $"Socket Error: {sox.Message}";
					}
					catch (Exception ex) // Catch other potential exceptions from SendPingAsync
					{
						resultLine = $"Error during ping: {ex.Message}";
					}

					await AppendResultAsync(resultLine);

					// Optional delay between pings, consider cancellation
					if (i < pingCount - 1)
					{
						try
						{
							await Task.Delay(500, cancellationToken); 
						}
						catch (TaskCanceledException) 
						{
							 await AppendResultAsync("\nPing operation cancelled during delay.");
							 break; 
						}
					}
				}
			}

			// Calculate and Display Summary (only if not cancelled early)
			if (!cancellationToken.IsCancellationRequested)
			{
				await AppendResultAsync("\nPing statistics:");
				double loss = packetsSent > 0 ? (double)(packetsSent - packetsReceived) / packetsSent * 100 : 0;
				await AppendResultAsync($"  Packets: Sent = {packetsSent}, Received = {packetsReceived}, Lost = {packetsSent - packetsReceived} ({loss:F1}% loss)");

				if (roundTripTimes.Any())
				{
					await AppendResultAsync("Approximate round trip times in milli-seconds:");
					await AppendResultAsync($"  Minimum = {roundTripTimes.Min()}ms, Maximum = {roundTripTimes.Max()}ms, Average = {roundTripTimes.Average():F0}ms");
				}
			}
		}
		catch (TaskCanceledException) // Catch cancellation during initial setup/resolve
		{
			await SetStatusAsync("Ping operation cancelled.");
		}
		catch (Exception ex)
		{
			// Handle exceptions occurring outside the loop (e.g., Ping initialization, initial DNS resolution)
			await SetStatusAsync($"An error occurred: {ex.Message}");
		}
		finally
		{
			// Always reset UI state on the main thread
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SetUiState(isPinging: false);
				_pingCts?.Dispose();
				_pingCts = null; // Signal that ping is no longer running
			});
		}
	}

	private string ResolveHostname(string hostname)
	{
		try
		{
			// Attempt to resolve first available address 
			IPAddress[] addresses = Dns.GetHostAddresses(hostname);
			return addresses.FirstOrDefault()?.ToString() ?? hostname; // Return first resolved or original
		}
		catch { return hostname; } // Return original hostname if resolution fails
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

	private void SetUiState(bool isPinging)
	{
		HostnameEntry.IsEnabled = !isPinging;
		PingCountEntry.IsEnabled = !isPinging;
		TimeoutEntry.IsEnabled = !isPinging;
		LoadingIndicator.IsVisible = isPinging;
		LoadingIndicator.IsRunning = isPinging;
		PingButton.Text = isPinging ? "Stop Ping" : "Start Ping";
		if (!isPinging)
		{
			// Only clear status label if it wasn't an error/cancellation message set by ExecutePingAsync
			if (!StatusLabel.Text.StartsWith("Stopping") && !StatusLabel.Text.StartsWith("Ping error") && !StatusLabel.Text.StartsWith("An error"))
			{
				 StatusLabel.Text = string.Empty; 
			}
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (_pingCts != null)
		{
			_pingCts.Cancel(); // Cancel any ongoing ping when leaving the page
			// UI cleanup happens in the finally block of ExecutePingAsync
		}
	}
} 