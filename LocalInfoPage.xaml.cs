using System;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel; // For MainThread

namespace NetworkTroubleshooter;

public partial class LocalInfoPage : ContentPage
{
	public LocalInfoPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadNetworkInfoAsync();
	}

	private async Task LoadNetworkInfoAsync()
	{
		LoadingIndicator.IsRunning = true;
		ResultsStackLayout.Children.Clear(); // Clear previous results

		// Add back the title label that might have been cleared
		ResultsStackLayout.Children.Add(new Label 
		{ 
			Text = "Local Network Information",
			FontSize = Device.GetNamedSize(NamedSize.Large, typeof(Label)),
			HorizontalOptions = LayoutOptions.Center 
		});

		try
		{
			NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
			bool infoDisplayed = false;

			foreach (NetworkInterface adapter in adapters)
			{
				// Filter for active, non-loopback, non-tunnel interfaces
				if (adapter.OperationalStatus == OperationalStatus.Up &&
					adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
					adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
				{
					infoDisplayed = true;
					IPInterfaceProperties properties = adapter.GetIPProperties();
					
					var interfaceLayout = new VerticalStackLayout { Spacing = 5, Margin = new Thickness(0, 10) };

					interfaceLayout.Children.Add(new Label { Text = $"Interface: {adapter.Name} ({adapter.Description})", FontAttributes = FontAttributes.Bold });
					
					string macAddress = adapter.GetPhysicalAddress()?.ToString() ?? "N/A";
					if (!string.IsNullOrEmpty(macAddress) && macAddress.Length == 12) // Format MAC address
					{
						 macAddress = string.Join(":", Enumerable.Range(0, 6).Select(i => macAddress.Substring(i * 2, 2)));
					}
					interfaceLayout.Children.Add(new Label { Text = $"  MAC Address: {macAddress}" });

					// IP Addresses
					foreach (UnicastIPAddressInformation ip in properties.UnicastAddresses)
					{
						// Display only IPv4 for simplicity, or add checks for IPv6 if needed
						if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) 
						{
							 string subnetMask = ip.IPv4Mask?.ToString() ?? "N/A";
							 interfaceLayout.Children.Add(new Label { Text = $"  IP Address: {ip.Address} / Mask: {subnetMask}" });
						}
						// else if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
						// {
						//    interfaceLayout.Children.Add(new Label { Text = $"  IPv6 Address: {ip.Address}" });
						// }
					}

					// Gateway Addresses
					foreach (GatewayIPAddressInformation gw in properties.GatewayAddresses)
					{
						interfaceLayout.Children.Add(new Label { Text = $"  Gateway: {gw.Address}" });
					}

					// DNS Servers
					var dnsServers = properties.DnsAddresses.Select(dns => dns.ToString());
					if (dnsServers.Any())
					{
						 interfaceLayout.Children.Add(new Label { Text = $"  DNS Servers: {string.Join(", ", dnsServers)}" });
					}
					 else
					{
						 interfaceLayout.Children.Add(new Label { Text = "  DNS Servers: N/A" });
					}

					// Add a separator
					interfaceLayout.Children.Add(new BoxView { HeightRequest = 1, Color = Colors.Gray, Margin = new Thickness(0, 5) });

					// Use MainThread for UI updates
					MainThread.BeginInvokeOnMainThread(() =>
					{
						ResultsStackLayout.Children.Add(interfaceLayout);
					});
				}
			}

			if (!infoDisplayed)
			{
				 MainThread.BeginInvokeOnMainThread(() =>
				 {
					 ResultsStackLayout.Children.Add(new Label { Text = "No active network interfaces found.", FontAttributes = FontAttributes.Italic });
				 });
			}
		}
		catch (Exception ex)
		{
			// Use MainThread for UI updates
			MainThread.BeginInvokeOnMainThread(() =>
			{
				ResultsStackLayout.Children.Add(new Label { Text = $"Error loading network info: {ex.Message}", TextColor = Colors.Red });
			});
		}
		finally
		{
			// Use MainThread for UI updates
			MainThread.BeginInvokeOnMainThread(() =>
			{
				LoadingIndicator.IsRunning = false;
			});
		}
	}
} 