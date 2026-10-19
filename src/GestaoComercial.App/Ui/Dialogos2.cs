using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Ficha do cliente: compras, fiado (extrato) e atalhos de cobrança.</summary>
public sealed class HistoricoClienteDialog : JanelaBase
{
    readonly Servico svc;
    readonly long clienteId;
    readonly Action? aoMudar;
    readonly Tabela tCompras, tFiado;
    readonly TextBlock lblSaldo = K.Rotulo("", true, 20);
    readonly TabControl abas = new();
    public Tabela Compras => tCompras;
    public Tabela Fiado => tFiado;
    public string TextoSaldo => lblSaldo.Text ?? "";

    public static Task Abrir(Window? dono, Servico svc, long clienteId, Action? aoMudar = null, bool abaFiado = false) =>
        dono == null ? Task.CompletedTask : new HistoricoClienteDialog(svc, clienteId, aoMudar, abaFiado).ShowDialog(dono);

    public HistoricoClienteDialog(Servico svc, long clienteId, Action? aoMudar = null, bool abaFiado = false)
    {
        this.svc = svc; this.clienteId = clienteId; this.aoMudar = aoMudar;
        var c = svc.ObterPessoa(TipoPessoa.Cliente, clienteId);
        Title = $"Cliente — {c.Nome}";
        Width = 880; Height = 640; MinWidth = 700; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var dados = new StackPanel
        {
            Spacing = 3,
            Children = { K.H1(c.Nome), K.Discreto(string.Join("   ·   ", new[] { c.Telefone, Util.FmtDocumento(c.Documento), c.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))) },
        };
        var saldoCartao = K.Cartao(new StackPanel { Children = { K.Etiqueta("Saldo no fiado"), lblSaldo } });
        var topo = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(saldoCartao, Dock.Right); topo.Children.Add(saldoCartao); topo.Children.Add(dados);

        tCompras = new Tabela(new Col("Nota", 90), new Col("Data", 150), new Col("Pagamento", 150), new Col("Total", 120, true), new Col("Situação", 130, Chip: true));
        tFiado = new Tabela(new Col("Data", 150), new Col("Descrição", 300, Expande: true), new Col("Valor", 120, true), new Col("Saldo", 120, true));
        abas.Items.Add(new TabItem { Header = "Compras", Content = K.Margem(tCompras, 0, 12, 0, 0) });
        abas.Items.Add(new TabItem { Header = "Fiado (extrato)", Content = K.Margem(tFiado, 0, 12, 0, 0) });
        if (abaFiado) abas.SelectedIndex = 1;

        var botoes = K.Linha(K.Botao("Receber pagamento", () => Fazer(ReceberAsync), icone: "caixa", classe: "primario"),
                             K.Botao("Cobrar no WhatsApp", () => Fazer(CobrarAsync), icone: "whatsapp"),
                             K.Botao("Imprimir extrato", () => Fazer(ImprimirAsync), icone: "imprimir"));
        var fechar = K.Botao("Fechar", Close);
        var rodape = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(fechar, Dock.Right); rodape.Children.Add(fechar); rodape.Children.Add(botoes);
        var doca = K.Doca(topo, abas, rodape); doca.Margin = new Thickness(24);
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Carregar();
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    void Carregar()
    {
        var saldo = svc.SaldoCliente(clienteId);
        lblSaldo.Text = saldo < 0 ? $"crédito {Util.FmtDinheiro(-saldo)}" : Util.FmtDinheiro(saldo);
        lblSaldo.Foreground = Cores.P(saldo > 0 ? Cores.Vermelho : Cores.Verde);
        tCompras.Definir(svc.ListarVendas(clienteId: clienteId).Select(v => new Linha
        {
            Id = v.Id.ToString(), Estilo = v.Cancelada ? "cancelado" : "",
            C = new[] { $"{v.Id:000000}", Util.FmtData(v.Data), v.FormaPagamento, Util.FmtDinheiro(v.Total), v.Cancelada ? "Cancelada" : "Concluída" },
        }));
        tFiado.Definir(svc.ExtratoCliente(clienteId).Select((l, i) => new Linha
        {
            Id = i.ToString(),
            C = new[] { Util.FmtData(l.Data), l.Descricao, (l.Valor < 0 ? "-" : "+") + Util.FmtDinheiro(Math.Abs(l.Valor)), Util.FmtDinheiro(l.Saldo) },
        }));
    }

    public async Task ReceberAsync()
    {
        if (await Fluxos.ReceberPagamentoAsync(this, svc, clienteId)) { Carregar(); aoMudar?.Invoke(); }
    }

    public async Task CobrarAsync()
    {
        var c = svc.ObterPessoa(TipoPessoa.Cliente, clienteId);
        var saldo = svc.SaldoCliente(clienteId);
        if (saldo <= 0) { await Plat.Atual.Mensagem(this, "Cobrança", $"{c.Nome} não tem nada a pagar.", Nivel.Info); return; }
        await Fluxos.WhatsAppAsync(this, c.Telefone, Impressos.MensagemCobranca(svc.ObterEmpresa(), c.Nome, saldo));
    }

    public async Task ImprimirAsync()
    {
        var c = svc.ObterPessoa(TipoPessoa.Cliente, clienteId);
        await Fluxos.ImprimirHtmlAsync(this, Impressos.ExtratoFiado(svc.ObterEmpresa(), c.Nome, svc.ExtratoCliente(clienteId), svc.SaldoCliente(clienteId)), $"extrato-fiado-{clienteId}.html");
    }
}

/// <summary>Produtos que precisam ser comprados: imprimir ou exportar.</summary>
public sealed class ReposicaoDialog : JanelaBase
{
    readonly Servico svc;
    public Tabela Tabela { get; }

    public static Task Abrir(Window? dono, Servico svc) => dono == null ? Task.CompletedTask : new ReposicaoDialog(svc).ShowDialog(dono);

    public ReposicaoDialog(Servico svc)
    {
        this.svc = svc;
        Title = "Lista de reposição";
        Width = 820; Height = 580; MinWidth = 640; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var itens = svc.ListarReposicao();
        Tabela = new Tabela(new Col("Código", 100), new Col("Produto", 260, Expande: true), new Col("Estoque", 100, true), new Col("Mínimo", 100, true), new Col("Sugestão de compra", 190, true));
        Tabela.Definir(itens.Select(i => new Linha
        {
            Id = i.Produto.Id.ToString(), Estilo = i.Produto.Estoque == 0 ? "alerta" : "",
            C = new[] { i.Produto.Codigo, i.Produto.Nome, i.Produto.Estoque.ToString(), i.Produto.EstoqueMin.ToString(), $"{i.Sugestao} {i.Produto.Unidade}" },
        }));
        var titulo = new StackPanel { Margin = new Thickness(0, 0, 0, 14), Spacing = 3, Children = { K.H1("Lista de reposição"), K.Discreto(itens.Count == 0 ? "Tudo certo! Nenhum produto abaixo do estoque mínimo." : $"{itens.Count} produto(s) no estoque mínimo ou abaixo. A sugestão é o dobro do mínimo.") } };
        var fechar = K.Botao("Fechar", Close);
        var rodape = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(fechar, Dock.Right); rodape.Children.Add(fechar);
        rodape.Children.Add(K.Linha(K.Botao("Imprimir lista", () => Fazer(ImprimirAsync), icone: "imprimir", classe: "primario"), K.Botao("Exportar CSV", () => Fazer(ExportarAsync), icone: "exportar")));
        var doca = K.Doca(titulo, Tabela, rodape); doca.Margin = new Thickness(24);
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    public async Task ImprimirAsync() =>
        await Fluxos.ImprimirHtmlAsync(this, Impressos.Reposicao(svc.ObterEmpresa(), svc.ListarReposicao()), "reposicao-estoque.html");

    public async Task ExportarAsync()
    {
        var itens = svc.ListarReposicao();
        if (itens.Count == 0) { await Plat.Atual.Mensagem(this, "Exportar", "Não há produtos para repor.", Nivel.Info); return; }
        var caminho = await Plat.Atual.SalvarArquivo(this, "Exportar lista de reposição", Config.Subpasta("exportacoes"), "reposicao.csv", "CSV (Excel)", "csv");
        if (caminho == null) return;
        var n = Util.ExportarCsv(caminho, new[] { "Código", "Produto", "Estoque", "Mínimo", "Sugestão de compra" },
            itens.Select(i => new object?[] { i.Produto.Codigo, i.Produto.Nome, i.Produto.Estoque, i.Produto.EstoqueMin, i.Sugestao }));
        await Plat.Atual.Mensagem(this, "Exportado", $"{n} produto(s) exportado(s) para:\n{caminho}", Nivel.Info);
    }
}
