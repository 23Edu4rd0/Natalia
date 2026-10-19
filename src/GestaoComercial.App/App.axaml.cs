using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Microsoft.Data.Sqlite;

namespace GestaoComercial.App;

public partial class App : Application
{
    SqliteConnection? conexao;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Info($"Iniciando {Config.NomeApp} {Config.Versao}");
            desktop.MainWindow = Iniciar();
            desktop.Exit += (_, _) => conexao?.Dispose();

            // Último recurso: erro não tratado em qualquer evento da interface não derruba o programa.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                e.Handled = true;
                _ = Erros.Tratar(e.Exception, desktop.MainWindow);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Erro("Exceção não tratada", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) => { Log.Erro("Tarefa com erro não observado", e.Exception); e.SetObserved(); };
        }
        base.OnFrameworkInitializationCompleted();
    }

    Window Iniciar()
    {
        try
        {
            conexao = Banco.Conectar(Config.CaminhoBanco());
            Banco.Migrar(conexao);
            var svc = new Servico(conexao);
            // backup diário em segundo plano, por uma conexão própria: não atrasa a abertura (banco grande) e não trava o uso
            var caminhoBanco = Config.CaminhoBanco(); var pastaBackups = Config.Subpasta("backups");
            _ = Task.Run(() =>
            {
                try { Servico.BackupAutomaticoDeArquivo(caminhoBanco, pastaBackups); }
                catch (Exception ex) { Log.Erro("Backup automático falhou", ex); } // falha de backup não impede o uso
            });
            return new MainWindow(svc);
        }
        catch (Exception ex)
        {
            Log.Erro("Falha ao abrir o banco de dados", ex);
            var msg = ex switch
            {
                AppException => ex.Message,
                SqliteException sq => Erros.MensagemBanco(sq),
                _ => $"{ex.GetType().Name}: {ex.Message}",
            };
            return new ErroFatalWindow($"{msg}\n\nArquivo: {Config.CaminhoBanco()}\nLog: {Config.ArquivoLog()}");
        }
    }
}

/// <summary>Janela mostrada quando o banco de dados não abre (nunca falha em silêncio).</summary>
sealed class ErroFatalWindow : JanelaBase
{
    public string Texto { get; }

    public ErroFatalWindow(string texto)
    {
        Texto = texto;
        Title = "Não foi possível abrir o banco de dados";
        Width = 560; SizeToContent = SizeToContent.Height; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var fechar = new Button { Content = "Fechar", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
        fechar.Classes.Add("primario");
        fechar.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 18,
            Children = { new SelectableTextBlock { Text = texto, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, fechar },
        };
    }
}
