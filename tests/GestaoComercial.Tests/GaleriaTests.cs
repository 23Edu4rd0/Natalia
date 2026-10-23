using Avalonia.Headless.XUnit;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GestaoComercial.Tests;

/// <summary>
/// Abre todas as telas com dados de uma loja de verdade, em tela grande e em notebook.
/// Serve de teste de fumaça (nenhuma tela pode quebrar) e, com GC_SHOT_DIR, gera as fotos para conferir o visual.
/// </summary>
public class GaleriaTests
{
    internal static void SemearLoja(Servico s, SqliteConnection conn)
    {
        var roupas = s.SalvarCategoria("Roupas"); var acess = s.SalvarCategoria("Acessórios"); var cosm = s.SalvarCategoria("Cosméticos");
        long P(string n, long c, long cu, long v, long est, long min, string? cod = null) =>
            s.SalvarProduto(new ProdutoDados(n, cod, CategoriaId: c, PrecoCusto: cu, PrecoVenda: v, EstoqueInicial: est, EstoqueMin: min));
        var ids = new[]
        {
            P("Blusa de alcinha branca", roupas, 2500, 5990, 18, 4, "7891000100011"), P("Calça jeans skinny", roupas, 6000, 12990, 9, 3),
            P("Vestido floral midi", roupas, 7000, 15990, 2, 3), P("Short jeans", roupas, 3500, 7990, 14, 4),
            P("Brinco de argola dourado", acess, 900, 2990, 40, 10), P("Colar de pérolas", acess, 1800, 4990, 6, 4),
            P("Bolsa transversal preta", acess, 5500, 11990, 5, 2), P("Batom matte vermelho", cosm, 1200, 3490, 25, 8),
            P("Base líquida bege", cosm, 2900, 6990, 0, 3), P("Perfume floral 100ml", cosm, 8000, 18990, 7, 2),
        };
        s.SalvarEmpresa(new Empresa("Natália Modas", "11222333000181", "Rua das Flores, 123 - Centro", "(11) 98888-7777", "Obrigada pela preferência!"));
        var forn = s.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados("Atacadão da Moda Ltda", "11222333000181", "(11) 3333-4444"));
        var maria = s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria Silva", "529.982.247-25", "(11) 99999-0000", "maria@email.com", "Rua A, 10"));
        var ana = s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Ana Paula Souza", "", "(11) 97777-1234"));
        var joana = s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Joana Lima", "", "(11) 96666-5555"));
        s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Carla Mendes"));
        s.RegistrarEntrada(forn, new[] { new ItemEntradaInput(ids[0], 10, 2500), new ItemEntradaInput(ids[3], 6, 3500) }, "NF 1234");

        // vendas espalhadas pelos últimos 14 dias (para o gráfico e os relatórios)
        var r = new Random(7);
        var clientes = new long?[] { null, null, maria, ana, joana, null };
        var formas = new[] { "Dinheiro", "Pix", "Cartão de débito", "Cartão de crédito", "Pix" };
        for (int dia = 13; dia >= 0; dia--)
        {
            int n = dia == 0 ? 4 : 1 + r.Next(4);
            for (int k = 0; k < n; k++)
            {
                var p = s.ObterProduto(ids[r.Next(ids.Length)]);
                if (p.Estoque < 2) continue;
                var cli = clientes[r.Next(clientes.Length)];
                var forma = cli != null && r.Next(4) == 0 ? "Fiado" : formas[r.Next(formas.Length)];
                var vid = s.RegistrarVenda(new[] { new ItemVendaInput(p.Id, 1 + r.Next(2), p.PrecoVenda) }, cli, forma: forma);
                if (dia > 0)
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "UPDATE vendas SET data = $d WHERE id = $id";
                        cmd.Parameters.AddWithValue("$d", DateTime.Today.AddDays(-dia).AddHours(10 + k).ToString("yyyy-MM-dd HH:mm:ss"));
                        cmd.Parameters.AddWithValue("$id", vid);
                        cmd.ExecuteNonQuery();
                    }
            }
        }
        if (s.SaldoCliente(maria) <= 0) s.RegistrarVenda(new[] { new ItemVendaInput(ids[4], 2, 2990) }, maria, forma: "Fiado");
    }

    static void Galeria(bool simples, int largura, int altura, string prefixo)
    {
        using var a = new Ambiente(simples);
        a.Janela.Width = largura; a.Janela.Height = altura;
        a.Janela.Show();
        SemearLoja(a.S, a.Conn);
        foreach (var nome in a.Janela.MenuVisivel)
        {
            a.Janela.IrPara(nome); Ambiente.Rodar(); a.Janela.UpdateLayout();
            a.Foto($"{prefixo}_{nome.Replace(' ', '_')}");
        }
    }

    [AvaloniaFact] public void Galeria_simples_tela_grande() => Galeria(true, 1920, 1050, "g1920s");
    [AvaloniaFact] public void Galeria_avancado_tela_grande() => Galeria(false, 1920, 1050, "g1920a");
    [AvaloniaFact] public void Galeria_simples_notebook() => Galeria(true, 1280, 760, "g1280s");
    [AvaloniaFact] public void Galeria_avancado_notebook() => Galeria(false, 1280, 760, "g1280a");
}
