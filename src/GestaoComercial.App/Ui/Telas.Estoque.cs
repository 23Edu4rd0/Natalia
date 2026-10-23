using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Estoque agrupado por tipo de produto, com ajuste de inventário e histórico.</summary>
public sealed class EstoqueTela : Tela
{
    public TextBox Busca { get; }
    public CheckBox SomenteBaixo { get; }
    public Tabela Tabela { get; }
    readonly K.Indicador vCusto = new("Estoque a preço de custo", "estoque", "#4F46E5", "#EEF2FF");
    readonly K.Indicador vVenda = new("Estoque a preço de venda", "etiqueta", "#059669", "#ECFDF5");
    readonly K.Indicador vBaixos = new("Produtos com estoque baixo", "alerta", "#DC2626", "#FEF2F2");

    public override string Titulo => "Estoque";
    public override string Subtitulo => "Quanto você tem de cada produto, agrupado por tipo.";

    public async Task ReposicaoAsync() => await ReposicaoDialog.Abrir(Janela, Svc);

    public EstoqueTela(MainWindow app) : base(app)
    {
        Busca = K.CaixaBusca("Buscar produto…", 300);
        SomenteBaixo = new CheckBox { Content = "Só estoque baixo/zerado", VerticalAlignment = VerticalAlignment.Center };
        var direita = K.Fila(4, K.Botao("Ajustar estoque…", SeguroAsync(AjustarAsync), icone: "editar"),
                                K.Botao("Histórico do produto", SeguroAsync(() => HistoricoAsync(true)), icone: "ver", classe: "fantasma"),
                                K.Botao("Histórico geral", SeguroAsync(() => HistoricoAsync(false)), icone: "notas", classe: "fantasma"),
                                K.Botao("Lista de reposição", SeguroAsync(ReposicaoAsync), icone: "entrada", classe: "fantasma"),
                                K.Botao("Exportar", SeguroAsync(ExportarAsync), icone: "exportar", classe: "fantasma"));

        Tabela = new Tabela(new Col("Tipo de produto / Produto", 260, Expande: true), new Col("Código", 100), new Col("Un.", 72), new Col("Estoque", 100, true),
                            new Col("Mínimo", 90, true), new Col("Custo unit.", 120, true), new Col("Venda unit.", 120, true), new Col("Valor (custo)", 140, true));
        var cartoes = K.Colunas(18, vCusto, vVenda, vBaixos);
        cartoes.Margin = new Thickness(0, 0, 0, 20);
        var rodape = K.Linha(8, new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(3), Background = Cores.P("#FECACA") }, K.Discreto("Em vermelho: estoque no mínimo ou abaixo dele."));
        Content = K.Doca(cartoes, K.Lista(Tabela, K.Ferramentas(K.Linha(14, Busca, SomenteBaixo), direita), rodape, "Nenhum produto para mostrar.\nCadastre produtos na página Produtos.", "estoque"));

        K.AoMudarTexto(Busca, K.Debounce(Seguro(Atualizar)));
        SomenteBaixo.IsCheckedChanged += (_, _) => Seguro(Atualizar)();
        Tabela.Duplo += SeguroAsync(AjustarAsync);
    }

    List<Produto> Produtos() => Svc.ListarProdutos(Busca.Text ?? "", apenasBaixo: SomenteBaixo.IsChecked == true);

    public override void Atualizar()
    {
        var linhas = new List<Linha>();
        var grupos = Produtos().GroupBy(p => p.Categoria)
                               .OrderBy(g => g.Key == "Sem categoria").ThenBy(g => Util.ChaveOrdenacao(g.Key), StringComparer.Ordinal);
        foreach (var g in grupos)
        {
            linhas.Add(new Linha
            {
                Id = "cat:" + g.Key, Estilo = "grupo",
                C = new[] { $"{g.Key}  ({g.Count()})", "", "", g.Sum(p => p.Estoque).ToString(), "", "", "", Util.FmtDinheiro(g.Sum(p => p.Estoque * p.PrecoCusto)) },
            });
            foreach (var p in g)
                linhas.Add(new Linha
                {
                    Id = p.Id.ToString(), Estilo = p.Baixo ? "alerta" : "",
                    Fabrica = () => new[] { "      " + p.Nome, p.Codigo, p.Unidade, p.Estoque.ToString(), p.EstoqueMin.ToString(), Util.FmtDinheiro(p.PrecoCusto),
                                            Util.FmtDinheiro(p.PrecoVenda), Util.FmtDinheiro(p.Estoque * p.PrecoCusto) },
                });
        }
        Tabela.Definir(linhas);
        var res = Svc.ResumoPorCategoria();
        vCusto.Definir(Util.FmtDinheiro(res.Sum(r => r.ValorCusto)), $"{res.Sum(r => r.Unidades)} unidade(s) em {res.Sum(r => r.Produtos)} produto(s)");
        vVenda.Definir(Util.FmtDinheiro(res.Sum(r => r.ValorVenda)), "se tudo for vendido pelo preço atual");
        var baixos = res.Sum(r => r.Baixos);
        vBaixos.Definir(baixos.ToString(), baixos == 0 ? "tudo abastecido" : "veja a lista de reposição", baixos > 0 ? "#B91C1C" : null);
    }

    long ProdutoSelecionado() => Exigir(Tabela.IdLong, "um produto (não um tipo)");

    public async Task AjustarAsync()
    {
        var pid = ProdutoSelecionado();
        var p = Svc.ObterProduto(pid);
        var campos = new List<Campo>
        {
            new() { Chave = "atual", Rotulo = "Estoque atual", Tipo = TipoCampo.Inteiro, SomenteLeitura = true },
            new() { Chave = "nova", Rotulo = "Nova quantidade (contagem real)", Tipo = TipoCampo.Inteiro, Obrigatorio = true },
            new() { Chave = "motivo", Rotulo = "Motivo", Obrigatorio = true, Dica = "Ex.: inventário, quebra, perda, devolução." },
        };
        if (await FormDialog.Abrir(Janela, $"Ajustar estoque — {p.Nome}", campos, new() { ["atual"] = p.Estoque, ["nova"] = p.Estoque },
                                   v => Svc.AjustarEstoque(pid, v.Num("nova"), v.Texto("motivo")), "Aplicar ajuste"))
            AtualizarTudo();
    }

    public async Task HistoricoAsync(bool doProduto)
    {
        if (doProduto)
        {
            var pid = ProdutoSelecionado();
            await MovimentosDialog.Abrir(Janela, Svc, pid, $"Movimentos — {Svc.ObterProduto(pid).Nome}");
        }
        else await MovimentosDialog.Abrir(Janela, Svc, null, "Histórico de movimentos de estoque");
    }

    public async Task ExportarAsync()
    {
        var produtos = Produtos();
        if (produtos.Count == 0) { Dica("Não há produtos para exportar."); return; }
        var caminho = await Plat.Atual.SalvarArquivo(Janela, "Exportar estoque", Config.Subpasta("exportacoes"), "estoque.csv", "CSV (Excel)", "csv");
        if (caminho == null) return;
        var n = Util.ExportarCsv(caminho, new[] { "Tipo", "Código", "Produto", "Un.", "Estoque", "Mínimo", "Custo unit.", "Venda unit.", "Valor em estoque (custo)" },
            produtos.Select(p => new object?[] { p.Categoria, p.Codigo, p.Nome, p.Unidade, p.Estoque, p.EstoqueMin, Util.FmtDinheiro(p.PrecoCusto, false),
                                                 Util.FmtDinheiro(p.PrecoVenda, false), Util.FmtDinheiro(p.Estoque * p.PrecoCusto, false) }));
        Sucesso($"{n} produto(s) exportado(s).", ("Abrir arquivo", () => Plat.Atual.Abrir(caminho)));
    }
}
