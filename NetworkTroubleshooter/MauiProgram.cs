using Microsoft.Extensions.Logging;

namespace NetworkTroubleshooter;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Register Pages/Routes
		builder.Services.AddSingleton<LocalInfoPage>();
		builder.Services.AddSingleton<DnsLookupPage>();
		builder.Services.AddSingleton<PingPage>();
		builder.Services.AddSingleton<TraceroutePage>();
		builder.Services.AddSingleton<PortScanPage>();
		builder.Services.AddSingleton<HttpCheckPage>();
		// Also register MainPage if it's still needed (e.g., for initial loading)
		builder.Services.AddSingleton<MainPage>();

		return builder.Build();
	}
}
