using Avalonia;
using GestaoComercial.Core;

namespace GestaoComercial.App;

static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Erro("Erro fatal", ex);
            Console.Error.WriteLine($"Erro fatal: {ex.Message}\nLog: {Config.ArquivoLog()}");
            return 1;
        }
    }

    // Também usado pelos testes automáticos e pelo designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
}
