using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Nova venda: produtos à esquerda, resumo e pagamento à direita.</summary>
public sealed class VendaTela : Tela
{
    const string Consumidor = "Consumidor final";

    public ComboBox Cliente { get; }
    public ComboBox Pagamento { get; }
    public EditorItens Editor { get; }
    public TextBox Desconto { get; }
    public TextBox Obs { get; }
    public TextBox ValorRecebido { get; }
    readonly TextBlock lblSub = K.Rotulo("Subtotal", false, 13, Cores.Texto2), lblSubValor = K.Rotulo("R$ 0,00", false, 13);
    readonly TextBlock lblDesc = K.Rotulo("Desconto", false, 13, Cores.Texto2), lblDescValor = K.Rotulo("", false, 13, Cores.Vermelho);
    readonly TextBlock lblTotal = K.Rotulo("R$ 0,00", true, 32, "#15803D");
    readonly TextBlock lblTroco = K.Rotulo("", true, 16);
    readonly StackPanel painelTroco;
    readonly Grid linhaDesconto;
    Dictionary<string, long?> clientes = new() { [Consumidor] = null };

    public string TextoTotal => "TOTAL: " + lblTotal.Text;
    public string TextoSubtotal => "Subtotal: " + lblSubValor.Text + (lblDescValor.Text?.Length > 0 ? "    Desconto: " + lblDescValor.Text : "");
    public string TextoTroco => lblTroco.Text ?? "";
    public bool TrocoVisivel => painelTroco.IsVisible;

    public override string Titulo => "Nova venda";
    public override string Subtitulo => "Adicione os produtos, escolha como o cliente vai pagar e finalize.";

    public VendaTela(MainWindow app) : base(app)
    {
        Editor = new EditorItens(Svc, true, Recalcular);

        Cliente = K.Combo(new[] { Consumidor }, double.NaN, Consumidor); Cliente.HorizontalAlignment = HorizontalAlignment.Stretch;
        Pagamento = K.Combo(Servico.FormasPagamento, double.NaN, Servico.FormasPagamento[0]); Pagamento.HorizontalAlignment = HorizontalAlignment.Stretch;
        Pagamento.SelectionChanged += (_, _) => Recalcular();
        Desconto = K.Caixa("", double.NaN, "0,00 ou 10%"); K.AoMudarTexto(Desconto, Recalcular);
        ValorRecebido = K.Caixa("", double.NaN, "Quanto o cliente entregou?"); K.AoMudarTexto(ValorRecebido, Recalcular);
        Obs = K.Caixa("", double.NaN, "Opcional");

        var novoCliente = K.Botao("Novo", SeguroAsync(NovoClienteAsync), icone: "mais");
        var gCliente = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        K.ColocarNaGrade(gCliente, Cliente, 0, 0); K.ColocarNaGrade(gCliente, K.Margem(novoCliente, 8, 0, 0, 0), 0, 1);

        painelTroco = new StackPanel
        {
            Spacing = 6, IsVisible = false,
            Children = { K.Campo("Valor recebido", ValorRecebido), new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { K.Rotulo("Troco:", false, 14, Cores.Texto2), lblTroco } } },
        };

        linhaDesconto = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), IsVisible = false };
        K.ColocarNaGrade(linhaDesconto, lblDesc, 0, 0); K.ColocarNaGrade(linhaDesconto, lblDescValor, 0, 1);
        var linhaSub = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        K.ColocarNaGrade(linhaSub, lblSub, 0, 0); K.ColocarNaGrade(linhaSub, lblSubValor, 0, 1);

        var finalizar = K.Botao("Finalizar venda   (F9)", SeguroAsync(FinalizarAsync), classe: "verde");
        finalizar.Tag = "Finalizar venda e emitir nota";
        finalizar.HorizontalAlignment = HorizontalAlignment.Stretch; finalizar.HorizontalContentAlignment = HorizontalAlignment.Center;
        finalizar.FontSize = 16; finalizar.MinHeight = 52;

        var resumo = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                K.H2("Resumo da venda"),
                K.Campo("Cliente", gCliente),
                K.Campo("Forma de pagamento", Pagamento),
                K.Campo("Desconto (R$ ou %)", Desconto),
                painelTroco,
                K.Campo("Observações", Obs),
                new Border { Height = 1, Background = Cores.P("#EEF0F5"), Margin = new Thickness(0, 2) },
                linhaSub, linhaDesconto,
                new StackPanel { Spacing = 0, Children = { K.Etiqueta("Total a pagar"), lblTotal } },
                finalizar,
            },
        };
        var cartaoResumo = K.Cartao(new ScrollViewer { Content = resumo, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        cartaoResumo.Width = 350; cartaoResumo.Margin = new Thickness(22, 0, 0, 0);

        var raiz = new DockPanel();
        DockPanel.SetDock(cartaoResumo, Dock.Right);
        raiz.Children.Add(cartaoResumo); raiz.Children.Add(Editor);
        Content = raiz;
    }

    public override void Atualizar()
    {
        var sel = Cliente.SelectedItem as string ?? Consumidor;
        clientes = new() { [Consumidor] = null };
        foreach (var c in Svc.ListarPessoas(TipoPessoa.Cliente))
        {
            var nome = clientes.ContainsKey(c.Nome) ? $"{c.Nome} (#{c.Id})" : c.Nome; // homônimos: distingue pelo id
            clientes[nome] = c.Id;
        }
        Cliente.ItemsSource = clientes.Keys.ToList();
        Cliente.SelectedItem = clientes.ContainsKey(sel) ? sel : Consumidor;
        Editor.CarregarAtalhos();
        Recalcular();
        Editor.Focar();
    }

    long LerDesconto() => Util.ParseDesconto(Desconto.Text, Editor.Total);

    void Recalcular()
    {
        var sub = Editor.Total;
        long desc;
        try { desc = Math.Min(LerDesconto(), sub); } catch (ValidacaoException) { desc = 0; } // ainda digitando
        var total = sub - desc;
        lblSubValor.Text = Util.FmtDinheiro(sub);
        linhaDesconto.IsVisible = desc > 0;
        lblDescValor.Text = desc > 0 ? "-" + Util.FmtDinheiro(desc) : "";
        lblTotal.Text = Util.FmtDinheiro(total);

        painelTroco.IsVisible = (Pagamento.SelectedItem as string) == "Dinheiro";
        if (painelTroco.IsVisible)
        {
            if (string.IsNullOrWhiteSpace(ValorRecebido.Text)) { lblTroco.Text = "-"; lblTroco.Foreground = Cores.P(Cores.Texto2); }
            else
            {
                long recebido;
                try { recebido = Util.ParseDinheiro(ValorRecebido.Text, "Valor recebido"); } catch (ValidacaoException) { recebido = -1; }
                if (recebido < 0) { lblTroco.Text = "valor inválido"; lblTroco.Foreground = Cores.P(Cores.Vermelho); }
                else if (recebido >= total) { lblTroco.Text = Util.FmtDinheiro(recebido - total); lblTroco.Foreground = Cores.P(Cores.Verde); }
                else { lblTroco.Text = $"faltam {Util.FmtDinheiro(total - recebido)}"; lblTroco.Foreground = Cores.P(Cores.Vermelho); }
            }
        }
    }

    void LimparTudo()
    {
        Editor.Limpar(); Desconto.Text = ""; Obs.Text = ""; ValorRecebido.Text = ""; Cliente.SelectedItem = Consumidor;
    }

    public async Task NovoClienteAsync()
    {
        var id = await Fluxos.NovoClienteRapidoAsync(Janela, Svc);
        if (id == null) return;
        Atualizar();
        var nome = clientes.FirstOrDefault(c => c.Value == id).Key;
        if (nome != null) Cliente.SelectedItem = nome;
    }

    /// <summary>Atalho F9.</summary>
    public void Finalizar() => SeguroAsync(FinalizarAsync)();

    public async Task FinalizarAsync()
    {
        var itens = Editor.ParaVenda();
        if (itens.Count == 0) { await Info("Adicione ao menos um produto à venda.", "Venda"); return; }
        var desconto = LerDesconto();
        var total = Editor.Total - desconto;
        if (total < 0) throw new ValidacaoException("O desconto não pode ser maior que o subtotal da venda.");
        var cliente = Cliente.SelectedItem as string ?? Consumidor;
        var forma = Pagamento.SelectedItem as string ?? Servico.FormasPagamento[0];
        if (forma == Servico.Fiado && clientes.GetValueOrDefault(cliente) == null)
            throw new ValidacaoException("Venda no fiado precisa de um cliente. Escolha o cliente ou clique em \"Novo\" para cadastrar.");
        if (!await Confirmar($"Confirmar venda?\n\nCliente: {cliente}\nItens: {itens.Count}\nTotal: {Util.FmtDinheiro(total)}\nPagamento: {forma}"))
            return;
        var vid = Svc.RegistrarVenda(itens, clientes.GetValueOrDefault(cliente), desconto, forma, Obs.Text);
        var trocoTexto = painelTroco.IsVisible && lblTroco.Foreground == Cores.P(Cores.Verde) ? $"\nTroco: {lblTroco.Text}" : "";
        LimparTudo();
        AtualizarTudo();
        if (await Confirmar($"Venda registrada com sucesso!\nNota nº {vid:000000} — {Util.FmtDinheiro(total)}{trocoTexto}\n\nDeseja abrir a nota para imprimir / salvar em PDF?", "Venda registrada"))
            NotaDialog.AbrirNoNavegador(Janela, Svc.ObterVenda(vid));
        Editor.Focar();
    }
}

/// <summary>Lista de vendas/notas emitidas: visualização, impressão, WhatsApp e cancelamento.</summary>
public sealed class NotasTela : Tela
{
    static readonly Dictionary<string, string?> Situacoes = new() { ["Todas"] = null, ["Concluídas"] = "CONCLUIDA", ["Canceladas"] = "CANCELADA" };

    public TextBox De { get; }
    public TextBox Ate { get; }
    public ComboBox Situacao { get; }
    public TextBox Busca { get; }
    public Tabela Tabela { get; }
    readonly TextBlock rodape = K.Rotulo("", true);
    public string TextoRodape => rodape.Text ?? "";

    public override string Titulo => "Vendas";
    public override string Subtitulo => "Todas as vendas e notas emitidas. Dê duplo clique para ver a nota.";

    public NotasTela(MainWindow app) : base(app)
    {
        De = K.Caixa(Util.InicioMesBr(), 120); Ate = K.Caixa(Util.HojeBr(), 120);
        Situacao = K.Combo(Situacoes.Keys, 140, "Todas");
        Busca = K.Caixa("", 220, "Nº da nota ou cliente");
        var filtros = K.Linha(10, K.Campo("De", De), K.Campo("Até", Ate), K.Campo("Situação", Situacao), K.Campo("Buscar", Busca));
        var filtrar = K.Botao("Filtrar", Seguro(Atualizar), icone: "busca", classe: "primario"); filtrar.VerticalAlignment = VerticalAlignment.Bottom;
        filtros.Children.Add(filtrar);
        K.AoApertarEnter(Busca, Seguro(Atualizar));
        Situacao.SelectionChanged += (_, _) => Seguro(Atualizar)();

        var acoes = K.Barra(
            K.Linha(K.Botao("Ver nota", SeguroAsync(VisualizarAsync), icone: "ver", classe: "primario"),
                    K.Botao("Imprimir / PDF", SeguroAsync(ImprimirAsync), icone: "imprimir"),
                    K.Botao("Cupom 80 mm", SeguroAsync(CupomAsync), icone: "notas"),
                    K.Botao("WhatsApp", SeguroAsync(WhatsAppAsync), icone: "whatsapp"),
                    K.Botao("Cancelar venda", SeguroAsync(CancelarAsync), icone: "lixeira", classe: "perigo")),
            K.Botao("Exportar CSV", SeguroAsync(ExportarAsync), icone: "exportar"));
        acoes.Margin = new Thickness(0, 14, 0, 14);

        Tabela = new Tabela(new Col("Nota", 90), new Col("Data", 160), new Col("Cliente", 240, Expande: true), new Col("Pagamento", 150),
                            new Col("Desconto", 100, true), new Col("Total", 120, true), new Col("Situação", 120, Chip: true));
        rodape.HorizontalAlignment = HorizontalAlignment.Right; rodape.Margin = new Thickness(0, 10, 0, 0);
        Content = K.Doca(new StackPanel { Children = { filtros, acoes } }, Tabela, rodape);
        Tabela.Duplo += SeguroAsync(VisualizarAsync);
    }

    List<Venda> Buscar(int limite = 1000) =>
        Svc.ListarVendas(Util.ParseDataBr(De.Text, "Data inicial"), Util.ParseDataBr(Ate.Text, "Data final"),
                         Situacoes[Situacao.SelectedItem as string ?? "Todas"], Busca.Text, limite);

    public override void Atualizar()
    {
        var vendas = Buscar();
        Tabela.Definir(vendas.Select(v => new Linha
        {
            Id = v.Id.ToString(), Estilo = v.Cancelada ? "cancelado" : "",
            Fabrica = () => new[] { $"{v.Id:000000}", Util.FmtData(v.Data), v.ClienteNome, v.FormaPagamento, Util.FmtDinheiro(v.Desconto), Util.FmtDinheiro(v.Total),
                                    v.Cancelada ? "Cancelada" : "Concluída" },
        }));
        var validas = vendas.Where(v => !v.Cancelada).ToList();
        rodape.Text = $"{validas.Count} venda(s) concluída(s) no período — Total: {Util.FmtDinheiro(validas.Sum(v => v.Total))}";
    }

    public Task VisualizarAsync() => NotaDialog.Abrir(Janela, Svc, Exigir(Tabela.IdLong, "uma nota"));

    public Task ImprimirAsync()
    {
        NotaDialog.AbrirNoNavegador(Janela, Svc.ObterVenda(Exigir(Tabela.IdLong, "uma nota")));
        return Task.CompletedTask;
    }

    public Task CupomAsync()
    {
        NotaDialog.AbrirNoNavegador(Janela, Svc.ObterVenda(Exigir(Tabela.IdLong, "uma nota")), cupom: true);
        return Task.CompletedTask;
    }

    public async Task WhatsAppAsync()
    {
        var v = Svc.ObterVenda(Exigir(Tabela.IdLong, "uma nota"));
        var telefone = v.ClienteId is long cid ? Svc.ObterPessoa(TipoPessoa.Cliente, cid).Telefone : "";
        await Fluxos.WhatsAppAsync(Janela, telefone, Nota.TextoWhatsApp(v));
    }

    public async Task CancelarAsync()
    {
        var vid = Exigir(Tabela.IdLong, "uma nota");
        var v = Svc.ObterVenda(vid);
        if (v.Cancelada) { await Info("Esta venda já está cancelada.", "Cancelar"); return; }
        var motivo = await Plat.Atual.Perguntar(Janela, "Cancelar venda",
            $"Cancelar a nota {vid:000000} ({Util.FmtDinheiro(v.Total)})?\nOs produtos voltam ao estoque.\n\nInforme o motivo:");
        if (motivo == null) return;
        Svc.CancelarVenda(vid, motivo);
        AtualizarTudo();
        await Info($"Nota {vid:000000} cancelada e estoque restaurado.");
    }

    public async Task ExportarAsync()
    {
        var vendas = Buscar(100000);
        if (vendas.Count == 0) { await Info("Não há vendas no período para exportar.", "Exportar"); return; }
        var caminho = await Plat.Atual.SalvarArquivo(Janela, "Exportar vendas", Config.Subpasta("exportacoes"), "vendas.csv", "CSV (Excel)", "csv");
        if (caminho == null) return;
        var n = Util.ExportarCsv(caminho, new[] { "Nota", "Data", "Cliente", "CPF/CNPJ", "Pagamento", "Subtotal", "Desconto", "Total", "Situação" },
            vendas.Select(v => new object?[] { $"{v.Id:000000}", Util.FmtData(v.Data), v.ClienteNome, v.ClienteDoc, v.FormaPagamento,
                                               Util.FmtDinheiro(v.Subtotal, false), Util.FmtDinheiro(v.Desconto, false), Util.FmtDinheiro(v.Total, false), v.Status }));
        await Info($"{n} venda(s) exportada(s) para:\n{caminho}", "Exportado");
    }
}
