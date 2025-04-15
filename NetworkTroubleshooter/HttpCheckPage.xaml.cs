using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace NetworkTroubleshooter;

public partial class HttpCheckPage : ContentPage
{
	// Reuse HttpClient for performance and resource management
	private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }; // Set a reasonable timeout
	private CancellationTokenSource _httpCts;

	public HttpCheckPage()
	{
		InitializeComponent();
	}

	private async void OnCheckClicked(object sender, EventArgs e)
	{
		if (_httpCts != null) // Already running, maybe cancel?
		{
			// For a single quick check, usually no need to cancel, but we can add it
			_httpCts.Cancel();
			SetStatusAsync("Previous check cancelled.");
			// Let finally block clean up UI
		}

		// --- Validate Inputs ---
		string urlInput = UrlEntry.Text?.Trim();
		if (string.IsNullOrWhiteSpace(urlInput) || !Uri.TryCreate(urlInput, UriKind.Absolute, out Uri requestUri))
		{
			StatusLabel.Text = "Please enter a valid, full URL (e.g., https://example.com).";
			return;
		}
		// -----------------------

		ResultsEditor.Text = string.Empty;
		StatusLabel.Text = string.Empty;
		SetUiState(isChecking: true);

		_httpCts = new CancellationTokenSource();
		var cancellationToken = _httpCts.Token;

		try
		{
			await AppendResultAsync($"Attempting GET request to: {requestUri}");

			using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri))
			{
				// Send request with cancellation token
				using (var response = await _httpClient.SendAsync(requestMessage, cancellationToken))
				{
					await AppendResultAsync($"\nStatus Code: {(int)response.StatusCode} ({response.ReasonPhrase})");
					
					// Optionally display headers (can be long)
					// await AppendResultAsync("\nResponse Headers:");
					// foreach (var header in response.Headers)
					// {
					//     await AppendResultAsync($"  {header.Key}: {string.Join(", ", header.Value)}");
					// }
					// if (response.Content?.Headers != null)
					// {
					//      foreach (var header in response.Content.Headers)
					//      {
					//          await AppendResultAsync($"  {header.Key}: {string.Join(", ", header.Value)}");
					//      }
					// }
				}
			}
		}
		catch (OperationCanceledException)
		{
			await SetStatusAsync("HTTP request timed out or was cancelled.");
		}
		catch (HttpRequestException httpEx)
		{
			// Provide more specific error details if available
			string errorDetails = httpEx.InnerException?.Message ?? httpEx.Message;
			await SetStatusAsync($"HTTP Request Error: {errorDetails}");
			await AppendResultAsync($"\nError: {errorDetails}"); // Also show in main results
		}
		catch (Exception ex)
		{
			await SetStatusAsync($"An unexpected error occurred: {ex.Message}");
			await AppendResultAsync($"\nUnexpected Error: {ex.Message}");
		}
		finally
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				SetUiState(isChecking: false);
				_httpCts?.Dispose();
				_httpCts = null;
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

	private void SetUiState(bool isChecking)
	{
		UrlEntry.IsEnabled = !isChecking;
		LoadingIndicator.IsVisible = isChecking;
		LoadingIndicator.IsRunning = isChecking;
		CheckButton.IsEnabled = !isChecking; // Disable button while checking
		if (!isChecking)
		{
			if (!StatusLabel.Text.StartsWith("Previous check cancelled")) // Avoid clearing cancellation message
			{
				StatusLabel.Text = string.Empty; 
			}
		}
	}
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// Cancel any potentially long-running request if navigating away
		_httpCts?.Cancel(); 
	}
} 