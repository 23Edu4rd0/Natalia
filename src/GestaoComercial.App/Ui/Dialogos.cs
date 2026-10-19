using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

        busca = K.Caixa(buscaInicial, dica: "Digite parte do nome ou do código");
        busca.HorizontalAlignment = HorizontalAlignment.Stretch;
        tabela = new Tabela(new Col("Código", 100), new Col("Produto", 260, Expande: true), new Col("Tipo", 130),
                            new Col("Preço venda", 120, true), new Col("Estoque", 100, true));
        var escolher = K.Botao("Selecionar", Escolher, true);
        var cancelar = K.Botao("Cancelar", Close);
        var barra = K.Linha(escolher, cancelar); barra.HorizontalAlignment = HorizontalAlignment.Right; barra.Margin = new Thickness(0, 10, 0, 0);
        var topo = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var rot = K.Rotulo("Buscar:"); rot.Margin = new Thickness(0, 0, 8, 0); DockPanel.SetDock(rot, Dock.Left);
        topo.Children.Add(rot); topo.Children.Add(busca);
        var doca = K.Doca(topo, tabela, barra); doca.Margin = new Thickness(16);
        Content = doca;

        K.AoMudarTexto(busca, Carregar);
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
/// </summary>
public sealed class EditorItens : UserControl
{
    readonly Servico svc;
    readonly bool venda;
    readonly Action? aoMudar;
    readonly List<ItemCarrinho> itens = new();
    Produto? produto;

    public TextBox Busca { get; }
    public TextBox Quantidade { get; }
    public TextBox Preco { get; }
    public Tabela Tabela { get; }
    readonly TextBlock lblProduto;
    readonly WrapPanel? atalhos;
    readonly StackPanel? atalhosCaixa;
    public IReadOnlyList<ItemCarrinho> Itens => itens;
    public IReadOnlyList<string> NomesAtalhos => atalhos?.Children.OfType<Button>().Select(b => (string)b.Tag!).ToList() ?? new List<string>();
    public Produto? ProdutoSelecionado => produto;

    Window? Janela => TopLevel.GetTopLevel(this) as Window;

    public EditorItens(Servico svc, bool venda, Action? aoMudar)
    {
        this.svc = svc; this.venda = venda; this.aoMudar = aoMudar;

        Busca = K.Caixa("", dica: "Digite o código, código de barras ou nome do produto");
        Busca.HorizontalAlignment = HorizontalAlignment.Stretch;
        Busca.InnerLeftContent = K.Margem(Icones.Criar("busca", 18, "#9CA3AF"), 10, 0, 0, 0);
        Quantidade = K.Caixa("1", 90);
        Preco = K.Caixa("", 140);
        lblProduto = K.Discreto("Nenhum produto selecionado");

        var linhaBusca = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        K.ColocarNaGrade(linhaBusca, Busca, 0, 0);
        K.ColocarNaGrade(linhaBusca, K.Margem(K.Botao("Buscar…", () => Fazer(BuscarAsync)), 8, 0, 0, 0), 0, 1);

        var adicionar = K.Botao("Adicionar", () => Fazer(AdicionarAsync), icone: "mais", classe: "primario");
        adicionar.VerticalAlignment = VerticalAlignment.Bottom;
        var linhaQtd = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 12,
            Children = { K.Campo("Quantidade", Quantidade), K.Campo(venda ? "Preço unit. (R$)" : "Custo unit. (R$)", Preco), adicionar },
        };
        var painelAdd = new StackPanel { Spacing = 10, Children = { linhaBusca } };
        if (venda)
        {
            atalhos = new WrapPanel { Orientation = Orientation.Horizontal };
            painelAdd.Children.Add(atalhosCaixa = new StackPanel { Spacing = 6, Children = { K.Etiqueta("Mais vendidos (clique para adicionar 1)"), atalhos } });
            atalhosCaixa.IsVisible = false;
        }
        painelAdd.Children.Add(lblProduto);
        painelAdd.Children.Add(linhaQtd);
        var caixaAdd = K.Cartao(painelAdd);
        caixaAdd.Margin = new Thickness(0, 0, 0, 14);

        Tabela = new Tabela(new Col("Código", 95), new Col("Produto", 300, Expande: true), new Col("Qtd.", 70, true),
                            new Col(venda ? "Preço unit." : "Custo unit.", 110, true), new Col("Total", 110, true));
        var barra = K.Linha(K.Botao("Remover item", () => Fazer(RemoverAsync), icone: "lixeira"),
                            K.Botao("Alterar quantidade", () => Fazer(AlterarQuantidadeAsync), icone: "editar"),
                            K.Botao("Limpar tudo", Limpar));
        barra.Margin = new Thickness(0, 12, 0, 0);
        Content = K.Doca(caixaAdd, Tabela, barra);

        K.AoMudarTexto(Busca, BuscaEditada);
        K.AoApertarEnter(Busca, () => Fazer(BuscarAsync));
        K.AoApertarEnter(Quantidade, () => Fazer(AdicionarAsync));
        K.AoApertarEnter(Preco, () => Fazer(AdicionarAsync));
        Tabela.Duplo += () => Fazer(AlterarQuantidadeAsync);
        Tabela.KeyDown += (_, e) => { if (e.Key == Key.Delete) Fazer(RemoverAsync); };
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, Janela); }
    }

    /// <summary>Mostra os produtos mais vendidos como botões de atalho (somente na venda).</summary>
    public void CarregarAtalhos()
    {
        if (atalhos == null || atalhosCaixa == null) return;
        atalhos.Children.Clear();
        foreach (var p in svc.ProdutosRapidos(8))
        {
            var b = K.Botao($"{p.Nome}  ·  {Util.FmtDinheiro(p.PrecoVenda)}", () => Fazer(() => AdicionarRapidoAsync(p)), classe: "chip");
            b.Tag = p.Nome; b.Margin = new Thickness(0, 0, 8, 8);
            atalhos.Children.Add(b);
        }
        atalhosCaixa.IsVisible = atalhos.Children.Count > 0;
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

    public void Limpar()
    {
        itens.Clear();
        produto = null;
        Busca.Text = ""; Quantidade.Text = "1"; Preco.Text = "";
        lblProduto.Text = "Nenhum produto selecionado";
        Redesenhar();
    }

    void Redesenhar()
    {
        Tabela.Definir(itens.Select((i, n) => new Linha
        {
            Id = n.ToString(),
            C = new[] { i.Codigo, i.Nome, i.Quantidade.ToString(), Util.FmtDinheiro(i.Preco), Util.FmtDinheiro(i.Quantidade * i.Preco) },
        }));
        aoMudar?.Invoke();
    }

    void BuscaEditada()
    {
        // se o texto deixou de corresponder ao produto escolhido, descarta a escolha
        if (produto != null && !string.Equals((Busca.Text ?? "").Trim(), produto.Codigo, StringComparison.OrdinalIgnoreCase))
        {
            produto = null;
            lblProduto.Text = "Nenhum produto selecionado";
        }
    }

    public async Task BuscarAsync()
    {
        var texto = (Busca.Text ?? "").Trim();
        var p = texto.Length > 0 ? svc.ObterProdutoPorCodigo(texto) : null;
        if (p == null)
        {
            var achados = texto.Length > 0 ? svc.ListarProdutos(texto) : new List<Produto>();
            if (achados.Count == 1) p = achados[0];
            else if (texto.Length > 0 && achados.Count == 0)
            {
                await Plat.Atual.Mensagem(Janela, "Busca", $"Nenhum produto encontrado para '{texto}'.", Nivel.Info);
                return;
            }
            else p = await SelecionarProdutoDialog.Abrir(Janela, svc, texto);
        }
        if (p != null) Selecionar(p);
    }

    public void Selecionar(Produto p)
    {
        produto = p;
        Busca.Text = p.Codigo;
        produto = p; // (a troca de texto acima não pode descartar a escolha)
        Preco.Text = Util.FmtDinheiro(venda ? p.PrecoVenda : p.PrecoCusto, false);
        lblProduto.Text = $"{p.Nome}  ({p.Categoria})   |   Estoque atual: {p.Estoque} {p.Unidade}";
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
                throw new ValidacaoException($"Estoque insuficiente de '{atual.Nome}': disponível {atual.Estoque}, já no carrinho {ja}, pedido +{qtd}.");
        }
        var existente = itens.FirstOrDefault(i => i.ProdutoId == p.Id && i.Preco == preco);
        if (existente != null) existente.Quantidade += qtd;
        else itens.Add(new ItemCarrinho { ProdutoId = p.Id, Codigo = p.Codigo, Nome = p.Nome, Quantidade = qtd, Preco = preco });
        produto = null;
        Busca.Text = ""; Quantidade.Text = "1"; Preco.Text = "";
        lblProduto.Text = "Nenhum produto selecionado";
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
        tabela = new Tabela(new Col("Tipo de produto", 280, Expande: true), new Col("Produtos", 90, true));
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
        var tabela = new Tabela(new Col("Data", 130), new Col("Produto", 220, Expande: true), new Col("Tipo", 130), new Col("Qtd.", 70, true),
                                new Col("Saldo", 70, true), new Col("Referência", 120), new Col("Observação", 260));
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
        var tabela = new Tabela(new Col("Código", 100), new Col("Produto", 280, Expande: true), new Col("Qtd.", 70, true), new Col("Custo unit.", 120, true), new Col("Total", 130, true));
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
