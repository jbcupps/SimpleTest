using System;
using System.Collections.Generic;
using System.Linq;
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
			StatusLabel.Text = "Stopping ping...";
			_pingCts.Cancel();
			// UI will be reset in the finally block of the task
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
		var resultsBuilder = new StringBuilder();
		var roundTripTimes = new List<long>();
		int packetsSent = 0;
		int packetsReceived = 0;

		try
		{
			using (var pingSender = new Ping())
			{
				 await AppendResultAsync($"Pinging {hostname} [{ResolveHostname(hostname)}] with {pingCount} requests:\n");

				for (int i = 0; i < pingCount; i++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						await AppendResultAsync("\nPing operation cancelled.");
						break;
					}

					packetsSent++;
					PingReply reply = null;
					string resultLine;
					try
					{
						reply = await pingSender.SendPingAsync(hostname, timeout);
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
					catch (PingException pex)
					{
						resultLine = $"Ping Error: {pex.InnerException?.Message ?? pex.Message}";
					}
					 catch (SocketException sox)
					 {
						 resultLine = $"Socket Error: {sox.Message}";
					 }
					catch (Exception ex) // Catch other potential exceptions from SendPingAsync
					{
						resultLine = $"Error during ping: {ex.Message}";
					}

					await AppendResultAsync(resultLine);

					if (i < pingCount - 1) // Avoid delay after last ping
					{
						 await Task.Delay(500, cancellationToken); // Delay between pings
					}
				}
			}

			// Calculate and Display Summary
			await AppendResultAsync("\nPing statistics:");
} 