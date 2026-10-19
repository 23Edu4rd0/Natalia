using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Traz a lista de produtos de uma planilha do Excel (salva como CSV) de uma vez.</summary>
public sealed class ImportarProdutosDialog : JanelaBase
{
    readonly Servico svc;
    readonly Action? aoConcluir;
    readonly SelectableTextBlock resultado = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public ResultadoImportacao? Resultado { get; private set; }
    public string TextoResultado => resultado.Text ?? "";

    public static Task Abrir(Window? dono, Servico svc, Action? aoConcluir) =>
        dono == null ? Task.CompletedTask : new ImportarProdutosDialog(svc, aoConcluir).ShowDialog(dono);

    public ImportarProdutosDialog(Servico svc, Action? aoConcluir)
    {
        this.svc = svc; this.aoConcluir = aoConcluir;
        Title = "Importar produtos";
        Width = 660; SizeToContent = SizeToContent.Height; MaxHeight = 760; CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        Control Passo(string n, string texto, Control? botao = null)
        {
            var num = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Cores.P(Cores.AzulFundo), VerticalAlignment = VerticalAlignment.Top,
                                   Child = new TextBlock { Text = n, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Cores.P(Cores.Azul), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var corpo = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = texto, TextWrapping = Avalonia.Media.TextWrapping.Wrap } } };
            if (botao != null) corpo.Children.Add(botao);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            K.ColocarNaGrade(g, num, 0, 0); corpo.Margin = new Thickness(12, 2, 0, 0); K.ColocarNaGrade(g, corpo, 0, 1);
            return g;
        }

        var baixar = K.Botao("Baixar planilha modelo", () => Fazer(BaixarModeloAsync), icone: "exportar");
        var escolher = K.Botao("Escolher planilha e importar", () => Fazer(() => ImportarAsync()), icone: "mais", classe: "primario");
        var fechar = K.Botao("Fechar", Close); fechar.HorizontalAlignment = HorizontalAlignment.Right;
        Content = new StackPanel
        {
            Margin = new Thickness(28), Spacing = 18,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { K.H1("Importar produtos"), K.Discreto("Já tem sua lista de produtos no Excel? Traga tudo de uma vez, sem digitar um por um.") } },
                Passo("1", "Baixe a planilha modelo e preencha com os seus produtos. Só o nome é obrigatório; o resto pode ficar em branco.", baixar),
                Passo("2", "No Excel, salve como “CSV (separado por vírgulas)” ou “CSV UTF-8”."),
                Passo("3", "Escolha o arquivo aqui. Produtos com o mesmo código são atualizados; os demais, criados.", escolher),
                resultado, fechar,
            },
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    public async Task BaixarModeloAsync()
    {
        var caminho = Path.Combine(Config.Subpasta("exportacoes"), "modelo-produtos.csv");
        File.WriteAllText(caminho, Servico.ModeloCsvProdutos, new UTF8Encoding(true));
        if (!Plat.Atual.Abrir(caminho))
            await Plat.Atual.Mensagem(this, "Modelo salvo", $"O modelo foi salvo em:\n{caminho}\n\nAbra-o no Excel, preencha e salve como CSV.", Nivel.Info);
    }

    public async Task ImportarAsync(string? caminho = null)
    {
        caminho ??= await Plat.Atual.AbrirArquivo(this, "Escolha a planilha de produtos", "Planilha CSV", new[] { "csv", "txt" });
        if (caminho == null) return;
        var r = svc.ImportarProdutosCsv(caminho);
        Resultado = r;
        var sb = new StringBuilder($"Pronto! {r.Criados} produto(s) criado(s) e {r.Atualizados} atualizado(s).");
        if (r.Ignorados > 0)
        {
            sb.Append($"\n\n{r.Ignorados} linha(s) não foram importadas:");
            foreach (var e in r.Erros.Take(12)) sb.Append("\n • ").Append(e);
            if (r.Erros.Count > 12) sb.Append($"\n • … e mais {r.Erros.Count - 12}.");
        }
        resultado.Text = sb.ToString();
        resultado.Foreground = Cores.P(r.Ignorados > 0 ? "#B45309" : "#166534");
        aoConcluir?.Invoke();
    }
}
