using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Fornecedor → mercadorias (entrada no estoque).</summary>
public sealed class EntradasTela : Tela
{
    public ComboBox Fornecedor { get; }
    public TextBox Documento { get; }
    public TextBox Data { get; }
    public TextBox Obs { get; }
    public EditorItens Editor { get; }
    readonly TextBlock lblTotal = K.Rotulo("TOTAL: R$ 0,00", true, 20);
    Dictionary<string, long> fornecedores = new();

    public override string Titulo => "Entrada de mercadorias";
    public override string Subtitulo => "Chegou mercadoria do fornecedor? Registre aqui para somar no estoque.";

    public EntradasTela(MainWindow app) : base(app)
    {
        Fornecedor = K.Combo(Array.Empty<string>(), 320);
        Documento = K.Caixa("", 150); Data = K.Caixa(Util.HojeBr(), 110); Obs = K.Caixa("", 340);
        var topo = K.Linha(14, K.Campo("Fornecedor", Fornecedor), K.Campo("Nº do documento / NF", Documento), K.Campo("Data", Data));
        topo.Margin = new Thickness(0, 0, 0, 14);

        Editor = new EditorItens(Svc, false, () => lblTotal.Text = $"TOTAL: {Util.FmtDinheiro(Editor!.Total)}");

        var registrar = K.Botao("Registrar entrada no estoque", SeguroAsync(RegistrarAsync), icone: "ok", classe: "verde");
        registrar.Padding = new Thickness(20, 10); registrar.HorizontalAlignment = HorizontalAlignment.Right; registrar.MinHeight = 46;
        lblTotal.HorizontalAlignment = HorizontalAlignment.Right; lblTotal.Foreground = Cores.P("#15803D");
        var dir = new StackPanel { Spacing = 4, Children = { lblTotal, registrar } };
        var esq = K.Linha(10, K.Campo("Observações", Obs), K.Botao("Histórico de entradas", SeguroAsync(HistoricoAsync), icone: "ver"));
        ((Control)esq.Children[1]).VerticalAlignment = VerticalAlignment.Bottom;
        esq.VerticalAlignment = VerticalAlignment.Bottom;
        var rodape = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(dir, Dock.Right); rodape.Children.Add(dir); rodape.Children.Add(esq);
        Content = K.Doca(topo, Editor, rodape);
    }

    public override void Atualizar()
    {
        var sel = Fornecedor.SelectedItem as string;
        fornecedores = new();
        foreach (var f in Svc.ListarPessoas(TipoPessoa.Fornecedor))
            fornecedores[fornecedores.ContainsKey(f.Nome) ? $"{f.Nome} (#{f.Id})" : f.Nome] = f.Id;
        Fornecedor.ItemsSource = fornecedores.Keys.ToList();
        Fornecedor.SelectedItem = sel != null && fornecedores.ContainsKey(sel) ? sel : null;
        Editor.Focar();
    }

    public async Task RegistrarAsync()
    {
        if (fornecedores.Count == 0) { Dica("Cadastre um fornecedor primeiro (página Fornecedores)."); return; }
        var nome = Fornecedor.SelectedItem as string;
        if (nome == null) { Dica("Selecione o fornecedor."); Fornecedor.Focus(); return; }
        var itens = Editor.ParaEntrada();
        if (itens.Count == 0) { Dica("Adicione ao menos um produto."); return; }
        var data = Util.ParseDataBr(Data.Text, "Data da entrada");
        if (!await Confirmar($"Registrar entrada de {itens.Count} produto(s) de '{nome}'?\nTotal: {Util.FmtDinheiro(Editor.Total)}\n\nO estoque e o preço de custo dos produtos serão atualizados."))
            return;
        var eid = Svc.RegistrarEntrada(fornecedores[nome], itens, Documento.Text, Obs.Text, data);
        Editor.Limpar(); Documento.Text = ""; Obs.Text = ""; Data.Text = Util.HojeBr();
        AtualizarTudo();
        Sucesso($"Entrada nº {eid:000000} registrada. Estoque atualizado.");
    }

    public Task HistoricoAsync() => HistoricoEntradasDialog.Abrir(Janela, Svc, App);
}

public sealed class HistoricoEntradasDialog : JanelaBase
{
    readonly Servico svc;
    readonly MainWindow app;
    readonly Tabela tabela;

    public static Task Abrir(Window? dono, Servico svc, MainWindow app) =>
        dono == null ? Task.CompletedTask : new HistoricoEntradasDialog(svc, app).ShowDialog(dono);

    public HistoricoEntradasDialog(Servico svc, MainWindow app)
    {
        this.svc = svc; this.app = app;
        Title = "Histórico de entradas de mercadorias";
        Width = 860; Height = 520; MinWidth = 640; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        tabela = new Tabela(new Col("Nº", 80), new Col("Data", 130), new Col("Fornecedor", 240, Expande: true), new Col("Documento", 120),
                            new Col("Total", 120, true), new Col("Situação", 100));
        var fechar = K.Botao("Fechar", Close);
        var rodape = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        DockPanel.SetDock(fechar, Dock.Right); rodape.Children.Add(fechar);
        rodape.Children.Add(K.Linha(K.Botao("Ver itens", () => Fazer(VerAsync), true), K.Botao("Cancelar entrada", () => Fazer(CancelarAsync))));
        var doca = K.Doca(null, tabela, rodape); doca.Margin = new Thickness(16);
        Content = doca;
        tabela.Duplo += () => Fazer(VerAsync);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Carregar();
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    void Carregar() =>
        tabela.Definir(svc.ListarEntradas().Select(e => new Linha
        {
            Id = e.Id.ToString(), Estilo = e.Cancelada ? "cancelado" : "",
            C = new[] { $"{e.Id:000000}", Util.FmtData(e.Data), e.Fornecedor, e.NumeroDoc, Util.FmtDinheiro(e.Total), e.Cancelada ? "Cancelada" : "Ativa" },
        }));

    async Task VerAsync()
    {
        var id = tabela.IdLong ?? throw new ValidacaoException("Selecione uma entrada na lista primeiro.");
        await EntradaDetalheDialog.Abrir(this, svc.ObterEntrada(id));
    }

    async Task CancelarAsync()
    {
        var id = tabela.IdLong ?? throw new ValidacaoException("Selecione uma entrada na lista primeiro.");
        var motivo = await Plat.Atual.Perguntar(this, "Cancelar entrada",
            "Os produtos serão retirados do estoque (só é possível se ainda estiverem lá).\n\nMotivo:");
        if (motivo == null) return;
        svc.CancelarEntrada(id, motivo);
        Carregar();
        app.AtualizarTudo();
        await Plat.Atual.Mensagem(this, "Pronto", $"Entrada {id:000000} cancelada e estoque ajustado.", Nivel.Info);
    }
}
