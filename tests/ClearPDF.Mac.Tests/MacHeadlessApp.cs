using Avalonia;
using Avalonia.Headless;
using ClearPDF.Mac;

[assembly: AvaloniaTestApplication(typeof(MacHeadlessApp))]

public class MacHeadlessApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        HeadlessTestHost.Enabled = true;
        return AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
