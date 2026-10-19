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
    readonly K.Indicador gaveta = new("Na gaveta", "caixa", "#4F46E5", "#E0E7FF");
    readonly K.Indicador vendido = new("Vendido", "venda", "#0E7490", "#CFFAFE");
    readonly K.Indicador fiado = new("No fiado", "receber", "#B45309", "#FEF3C7");
    readonly K.Indicador recebido = new("Fiado pago", "ok", "#16A34A", "#DCFCE7");
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

        Data = K.Caixa(Util.HojeBr(), 130);
        var filtros = K.Linha(10, K.Campo("Dia", Data), K.Botao("Hoje", Seguro(() => { Data.Text = Util.HojeBr(); Atualizar(); })), K.Botao("Ver", Seguro(Atualizar)));
        ((Control)filtros.Children[1]).VerticalAlignment = VerticalAlignment.Bottom; ((Control)filtros.Children[2]).VerticalAlignment = VerticalAlignment.Bottom;
        K.AoApertarEnter(Data, Seguro(Atualizar));

        var cartoes = new UniformGrid { Rows = 1, Columns = 4, Margin = new Thickness(0, 16, 0, 16) };
        foreach (var c in new[] { gaveta, vendido, fiado, recebido }) { c.Margin = new Thickness(0, 0, 14, 0); cartoes.Children.Add(c); }

        tForma = new Tabela(new Col("Forma de pagamento", 200, Expande: true), new Col("Vendas", 90, true), new Col("Total", 120, true));
        tMovs = new Tabela(new Col("Hora", 80), new Col("Tipo", 150, Chip: true), new Col("Motivo", 200, Expande: true), new Col("Valor", 110, true));
        var excluir = K.Botao("Excluir lançamento", SeguroAsync(ExcluirAsync), icone: "lixeira", classe: "perigo"); excluir.Margin = new Thickness(0, 10, 0, 0);

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        K.ColocarNaGrade(g, K.H2("Vendas por forma de pagamento"), 0, 0);
        K.ColocarNaGrade(g, K.Margem(K.H2("Entradas e retiradas do caixa"), 14, 0, 0, 0), 0, 1);
        tForma.Margin = new Thickness(0, 10, 14, 0); tMovs.Margin = new Thickness(14, 10, 0, 0);
        K.ColocarNaGrade(g, tForma, 1, 0); K.ColocarNaGrade(g, tMovs, 1, 1);
        excluir.HorizontalAlignment = HorizontalAlignment.Right; excluir.Margin = new Thickness(0, 10, 0, 0);
        K.ColocarNaGrade(g, excluir, 2, 1);
        Content = K.Doca(new StackPanel { Children = { filtros, cartoes } }, g);
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
    readonly K.Indicador total = new("Total a receber", "receber", "#B45309", "#FEF3C7");
    readonly K.Indicador quantos = new("Clientes devendo", "clientes", "#4F46E5", "#E0E7FF");
    public Tabela Tabela { get; }
    public K.Indicador Total => total;
    List<Devedor> devedores = new();

    public override string Titulo => "A receber";
    public override string Subtitulo => "Compras no fiado e quanto cada cliente ainda deve.";
    readonly Control acoes;
    public override Control? AcoesCabecalho => acoes;

    public ReceberTela(MainWindow app) : base(app)
    {
        acoes = K.Botao("Receber pagamento", SeguroAsync(ReceberAsync), icone: "caixa", classe: "primario");
        var cartoes = new UniformGrid { Rows = 1, Columns = 3, Margin = new Thickness(0, 0, 0, 16) };
        total.Margin = new Thickness(0, 0, 14, 0); quantos.Margin = new Thickness(0, 0, 14, 0);
        cartoes.Children.Add(total); cartoes.Children.Add(quantos);
        Tabela = new Tabela(new Col("Cliente", 240, Expande: true), new Col("Telefone", 160), new Col("Última compra no fiado", 190), new Col("Deve", 140, true));
        var barra = K.Linha(K.Botao("Receber pagamento", SeguroAsync(ReceberAsync), icone: "caixa", classe: "primario"),
                            K.Botao("Ver extrato", SeguroAsync(ExtratoAsync), icone: "ver"),
                            K.Botao("Cobrar no WhatsApp", SeguroAsync(CobrarAsync), icone: "whatsapp"),
                            K.Botao("Imprimir extrato", SeguroAsync(ImprimirAsync), icone: "imprimir"));
        barra.Margin = new Thickness(0, 0, 0, 14);
        var dica = K.Discreto("Para vender no fiado: em \"Nova venda\" escolha o cliente e a forma de pagamento \"Fiado\".");
        dica.Margin = new Thickness(0, 10, 0, 0);
        Content = K.Doca(new StackPanel { Children = { cartoes, barra } }, Tabela.ComVazio("Ninguém está devendo. Tudo em dia!\nPara vender no fiado, escolha o cliente e a forma “Fiado” na venda."), dica);
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
