using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace NetworkTroubleshooter;

public partial class DnsLookupPage : ContentPage
{
	public DnsLookupPage()
	{
		InitializeComponent();
		// Set default record type
		RecordTypePicker.SelectedItem = RecordType.A;
	}

	private async void OnLookupClicked(object sender, EventArgs e)
	{
		string hostname = HostnameEntry.Text?.Trim();
		string dnsServerIp = DnsServerEntry.Text?.Trim();
		RecordType selectedRecordType = (RecordType)(RecordTypePicker.SelectedItem ?? RecordType.Unknown);

		ResultsEditor.Text = string.Empty;
		StatusLabel.Text = string.Empty;

		if (string.IsNullOrWhiteSpace(hostname))
		{
			StatusLabel.Text = "Please enter a hostname or IP address.";
			return;
		}

		if (selectedRecordType == RecordType.Unknown || selectedRecordType == RecordType.Invalid)
		{
			 StatusLabel.Text = "Please select a valid DNS record type.";
			 return;
		}

		// --- UI State: Start Operation ---
		LookupButton.IsEnabled = false;
		LoadingIndicator.IsVisible = true;
		LoadingIndicator.IsRunning = true;
		// ----------------------------------

		try
		{
			IDnsResolver dnsResolver;
			if (!string.IsNullOrWhiteSpace(dnsServerIp) && IPAddress.TryParse(dnsServerIp, out IPAddress serverAddr))
			{
				// Use custom DNS server
				dnsResolver = new DnsClient(serverAddr, 5000); // 5 second timeout
				 ResultsEditor.Text += $"Using DNS Server: {serverAddr}\n";
			}
			else
			{
				// Use system default DNS servers
				dnsResolver = DnsClient.Default;
				 ResultsEditor.Text += $"Using System Default DNS Servers\n";
			}
			
			string nameToQuery = hostname;

			// Handle PTR request for IP addresses
			if (selectedRecordType == RecordType.Ptr && IPAddress.TryParse(hostname, out IPAddress ipAddress))
			{
				nameToQuery = ipAddress.GetArpaName()?.ToString() ?? hostname;
				if (nameToQuery == hostname)
				{
					StatusLabel.Text = "Could not generate reverse lookup name for the IP.";
					return; // Early exit if ARPA name generation fails
				}
				 ResultsEditor.Text += $"Performing PTR lookup for: {nameToQuery}\n";
			}
			else
			{
				 ResultsEditor.Text += $"Querying for {selectedRecordType} records for: {hostname}\n";
			}
			ResultsEditor.Text += new string('-', 20) + "\n";

			// Perform DNS Query
			DnsMessage dnsMessage = await dnsResolver.ResolveAsync(DomainName.Parse(nameToQuery), selectedRecordType);

			// Process Response
			if (dnsMessage == null)
			{
				StatusLabel.Text = "Query failed: No response from server (timeout?).";
			}
			else if (dnsMessage.ReturnCode != ReturnCode.NoError)
			{
				StatusLabel.Text = $"Query failed: {dnsMessage.ReturnCode}";
			}
			else if (dnsMessage.AnswerRecords.Count == 0)
			{
				 ResultsEditor.Text += "No records found.";
			}
			else
			{
				var resultBuilder = new StringBuilder();
				resultBuilder.AppendLine($"Found {dnsMessage.AnswerRecords.Count} record(s):");
				foreach (DnsRecordBase record in dnsMessage.AnswerRecords)
				{
					resultBuilder.AppendLine($"  {FormatDnsRecord(record)}");
				}
				 ResultsEditor.Text += resultBuilder.ToString();
			}
		}
		catch (TimeoutException)
		{
			StatusLabel.Text = "Query timed out.";
		}
		catch (SocketException sockEx)
		{
			 StatusLabel.Text = $"Network Error: {sockEx.Message} (Code: {sockEx.SocketErrorCode})";
		}
		 catch (ArgumentException argEx)
		 {
			 StatusLabel.Text = $"Invalid Input: {argEx.Message}"; // e.g., bad domain name format
		 }
		catch (Exception ex)
		{
			StatusLabel.Text = $"An error occurred: {ex.Message}";
		}
		finally
		{
			// --- UI State: End Operation ---
			MainThread.BeginInvokeOnMainThread(() =>
			{
				 LookupButton.IsEnabled = true;
				 LoadingIndicator.IsRunning = false;
				 LoadingIndicator.IsVisible = false;
			});
			// ---------------------------------
		}
	}

	// Helper function to format DNS records nicely
	private string FormatDnsRecord(DnsRecordBase record)
	{
		 switch (record)
		 {
			 case ARecord a: return $"A: {a.Address} (TTL: {a.TimeToLive})";
			 case AaaaRecord aaaa: return $"AAAA: {aaaa.Address} (TTL: {aaaa.TimeToLive})";
			 case CNameRecord cname: return $"CNAME: {cname.CanonicalName} (TTL: {cname.TimeToLive})";
			 case MxRecord mx: return $"MX: {mx.ExchangeDomainName} (Pref: {mx.Preference}, TTL: {mx.TimeToLive})";
			 case NsRecord ns: return $"NS: {ns.NameServer} (TTL: {ns.TimeToLive})";
			 case PtrRecord ptr: return $"PTR: {ptr.PointerDomainName} (TTL: {ptr.TimeToLive})";
			 case SoaRecord soa: return $"SOA: {soa.MasterName}, {soa.ResponsibleName}, Serial: {soa.SerialNumber} (TTL: {soa.TimeToLive})";
			 case TxtRecord txt: 
				 // Join potentially multiple text strings
				 string textData = string.Join(" ", txt.TextDatas ?? new List<string>{ "(empty)" }); 
				 return $"TXT: \"{textData}\" (TTL: {txt.TimeToLive})";
			 default: return $"{record.RecordType}: {record.ToString()} (TTL: {record.TimeToLive})";
		 }
	}
} 