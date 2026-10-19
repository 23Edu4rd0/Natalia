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
    readonly TextBlock resumo = K.Rotulo("", true, 14);

    public override string Titulo => "Estoque";
    public override string Subtitulo => "Quanto você tem de cada produto, agrupado por tipo.";

    public async Task ReposicaoAsync() => await ReposicaoDialog.Abrir(Janela, Svc);

    public EstoqueTela(MainWindow app) : base(app)
    {
        Busca = K.Caixa("", 300, "Buscar produto…");
        Busca.InnerLeftContent = K.Margem(Icones.Criar("busca", 18, "#9CA3AF"), 10, 0, 0, 0);
        SomenteBaixo = new CheckBox { Content = "Somente estoque baixo/zerado", VerticalAlignment = VerticalAlignment.Center };
        var direita = K.Linha(K.Botao("Ajustar estoque…", SeguroAsync(AjustarAsync), icone: "editar", classe: "primario"),
                              K.Botao("Histórico do produto", SeguroAsync(() => HistoricoAsync(true)), icone: "ver"),
                              K.Botao("Histórico geral", SeguroAsync(() => HistoricoAsync(false))),
                              K.Botao("Lista de reposição", SeguroAsync(ReposicaoAsync), icone: "notas"),
                              K.Botao("Exportar CSV", SeguroAsync(ExportarAsync), icone: "exportar"));
        var barra = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 14), Children = { K.Linha(14, Busca, SomenteBaixo), direita } };

        Tabela = new Tabela(new Col("Tipo de produto / Produto", 300, Expande: true), new Col("Código", 90), new Col("Un.", 60), new Col("Estoque", 100, true),
                            new Col("Mínimo", 90, true), new Col("Custo unit.", 110, true), new Col("Venda unit.", 110, true), new Col("Valor (custo)", 140, true));
        var resumoCartao = K.Cartao(resumo); resumoCartao.Margin = new Thickness(0, 0, 0, 14);
        var nota = K.Discreto("Linhas em vermelho: estoque no mínimo ou abaixo dele."); nota.Margin = new Thickness(0, 10, 0, 0);
        Content = K.Doca(new StackPanel { Children = { resumoCartao, barra } }, Tabela.ComVazio("Nenhum produto para mostrar.\nCadastre produtos na página Produtos."), nota);

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
        resumo.Text = $"Valor total em estoque: {Util.FmtDinheiro(res.Sum(r => r.ValorCusto))} (custo)  |  {Util.FmtDinheiro(res.Sum(r => r.ValorVenda))} (venda)  |  Produtos com estoque baixo: {res.Sum(r => r.Baixos)}";
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
