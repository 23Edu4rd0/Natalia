using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Páginas do menu: só são criadas na primeira vez que a pessoa abre (a janela principal abre mais rápido).</summary>
public sealed class ColecaoTelas : IReadOnlyDictionary<string, Tela>
{
    readonly List<string> nomes = new();
    readonly Dictionary<string, Func<Tela>> fabricas = new();
    readonly Dictionary<string, Tela> criadas = new();

    public void Adicionar(string nome, Func<Tela> fabrica) { nomes.Add(nome); fabricas[nome] = fabrica; }
    public bool JaCriada(string nome) => criadas.ContainsKey(nome);

    public Tela this[string chave]
    {
        get
        {
            if (!criadas.TryGetValue(chave, out var t)) criadas[chave] = t = fabricas[chave]();
            return t;
        }
    }
    public IEnumerable<string> Keys => nomes;
    public IEnumerable<Tela> Values => nomes.Select(n => this[n]);
    public int Count => nomes.Count;
    public bool ContainsKey(string chave) => fabricas.ContainsKey(chave);
    public bool TryGetValue(string chave, out Tela valor)
    {
        if (fabricas.ContainsKey(chave)) { valor = this[chave]; return true; }
        valor = null!; return false;
    }
    public IEnumerator<KeyValuePair<string, Tela>> GetEnumerator() => nomes.Select(n => new KeyValuePair<string, Tela>(n, this[n])).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Janela principal: menu lateral à esquerda, cabeçalho da página, a página atual e avisos rápidos.</summary>
public sealed class MainWindow : JanelaBase
{
    sealed record ItemMenu(string Nome, Button Botao, Control IconeCtl);

    public Servico Svc { get; }
    public ColecaoTelas Telas { get; } = new();
    public string PaginaAtual { get; private set; } = "";

    readonly List<ItemMenu> menu = new();
    readonly ContentControl pagina = new();
    readonly TextBlock titulo = K.H1(""), subtitulo = K.Discreto("");
    readonly ContentControl acoes = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock nomeEmpresa = new() { FontSize = 12, Foreground = Cores.P("#A5B4FC"), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly StackPanel pilhaToasts = new() { Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 24, 24), MaxWidth = 500 };
    readonly Dictionary<string, (long Versao, DateTime Dia)> ultimaAtualizacao = new();
    long versaoEmpresa = -1;

    public int ToastsAbertos => pilhaToasts.Children.Count;

    public MainWindow(Servico svc)
    {
        Svc = svc;
        Title = $"{Config.NomeApp} {Config.Versao}";
        Width = 1280; Height = 800; MinWidth = 1100; MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var barra = new StackPanel { Spacing = 3, Margin = new Thickness(12, 4, 12, 12) };
        void Grupo(string nome) =>
            barra.Children.Add(new TextBlock { Text = nome.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Cores.P("#818CF8"), Margin = new Thickness(14, 12, 0, 3) });
        void Item(string nome, string icone, Func<Tela> fabrica, string? dica = null)
        {
            Telas.Adicionar(nome, fabrica);
            var ic = Icones.Criar(icone, 20, "#A5B4FC");
            var botao = new Button { Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { ic, new TextBlock { Text = nome, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 } } } };
            botao.Classes.Add("nav");
            botao.Tag = nome;
            if (dica != null) ToolTip.SetTip(botao, dica);
            botao.Click += (_, _) => IrPara(nome);
            barra.Children.Add(botao);
            menu.Add(new ItemMenu(nome, botao, ic));
        }

        Item("Painel", "inicio", () => new PainelTela(this));
        Grupo("Vendas");
        Item("Nova venda", "venda", () => new VendaTela(this), "Atalho: F2");
        Item("Vendas", "notas", () => new NotasTela(this));
        Item("Caixa", "caixa", () => new CaixaTela(this));
        Item("A receber", "receber", () => new ReceberTela(this));
        Grupo("Cadastros");
        Item("Clientes", "clientes", () => new ClientesTela(this));
        Item("Produtos", "produtos", () => new ProdutosTela(this));
        Item("Fornecedores", "fornecedores", () => new FornecedoresTela(this));
        Grupo("Estoque");
        Item("Estoque", "estoque", () => new EstoqueTela(this));
        Item("Entrada de mercadorias", "entrada", () => new EntradasTela(this));
        Grupo("Análise");
        Item("Relatórios", "relatorios", () => new RelatoriosTela(this));
        Item("Configurações", "config", () => new ConfigTela(this));

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
        var rodapeMenu = new StackPanel { Margin = new Thickness(20, 8, 16, 16), Children = { new TextBlock { Text = $"Versão {Config.Versao}", FontSize = 11, Foreground = Cores.P("#818CF8") }, new TextBlock { Text = "F1 ajuda  ·  F2 nova venda  ·  F9 finalizar", FontSize = 11, Foreground = Cores.P("#818CF8") } } };
        var lateral = new Border { Background = Cores.P("#1E1B4B"), Width = 250 };
        var docaLateral = new DockPanel();
        DockPanel.SetDock(marca, Dock.Top); DockPanel.SetDock(rodapeMenu, Dock.Bottom);
        docaLateral.Children.Add(marca); docaLateral.Children.Add(rodapeMenu);
        docaLateral.Children.Add(new ScrollViewer { Content = barra, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
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
        Content = new Grid { Children = { raiz, pilhaToasts } };

        KeyDown += AoApertarTecla;
        Opened += (_, _) =>
        {
            IrPara("Painel");
            // diagnóstico de velocidade de abertura: fora do caminho crítico
            Dispatcher.UIThread.Post(() =>
            {
                try { Log.Info($"Janela principal aberta {(DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMilliseconds:F0} ms após o início do processo"); }
                catch { /* diagnóstico opcional */ }
            }, DispatcherPriority.Background);
        };
    }

    void AoApertarTecla(object? s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F1: Sobre(); e.Handled = true; break;
            case Key.F2: IrPara("Nova venda"); ((VendaTela)Telas["Nova venda"]).Editor.Focar(); e.Handled = true; break;
            case Key.F9 when PaginaAtual == "Nova venda": ((VendaTela)Telas["Nova venda"]).Finalizar(); e.Handled = true; break;
        }
    }

    public void Sobre() => _ = Plat.Atual.Mensagem(this, "Sobre",
        $"{Config.NomeApp} {Config.Versao}\n\nVendas, caixa, fiado, clientes, estoque e notas em um só lugar.\n\n" +
        $"Dados: {Config.PastaDados()}\nLog: {Config.ArquivoLog()}\n\nAtalhos: F2 nova venda · F9 finalizar venda · F1 ajuda\n\nAs notas são comprovantes internos, sem valor fiscal.", Nivel.Info);

    // ------------------------------------------------------------------ avisos rápidos
    /// <summary>Mostra um aviso no canto da tela que some sozinho (mais tempo se tiver botões).</summary>
    public void MostrarToast(string texto, Nivel nivel, IReadOnlyList<(string Rotulo, Action Acao)>? botoes, int segundos)
    {
        var (cor, fundo) = nivel switch { Nivel.Erro => (Cores.Vermelho, "#FFFFFF"), Nivel.Aviso => ("#D97706", "#FFFFFF"), _ => (Cores.Verde, "#FFFFFF") };
        var icone = Icones.Criar(nivel == Nivel.Info ? "ok" : "alerta", 18, cor);
        var msg = new SelectableTextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = Cores.P(Cores.Texto), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 380 };
        var fechar = new Button { Content = "×", Padding = new Thickness(8, 0), MinHeight = 28, MinWidth = 28, Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 18, Foreground = Cores.P("#9CA3AF"), VerticalAlignment = VerticalAlignment.Top };
        var corpo = new StackPanel { Spacing = 10 };
        var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        icone.Margin = new Thickness(0, 2, 10, 0); icone.VerticalAlignment = VerticalAlignment.Top;
        K.ColocarNaGrade(linha, icone, 0, 0); K.ColocarNaGrade(linha, msg, 0, 1); K.ColocarNaGrade(linha, fechar, 0, 2);
        corpo.Children.Add(linha);
        var card = new Border
        {
            Background = Cores.P(fundo), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12, 10, 12), BorderThickness = new Thickness(1, 1, 1, 1), BorderBrush = Cores.P(Cores.Borda),
            BoxShadow = BoxShadows.Parse("0 8 24 0 #33000000"), Child = corpo, MinWidth = 320,
        };
        void Remover() { if (pilhaToasts.Children.Contains(card)) pilhaToasts.Children.Remove(card); }
        if (botoes is { Count: > 0 })
        {
            var linhaBotoes = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 0, 0, 0) };
            foreach (var (rotulo, acao) in botoes)
            {
                var b = K.Botao(rotulo, () =>
                {
                    Remover();
                    try { acao(); } catch (Exception ex) { _ = Erros.Tratar(ex, this); }
                }, classe: "chip");
                b.Margin = new Thickness(0, 0, 8, 4);
                linhaBotoes.Children.Add(b);
            }
            corpo.Children.Add(linhaBotoes);
        }
        fechar.Click += (_, _) => Remover();
        pilhaToasts.Children.Add(card);
        while (pilhaToasts.Children.Count > 4) pilhaToasts.Children.RemoveAt(0);
        var espera = TimeSpan.FromSeconds(botoes is { Count: > 0 } ? Math.Max(segundos, 10) : segundos);
        Task.Delay(espera).ContinueWith(_ => Dispatcher.UIThread.Post(Remover), TaskScheduler.Default);
    }

    // ------------------------------------------------------------------ navegação
    /// <summary>Mostra uma página pelo nome do menu. Só recarrega os dados se algo mudou desde a última vez.</summary>
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
        pagina.Content = Telas[nome];
        Mostrar(forcar: false);
    }

    /// <summary>Recarrega a página atual de qualquer jeito (usado depois de operações que mudam estoque/vendas/fiado).</summary>
    public void AtualizarTudo() => Mostrar(forcar: true);

    void Mostrar(bool forcar)
    {
        if (!Telas.ContainsKey(PaginaAtual)) return;
        var t = Telas[PaginaAtual];
        try
        {
            if (versaoEmpresa != Svc.Versao) { nomeEmpresa.Text = Svc.ObterEmpresa().Nome; versaoEmpresa = Svc.Versao; }
            var estado = (Svc.Versao, DateTime.Today);
            if (forcar || !ultimaAtualizacao.TryGetValue(PaginaAtual, out var u) || u != estado)
            {
                t.Atualizar();
                ultimaAtualizacao[PaginaAtual] = estado;
            }
            titulo.Text = t.Titulo.Length > 0 ? t.Titulo : PaginaAtual;
            subtitulo.Text = t.Subtitulo;
            acoes.Content = t.AcoesCabecalho;
        }
        catch (Exception ex) { _ = Erros.Tratar(ex, this); }
    }
}
