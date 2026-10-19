using System.Diagnostics;
using Avalonia.Headless.XUnit;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Xunit;
using Xunit.Abstractions;

namespace GestaoComercial.Tests;

/// <summary>Benchmark opcional: GC_BENCH_DIR=pasta com um gestao.db grande.</summary>
public class BenchTests
{
    readonly ITestOutputHelper o;
    public BenchTests(ITestOutputHelper o) { this.o = o; }

    static string T(Action f, int n = 3)
    {
        var melhor = double.MaxValue;
        for (int i = 0; i < n; i++) { var sw = Stopwatch.StartNew(); f(); melhor = Math.Min(melhor, sw.Elapsed.TotalMilliseconds); }
        return $"{melhor:F0} ms";
    }

    [AvaloniaFact]
    public void Desempenho_com_20_mil_produtos()
    {
        var dir = Environment.GetEnvironmentVariable("GC_BENCH_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Environment.SetEnvironmentVariable("GESTAO_DATA_DIR", dir);
        using var conn = Banco.Conectar(Path.Combine(dir, "gestao.db")); Banco.Migrar(conn);
        var s = new Servico(conn);
        var linhas = new List<string>
        {
            "=== C# (20 mil produtos) ===",
            "serviço: listar 20k produtos      : " + T(() => s.ListarProdutos()),
            "serviço: buscar 'Parafuso 1'      : " + T(() => s.ListarProdutos("Parafuso 1")),
            "serviço: relatório do mês         : " + T(() => { s.ResumoVendasPeriodo("2026-10-01", "2026-10-31"); s.MaisVendidos("2026-10-01", "2026-10-31"); }),
        };
        // decomposição do custo
        var prods = s.ListarProdutos();
        linhas.Add("  só consulta SQL (sem ordenar)    : " + T(() => { using var c = conn.CreateCommand(); c.CommandText = "SELECT p.id, p.codigo, p.nome, p.categoria_id, p.unidade, p.preco_custo, p.preco_venda, p.estoque, p.estoque_min, p.ativo FROM produtos p"; using var r = c.ExecuteReader(); int n = 0; while (r.Read()) { r.GetInt64(0); r.GetString(1); n++; } }));
        linhas.Add("  ordenar 20k nomes (chave/acento) : " + T(() => { var l = prods.ToList(); l.Sort((a, b) => Util.ComparaNomes(a.Nome, b.Nome)); }));
        linhas.Add("  formatar 20k linhas (textos)     : " + T(() => { foreach (var p in prods) _ = new[] { p.Codigo, p.Nome, p.Categoria, p.Unidade, Util.FmtDinheiro(p.PrecoCusto), Util.FmtDinheiro(p.PrecoVenda), p.Estoque.ToString(), p.EstoqueMin.ToString(), "Ativo" }; }));
        Plat.Atual = new PlataformaFalsa();
        var w = new MainWindow(s) { Width = 1240, Height = 760 }; w.Show();
        var p = (ProdutosTela)w.Telas["Produtos"]; w.IrPara("Produtos"); w.UpdateLayout();
        linhas.Add("TELA: abrir aba Produtos (20k)    : " + T(() => { p.Atualizar(); Ambiente.Rodar(); w.UpdateLayout(); }, 2));
        linhas.Add("TELA: digitar na busca + limpar   : " + T(() => { p.Busca.Text = "Caneta modelo 1"; Ambiente.Rodar(); w.UpdateLayout(); p.Busca.Text = ""; Ambiente.Rodar(); w.UpdateLayout(); }, 2));
        w.Close();
        foreach (var l in linhas) o.WriteLine(l);
        File.WriteAllLines(Path.Combine(dir, "bench_cs.txt"), linhas);
    }
}
