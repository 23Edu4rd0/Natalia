using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Nova venda: produtos à esquerda, resumo e pagamento à direita.</summary>
public sealed class VendaTela : Tela
{
    const string Consumidor = "Consumidor final";
    public const string ConfigAposVenda = "apos_venda";

    public AutoCompleteBox Cliente { get; }
    /// <summary>Texto do cliente (vazio ou "Consumidor final" = sem cliente).</summary>
    public string ClienteNome { get => (Cliente.Text ?? "").Trim(); set { Cliente.IsDropDownOpen = false; Cliente.SelectedItem = null; Cliente.Text = value; AtualizarInfoCliente(); } }
    public ComboBox Pagamento { get; }
    public EditorItens Editor { get; }
    public TextBox Desconto { get; }
    public TextBox Obs { get; }
    public TextBox ValorRecebido { get; }
    readonly TextBlock lblSub = K.Rotulo("Subtotal", false, 13.5, Cores.Texto2), lblSubValor = K.Rotulo("R$ 0,00", false, 13.5, Cores.Corpo);
    readonly TextBlock lblDesc = K.Rotulo("Desconto", false, 13.5, Cores.Texto2), lblDescValor = K.Rotulo("", false, 13.5, Cores.Vermelho);
    readonly TextBlock lblTotal = new() { Text = "R$ 0,00", FontSize = 34, FontWeight = FontWeight.Bold, Foreground = Cores.P(Cores.Texto), LetterSpacing = -0.5 };
    readonly TextBlock lblTroco = K.Rotulo("", true, 16);
    readonly TextBlock lblClienteInfo = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    readonly Border caixaInfoCliente;
    readonly StackPanel painelTroco;
    readonly StackPanel extras;
    readonly Button botaoExtras;
    readonly Grid linhaDesconto;
    readonly Dictionary<string, Button> blocosPagamento = new();
    readonly ComboBox outraForma;
    Dictionary<string, long?> clientes = new();
    bool finalizando;

    public string TextoTotal => "TOTAL: " + lblTotal.Text;
    public string TextoSubtotal => "Subtotal: " + lblSubValor.Text + (lblDescValor.Text?.Length > 0 ? "    Desconto: " + lblDescValor.Text : "");
    public string TextoTroco => lblTroco.Text ?? "";
    public string TextoInfoCliente => lblClienteInfo.IsVisible ? lblClienteInfo.Text ?? "" : "";
    public bool TrocoVisivel => painelTroco.IsVisible;

    public override string Titulo => "Nova venda";
    public override string Subtitulo => "Digite o nome ou leia o código de barras. “3x nome” vende 3 de uma vez.";

    static readonly (string Forma, string Rotulo, string Icone)[] Blocos =
    {
        ("Dinheiro", "Dinheiro", "dinheiro"), ("Pix", "Pix", "pix"), ("Cartão de débito", "Débito", "cartao"),
        ("Cartão de crédito", "Crédito", "cartao"), ("Fiado", "Fiado", "receber"), ("Transferência", "Outra", "mais-horiz"),
    };

    public VendaTela(MainWindow app) : base(app)
    {
        Editor = new EditorItens(Svc, true, Recalcular, App.Avancado);

        Cliente = new AutoCompleteBox
        {
            Watermark = "Consumidor final (ou busque)", MinimumPrefixLength = 0, MaxDropDownHeight = 260, FilterMode = AutoCompleteFilterMode.Custom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextFilter = (busca, item) => string.IsNullOrWhiteSpace(busca) || Util.ChaveOrdenacao(item).Contains(Util.ChaveOrdenacao(busca.Trim())),
        };
        Cliente.GotFocus += (_, _) => { if (string.IsNullOrEmpty(Cliente.Text)) Cliente.IsDropDownOpen = true; };
        Cliente.PropertyChanged += (_, e) =>
        {
            if (e.Property != AutoCompleteBox.TextProperty) return;
            // digitou algo diferente do cliente escolhido antes: a escolha antiga não pode "voltar" ao sair do campo
            if (Cliente.SelectedItem is string s && !string.Equals(s, Cliente.Text, StringComparison.OrdinalIgnoreCase)) Cliente.SelectedItem = null;
            AtualizarInfoCliente();
        };
        Pagamento = K.Combo(Servico.FormasPagamento, double.NaN, Servico.FormasPagamento[0]); Pagamento.HorizontalAlignment = HorizontalAlignment.Stretch;
        Pagamento.SelectionChanged += (_, _) => { MarcarPagamento(); Recalcular(); AtualizarInfoCliente(); };
        Desconto = K.Caixa("", double.NaN, "0,00 ou 10%"); K.AoMudarTexto(Desconto, Recalcular);
        ValorRecebido = K.Caixa("", double.NaN, "Quanto o cliente entregou?"); K.AoMudarTexto(ValorRecebido, Recalcular);
        Obs = K.Caixa("", double.NaN, "Opcional");

        var novoCliente = K.Botao("Novo", SeguroAsync(NovoClienteAsync), icone: "mais");
        ToolTip.SetTip(novoCliente, "Cadastrar cliente novo");
        var gCliente = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        K.ColocarNaGrade(gCliente, Cliente, 0, 0); K.ColocarNaGrade(gCliente, K.Margem(novoCliente, 8, 0, 0, 0), 0, 1);
        caixaInfoCliente = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), IsVisible = false, Child = lblClienteInfo };

        // formas de pagamento como blocos (um clique); "Outra" mostra a lista com Transferência/Outro
        var gradePag = new UniformGrid { Columns = 3 };
        foreach (var (forma, rotulo, icone) in Blocos)
        {
            var b = new Button { Tag = "Pagamento: " + rotulo, Padding = new Thickness(6, 8), MinHeight = 60 };
            b.Classes.Add("bloco");
            b.HorizontalContentAlignment = HorizontalAlignment.Center;
            var ic = Icones.Criar(icone, 18, Cores.Texto2); ic.HorizontalAlignment = HorizontalAlignment.Center;
            b.Content = new StackPanel { Spacing = 4, Children = { ic, new TextBlock { Text = rotulo, FontSize = 13, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center } } };
            var idx = gradePag.Children.Count;
            b.Margin = new Thickness(idx % 3 == 0 ? 0 : 4, 0, idx % 3 == 2 ? 0 : 4, 8);
            b.Click += (_, _) => Pagamento.SelectedItem = forma == "Transferência" && Pagamento.SelectedItem is "Outro" ? "Outro" : forma;
            blocosPagamento[forma] = b;
            gradePag.Children.Add(b);
        }
        outraForma = K.Combo(new[] { "Transferência", "Outro" }, double.NaN, "Transferência");
        outraForma.HorizontalAlignment = HorizontalAlignment.Stretch; outraForma.IsVisible = false;
        outraForma.SelectionChanged += (_, _) => { if (outraForma.SelectedItem is string f && Pagamento.SelectedItem as string != f && outraForma.IsVisible) Pagamento.SelectedItem = f; };

        Button Chip(string texto, Action acao) { var b = K.Botao(texto, acao, classe: "chip"); b.Margin = new Thickness(0, 0, 6, 6); return b; }
        var chipsDesconto = new WrapPanel { Children = { Chip("5%", () => Desconto.Text = "5%"), Chip("10%", () => Desconto.Text = "10%"), Chip("Sem desconto", () => Desconto.Text = "") } };
        var chipsRecebido = new WrapPanel
        {
            Children = { Chip("Valor exato", () => ValorRecebido.Text = Util.FmtDinheiro(Editor.Total - SafeDesconto(), false)), Chip("R$ 20", () => ValorRecebido.Text = "20,00"), Chip("R$ 50", () => ValorRecebido.Text = "50,00"), Chip("R$ 100", () => ValorRecebido.Text = "100,00") },
        };
        painelTroco = new StackPanel
        {
            Spacing = 6, IsVisible = false,
            Children = { K.Campo("Valor recebido", ValorRecebido), chipsRecebido, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { K.Rotulo("Troco", false, 14, Cores.Texto2), lblTroco } } },
        };

        linhaDesconto = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), IsVisible = false };
        K.ColocarNaGrade(linhaDesconto, lblDesc, 0, 0); K.ColocarNaGrade(linhaDesconto, lblDescValor, 0, 1);
        var linhaSub = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        K.ColocarNaGrade(linhaSub, lblSub, 0, 0); K.ColocarNaGrade(linhaSub, lblSubValor, 0, 1);

        var finalizar = new Button { Tag = "Finalizar venda e emitir nota", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 56, FontSize = 16 };
        finalizar.Classes.Add("verde");
        var conteudoFin = new DockPanel();
        var f9 = K.Tecla("F9", clara: true); DockPanel.SetDock(f9, Dock.Right); conteudoFin.Children.Add(f9);
        conteudoFin.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(28, 0, 0, 0), Children = { Icones.Criar("ok", 20, "#FFFFFF"), new TextBlock { Text = "Finalizar venda", VerticalAlignment = VerticalAlignment.Center } } });
        finalizar.Content = conteudoFin;
        finalizar.Click += (_, _) => SeguroAsync(FinalizarAsync)();

        // modo simples: desconto, troco e observação ficam atrás de um botão (modo avançado mostra sempre)
        extras = new StackPanel
        {
            Spacing = 16,
            Children = { new StackPanel { Spacing = 6, Children = { K.Campo("Desconto (R$ ou %)", Desconto), chipsDesconto } }, painelTroco, K.Campo("Observações", Obs) },
        };
        botaoExtras = K.Botao("＋ Desconto, troco ou observação", () => MostrarExtras(!extras.IsVisible), classe: "chip");
        botaoExtras.HorizontalAlignment = HorizontalAlignment.Left;
        botaoExtras.IsVisible = !App.Avancado;
        extras.IsVisible = App.Avancado;

        var topo = new StackPanel
        {
            Spacing = 18,
            Children =
            {
                K.H2("Resumo da venda"),
                new StackPanel { Spacing = 8, Children = { K.Campo("Cliente", gCliente), caixaInfoCliente } },
                new StackPanel { Spacing = 6, Children = { K.Etiqueta("Forma de pagamento"), K.Margem(gradePag, 0, 0, 0, -8), outraForma } },
                botaoExtras,
                extras,
            },
        };
        var totalCaixa = new StackPanel { Spacing = 0, Children = { K.Etiqueta("Total a pagar"), lblTotal } };
        var baixo = new StackPanel
        {
            Spacing = 10,
            Children = { linhaSub, linhaDesconto, new Border { Height = 1, Background = Cores.P(Cores.Linha), Margin = new Thickness(0, 4) }, totalCaixa, K.Margem(finalizar, 0, 6, 0, 0) },
        };
        var cartaoBaixo = new Border { Child = baixo, Padding = new Thickness(22, 16, 22, 22), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0), Background = Cores.P("#FCFCFD") };
        var corpoResumo = new DockPanel();
        DockPanel.SetDock(cartaoBaixo, Dock.Bottom); corpoResumo.Children.Add(cartaoBaixo);
        corpoResumo.Children.Add(new ScrollViewer { Content = K.Margem(topo, 22, 20, 22, 20), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        var cartaoResumo = K.Cartao(corpoResumo);
        cartaoResumo.Padding = new Thickness(0); cartaoResumo.ClipToBounds = true;
        cartaoResumo.Width = 384; cartaoResumo.Margin = new Thickness(20, 0, 0, 0);

        // o resumo (com o botão Finalizar) fica sempre visível; só a coluna dos produtos rola em telas baixas
        var raiz = new DockPanel();
        DockPanel.SetDock(cartaoResumo, Dock.Right);
        raiz.Children.Add(cartaoResumo); raiz.Children.Add(K.Rolagem(Editor, 600));
        Content = raiz;
        MarcarPagamento();
    }

    /// <summary>Destaca o bloco da forma de pagamento escolhida.</summary>
    void MarcarPagamento()
    {
        var f = Pagamento.SelectedItem as string ?? "";
        var outra = f is "Transferência" or "Outro";
        foreach (var (forma, b) in blocosPagamento)
        {
            var ativo = forma == f || (forma == "Transferência" && outra);
            if (ativo) b.Classes.Add("ativo"); else b.Classes.Remove("ativo");
            var pilha = (StackPanel)b.Content!;
            Icones.Pintar(pilha.Children[0], ativo ? Cores.Primaria : Cores.Texto2);
            ((TextBlock)pilha.Children[1]).Foreground = Cores.P(ativo ? Cores.Azul : Cores.Corpo);
        }
        if (outraForma == null) return;
        outraForma.IsVisible = outra;
        if (outra && outraForma.SelectedItem as string != f) outraForma.SelectedItem = f;
    }

    public override void Atualizar()
    {
        var texto = ClienteNome;
        clientes = new();
        foreach (var c in Svc.ListarPessoas(TipoPessoa.Cliente))
        {
            var nome = clientes.ContainsKey(c.Nome) ? $"{c.Nome} (#{c.Id})" : c.Nome; // homônimos: distingue pelo id
            clientes[nome] = c.Id;
        }
        Cliente.ItemsSource = clientes.Keys.ToList();
        if (texto.Length > 0 && !clientes.Keys.Contains(texto, StringComparer.OrdinalIgnoreCase)) Cliente.Text = "";
        Editor.CarregarAtalhos();
        Recalcular();
        AtualizarInfoCliente();
        Editor.Focar();
    }

    /// <summary>Descobre quem é o cliente digitado. Vazio ou "Consumidor final" = sem cliente.</summary>
    (long? Id, string Nome) ResolverCliente()
    {
        var t = ClienteNome;
        if (t.Length == 0 || string.Equals(t, Consumidor, StringComparison.OrdinalIgnoreCase)) return (null, Consumidor);
        var achado = clientes.FirstOrDefault(c => string.Equals(c.Key, t, StringComparison.OrdinalIgnoreCase));
        if (achado.Key == null)
        {
            var parecidos = clientes.Where(c => Util.ChaveOrdenacao(c.Key).Contains(Util.ChaveOrdenacao(t))).ToList();
            if (parecidos.Count == 1) achado = parecidos[0];
            else throw new ValidacaoException($"Não encontrei o cliente “{t}”. Escolha um da lista ou clique em “Novo” para cadastrar.");
        }
        return (achado.Value, achado.Key);
    }

    void AtualizarInfoCliente()
    {
        var t = ClienteNome;
        var fiado = (Pagamento?.SelectedItem as string) == Servico.Fiado;
        string? msg = null; var cor = Cores.Texto2;
        if (t.Length == 0 || string.Equals(t, Consumidor, StringComparison.OrdinalIgnoreCase))
        { if (fiado) { msg = "Para vender no fiado, escolha o cliente."; cor = Cores.Ambar; } }
        else
        {
            var achado = clientes.FirstOrDefault(c => string.Equals(c.Key, t, StringComparison.OrdinalIgnoreCase));
            if (achado.Key == null) { if (!clientes.Keys.Any(k => Util.ChaveOrdenacao(k).Contains(Util.ChaveOrdenacao(t)))) { msg = "Cliente não encontrado. Clique em “Novo” para cadastrar."; cor = Cores.Ambar; } }
            else if (achado.Value is long id)
            {
                var saldo = Svc.SaldoCliente(id);
                if (saldo > 0) { msg = $"Esse cliente já deve {Util.FmtDinheiro(saldo)} no fiado."; cor = Cores.Ambar; }
                else if (fiado) { msg = "Sem dívida no fiado."; cor = Cores.Verde; }
            }
        }
        lblClienteInfo.IsVisible = msg != null;
        lblClienteInfo.Text = msg ?? "";
        lblClienteInfo.Foreground = Cores.P(cor == Cores.Verde ? "#065F46" : cor == Cores.Ambar ? "#92400E" : Cores.Texto2);
        caixaInfoCliente.IsVisible = msg != null;
        caixaInfoCliente.Background = Cores.P(cor == Cores.Verde ? "#ECFDF5" : cor == Cores.Ambar ? "#FFFBEB" : Cores.CinzaFundo);
    }

    long LerDesconto() => Util.ParseDesconto(Desconto.Text, Editor.Total);
    long SafeDesconto() { try { return Math.Min(LerDesconto(), Editor.Total); } catch (ValidacaoException) { return 0; } }

    void Recalcular()
    {
        var sub = Editor.Total;
        var desc = SafeDesconto();   // enquanto digita, valor inválido conta como zero
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

    /// <summary>Mostra/esconde desconto, troco e observação (só faz diferença no modo simples).</summary>
    public void MostrarExtras(bool mostrar)
    {
        if (App.Avancado) return;
        extras.IsVisible = mostrar;
        botaoExtras.Content = mostrar ? "－ Esconder desconto, troco e observação" : "＋ Desconto, troco ou observação";
    }
    public bool ExtrasVisiveis => extras.IsVisible;

    void LimparTudo()
    {
        MostrarExtras(false);
        Editor.Limpar(); Desconto.Text = ""; Obs.Text = ""; ValorRecebido.Text = ""; Cliente.Text = ""; Pagamento.SelectedIndex = 0;
    }

    public async Task NovoClienteAsync()
    {
        var id = await Fluxos.NovoClienteRapidoAsync(Janela, Svc);
        if (id == null) return;
        Atualizar();
        var nome = clientes.FirstOrDefault(c => c.Value == id).Key;
        if (nome != null) ClienteNome = nome;
    }

    /// <summary>Atalho F9.</summary>
    public void Finalizar() => SeguroAsync(FinalizarAsync)();

    /// <summary>
    /// Registra a venda direto, sem janela de confirmação (o aviso traz o botão Desfazer).
    /// O que acontece depois (imprimir, cupom...) vem da opção escolhida em Configurações.
    /// </summary>
    public async Task FinalizarAsync()
    {
        if (finalizando) return;     // duplo clique / F9 repetido não registra duas vezes
        var itens = Editor.ParaVenda();
        if (itens.Count == 0) { Plat.Atual.Toast(Janela, "Adicione ao menos um produto à venda.", Nivel.Aviso); return; }
        var desconto = LerDesconto();
        var total = Editor.Total - desconto;
        if (total < 0) throw new ValidacaoException("O desconto não pode ser maior que o subtotal da venda.");
        var (clienteId, clienteNome) = ResolverCliente();
        var forma = Pagamento.SelectedItem as string ?? Servico.FormasPagamento[0];
        if (forma == Servico.Fiado && clienteId == null)
            throw new ValidacaoException("Venda no fiado precisa de um cliente. Escolha o cliente ou clique em “Novo” para cadastrar.");
        var troco = painelTroco.IsVisible && lblTroco.Foreground == Cores.P(Cores.Verde) ? $"  ·  Troco {lblTroco.Text}" : "";

        long vid;
        finalizando = true;
        try { vid = Svc.RegistrarVenda(itens, clienteId, desconto, forma, Obs.Text); }
        finally { finalizando = false; }
        LimparTudo();
        AtualizarTudo();
        Editor.Focar();

        var venda = Svc.ObterVenda(vid);
        var titulo = $"Venda {vid:000000} registrada — {Util.FmtDinheiro(total)}{troco}";
        var desfazer = ("Desfazer", (Action)(() => Desfazer(vid)));
        var modo = Svc.ObterConfig(ConfigAposVenda, "aviso");
        switch (modo)
        {
            case "perguntar":
                if (await Confirmar($"{titulo}\n\nDeseja abrir a nota para imprimir / salvar em PDF?", "Venda registrada")) NotaDialog.AbrirNoNavegador(Janela, venda);
                break;
            case "cupom":
                NotaDialog.AbrirNoNavegador(Janela, venda, cupom: true);
                Plat.Atual.Toast(Janela, titulo, Nivel.Info, new[] { desfazer }, 8);
                break;
            case "nada":
                Plat.Atual.Toast(Janela, titulo, Nivel.Info, new[] { desfazer }, 8);
                break;
            default:
                var botoes = new List<(string, Action)>
                {
                    ("Imprimir nota", () => NotaDialog.AbrirNoNavegador(Janela, venda)),
                    ("Cupom 80 mm", () => NotaDialog.AbrirNoNavegador(Janela, venda, cupom: true)),
                };
                var tel = clienteId is long cid ? Svc.ObterPessoa(TipoPessoa.Cliente, cid).Telefone : "";
                if (tel.Length > 0) botoes.Add(("WhatsApp", () => _ = Fluxos.WhatsAppAsync(Janela, tel, Nota.TextoWhatsApp(venda))));
                botoes.Add(desfazer);
                Plat.Atual.Toast(Janela, titulo, Nivel.Info, botoes, 10);
                break;
        }
    }

    void Desfazer(long vid)
    {
        Svc.CancelarVenda(vid, "Desfeita logo após o registro");
        AtualizarTudo();
        Plat.Atual.Toast(Janela, $"Venda {vid:000000} desfeita. Os produtos voltaram ao estoque.");
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
    readonly TextBlock rodape = K.Rotulo("", true, null, Cores.Corpo);
    public string TextoRodape => rodape.Text ?? "";

    public override string Titulo => "Vendas";
    public override string Subtitulo => "Todas as vendas e notas emitidas. Dê duplo clique para ver a nota.";

    public NotasTela(MainWindow app) : base(app)
    {
        De = K.Data(Util.InicioMesBr()); Ate = K.Data(Util.HojeBr());
        Situacao = K.Combo(Situacoes.Keys, 140, "Todas");
        Busca = K.CaixaBusca("Nº da nota ou cliente", 240);
        var filtrar = K.Botao("Filtrar", Seguro(Atualizar), icone: "busca");
        var filtros = K.Fila(10, Busca, K.Linha(8, De, K.Rotulo("até", false, 13, Cores.Texto2), Ate), Situacao, filtrar);
        K.AoApertarEnter(Busca, Seguro(Atualizar));
        K.AoApertarEnter(De, Seguro(Atualizar)); K.AoApertarEnter(Ate, Seguro(Atualizar));
        Situacao.SelectionChanged += (_, _) => Seguro(Atualizar)();

        var acoes = K.Fila(4,
            K.Botao("Ver nota", SeguroAsync(VisualizarAsync), icone: "ver", classe: "fantasma"),
            K.Botao("Imprimir / PDF", SeguroAsync(ImprimirAsync), icone: "imprimir", classe: "fantasma"),
            K.Botao("Cupom 80 mm", SeguroAsync(CupomAsync), icone: "notas", classe: "fantasma"),
            K.Botao("WhatsApp", SeguroAsync(WhatsAppAsync), icone: "whatsapp", classe: "fantasma"),
            K.Botao("Cancelar venda", SeguroAsync(CancelarAsync), icone: "lixeira", classe: "fantasma perigo"),
            K.Botao("Exportar", SeguroAsync(ExportarAsync), icone: "exportar", classe: "fantasma"));

        Tabela = new Tabela(new Col("Nota", 100), new Col("Data", 160), new Col("Cliente", 200, Expande: true), new Col("Pagamento", 160),
                            new Col("Desconto", 110, true), new Col("Total", 130, true), new Col("Situação", 130, Chip: true));
        rodape.FontSize = 13;
        Content = K.Lista(Tabela, K.Ferramentas(filtros, acoes), rodape,
                          "Nenhuma venda neste período.\nAltere as datas acima ou faça uma venda em “Nova venda”.", "notas");
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
        if (v.Cancelada) { Dica("Esta venda já está cancelada."); return; }
        var motivo = await Plat.Atual.Perguntar(Janela, "Cancelar venda",
            $"Cancelar a nota {vid:000000} ({Util.FmtDinheiro(v.Total)})?\nOs produtos voltam ao estoque.\n\nInforme o motivo:");
        if (motivo == null) return;
        Svc.CancelarVenda(vid, motivo);
        AtualizarTudo();
        Sucesso($"Nota {vid:000000} cancelada e estoque restaurado.");
    }

    public async Task ExportarAsync()
    {
        var vendas = Buscar(100000);
        if (vendas.Count == 0) { Dica("Não há vendas no período para exportar."); return; }
        var caminho = await Plat.Atual.SalvarArquivo(Janela, "Exportar vendas", Config.Subpasta("exportacoes"), "vendas.csv", "CSV (Excel)", "csv");
        if (caminho == null) return;
        var n = Util.ExportarCsv(caminho, new[] { "Nota", "Data", "Cliente", "CPF/CNPJ", "Pagamento", "Subtotal", "Desconto", "Total", "Situação" },
            vendas.Select(v => new object?[] { $"{v.Id:000000}", Util.FmtData(v.Data), v.ClienteNome, v.ClienteDoc, v.FormaPagamento,
                                               Util.FmtDinheiro(v.Subtotal, false), Util.FmtDinheiro(v.Desconto, false), Util.FmtDinheiro(v.Total, false), v.Status }));
        Sucesso($"{n} venda(s) exportada(s).", ("Abrir arquivo", () => Plat.Atual.Abrir(caminho)));
    }
}
