using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Janela principal: menu lateral à esquerda, cabeçalho da página e a página atual.</summary>
public sealed class MainWindow : JanelaBase
{
    sealed record ItemMenu(string Nome, string Icone, Tela Tela, Button Botao, Control IconeCtl);

    public Servico Svc { get; }
    public Dictionary<string, Tela> Telas { get; } = new();
    public string PaginaAtual { get; private set; } = "";

    readonly List<ItemMenu> menu = new();
    readonly ContentControl pagina = new();
    readonly TextBlock titulo = K.H1(""), subtitulo = K.Discreto("");
    readonly ContentControl acoes = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock nomeEmpresa = new() { FontSize = 12, Foreground = Cores.P("#A5B4FC"), TextTrimming = TextTrimming.CharacterEllipsis };

    public MainWindow(Servico svc)
    {
        Svc = svc;
        Title = $"{Config.NomeApp} {Config.Versao}";
        Width = 1280; Height = 800; MinWidth = 1100; MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var barra = new StackPanel { Spacing = 3, Margin = new Thickness(12, 4, 12, 12) };
        void Grupo(string nome)
        {
            var t = new TextBlock { Text = nome.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Cores.P("#818CF8"), Margin = new Thickness(14, 12, 0, 3) };
            barra.Children.Add(t);
        }
        void Item(string nome, string icone, Tela tela)
        {
            Telas[nome] = tela;
            var ic = Icones.Criar(icone, 20, "#A5B4FC");
            var botao = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { ic, new TextBlock { Text = nome, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 } } } };
            botao.Classes.Add("nav");
            botao.Tag = nome;
            botao.Click += (_, _) => IrPara(nome);
            barra.Children.Add(botao);
            menu.Add(new ItemMenu(nome, icone, tela, botao, ic));
        }

        Item("Painel", "inicio", new PainelTela(this));
        Grupo("Vendas");
        Item("Nova venda", "venda", new VendaTela(this));
        Item("Vendas", "notas", new NotasTela(this));
        Item("Caixa", "caixa", new CaixaTela(this));
        Item("A receber", "receber", new ReceberTela(this));
        Grupo("Cadastros");
        Item("Clientes", "clientes", new ClientesTela(this));
        Item("Produtos", "produtos", new ProdutosTela(this));
        Item("Fornecedores", "fornecedores", new FornecedoresTela(this));
        Grupo("Estoque");
        Item("Estoque", "estoque", new EstoqueTela(this));
        Item("Entrada de mercadorias", "entrada", new EntradasTela(this));
        Grupo("Análise");
        Item("Relatórios", "relatorios", new RelatoriosTela(this));
        Item("Configurações", "config", new ConfigTela(this));

        // marca
        var logo = new Border
        {
            Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = Cores.P(Cores.Primaria),
            Child = Icones.Criar("sacola", 22, "#FFFFFF"),
        };
        logo.Child!.HorizontalAlignment = HorizontalAlignment.Center;
        var marca = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(20, 22, 16, 8),
            Children = { logo, new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { new TextBlock { Text = "Gestão Comercial", FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Brushes.White }, nomeEmpresa } } },
        };
        var rodapeMenu = new StackPanel { Margin = new Thickness(20, 8, 16, 16), Children = { new TextBlock { Text = $"Versão {Config.Versao}", FontSize = 11, Foreground = Cores.P("#818CF8") }, new TextBlock { Text = "F1 = ajuda  ·  F2 = nova venda", FontSize = 11, Foreground = Cores.P("#818CF8") } } };
        var lateral = new Border { Background = Cores.P("#1E1B4B"), Width = 250 };
        var docaLateral = new DockPanel();
        DockPanel.SetDock(marca, Dock.Top); DockPanel.SetDock(rodapeMenu, Dock.Bottom);
        docaLateral.Children.Add(marca); docaLateral.Children.Add(rodapeMenu);
        docaLateral.Children.Add(new ScrollViewer { Content = barra, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        lateral.Child = docaLateral;

        // cabeçalho da página
        var textos = new StackPanel { Spacing = 2, Children = { titulo, subtitulo } };
        var cabecalho = new DockPanel { Margin = new Thickness(32, 26, 32, 16) };
        DockPanel.SetDock(acoes, Dock.Right); cabecalho.Children.Add(acoes); cabecalho.Children.Add(textos);
        pagina.Margin = new Thickness(32, 0, 32, 24);

        var conteudo = new DockPanel();
        DockPanel.SetDock(cabecalho, Dock.Top);
        conteudo.Children.Add(cabecalho); conteudo.Children.Add(pagina);

        var raiz = new DockPanel();
        DockPanel.SetDock(lateral, Dock.Left);
        raiz.Children.Add(lateral); raiz.Children.Add(conteudo);
        Content = raiz;

        KeyDown += AoApertarTecla;
        Opened += (_, _) =>
        {
            IrPara("Painel");
            try { Log.Info($"Janela principal aberta {(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds:F0} ms após o início do processo"); }
            catch { /* diagnóstico opcional */ }
        };
    }

    void AoApertarTecla(object? s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F1: Sobre(); e.Handled = true; break;
            case Key.F2: IrPara("Nova venda"); ((VendaTela)Telas["Nova venda"]).Editor.Focar(); e.Handled = true; break;
            case Key.F9 when Telas.GetValueOrDefault(PaginaAtual) is VendaTela v: v.Finalizar(); e.Handled = true; break;
        }
    }

    public void Sobre() => _ = Plat.Atual.Mensagem(this, "Sobre",
        $"{Config.NomeApp} {Config.Versao}\n\nVendas, caixa, fiado, clientes, estoque e notas em um só lugar.\n\n" +
        $"Dados: {Config.PastaDados()}\nLog: {Config.ArquivoLog()}\n\nAtalhos: F2 nova venda · F9 finalizar venda · F1 ajuda\n\nAs notas são comprovantes internos, sem valor fiscal.", Nivel.Info);

    /// <summary>Mostra uma página pelo nome do menu e a atualiza.</summary>
    public void IrPara(string nome)
    {
        var item = menu.First(m => m.Nome == nome);
        PaginaAtual = nome;
        foreach (var m in menu)
        {
            var ativo = m == item;
            if (ativo) m.Botao.Classes.Add("ativo"); else m.Botao.Classes.Remove("ativo");
            Icones.Pintar(m.IconeCtl, ativo ? "#FFFFFF" : "#A5B4FC");
        }
        pagina.Content = item.Tela;
        AtualizarTudo();
    }

    /// <summary>Recarrega a página atual (ao trocar de página e depois de operações que mudam estoque/vendas).</summary>
    public void AtualizarTudo()
    {
        if (!Telas.TryGetValue(PaginaAtual, out var t)) return;
        try
        {
            nomeEmpresa.Text = Svc.ObterEmpresa().Nome;
            t.Atualizar();
            titulo.Text = t.Titulo.Length > 0 ? t.Titulo : PaginaAtual;
            subtitulo.Text = t.Subtitulo;
            acoes.Content = t.AcoesCabecalho;
        }
        catch (Exception ex) { _ = Erros.Tratar(ex, this); }
    }
}
