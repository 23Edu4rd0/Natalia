using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Microsoft.Data.Sqlite;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(GestaoComercial.Tests.TestAppBuilder))]

namespace GestaoComercial.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<GestaoComercial.App.App>()
                  .UseSkia()
                  .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                  .WithInterFont();
}

/// <summary>Substitui caixas de diálogo / navegador / seletor de arquivo: registra tudo e responde de forma programada.</summary>
public sealed class PlataformaFalsa : IPlataforma
{
    public List<(Nivel Nivel, string Titulo, string Texto)> Msgs { get; } = new();
    public bool RespostaConfirmar { get; set; } = true;
    public string? RespostaPergunta { get; set; } = "motivo qualquer";
    public string? ArquivoParaSalvar { get; set; }
    public List<string> Abertos { get; } = new();
    public bool AbrirFunciona { get; set; } = true;
    public List<string> Perguntas { get; } = new();
    public List<string> Confirmacoes { get; } = new();

    public (Nivel Nivel, string Titulo, string Texto) Ultima => Msgs[^1];

    public Task Mensagem(Window? dono, string titulo, string texto, Nivel nivel) { Msgs.Add((nivel, titulo, texto)); return Task.CompletedTask; }
    public Task<bool> Confirmar(Window? dono, string titulo, string texto) { Confirmacoes.Add(texto); return Task.FromResult(RespostaConfirmar); }
    public Task<string?> Perguntar(Window? dono, string titulo, string texto, string inicial = "") { Perguntas.Add(texto); return Task.FromResult(RespostaPergunta); }
    public Task<string?> SalvarArquivo(Window? dono, string titulo, string pastaInicial, string nomeInicial, string descricao, string extensao) => Task.FromResult(ArquivoParaSalvar);
    public bool Abrir(string caminho) { Abertos.Add(caminho); return AbrirFunciona; }
}

/// <summary>Ambiente de um teste de interface: banco temporário + janela principal renderizada em memória.</summary>
public sealed class Ambiente : IDisposable
{
    public string Pasta { get; } = Path.Combine(Path.GetTempPath(), "gc-ui-" + Guid.NewGuid().ToString("N"));
    public SqliteConnection Conn { get; }
    public Servico S { get; }
    public MainWindow Janela { get; }
    public PlataformaFalsa P { get; } = new();
    readonly string? antigaPasta = Environment.GetEnvironmentVariable("GESTAO_DATA_DIR");

    public Ambiente()
    {
        Directory.CreateDirectory(Pasta);
        Environment.SetEnvironmentVariable("GESTAO_DATA_DIR", Pasta);   // notas/exportações/logs também vão para a pasta temporária
        Conn = Banco.Conectar(Path.Combine(Pasta, "gestao.db"));
        Banco.Migrar(Conn);
        S = new Servico(Conn);
        Plat.Atual = P;
        Janela = new MainWindow(S) { Width = 1240, Height = 760 };
    }

    public T Tela<T>(string nome) where T : Tela { Janela.IrPara(nome); Rodar(); Janela.UpdateLayout(); return (T)Janela.Telas[nome]; }

    public static void Rodar() => Dispatcher.UIThread.RunJobs();

    /// <summary>Executa uma ação que abre um diálogo modal, interage com ele e espera o fim.</summary>
    public async Task ComDialogo<T>(Func<Task> abre, Action<T> interage) where T : Window
    {
        var tarefa = abre();
        Rodar();
        var dlg = JanelaBase.Abertas.OfType<T>().LastOrDefault() ?? throw new InvalidOperationException($"Diálogo {typeof(T).Name} não abriu");
        interage(dlg);
        Rodar();
        await tarefa;
    }

    public void Foto(string nome, Window? janela = null)
    {
        var pasta = Environment.GetEnvironmentVariable("GC_SHOT_DIR");
        if (string.IsNullOrEmpty(pasta)) return;
        Directory.CreateDirectory(pasta);
        Rodar();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        (janela ?? Janela).CaptureRenderedFrame()?.Save(Path.Combine(pasta, nome + ".png"));
    }

    public void Dispose()
    {
        foreach (var j in JanelaBase.Abertas.ToList()) j.Close();
        Janela.Close();
        Conn.Dispose();
        Plat.Atual = new PlataformaFalsa();
        Environment.SetEnvironmentVariable("GESTAO_DATA_DIR", antigaPasta);
        try { Directory.Delete(Pasta, true); } catch { }
    }
}
