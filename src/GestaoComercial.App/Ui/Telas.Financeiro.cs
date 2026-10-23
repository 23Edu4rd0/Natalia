using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Caixa do dia: conferência do dinheiro, vendas por forma de pagamento, entradas de troco e retiradas.</summary>
public sealed class CaixaTela : Tela
{
    public TextBox Data { get; }
    readonly K.Indicador gaveta = new("Dinheiro na gaveta", "dinheiro", "#4F46E5", "#EEF2FF");
    readonly K.Indicador vendido = new("Vendido no dia", "venda", "#0891B2", "#ECFEFF");
    readonly K.Indicador fiado = new("Vendido no fiado", "receber", "#D97706", "#FFFBEB");
    readonly K.Indicador recebido = new("Fiado recebido", "ok", "#059669", "#ECFDF5");
    readonly Tabela tForma, tMovs;
    ResumoCaixa? resumo;
    public ResumoCaixa? Resumo => resumo;
    public K.Indicador Gaveta => gaveta;
    public K.Indicador Vendido => vendido;
    public Tabela Lancamentos => tMovs;
    public Tabela PorForma => tForma;

    public override string Titulo => "Caixa";
    public override string Subtitulo => "Confira o dinheiro do dia, registre troco inicial e retiradas.";
    readonly Control acoes;
    public override Control? AcoesCabecalho => acoes;

    public CaixaTela(MainWindow app) : base(app)
    {
        acoes = K.Linha(10,
            K.Botao("Entrada de troco", SeguroAsync(() => RegistrarAsync("SUPRIMENTO")), icone: "mais"),
            K.Botao("Retirada", SeguroAsync(() => RegistrarAsync("SANGRIA")), icone: "menos"),
            K.Botao("Imprimir fechamento", SeguroAsync(ImprimirAsync), icone: "imprimir", classe: "primario"));

        Data = K.Data(Util.HojeBr());
        var filtros = K.Linha(8, Data, K.Botao("Ver", Seguro(Atualizar)), K.Botao("Hoje", Seguro(() => { Data.Text = Util.HojeBr(); Atualizar(); }), classe: "fantasma"));
        filtros.Margin = new Thickness(0, 0, 0, 18);
        K.AoApertarEnter(Data, Seguro(Atualizar));

        var cartoes = K.Colunas(18, gaveta, vendido, fiado, recebido);
        cartoes.Margin = new Thickness(0, 0, 0, 20);

        tForma = new Tabela(new Col("Forma de pagamento", 180, Expande: true), new Col("Vendas", 90, true), new Col("Total", 130, true));
        tMovs = new Tabela(new Col("Hora", 80), new Col("Tipo", 150, Chip: true), new Col("Motivo", 160, Expande: true), new Col("Valor", 120, true));
        var excluir = K.Botao("Excluir lançamento", SeguroAsync(ExcluirAsync), icone: "lixeira", classe: "fantasma perigo");
        var lForma = K.Lista(tForma, K.Ferramentas(K.H2("Vendas por forma de pagamento"), null), vazio: "Nenhuma venda neste dia.", iconeVazio: "venda");
        var lMovs = K.Lista(tMovs, K.Ferramentas(K.H2("Entradas e retiradas"), excluir), vazio: "Nenhuma entrada de troco ou retirada.\nUse os botões lá em cima para registrar.", iconeVazio: "caixa");

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20,*") };
        K.ColocarNaGrade(g, lForma, 0, 0); K.ColocarNaGrade(g, lMovs, 0, 2);
        Content = K.Rolagem(K.Doca(new StackPanel { Children = { filtros, cartoes } }, g), 620);
    }

    public override void Atualizar()
    {
        var iso = Util.ParseDataBr(Data.Text, "Dia");
        var r = Svc.ObterResumoCaixa(iso);
        resumo = r;
        gaveta.Definir(Util.FmtDinheiro(r.DinheiroEsperado), "troco + dinheiro − retiradas");
        vendido.Definir(Util.FmtDinheiro(r.VendasTotal), $"{r.VendasQtd} venda(s)");
        fiado.Definir(Util.FmtDinheiro(r.FiadoLancado), "a receber depois");
        recebido.Definir(Util.FmtDinheiro(r.RecebimentosTotal), "pagamentos de fiado no dia");
        var linhas = r.VendasPorForma.Select((f, i) => new Linha { Id = i.ToString(), C = new[] { f.Forma, f.Qtd.ToString(), Util.FmtDinheiro(f.Total) } }).ToList();
        if (r.VendasPorForma.Count > 0) linhas.Add(new Linha { Estilo = "grupo", C = new[] { "Total vendido", r.VendasQtd.ToString(), Util.FmtDinheiro(r.VendasTotal) } });
        tForma.Definir(linhas);
        tMovs.Definir(r.Movimentos.Select(m => new Linha
        {
            Id = m.Id.ToString(),
            C = new[] { Util.FmtData(m.Data).Split(' ').Last(), m.Tipo == "SANGRIA" ? "Retirada" : "Entrada (troco)", m.Motivo, (m.Tipo == "SANGRIA" ? "-" : "+") + Util.FmtDinheiro(m.Valor) },
        }));
    }

    public async Task RegistrarAsync(string tipo)
    {
        var retirada = tipo == "SANGRIA";
        var campos = new List<Campo>
        {
            new() { Chave = "valor", Rotulo = "Valor (R$)", Tipo = TipoCampo.Dinheiro, Obrigatorio = true },
            new() { Chave = "motivo", Rotulo = "Motivo", Obrigatorio = retirada, Dica = retirada ? "Ex.: pagamento de fornecedor, vale, depósito." : "Ex.: troco inicial do dia." },
        };
        if (await FormDialog.Abrir(Janela, retirada ? "Retirada de dinheiro do caixa" : "Entrada de troco no caixa", campos, null,
                                   v => Svc.RegistrarMovimentoCaixa(tipo, v.Num("valor"), v.Texto("motivo")), "Registrar"))
        {
            Data.Text = Util.HojeBr();
            Atualizar();
        }
    }

    public async Task ExcluirAsync()
    {
        var id = Exigir(tLancamentoId(), "um lançamento");
        if (!await Confirmar("Excluir este lançamento do caixa?")) return;
        Svc.ExcluirMovimentoCaixa(id);
        Atualizar();
    }

    long? tLancamentoId() => tMovs.IdLong;

    public async Task ImprimirAsync()
    {
        var r = Svc.ObterResumoCaixa(Util.ParseDataBr(Data.Text, "Dia"));
        await Fluxos.ImprimirHtmlAsync(Janela, Impressos.FechamentoCaixa(Svc.ObterEmpresa(), r), $"fechamento-caixa-{r.Data}.html");
    }
}

/// <summary>Fiado: quem está devendo, receber pagamentos, extrato e cobrança por WhatsApp.</summary>
public sealed class ReceberTela : Tela
{
    readonly K.Indicador total = new("Total a receber", "receber", "#D97706", "#FFFBEB");
    readonly K.Indicador quantos = new("Clientes devendo", "clientes", "#4F46E5", "#EEF2FF");
    public Tabela Tabela { get; }
    public K.Indicador Total => total;
    List<Devedor> devedores = new();

    public override string Titulo => "A receber";
    public override string Subtitulo => "Compras no fiado e quanto cada cliente ainda deve.";
    readonly Control acoes;
    public override Control? AcoesCabecalho => acoes;

    public ReceberTela(MainWindow app) : base(app)
    {
        acoes = K.Botao("Receber pagamento", SeguroAsync(ReceberAsync), icone: "dinheiro", classe: "primario");
        var dica = new Border
        {
            Background = Cores.P(Cores.PrimariaSuave), CornerRadius = new CornerRadius(14), Padding = new Thickness(20, 18), BorderBrush = Cores.P("#E0E7FF"), BorderThickness = new Thickness(1),
            Child = new DockPanel
            {
                Children =
                {
                    K.Margem(K.Bolha("info", Cores.Primaria, "#FFFFFF", 40), 0, 0, 14, 0),
                    new StackPanel
                    {
                        Spacing = 4, VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock { Text = "Como vender no fiado", FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 14, Foreground = Cores.P(Cores.Azul) },
                            new TextBlock { Text = "Na Nova venda, escolha a cliente e a forma de pagamento “Fiado”. A dívida aparece aqui na hora.", FontSize = 13, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Foreground = Cores.P("#4338CA") },
                        },
                    },
                },
            },
        };
        DockPanel.SetDock(((DockPanel)dica.Child).Children[0], Dock.Left);
        var cartoes = K.Colunas(18, total, quantos, dica);
        cartoes.Margin = new Thickness(0, 0, 0, 20);
        Tabela = new Tabela(new Col("Cliente", 200, Expande: true), new Col("Telefone", 160), new Col("Última compra no fiado", 200), new Col("Deve", 140, true));
        var barra = K.Fila(4, K.Botao("Ver extrato", SeguroAsync(ExtratoAsync), icone: "ver", classe: "fantasma"),
                              K.Botao("Cobrar no WhatsApp", SeguroAsync(CobrarAsync), icone: "whatsapp", classe: "fantasma"),
                              K.Botao("Imprimir extrato", SeguroAsync(ImprimirAsync), icone: "imprimir", classe: "fantasma"));
        var lista = K.Lista(Tabela, K.Ferramentas(K.H2("Quem está devendo"), barra), K.Discreto("Dica: dê dois cliques num nome para ver o extrato."),
                            "Ninguém está devendo. Tudo em dia!\nPara vender no fiado, escolha o cliente e a forma “Fiado” na venda.", "ok");
        Content = K.Doca(cartoes, lista);
        Tabela.Duplo += SeguroAsync(ExtratoAsync);
    }

    public override void Atualizar()
    {
        devedores = Svc.ListarDevedores();
        var abertos = devedores.Where(d => d.Saldo > 0).ToList();
        total.Definir(Util.FmtDinheiro(abertos.Sum(d => d.Saldo)), "soma de todo o fiado em aberto");
        quantos.Definir(abertos.Count.ToString(), abertos.Count == 1 ? "cliente com saldo em aberto" : "clientes com saldo em aberto");
        Tabela.Definir(devedores.Select(d => new Linha
        {
            Id = d.ClienteId.ToString(), Estilo = d.Saldo < 0 ? "cancelado" : "",
            C = new[] { d.Nome, d.Telefone, Util.FmtData(d.UltimaCompra), d.Saldo < 0 ? $"crédito {Util.FmtDinheiro(-d.Saldo)}" : Util.FmtDinheiro(d.Saldo) },
        }));
    }

    public async Task ReceberAsync()
    {
        var id = Exigir(Tabela.IdLong, "um cliente");
        if (await Fluxos.ReceberPagamentoAsync(Janela, Svc, id)) AtualizarTudo();
    }

    public Task ExtratoAsync() => HistoricoClienteDialog.Abrir(Janela, Svc, Exigir(Tabela.IdLong, "um cliente"), AtualizarTudo, abaFiado: true);

    public async Task CobrarAsync()
    {
        var id = Exigir(Tabela.IdLong, "um cliente");
        var d = devedores.First(x => x.ClienteId == id);
        if (d.Saldo <= 0) { Dica($"{d.Nome} não tem nada a pagar."); return; }
        await Fluxos.WhatsAppAsync(Janela, d.Telefone, Impressos.MensagemCobranca(Svc.ObterEmpresa(), d.Nome, d.Saldo));
    }

    public async Task ImprimirAsync()
    {
        var id = Exigir(Tabela.IdLong, "um cliente");
        var c = Svc.ObterPessoa(TipoPessoa.Cliente, id);
        await Fluxos.ImprimirHtmlAsync(Janela, Impressos.ExtratoFiado(Svc.ObterEmpresa(), c.Nome, Svc.ExtratoCliente(id), Svc.SaldoCliente(id)), $"extrato-fiado-{id}.html");
    }
}
