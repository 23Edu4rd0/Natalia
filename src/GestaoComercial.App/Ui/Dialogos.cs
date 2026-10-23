using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Seleção de produto numa lista pesquisável.</summary>
public sealed class SelecionarProdutoDialog : JanelaBase
{
    readonly Servico svc;
    readonly Tabela tabela;
    readonly TextBox busca;
    List<Produto> produtos = new();
    public Produto? Escolhido { get; private set; }

    public static async Task<Produto?> Abrir(Window? dono, Servico svc, string buscaInicial)
    {
        var d = new SelecionarProdutoDialog(svc, buscaInicial);
        if (dono != null) await d.ShowDialog(dono); else d.Show();
        return d.Escolhido;
    }

    public SelecionarProdutoDialog(Servico svc, string buscaInicial)
    {
        this.svc = svc;
        Title = "Selecionar produto";
        Width = 800; Height = 540; MinWidth = 600; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        busca = K.CaixaBusca("Digite parte do nome ou do código");
        busca.Text = buscaInicial;
        busca.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabela = new Tabela(new Col("Código", 100), new Col("Produto", 260, Expande: true), new Col("Tipo", 130),
                            new Col("Preço venda", 120, true), new Col("Estoque", 100, true));
        var escolher = K.Botao("Selecionar", Escolher, true);
        var cancelar = K.Botao("Cancelar", Close);
        var barra = K.Linha(escolher, cancelar); barra.HorizontalAlignment = HorizontalAlignment.Right; barra.Margin = new Thickness(0, 10, 0, 0);
        var topo = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 0, 14), Children = { K.H2("Escolha o produto"), busca } };
        barra.Margin = new Thickness(0, 14, 0, 0);
        var doca = K.Doca(topo, tabela, barra); doca.Margin = new Thickness(24, 20, 24, 20);
        Content = doca;

        K.AoMudarTexto(busca, K.Debounce(Carregar));
        tabela.Duplo += Escolher;
        K.AoApertarEnter(busca, Escolher);
        busca.KeyDown += (_, e) => { if (e.Key == Key.Down) { tabela.Focus(); e.Handled = true; } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Carregar();
        Opened += (_, _) => busca.Focus();
    }

    public void Carregar()
    {
        produtos = svc.ListarProdutos(busca.Text ?? "").Take(300).ToList();
        tabela.Definir(produtos.Select(p => new Linha
        {
            Id = p.Id.ToString(), Estilo = p.Baixo ? "alerta" : "",
            C = new[] { p.Codigo, p.Nome, p.Categoria, Util.FmtDinheiro(p.PrecoVenda), p.Estoque.ToString() },
        }));
        if (produtos.Count > 0 && tabela.SelectedItem == null) tabela.SelectedIndex = 0;
    }

    public void Escolher()
    {
        var id = tabela.IdLong ?? produtos.FirstOrDefault()?.Id;
        Escolhido = produtos.FirstOrDefault(p => p.Id == id);
        if (Escolhido != null) Close();
    }
}

public sealed class ItemCarrinho
{
    public required long ProdutoId { get; init; }
    public required string Codigo { get; init; }
    public required string Nome { get; init; }
    public long Quantidade { get; set; }
    public long Preco { get; init; }
}

/// <summary>
/// Lista de itens (produto, quantidade, preço) das telas de venda e de entrada.
/// venda=true: preço de venda e valida o estoque; venda=false: custo unitário.
/// Enquanto a pessoa digita, aparece uma lista de sugestões (setas + Enter, ou clique).
/// </summary>
public sealed class EditorItens : UserControl
{
    readonly Servico svc;
    readonly bool venda, avancado;
    readonly Action? aoMudar;
    readonly List<ItemCarrinho> itens = new();
    Produto? produto;

    public TextBox Busca { get; }
    public TextBox Quantidade { get; }
    public TextBox Preco { get; }
    public Tabela Tabela { get; }
    readonly TextBlock lblProduto;
    readonly Border selecionado;
    readonly UniformGrid? atalhos;
    readonly StackPanel? atalhosCaixa;
    readonly Border contador = K.Selo("0", Cores.PrimariaSuave, Cores.Azul);
    public IReadOnlyList<ItemCarrinho> Itens => itens;
    public IReadOnlyList<string> NomesAtalhos => atalhos?.Children.OfType<Button>().Select(b => (string)b.Tag!).ToList() ?? new List<string>();
    public Produto? ProdutoSelecionado => produto;

    // ---- sugestões enquanto digita
    readonly Popup popup;
    readonly StackPanel listaSugestoes = new() { Spacing = 2 };
    readonly TextBlock rodapeSugestoes = new() { FontSize = 12, Foreground = Cores.P(Cores.Texto3), Margin = new Thickness(10, 8, 10, 4) };
    List<Produto> sugestoes = new();
    int indiceSugestao;
    bool suprimirSugestoes;
    List<(Produto P, string Chave)> catalogo = new();
    long versaoCatalogo = -1;
    readonly Action sugerirComAtraso;

    /// <summary>Produtos sugeridos para o texto digitado (na ordem em que aparecem).</summary>
    public IReadOnlyList<Produto> Sugestoes => sugestoes;
    public bool SugestoesAbertas => popup.IsOpen;
    public int IndiceSugestao => indiceSugestao;

    Window? Janela => TopLevel.GetTopLevel(this) as Window;

    public EditorItens(Servico svc, bool venda, Action? aoMudar, bool avancado = true)
    {
        this.svc = svc; this.venda = venda; this.aoMudar = aoMudar; this.avancado = avancado;

        Busca = K.CaixaBusca("Digite o nome, o código ou leia o código de barras");
        Busca.Classes.Add("grande");
        Busca.HorizontalAlignment = HorizontalAlignment.Stretch;
        var verLista = K.Botao("Ver lista", () => Fazer(ListaCompletaAsync), icone: "notas", classe: "fantasma");
        verLista.Tag = "Buscar…"; verLista.MinHeight = 34; verLista.Margin = new Thickness(0, 0, 6, 0);
        ToolTip.SetTip(verLista, "Procurar na lista completa de produtos");
        Busca.InnerRightContent = verLista;
        Quantidade = K.Caixa("1"); Quantidade.Classes.Add("grande"); Quantidade.HorizontalContentAlignment = HorizontalAlignment.Center;
        Preco = K.Caixa(""); Preco.Classes.Add("grande");
        lblProduto = new TextBlock { FontSize = 13, Foreground = Cores.P(Cores.Azul), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        selecionado = new Border
        {
            Background = Cores.P(Cores.PrimariaSuave), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), IsVisible = false,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Icones.Criar("etiqueta", 16, Cores.Primaria), lblProduto } },
        };

        var adicionar = K.Botao("Adicionar", () => Fazer(AdicionarAsync), icone: "mais", classe: "primario");
        adicionar.MinHeight = 50; adicionar.Padding = new Thickness(18, 0); adicionar.VerticalAlignment = VerticalAlignment.Bottom;

        // busca ocupando a linha toda; quantidade, preço e "Adicionar" logo abaixo (com o produto escolhido ao lado)
        Quantidade.Width = 90; Preco.Width = 140;
        var linhaQtd = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { K.Campo("Qtd.", Quantidade), K.Campo(venda ? "Preço (R$)" : "Custo (R$)", Preco), adicionar } };
        selecionado.VerticalAlignment = VerticalAlignment.Bottom;
        selecionado.Margin = new Thickness(0, 0, 0, 6);
        var linha = new Grid();
        linha.Children.Add(K.Campo("Produto", Busca));
        // lista suspensa de sugestões
        var cartaoSugestoes = new Border
        {
            Background = Brushes.White, CornerRadius = new CornerRadius(14), BorderBrush = Cores.P(Cores.Borda), BorderThickness = new Thickness(1),
            Padding = new Thickness(6), BoxShadow = BoxShadows.Parse("0 18 40 -12 #400F172A, 0 2 6 0 #140F172A"),
            Child = new StackPanel { Children = { listaSugestoes, rodapeSugestoes } },
        };
        popup = new Popup
        {
            PlacementTarget = Busca, Placement = PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 6, IsLightDismissEnabled = true,
            Child = cartaoSugestoes,
        };
        linha.Children.Add(popup);

        var painelAdd = new StackPanel { Spacing = 14, Children = { linha, K.Ferramentas(linhaQtd, selecionado) } };
        if (venda)
        {
            atalhos = new UniformGrid { Columns = 4 };
            // quantos "mais vendidos" cabem: colunas conforme a largura, no máximo 2 linhas
            atalhos.SizeChanged += (_, e) => AjustarAtalhos(e.NewSize.Width);
            painelAdd.Children.Add(atalhosCaixa = new StackPanel
            {
                Spacing = 8, IsVisible = false,
                Children = { new TextBlock { Text = "Mais vendidos — clique para adicionar", FontSize = 12.5, FontWeight = FontWeight.Medium, Foreground = Cores.P(Cores.Texto2) }, K.Margem(atalhos, 0, 0, 0, -8) },
            });
        }
        var caixaAdd = K.Cartao(painelAdd);
        caixaAdd.Margin = new Thickness(0, 0, 0, 20);

        Tabela = new Tabela(new Col("Código", 96), new Col("Produto", 160, Expande: true), new Col("Qtd.", 76, true),
                            new Col(venda ? "Preço unit." : "Custo unit.", 116, true), new Col("Total", 124, true));
        var acoes = K.Fila(2, K.Botao("Alterar quantidade", () => Fazer(AlterarQuantidadeAsync), icone: "editar", classe: "fantasma"),
                               K.Botao("Remover item", () => Fazer(RemoverAsync), icone: "lixeira", classe: "fantasma"),
                               K.Botao("Limpar tudo", Limpar, icone: "fechar", classe: "fantasma"));
        contador.IsVisible = false;
        var titulo = K.Linha(10, K.H2(venda ? "Itens da venda" : "Itens da entrada"), contador);
        var lista = K.Lista(Tabela, K.Ferramentas(titulo, acoes),
                            vazio: venda ? "Nenhum produto na venda ainda.\nDigite o nome, leia o código de barras ou clique em um dos mais vendidos."
                                         : "Nenhum produto na entrada ainda.\nBusque o produto acima, informe a quantidade e o custo.",
                            iconeVazio: venda ? "venda" : "entrada");
        Content = K.Doca(caixaAdd, lista);

        sugerirComAtraso = K.Debounce(AtualizarSugestoes, 120);
        K.AoMudarTexto(Busca, () => { BuscaEditada(); if (!suprimirSugestoes) sugerirComAtraso(); });
        Busca.AddHandler(KeyDownEvent, TeclaNaBusca, RoutingStrategies.Tunnel);
        K.AoApertarEnter(Quantidade, () => Fazer(AdicionarAsync));
        K.AoApertarEnter(Preco, () => Fazer(AdicionarAsync));
        Tabela.Duplo += () => Fazer(AlterarQuantidadeAsync);
        Tabela.KeyDown += (_, e) => { if (e.Key == Key.Delete) Fazer(RemoverAsync); };
        AtualizarSelecionado();
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, Janela); }
    }

    // ------------------------------------------------------------------ sugestões
    void TeclaNaBusca(object? s, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (!popup.IsOpen) AtualizarSugestoes(); else MoverSugestao(+1);
                e.Handled = true; break;
            case Key.Up:
                if (popup.IsOpen) { MoverSugestao(-1); e.Handled = true; }
                break;
            case Key.Escape:
                if (popup.IsOpen) { FecharSugestoes(); e.Handled = true; }
                break;
            case Key.Enter:
                e.Handled = true;
                Fazer(EnterAsync);
                break;
        }
    }

    /// <summary>Enter na busca: código de barras exato tem prioridade; senão, usa a sugestão destacada; senão, busca normal.</summary>
    public async Task EnterAsync()
    {
        var (_, texto) = SepararQuantidade(Busca.Text);
        if (popup.IsOpen && sugestoes.Count > 0 && svc.ObterProdutoPorCodigo(texto) == null)
        {
            await EscolherSugestaoAsync(indiceSugestao);
            return;
        }
        FecharSugestoes();
        await BuscarAsync();
    }

    void MoverSugestao(int delta)
    {
        if (sugestoes.Count == 0) return;
        indiceSugestao = (indiceSugestao + delta + sugestoes.Count) % sugestoes.Count;
        DesenharSugestoes();
    }

    static (long? Qtd, string Texto) SepararQuantidade(string? t)
    {
        var texto = (t ?? "").Trim();
        var m = QuantidadeNaBusca.Match(texto);
        return m.Success ? (long.Parse(m.Groups[1].Value), m.Groups[2].Value.Trim()) : (null, texto);
    }

    /// <summary>Recalcula as sugestões para o texto atual (sem diferenciar acentos e maiúsculas).</summary>
    public void AtualizarSugestoes()
    {
        var (_, texto) = SepararQuantidade(Busca.Text);
        if (texto.Length == 0 || (produto != null && texto == produto.Codigo)) { FecharSugestoes(); return; }
        if (versaoCatalogo != svc.Versao)
        {
            catalogo = svc.ListarProdutos().Select(p => (p, Util.ChaveOrdenacao(p.Nome))).ToList();
            versaoCatalogo = svc.Versao;
        }
        var chave = Util.ChaveOrdenacao(texto);
        int Pontos((Produto P, string Chave) x)
        {
            if (string.Equals(x.P.Codigo, texto, StringComparison.OrdinalIgnoreCase)) return 0;
            if (x.P.Codigo.StartsWith(texto, StringComparison.OrdinalIgnoreCase)) return 1;
            if (x.Chave.StartsWith(chave, StringComparison.Ordinal)) return 2;
            if (x.Chave.Contains(" " + chave, StringComparison.Ordinal)) return 3;
            if (x.Chave.Contains(chave, StringComparison.Ordinal)) return 4;
            return 99;
        }
        var achados = catalogo.Select(x => (x.P, Pontos: Pontos(x), x.Chave)).Where(x => x.Pontos < 99).ToList();
        sugestoes = achados.OrderBy(x => x.Pontos).ThenBy(x => x.Chave, StringComparer.Ordinal).Take(7).Select(x => x.P).ToList();
        indiceSugestao = 0;
        DesenharSugestoes(achados.Count, texto);
        popup.Width = Math.Max(420, Busca.Bounds.Width);
        popup.IsOpen = true;
    }

    void FecharSugestoes() { if (popup.IsOpen) popup.IsOpen = false; }

    void DesenharSugestoes(int? total = null, string? texto = null)
    {
        listaSugestoes.Children.Clear();
        for (int i = 0; i < sugestoes.Count; i++)
        {
            var p = sugestoes[i]; var idx = i;
            var b = new Button
            {
                Tag = p.Nome, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8), MinHeight = 52, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(0),
                Background = i == indiceSugestao ? Cores.P(Cores.PrimariaSuave) : Brushes.Transparent,
            };
            b.Classes.Add("sugestao");
            var inicial = new Border
            {
                Width = 36, Height = 36, CornerRadius = new CornerRadius(10), Background = Cores.P(i == indiceSugestao ? "#E0E7FF" : Cores.CinzaFundo), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = p.Nome.Length > 0 ? p.Nome[..1].ToUpperInvariant() : "?", FontWeight = FontWeight.SemiBold, FontSize = 15, Foreground = Cores.P(Cores.Primaria), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var (corEst, txtEst) = p.Estoque <= 0 ? ("#DC2626", "sem estoque") : p.Baixo ? ("#B45309", $"{p.Estoque} em estoque") : ("#059669", $"{p.Estoque} em estoque");
            var meio = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0), Spacing = 1,
                Children =
                {
                    new TextBlock { Text = p.Nome, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = $"Cód. {p.Codigo}  ·  {p.Categoria}", FontSize = 12, Foreground = Cores.P(Cores.Texto2), TextTrimming = TextTrimming.CharacterEllipsis },
                },
            };
            var dir = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center, Spacing = 1,
                Children =
                {
                    new TextBlock { Text = Util.FmtDinheiro(venda ? p.PrecoVenda : p.PrecoCusto), FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto), HorizontalAlignment = HorizontalAlignment.Right },
                    new TextBlock { Text = txtEst, FontSize = 12, Foreground = Cores.P(corEst), HorizontalAlignment = HorizontalAlignment.Right },
                },
            };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            K.ColocarNaGrade(g, inicial, 0, 0); K.ColocarNaGrade(g, meio, 0, 1); K.ColocarNaGrade(g, dir, 0, 2);
            b.Content = g;
            b.Click += (_, _) => Fazer(() => EscolherSugestaoAsync(idx));
            listaSugestoes.Children.Add(b);
        }
        if (total != null)
        {
            if (sugestoes.Count == 0)
            {
                listaSugestoes.Children.Add(new StackPanel
                {
                    Margin = new Thickness(12, 12, 12, 4), Spacing = 4,
                    Children =
                    {
                        new TextBlock { Text = $"Nenhum produto com “{texto}”.", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto) },
                        new TextBlock { Text = "Aperte Enter para cadastrar agora mesmo.", FontSize = 13, Foreground = Cores.P(Cores.Texto2) },
                    },
                });
                rodapeSugestoes.Text = "Esc fecha";
            }
            else
            {
                var mais = total.Value - sugestoes.Count;
                rodapeSugestoes.Text = (mais > 0 ? $"+{mais} outro(s) — clique em “Ver lista” para ver todos.   " : "") +
                                       (venda ? "↑ ↓ escolher  ·  Enter adiciona  ·  Esc fecha" : "↑ ↓ escolher  ·  Enter seleciona  ·  Esc fecha");
            }
        }
    }

    /// <summary>Usa a sugestão escolhida: na venda já entra no carrinho (com a quantidade do "3x", se houver).</summary>
    public async Task EscolherSugestaoAsync(int indice)
    {
        if (indice < 0 || indice >= sugestoes.Count) return;
        var p = sugestoes[indice];
        var (qtd, _) = SepararQuantidade(Busca.Text);
        FecharSugestoes();
        if (qtd != null) Quantidade.Text = qtd.ToString();
        Selecionar(p);
        if (venda) await AdicionarAsync();
    }

    async Task ListaCompletaAsync()
    {
        FecharSugestoes();
        var (qtd, texto) = SepararQuantidade(Busca.Text);
        var p = await SelecionarProdutoDialog.Abrir(Janela, svc, texto);
        if (p == null) return;
        if (qtd != null) Quantidade.Text = qtd.ToString();
        Selecionar(p);
    }

    // ------------------------------------------------------------------ atalhos e carrinho
    void AjustarAtalhos(double largura)
    {
        if (atalhos == null || largura <= 0) return;
        var colunas = Math.Clamp((int)(largura / 175), 2, 8);
        if (atalhos.Columns != colunas) atalhos.Columns = colunas;
        for (int i = 0; i < atalhos.Children.Count; i++) atalhos.Children[i].IsVisible = i < colunas * 2;
    }

    /// <summary>Mostra os produtos mais vendidos como blocos de atalho (somente na venda).</summary>
    public void CarregarAtalhos()
    {
        if (atalhos == null || atalhosCaixa == null) return;
        atalhos.Children.Clear();
        foreach (var p in svc.ProdutosRapidos(8))
        {
            var b = new Button { Tag = p.Nome, Padding = new Thickness(12, 8), Margin = new Thickness(0, 0, 8, 8) };
            b.Classes.Add("bloco");
            b.Content = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    new TextBlock { Text = p.Nome, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = Util.FmtDinheiro(p.PrecoVenda), FontSize = 12.5, FontWeight = FontWeight.Medium, Foreground = Cores.P(Cores.Primaria) },
                },
            };
            b.Click += (_, _) => Fazer(() => AdicionarRapidoAsync(p));
            atalhos.Children.Add(b);
        }
        atalhosCaixa.IsVisible = atalhos.Children.Count > 0;
        AjustarAtalhos(atalhos.Bounds.Width);
    }

    /// <summary>Atalho: adiciona 1 unidade do produto ao carrinho.</summary>
    public async Task AdicionarRapidoAsync(Produto p)
    {
        Selecionar(p);
        Quantidade.Text = "1";
        await AdicionarAsync();
    }

    public long Total => itens.Sum(i => i.Quantidade * i.Preco);
    public List<ItemVendaInput> ParaVenda() => itens.Select(i => new ItemVendaInput(i.ProdutoId, i.Quantidade, i.Preco)).ToList();
    public List<ItemEntradaInput> ParaEntrada() => itens.Select(i => new ItemEntradaInput(i.ProdutoId, i.Quantidade, i.Preco)).ToList();
    public void Focar() => Busca.Focus();

    void DefinirBusca(string texto)
    {
        suprimirSugestoes = true;
        try { Busca.Text = texto; }
        finally { suprimirSugestoes = false; }
    }

    public void Limpar()
    {
        itens.Clear();
        produto = null;
        DefinirBusca(""); Quantidade.Text = "1"; Preco.Text = "";
        FecharSugestoes();
        AtualizarSelecionado();
        Redesenhar();
    }

    void Redesenhar()
    {
        Tabela.Definir(itens.Select((i, n) => new Linha
        {
            Id = n.ToString(),
            C = new[] { i.Codigo, i.Nome, i.Quantidade.ToString(), Util.FmtDinheiro(i.Preco), Util.FmtDinheiro(i.Quantidade * i.Preco) },
        }));
        var unidades = itens.Sum(i => i.Quantidade);
        ((TextBlock)contador.Child!).Text = unidades == 1 ? "1 unidade" : $"{unidades} unidades";
        contador.IsVisible = itens.Count > 0;
        aoMudar?.Invoke();
    }

    void AtualizarSelecionado()
    {
        selecionado.IsVisible = produto != null;
        lblProduto.Text = produto == null ? "Nenhum produto selecionado" : $"{produto.Nome}  ·  {produto.Categoria}  ·  estoque atual: {produto.Estoque} {produto.Unidade}";
    }

    void BuscaEditada()
    {
        // se o texto deixou de corresponder ao produto escolhido, descarta a escolha
        if (produto != null && !string.Equals((Busca.Text ?? "").Trim(), produto.Codigo, StringComparison.OrdinalIgnoreCase))
        {
            produto = null;
            AtualizarSelecionado();
        }
    }

    static readonly Regex QuantidadeNaBusca = new(@"^(\d{1,4})\s*[*xX]\s*(\S.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Busca por código ou nome. Atalhos: "3x camiseta" já define a quantidade; na venda, ler um código de barras exato
    /// adiciona o produto direto; se o produto não existe, oferece cadastrá-lo na hora.
    /// </summary>
    public async Task BuscarAsync()
    {
        FecharSugestoes();
        var texto = (Busca.Text ?? "").Trim();
        var m = QuantidadeNaBusca.Match(texto);
        if (m.Success) { Quantidade.Text = m.Groups[1].Value; texto = m.Groups[2].Value.Trim(); DefinirBusca(texto); }
        var exato = texto.Length > 0 ? svc.ObterProdutoPorCodigo(texto) : null;
        var p = exato;
        var recemCadastrado = false;
        if (p == null)
        {
            var achados = texto.Length > 0 ? svc.ListarProdutos(texto) : new List<Produto>();
            if (achados.Count == 1) p = achados[0];
            else if (texto.Length > 0 && achados.Count == 0)
            {
                if (!await Plat.Atual.Confirmar(Janela, "Produto não encontrado", $"Não encontrei “{texto}”.\n\nQuer cadastrar esse produto agora?")) return;
                p = await Fluxos.NovoProdutoAsync(Janela, svc, texto, avancado);
                recemCadastrado = p != null;
            }
            else p = await SelecionarProdutoDialog.Abrir(Janela, svc, texto);
        }
        if (p == null) return;
        Selecionar(p);
        if (venda && (exato != null || recemCadastrado) && p.Estoque > 0) await AdicionarAsync();
    }

    public void Selecionar(Produto p)
    {
        produto = p;
        DefinirBusca(p.Codigo);
        produto = p; // (a troca de texto acima não pode descartar a escolha)
        FecharSugestoes();
        Preco.Text = Util.FmtDinheiro(venda ? p.PrecoVenda : p.PrecoCusto, false);
        AtualizarSelecionado();
        Quantidade.Focus();
        Quantidade.SelectAll();
    }

    long NoCarrinho(long produtoId) => itens.Where(i => i.ProdutoId == produtoId).Sum(i => i.Quantidade);

    public async Task AdicionarAsync()
    {
        var p = produto;
        if (p == null)
        {
            if ((Busca.Text ?? "").Trim().Length > 0) { await BuscarAsync(); return; }
            throw new ValidacaoException("Busque e selecione um produto primeiro.");
        }
        var qtd = Util.ParseInteiro(Quantidade.Text, "Quantidade", 1);
        var preco = Util.ParseDinheiro(Preco.Text, venda ? "Preço" : "Custo");
        if (venda)
        {
            var atual = svc.ObterProduto(p.Id); // o estoque pode ter mudado desde a busca
            if (!atual.Ativo) throw new ValidacaoException($"O produto '{atual.Nome}' está inativo.");
            var ja = NoCarrinho(p.Id);
            if (ja + qtd > atual.Estoque)
                throw new ValidacaoException(atual.Estoque <= 0 ? $"'{atual.Nome}' está sem estoque."
                                                               : $"Estoque insuficiente de '{atual.Nome}': disponível {atual.Estoque}, já na venda {ja}, pedido +{qtd}.");
        }
        var existente = itens.FirstOrDefault(i => i.ProdutoId == p.Id && i.Preco == preco);
        if (existente != null) existente.Quantidade += qtd;
        else itens.Add(new ItemCarrinho { ProdutoId = p.Id, Codigo = p.Codigo, Nome = p.Nome, Quantidade = qtd, Preco = preco });
        produto = null;
        DefinirBusca(""); Quantidade.Text = "1"; Preco.Text = "";
        AtualizarSelecionado();
        Redesenhar();
        Busca.Focus();
    }

    int? IndiceSelecionado => int.TryParse(Tabela.IdSelecionado, out var n) ? n : null;

    public Task RemoverAsync()
    {
        var i = IndiceSelecionado ?? throw new ValidacaoException("Selecione o item que deseja remover.");
        itens.RemoveAt(i);
        Redesenhar();
        return Task.CompletedTask;
    }

    public async Task AlterarQuantidadeAsync()
    {
        var i = IndiceSelecionado ?? throw new ValidacaoException("Selecione o item que deseja alterar.");
        var item = itens[i];
        var txt = await Plat.Atual.Perguntar(Janela, "Alterar quantidade", $"Nova quantidade de '{item.Nome}':", item.Quantidade.ToString());
        if (txt == null) return;
        var nova = Util.ParseInteiro(txt, "Quantidade", 1);
        if (venda)
        {
            var atual = svc.ObterProduto(item.ProdutoId);
            var outros = NoCarrinho(item.ProdutoId) - item.Quantidade;
            if (outros + nova > atual.Estoque) throw new ValidacaoException($"Estoque insuficiente: disponível {atual.Estoque}.");
        }
        item.Quantidade = nova;
        Redesenhar();
    }
}

/// <summary>Cadastro de tipos de produto (categorias).</summary>
public sealed class CategoriasDialog : JanelaBase
{
    readonly Servico svc;
    readonly Tabela tabela;
    readonly Action? aoFechar;

    public static Task Abrir(Window? dono, Servico svc, Action? aoFechar) =>
        dono == null ? Task.CompletedTask : new CategoriasDialog(svc, aoFechar).ShowDialog(dono);

    CategoriasDialog(Servico svc, Action? aoFechar)
    {
        this.svc = svc; this.aoFechar = aoFechar;
        Title = "Tipos de produto";
        Width = 480; Height = 460; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        tabela = new Tabela(new Col("Tipo de produto", 240, Expande: true), new Col("Produtos", 110, true));
        var barra = K.Linha(K.Botao("Novo", () => Fazer(NovoAsync)), K.Botao("Renomear", () => Fazer(RenomearAsync)),
                            K.Botao("Excluir", () => Fazer(ExcluirAsync)));
        var fechar = K.Botao("Fechar", Close);
        var rodape = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(fechar, Dock.Right); rodape.Children.Add(fechar); rodape.Children.Add(barra);
        var info = K.Discreto("Os tipos organizam o estoque (ex.: Bebidas, Limpeza, Papelaria)."); info.Margin = new Thickness(0, 0, 0, 8);
        var doca = K.Doca(info, tabela, rodape); doca.Margin = new Thickness(16);
        Content = doca;
        tabela.Duplo += () => Fazer(RenomearAsync);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) => aoFechar?.Invoke();
        Recarregar();
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    void Recarregar() =>
        tabela.Definir(svc.ListarCategorias().Select(c => new Linha { Id = c.Id.ToString(), C = new[] { c.Nome, c.Produtos.ToString() } }));

    Task<bool> Form(string titulo, string nome, long? id) =>
        FormDialog.Abrir(this, titulo, new() { new Campo { Chave = "nome", Rotulo = "Nome", Obrigatorio = true } },
                         new() { ["nome"] = nome }, v => svc.SalvarCategoria(v.Texto("nome"), id));

    async Task NovoAsync() { await Form("Novo tipo de produto", "", null); Recarregar(); }

    async Task RenomearAsync()
    {
        var id = tabela.IdLong ?? throw new ValidacaoException("Selecione um tipo na lista primeiro.");
        var atual = svc.ListarCategorias().First(c => c.Id == id);
        await Form("Renomear tipo de produto", atual.Nome, id);
        Recarregar();
    }

    async Task ExcluirAsync()
    {
        var id = tabela.IdLong ?? throw new ValidacaoException("Selecione um tipo na lista primeiro.");
        if (await Plat.Atual.Confirmar(this, "Excluir", "Excluir o tipo selecionado?"))
        {
            svc.ExcluirCategoria(id);
            Recarregar();
        }
    }
}

/// <summary>Histórico de movimentos de estoque (geral ou de um produto).</summary>
public sealed class MovimentosDialog : JanelaBase
{
    static readonly Dictionary<string, string> Tipos = new()
    {
        ["ENTRADA"] = "Entrada", ["VENDA"] = "Venda", ["CANCELAMENTO_VENDA"] = "Venda cancelada",
        ["CANCELAMENTO_ENTRADA"] = "Entrada cancelada", ["AJUSTE"] = "Ajuste",
    };

    public static Task Abrir(Window? dono, Servico svc, long? produtoId, string titulo) =>
        dono == null ? Task.CompletedTask : new MovimentosDialog(svc, produtoId, titulo).ShowDialog(dono);

    public MovimentosDialog(Servico svc, long? produtoId, string titulo)
    {
        Title = titulo;
        Width = 1000; Height = 560; MinWidth = 700; MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var tabela = new Tabela(new Col("Data", 130), new Col("Produto", 220, Expande: true), new Col("Tipo", 130), new Col("Qtd.", 76, true),
                                new Col("Saldo", 96, true), new Col("Referência", 120), new Col("Observação", 260));
        tabela.Definir(svc.ListarMovimentos(produtoId).Select(m => new Linha
        {
            Id = m.Id.ToString(),
            C = new[] { Util.FmtData(m.Data), $"{m.Codigo} - {m.Nome}", Tipos.GetValueOrDefault(m.Tipo, m.Tipo),
                        m.Quantidade > 0 ? $"+{m.Quantidade}" : m.Quantidade.ToString(), m.SaldoApos.ToString(), m.Referencia, m.Obs },
        }));
        var fechar = K.Botao("Fechar", Close); fechar.HorizontalAlignment = HorizontalAlignment.Right;
        var nota = K.Discreto("Mostrando os 500 movimentos mais recentes."); nota.Margin = new Thickness(0, 8, 0, 8);
        var rodape = new StackPanel { Children = { nota, fechar } };
        var doca = K.Doca(null, tabela, rodape); doca.Margin = new Thickness(16);
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }
}

/// <summary>Visualização da nota dentro do programa (sempre funciona, mesmo sem navegador).</summary>
public sealed class NotaDialog : JanelaBase
{
    readonly Venda venda;

    public static Task Abrir(Window? dono, Servico svc, long vendaId) =>
        dono == null ? Task.CompletedTask : new NotaDialog(svc.ObterVenda(vendaId)).ShowDialog(dono);

    public NotaDialog(Venda venda)
    {
        this.venda = venda;
        Title = $"Nota {Nota.Numero(venda)}";
        Width = 720; Height = 640; MinWidth = 600; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var texto = new TextBox
        {
            Text = Nota.GerarTexto(venda), IsReadOnly = true, AcceptsReturn = true, FontFamily = new Avalonia.Media.FontFamily("Cascadia Mono,Consolas,DejaVu Sans Mono,Liberation Mono,monospace"),
            FontSize = 12.5, TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(texto, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        var imprimir = K.Botao("Imprimir / Salvar PDF (navegador)", () => AbrirNoNavegador(this, venda), true);
        var fechar = K.Botao("Fechar", Close);
        var rodape = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(fechar, Dock.Right); rodape.Children.Add(fechar); rodape.Children.Add(imprimir);
        imprimir.HorizontalAlignment = HorizontalAlignment.Left;
        var doca = K.Doca(null, texto, rodape); doca.Margin = new Thickness(16);
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    /// <summary>Grava a nota em HTML e abre no navegador (de onde dá para imprimir ou salvar em PDF).</summary>
    public static async void AbrirNoNavegador(Window? dono, Venda venda, bool cupom = false)
    {
        try
        {
            var caminho = Nota.SalvarHtml(venda, Config.Subpasta("notas"), cupom);
            if (!Plat.Atual.Abrir(caminho))
                await Plat.Atual.Mensagem(dono, "Nota salva",
                    $"Não foi possível abrir o navegador automaticamente.\n\nA nota foi salva em:\n{caminho}\n\nAbra esse arquivo no navegador para imprimir ou salvar como PDF.", Nivel.Info);
        }
        catch (Exception ex) { await Erros.Tratar(ex, dono); }
    }
}

public sealed class EntradaDetalheDialog : JanelaBase
{
    public static Task Abrir(Window? dono, Entrada e) => dono == null ? Task.CompletedTask : new EntradaDetalheDialog(e).ShowDialog(dono);

    public EntradaDetalheDialog(Entrada e)
    {
        Title = $"Entrada {e.Id:000000}";
        Width = 760; Height = 480; MinWidth = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var cab = $"Fornecedor: {e.Fornecedor}\nData: {Util.FmtData(e.Data)}    Documento: {(e.NumeroDoc.Length > 0 ? e.NumeroDoc : "-")}\nStatus: {e.Status}";
        if (e.Cancelada) cab += $"  (motivo: {e.MotivoCancelamento})";
        if (e.Obs.Length > 0) cab += $"\nObs.: {e.Obs}";
        var tabela = new Tabela(new Col("Código", 100), new Col("Produto", 280, Expande: true), new Col("Qtd.", 76, true), new Col("Custo unit.", 120, true), new Col("Total", 130, true));
        tabela.Definir(e.Itens.Select(i => new Linha
        {
            Id = i.Id.ToString(),
            C = new[] { i.Codigo, i.Nome, i.Quantidade.ToString(), Util.FmtDinheiro(i.CustoUnit), Util.FmtDinheiro(i.Quantidade * i.CustoUnit) },
        }));
        var total = K.Rotulo($"Total: {Util.FmtDinheiro(e.Total)}", true, 15); total.HorizontalAlignment = HorizontalAlignment.Right; total.Margin = new Thickness(0, 8, 0, 8);
        var fechar = K.Botao("Fechar", Close); fechar.HorizontalAlignment = HorizontalAlignment.Right;
        var topo = new TextBlock { Text = cab, Margin = new Thickness(0, 0, 0, 10) };
        var doca = K.Doca(topo, tabela, new StackPanel { Children = { total, fechar } }); doca.Margin = new Thickness(16);
        Content = doca;
        KeyDown += (_, ev) => { if (ev.Key == Key.Escape) Close(); };
    }
}
