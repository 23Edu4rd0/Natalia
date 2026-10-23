using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Microsoft.Data.Sqlite;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

public enum Nivel { Info, Aviso, Erro }

/// <summary>Tudo que depende do sistema operacional / janelas modais (substituível nos testes automáticos).</summary>
public interface IPlataforma
{
    Task Mensagem(Window? dono, string titulo, string texto, Nivel nivel);
    Task<bool> Confirmar(Window? dono, string titulo, string texto);
    Task<string?> Perguntar(Window? dono, string titulo, string texto, string inicial = "");
    Task<string?> SalvarArquivo(Window? dono, string titulo, string pastaInicial, string nomeInicial, string descricao, string extensao);
    Task<string?> AbrirArquivo(Window? dono, string titulo, string descricao, string[] extensoes);
    bool Abrir(string caminho);
    /// <summary>Aviso rápido que não bloqueia o trabalho: some sozinho e pode ter botões de ação (ex.: Imprimir, Desfazer).</summary>
    void Toast(Window? dono, string texto, Nivel nivel = Nivel.Info, IReadOnlyList<(string Rotulo, Action Acao)>? acoes = null, int segundos = 5);
}

public static class Plat
{
    public static IPlataforma Atual { get; set; } = new PlataformaReal();
}

sealed class PlataformaReal : IPlataforma
{
    public Task Mensagem(Window? dono, string titulo, string texto, Nivel nivel) => CaixaMensagem.Mostrar(dono, titulo, texto, nivel, false).ContinueWith(_ => { });
    public Task<bool> Confirmar(Window? dono, string titulo, string texto) => CaixaMensagem.Mostrar(dono, titulo, texto, Nivel.Info, true);
    public Task<string?> Perguntar(Window? dono, string titulo, string texto, string inicial = "") => CaixaPergunta.Mostrar(dono, titulo, texto, inicial);

    public async Task<string?> SalvarArquivo(Window? dono, string titulo, string pastaInicial, string nomeInicial, string descricao, string extensao)
    {
        if (dono == null) return null;
        var pasta = await dono.StorageProvider.TryGetFolderFromPathAsync(pastaInicial);
        var arq = await dono.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = titulo,
            SuggestedFileName = nomeInicial,
            DefaultExtension = extensao,
            SuggestedStartLocation = pasta,
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType(descricao) { Patterns = new[] { "*." + extensao } } },
        });
        var caminho = arq?.TryGetLocalPath();
        if (caminho != null && !Path.HasExtension(caminho)) caminho += "." + extensao;
        return caminho;
    }

    public async Task<string?> AbrirArquivo(Window? dono, string titulo, string descricao, string[] extensoes)
    {
        if (dono == null) return null;
        var arqs = await dono.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = titulo, AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType(descricao) { Patterns = extensoes.Select(e => "*." + e).ToArray() } },
        });
        return arqs.Count > 0 ? arqs[0].TryGetLocalPath() : null;
    }

    public void Toast(Window? dono, string texto, Nivel nivel = Nivel.Info, IReadOnlyList<(string Rotulo, Action Acao)>? acoes = null, int segundos = 5)
    {
        var j = dono;
        while (j != null && j is not MainWindow) j = j.Owner as Window;
        if (j is MainWindow principal) principal.MostrarToast(texto, nivel, acoes, segundos);
        else _ = Mensagem(dono, nivel == Nivel.Erro ? "Erro" : "Aviso", texto, nivel);   // sem janela principal: cai para a caixa de mensagem
    }

    /// <summary>Abre arquivo, pasta ou página com o programa padrão do sistema.</summary>
    public bool Abrir(string caminho)
    {
        try
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return false;
        }
    }
}

/// <summary>Tratamento central de erros: tudo vira mensagem amigável; nada derruba o programa.</summary>
public static class Erros
{
    public static string MensagemBanco(SqliteException ex)
    {
        var m = ex.Message.ToLowerInvariant();
        if (ex.SqliteErrorCode is 5 or 6 || m.Contains("locked") || m.Contains("busy"))
            return "O banco de dados está ocupado (outra janela ou programa está usando o arquivo). Feche as outras janelas do programa e tente novamente.";
        if (ex.SqliteErrorCode == 8 || m.Contains("readonly") || m.Contains("read-only"))
            return "Sem permissão para gravar no banco de dados. Verifique as permissões da pasta de dados.";
        if (ex.SqliteErrorCode == 13 || m.Contains("full"))
            return "O disco está cheio. Libere espaço e tente novamente.";
        if (ex.SqliteErrorCode is 11 or 26 || m.Contains("malformed") || m.Contains("not a database") || m.Contains("corrupt"))
            return "O arquivo do banco de dados parece estar corrompido. Para restaurar, feche o programa e copie um arquivo da pasta 'backups' (dentro da pasta de dados) por cima de 'gestao.db'.";
        return $"Erro no banco de dados: {ex.Message}";
    }

    public static async Task Tratar(Exception ex, Window? dono)
    {
        try
        {
            switch (ex)
            {
                case AppException:
                    await Plat.Atual.Mensagem(dono, "Atenção", ex.Message, Nivel.Aviso); break;
                case SqliteException sq:
                    Log.Erro("Erro de banco de dados", ex);
                    await Plat.Atual.Mensagem(dono, "Erro no banco de dados", MensagemBanco(sq), Nivel.Erro); break;
                case UnauthorizedAccessException:
                    Log.Erro("Erro de permissão", ex);
                    await Plat.Atual.Mensagem(dono, "Sem permissão", $"Não foi possível acessar o arquivo:\n{ex.Message}", Nivel.Erro); break;
                case IOException:
                    Log.Erro("Erro de arquivo", ex);
                    await Plat.Atual.Mensagem(dono, "Erro de arquivo", $"Falha ao ler/gravar arquivo:\n{ex.Message}", Nivel.Erro); break;
                default:
                    Log.Erro("Erro inesperado", ex);
                    await Plat.Atual.Mensagem(dono, "Erro inesperado",
                        $"Ocorreu um erro inesperado:\n\n{ex.GetType().Name}: {ex.Message}\n\nOs detalhes foram gravados em:\n{Config.ArquivoLog()}", Nivel.Erro);
                    break;
            }
        }
        catch (Exception falha) { Log.Erro("Falha ao exibir erro", falha); }
    }
}

/// <summary>Base das janelas do programa (fonte, ícone e atalhos).</summary>
public class JanelaBase : Window
{
    /// <summary>Janelas atualmente abertas (usado pelos testes automáticos para achar diálogos).</summary>
    public static List<Window> Abertas { get; } = new();

    public JanelaBase()
    {
        FontSize = 14;
        Foreground = Cores.P(Cores.Corpo);
        Background = Cores.P(Cores.Fundo);
        Abertas.Add(this);
        Closed += (_, _) => Abertas.Remove(this);
        try
        {
            using var s = Avalonia.Platform.AssetLoader.Open(new Uri("avares://GestaoComercial/Assets/icone.png"));
            Icon = new WindowIcon(new Avalonia.Media.Imaging.Bitmap(s));
        }
        catch { /* ícone é opcional */ }
    }
}

sealed class CaixaMensagem : JanelaBase
{
    bool resultado;

    public static async Task<bool> Mostrar(Window? dono, string titulo, string texto, Nivel nivel, bool pergunta)
    {
        var j = new CaixaMensagem(titulo, texto, nivel, pergunta);
        if (dono == null || !dono.IsVisible) { j.WindowStartupLocation = WindowStartupLocation.CenterScreen; j.Show(); await j.Fechada(); }
        else await j.ShowDialog(dono);
        return j.resultado;
    }

    TaskCompletionSource fim = new();
    Task Fechada() => fim.Task;

    CaixaMensagem(string titulo, string texto, Nivel nivel, bool pergunta)
    {
        Title = titulo;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Closed += (_, _) => fim.TrySetResult();

        Background = Brushes.White;
        var (cor, fundo, nomeIcone) = pergunta ? ("#4F46E5", "#EEF2FF", "info")
            : nivel switch { Nivel.Erro => ("#DC2626", "#FEF2F2", "fechar"), Nivel.Aviso => ("#D97706", "#FFFBEB", "alerta"), _ => ("#059669", "#ECFDF5", "ok") };
        var icone = K.Bolha(nomeIcone, cor, fundo, 44);
        icone.VerticalAlignment = VerticalAlignment.Top; icone.Margin = new Thickness(0, 0, 16, 0);
        var tit = new TextBlock { Text = titulo, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto), Margin = new Thickness(0, 0, 0, 6) };
        var msg = new SelectableTextBlock { Text = texto, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440, MinWidth = 300, FontSize = 14, LineHeight = 21, Foreground = Cores.P(Cores.Cinza) };

        var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button Mk(string t, bool val, bool primario)
        {
            var b = new Button { Content = t, MinWidth = 96, HorizontalContentAlignment = HorizontalAlignment.Center };
            if (primario) b.Classes.Add("primario");
            b.Click += (_, _) => { resultado = val; Close(); };
            return b;
        }
        Button principal;
        if (pergunta) { principal = Mk("Sim", true, true); botoes.Children.Add(Mk("Não", false, false)); botoes.Children.Add(principal); }
        else { principal = Mk("OK", true, true); botoes.Children.Add(principal); }

        var corpo = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(26, 24, 30, 24) };
        corpo.Children.Add(icone);
        var direita = new StackPanel(); Grid.SetColumn(direita, 1);
        direita.Children.Add(tit); direita.Children.Add(msg);
        corpo.Children.Add(direita);
        var rodape = new Border { Child = botoes, Padding = new Thickness(24, 14), Background = Cores.P("#F8FAFC"), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0) };
        var doca = new DockPanel(); DockPanel.SetDock(rodape, Dock.Bottom); doca.Children.Add(rodape); doca.Children.Add(corpo);
        Content = doca;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { resultado = false; Close(); }
            else if (e.Key == Key.Enter) { resultado = true; Close(); }
        };
        Opened += (_, _) => principal.Focus();
    }
}

sealed class CaixaPergunta : JanelaBase
{
    string? resposta;

    public static async Task<string?> Mostrar(Window? dono, string titulo, string texto, string inicial)
    {
        var j = new CaixaPergunta(titulo, texto, inicial);
        if (dono == null) { j.WindowStartupLocation = WindowStartupLocation.CenterScreen; j.Show(); await j.fim.Task; }
        else await j.ShowDialog(dono);
        return j.resposta;
    }

    readonly TaskCompletionSource fim = new();

    CaixaPergunta(string titulo, string texto, string inicial)
    {
        Title = titulo;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Closed += (_, _) => fim.TrySetResult();
        Background = Brushes.White;
        var caixa = new TextBox { Text = inicial, MinWidth = 400 };
        void Ok() { resposta = caixa.Text ?? ""; Close(); }
        var ok = new Button { Content = "OK", MinWidth = 96 }; ok.Classes.Add("primario"); ok.Click += (_, _) => Ok();
        var cancelar = new Button { Content = "Cancelar", MinWidth = 96 }; cancelar.Click += (_, _) => Close();
        var corpo = new StackPanel
        {
            Margin = new Thickness(26, 22, 26, 22), Spacing = 12,
            Children =
            {
                new TextBlock { Text = titulo, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto) },
                new TextBlock { Text = texto, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 440, Foreground = Cores.P(Cores.Cinza) },
                caixa,
            },
        };
        var rodape = new Border
        {
            Padding = new Thickness(24, 14), Background = Cores.P("#F8FAFC"), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancelar, ok } },
        };
        var doca = new DockPanel(); DockPanel.SetDock(rodape, Dock.Bottom); doca.Children.Add(rodape); doca.Children.Add(corpo);
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Enter) Ok(); };
        Opened += (_, _) => { caixa.Focus(); caixa.SelectAll(); };
    }
}
