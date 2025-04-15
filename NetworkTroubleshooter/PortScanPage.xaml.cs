using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace NetworkTroubleshooter;

public partial class PortScanPage : ContentPage
{
	private CancellationTokenSource _scanCts;

	public PortScanPage()
	{
		InitializeComponent();
	}

	private void OnScanClicked(object sender, EventArgs e)
	{
		if (_scanCts != null) // Scan is running, so stop it
		{
			SetStatusAsync("Stopping scan...");
			_scanCts.Cancel();
		}
		else // Start scan
		{
			// --- Validate Inputs ---
			string hostname = HostnameEntry.Text?.Trim();
			if (string.IsNullOrWhiteSpace(hostname))
			{
				StatusLabel.Text = "Please enter a hostname or IP address.";
				return;
			}

			string portsInput = PortsEntry.Text?.Trim();
			if (string.IsNullOrWhiteSpace(portsInput))
			{
				 StatusLabel.Text = "Please enter ports to scan.";
				 return;
			}

			List<int> portsToScan;
			try
			{
				portsToScan = ParsePorts(portsInput);
				 if (!portsToScan.Any())
				 {
					 StatusLabel.Text = "No valid ports found in the input.";
					 return;
				 }
			}
			catch (Exception ex)
			{
				 StatusLabel.Text = $"Error parsing ports: {ex.Message}";
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
			SetUiState(isScanning: true);

			_scanCts = new CancellationTokenSource();
			var cancellationToken = _scanCts.Token;

			// Run scan logic in background task
			Task.Run(async () => await ExecutePortScanAsync(hostname, portsToScan, timeout, cancellationToken), cancellationToken);
		}
	}

	private List<int> ParsePorts(string portsInput)
	{
		var ports = new HashSet<int>(); // Use HashSet to avoid duplicates
		if (string.IsNullOrWhiteSpace(portsInput)) return ports.ToList();

		string[] parts = portsInput.Split(',');

		foreach (string part in parts)
		{
			string trimmedPart = part.Trim();
			if (string.IsNullOrEmpty(trimmedPart)) continue;

			if (trimmedPart.Contains('-'))
			{
				// Handle range
				string[] rangeParts = trimmedPart.Split('-');
				if (rangeParts.Length == 2 && 
					int.TryParse(rangeParts[0].Trim(), out int startPort) && 
					int.TryParse(rangeParts[1].Trim(), out int endPort) &&
					startPort > 0 && endPort <= 65535 && startPort <= endPort)
				{
					for (int port = startPort; port <= endPort; port++)
					{
						ports.Add(port);
					}
				}
				else
				{
					throw new FormatException($"Invalid port range format: {trimmedPart}");
				}
			}
			else
			{
				// Handle single port
				if (int.TryParse(trimmedPart, out int port) && port > 0 && port <= 65535)
				{
					ports.Add(port);
				}
				 else
				 {
					  throw new FormatException($"Invalid port number: {trimmedPart}");
				 }
			}
		}
		return ports.OrderBy(p => p).ToList(); // Return sorted list
	}

	private async Task ExecutePortScanAsync(string hostname, List<int> portsToScan, int timeout, CancellationToken cancellationToken)
	{
		await AppendResultAsync($"Starting TCP port scan for {hostname}...");

		try
		{
			foreach (int port in portsToScan)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					await AppendResultAsync("\nScan cancelled by user.");
					break;
				}

				string status = "Unknown";
				Stopwatch stopwatch = Stopwatch.StartNew();
				try
				{
					using (var tcpClient = new TcpClient())
					{
						var connectTask = tcpClient.ConnectAsync(hostname, port);
						// Wait for connection attempt with timeout and cancellation
						await connectTask.WaitAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);
						
						// If WaitAsync completes without exception, connection was successful (or failed fast)
						if (tcpClient.Connected) 
						{
						   status = "Open";
						   tcpClient.Close(); // Ensure connection is closed 
						}
						else
						{
							// This state might be less common if WaitAsync times out first
							status = "Filtered/Error"; 
						}
					}
				}
				 catch (OperationCanceledException) // Handles both cancellationToken and WaitAsync timeout
				 {
					 if (cancellationToken.IsCancellationRequested)
					 {
						 await AppendResultAsync("\nScan cancelled by user.");
						 break;
					 }
					 else
					 {
						  status = "Filtered/Timeout";
					 }
				 }
				 catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
				 {
					 status = "Closed";
				 }
				  catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound)
				  {
					  await AppendResultAsync($"Error: Could not resolve host '{hostname}'. Stopping scan.");
					  break; // Stop scan if host resolution fails
				  }
				  catch (SocketException ex)
				  {
					  status = $"Error ({ex.SocketErrorCode})";
				  }
				  catch (Exception ex)
				  {
					  status = $"Error ({ex.GetType().Name})";
				  }
				finally
				{
					stopwatch.Stop();
				}

				 await AppendResultAsync($"Port {port}: {status} ({stopwatch.ElapsedMilliseconds}ms)");
				 
				 // Small delay to prevent overwhelming the system/network, optional
				 // await Task.Delay(10, cancellationToken);
			}
			
			if (!cancellationToken.IsCancellationRequested)
			{
				await AppendResultAsync("\nScan complete.");
			}

		}
		catch (TaskCanceledException)
		{
			await SetStatusAsync("Scan operation cancelled."); // Catch cancellation during setup
		}
		catch (Exception ex)
		{
			 await SetStatusAsync($"An error occurred during scan setup: {ex.Message}");
		}
		finally
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SetUiState(isScanning: false);
				_scanCts?.Dispose();
				_scanCts = null;
			});
		}
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

	private void SetUiState(bool isScanning)
	{
		HostnameEntry.IsEnabled = !isScanning;
		PortsEntry.IsEnabled = !isScanning;
		TimeoutEntry.IsEnabled = !isScanning;
		LoadingIndicator.IsVisible = isScanning;
		LoadingIndicator.IsRunning = isScanning;
		ScanButton.Text = isScanning ? "Stop Scan" : "Start Scan";
		if (!isScanning)
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
		if (_scanCts != null)
		{
			_scanCts.Cancel();
		}
	}
} 