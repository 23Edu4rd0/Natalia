using GestaoComercial.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GestaoComercial.Tests;

public class FiadoTests : BaseTeste
{
    long Cliente(string nome = "Maria") => S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados(nome, Telefone: "(11) 98888-7777"));

    [Fact]
    public void Venda_fiado_exige_cliente()
    {
        var p = Produto(estoque: 5);
        var ex = Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 1) }, forma: "Fiado"));
        Assert.Contains("precisa de um cliente", ex.Message);
        Assert.Equal(5, Estoque(p));
    }

    [Fact]
    public void Fiado_gera_saldo_e_pagamento_parcial_e_total()
    {
        var p = Produto(estoque: 10); var c = Cliente();
        S.RegistrarVenda(new[] { IV(p, 2, 500) }, c, forma: "Fiado");       // 1000
        S.RegistrarVenda(new[] { IV(p, 1, 300) }, c, forma: "Fiado");       // +300
        S.RegistrarVenda(new[] { IV(p, 1, 999) }, c, forma: "Pix");         // não entra
        Assert.Equal(1300, S.SaldoCliente(c));
        var d = Assert.Single(S.ListarDevedores());
        Assert.Equal((c, 1300L), (d.ClienteId, d.Saldo));
        Assert.Equal(1300, S.TotalAReceber());

        S.RegistrarRecebimento(c, 500, "Dinheiro");
        Assert.Equal(800, S.SaldoCliente(c));
        Assert.Throws<ValidacaoException>(() => S.RegistrarRecebimento(c, 801, "Pix"));      // maior que a dívida
        Assert.Throws<ValidacaoException>(() => S.RegistrarRecebimento(c, 0, "Pix"));
        Assert.Throws<ValidacaoException>(() => S.RegistrarRecebimento(c, 100, "Fiado"));    // não paga fiado com fiado
        S.RegistrarRecebimento(c, 800, "Pix");
        Assert.Equal(0, S.SaldoCliente(c));
        Assert.Empty(S.ListarDevedores());
        Assert.Throws<ValidacaoException>(() => S.RegistrarRecebimento(c, 1, "Pix"));        // nada a pagar
    }

    [Fact]
    public void Cancelar_venda_fiado_reduz_a_divida_e_estorno_a_restaura()
    {
        var p = Produto(estoque: 10); var c = Cliente();
        var v = S.RegistrarVenda(new[] { IV(p, 2, 500) }, c, forma: "Fiado");
        var r = S.RegistrarRecebimento(c, 400, "Dinheiro");
        Assert.Equal(600, S.SaldoCliente(c));
        S.EstornarRecebimento(r, "lançado errado");
        Assert.Equal(1000, S.SaldoCliente(c));
        Assert.Throws<ConflitoException>(() => S.EstornarRecebimento(r, "de novo"));
        Assert.True(S.ListarRecebimentos(c)[0].Estornado);
        S.CancelarVenda(v, "desistiu");
        Assert.Equal(0, S.SaldoCliente(c));
    }

    [Fact]
    public void Extrato_com_saldo_corrente_e_sem_notas_canceladas()
    {
        var p = Produto(estoque: 10); var c = Cliente();
        S.RegistrarVenda(new[] { IV(p, 1, 1000) }, c, forma: "Fiado");
        var cancelada = S.RegistrarVenda(new[] { IV(p, 1, 777) }, c, forma: "Fiado"); S.CancelarVenda(cancelada, "x");
        S.RegistrarRecebimento(c, 250, "Pix");
        var ex = S.ExtratoCliente(c);
        Assert.Equal(new long[] { 1000, -250 }, ex.Select(l => l.Valor));
        Assert.Equal(new long[] { 1000, 750 }, ex.Select(l => l.Saldo));
        Assert.Contains("Compra fiado", ex[0].Descricao); Assert.Contains("Pagamento (Pix)", ex[1].Descricao);
    }

    [Fact]
    public void Vendas_do_cliente_e_historico()
    {
        var p = Produto(estoque: 10); var a = Cliente("Ana"); var b = Cliente("Bia");
        S.RegistrarVenda(new[] { IV(p, 1) }, a); S.RegistrarVenda(new[] { IV(p, 1) }, a); S.RegistrarVenda(new[] { IV(p, 1) }, b);
        Assert.Equal(2, S.ListarVendas(clienteId: a).Count);
    }
}

public class CaixaTests : BaseTeste
{
    [Fact]
    public void Fechamento_do_dia_soma_dinheiro_certo()
    {
        var hoje = DateTime.Today.ToString("yyyy-MM-dd");
        var p = Produto(estoque: 50, custo: 100); var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Zé"));
        S.RegistrarMovimentoCaixa("SUPRIMENTO", 10000, "troco inicial");
        S.RegistrarVenda(new[] { IV(p, 2, 1000) }, forma: "Dinheiro");               // 2000 em dinheiro
        S.RegistrarVenda(new[] { IV(p, 1, 3000) }, forma: "Pix");                    // 3000 no pix
        S.RegistrarVenda(new[] { IV(p, 1, 500) }, c, forma: "Fiado");                // 500 no fiado
        var cancelada = S.RegistrarVenda(new[] { IV(p, 1, 9999) }, forma: "Dinheiro"); S.CancelarVenda(cancelada, "x");
        S.RegistrarRecebimento(c, 200, "Dinheiro");                                  // +200 em dinheiro
        S.RegistrarMovimentoCaixa("SANGRIA", 1500, "pagar fornecedor");
        var r = S.ObterResumoCaixa(hoje);
        Assert.Equal((3L, 5500L, 500L), (r.VendasQtd, r.VendasTotal, r.FiadoLancado));
        Assert.Equal((10000L, 1500L), (r.Suprimentos, r.Sangrias));
        Assert.Equal(200, r.RecebimentosTotal);
        Assert.Equal(10000 + 2000 + 200 - 1500, r.DinheiroEsperado);
        Assert.Equal(2, r.Movimentos.Count);
        Assert.Contains(r.VendasPorForma, f => f.Forma == "Fiado" && f.Total == 500);
        var html = Impressos.FechamentoCaixa(S.ObterEmpresa(), r);
        Assert.Contains("R$ 107,00", html);
    }

    [Fact]
    public void Movimentos_validacoes_e_exclusao_so_de_hoje()
    {
        Assert.Throws<ValidacaoException>(() => S.RegistrarMovimentoCaixa("OUTRO", 100, "x"));
        Assert.Throws<ValidacaoException>(() => S.RegistrarMovimentoCaixa("SUPRIMENTO", 0, "x"));
        Assert.Throws<ValidacaoException>(() => S.RegistrarMovimentoCaixa("SANGRIA", 100, ""));      // retirada exige motivo
        var id = S.RegistrarMovimentoCaixa("SUPRIMENTO", 100, "");
        S.ExcluirMovimentoCaixa(id);
        Assert.Empty(S.ObterResumoCaixa(DateTime.Today.ToString("yyyy-MM-dd")).Movimentos);
        Assert.Throws<NaoEncontradoException>(() => S.ExcluirMovimentoCaixa(id));
    }
}

public class PrecosReposicaoTests : BaseTeste
{
    [Fact]
    public void Reajuste_de_precos_por_categoria_e_arredondamento()
    {
        var c = S.SalvarCategoria("Bebidas");
        var a = Produto("A", custo: 100, venda: 250, cat: c); var b = Produto("B", custo: 100, venda: 333); // sem categoria
        Assert.Equal(1, S.ReajustarPrecos(c, 10));
        Assert.Equal(275, S.ObterProduto(a).PrecoVenda); Assert.Equal(100, S.ObterProduto(a).PrecoCusto); Assert.Equal(333, S.ObterProduto(b).PrecoVenda);
        Assert.Equal(2, S.ReajustarPrecos(null, -10, incluirCusto: true));
        Assert.Equal((248L, 90L), (S.ObterProduto(a).PrecoVenda, S.ObterProduto(a).PrecoCusto));       // 247,5 -> 248
        Assert.Equal(1, S.ReajustarPrecos(0, 5)); Assert.Equal(315, S.ObterProduto(b).PrecoVenda);      // só sem categoria
        Assert.Throws<ValidacaoException>(() => S.ReajustarPrecos(null, 0));
        Assert.Throws<ValidacaoException>(() => S.ReajustarPrecos(null, -95));
        Assert.Throws<ValidacaoException>(() => S.ReajustarPrecos(null, 501));
    }

    [Fact]
    public void Lista_de_reposicao_e_sugestao()
    {
        Produto("Baixo", estoque: 2, min: 10); Produto("Zerado", estoque: 0, min: 0); Produto("Ok", estoque: 50, min: 5);
        var l = S.ListarReposicao().ToDictionary(i => i.Produto.Nome);
        Assert.Equal(2, l.Count);
        Assert.Equal(18, l["Baixo"].Sugestao);       // 2*10 - 2
        Assert.Equal(1, l["Zerado"].Sugestao);       // mínimo 1
        Assert.Contains("Baixo", Impressos.Reposicao(S.ObterEmpresa(), l.Values));
    }

    [Fact]
    public void Produtos_rapidos_sao_os_mais_vendidos_com_estoque()
    {
        var a = Produto("A", estoque: 20); var b = Produto("B", estoque: 20); var c = Produto("C", estoque: 1);
        S.RegistrarVenda(new[] { IV(a, 2) }); S.RegistrarVenda(new[] { IV(b, 7) }); S.RegistrarVenda(new[] { IV(c, 1) });  // C fica sem estoque
        Assert.Equal(new[] { "B", "A" }, S.ProdutosRapidos().Select(p => p.Nome));
        Assert.Single(S.ProdutosRapidos(1));
    }
}

public class DescontoETextoTests : BaseTeste
{
    [Theory]
    [InlineData("", 10000, 0)] [InlineData("5,00", 10000, 500)] [InlineData("10%", 10000, 1000)] [InlineData("12,5%", 10000, 1250)]
    [InlineData(" 10 % ", 10000, 1000)] [InlineData("33,33%", 1000, 333)] [InlineData("100%", 777, 777)]
    public void Desconto_valores_e_porcentagem(string txt, long sub, long esperado) => Assert.Equal(esperado, Util.ParseDesconto(txt, sub));

    [Theory] [InlineData("101%")] [InlineData("abc%")] [InlineData("-5%")] [InlineData("abc")]
    public void Desconto_invalido(string txt) => Assert.Throws<ValidacaoException>(() => Util.ParseDesconto(txt, 10000));

    [Fact]
    public void Cupom_whatsapp_e_cobranca()
    {
        S.SalvarEmpresa(new Empresa("Loja Bela", "", "", "", "Volte sempre!"));
        var p = Produto("Blusa", estoque: 5);
        var v = S.ObterVenda(S.RegistrarVenda(new[] { IV(p, 2, 4990) }, desconto: 500, forma: "Pix"));
        var cupom = Nota.GerarHtml(v, cupom: true);
        Assert.Contains("class=\"cupom\"", cupom); Assert.DoesNotContain("class=\"cupom\"", Nota.GerarHtml(v));
        var msg = Nota.TextoWhatsApp(v);
        Assert.Contains("*Loja Bela*", msg); Assert.Contains("2x Blusa", msg); Assert.Contains("R$ 94,80", msg); Assert.Contains("Volte sempre!", msg);
        Assert.StartsWith("https://wa.me/5511988887777?text=", Nota.LinkWhatsApp("(11) 98888-7777", msg));
        Assert.StartsWith("https://wa.me/?text=", Nota.LinkWhatsApp("", msg));
        Assert.StartsWith("https://wa.me/5511988887777?", Nota.LinkWhatsApp("+55 11 98888-7777", msg));
        Assert.Contains("R$ 150,00", Impressos.MensagemCobranca(S.ObterEmpresa(), "Maria da Silva", 15000)); Assert.Contains("Olá, Maria!", Impressos.MensagemCobranca(S.ObterEmpresa(), "Maria da Silva", 15000));
    }
}

public class MigracaoTests : IDisposable
{
    readonly string pasta = Path.Combine(Path.GetTempPath(), "gc-mig-" + Guid.NewGuid().ToString("N"));
    public MigracaoTests() => Directory.CreateDirectory(pasta);
    public void Dispose() { try { Directory.Delete(pasta, true); } catch { } }

    [Fact]
    public void Banco_v1_antigo_sobe_para_v2_sem_perder_dados()
    {
        var arq = Path.Combine(pasta, "antigo.db");
        using (var c = Banco.Conectar(arq))
        {
            Banco.Migrar(c);
            var s = new Servico(c);
            var p = s.SalvarProduto(new ProdutoDados("Antigo", PrecoVenda: 1000, EstoqueInicial: 5));
            s.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 1000) });
            void Sql(string q) { using var cmd = c.CreateCommand(); cmd.CommandText = q; cmd.ExecuteNonQuery(); }
            Sql("DROP TABLE recebimentos"); Sql("DROP TABLE caixa_movs");                                     // volta ao estado da versão 1
            Sql("DROP TABLE notas_fiscais"); Sql("DROP TABLE clientes_fiscal");
            foreach (var col in new[] { "ncm", "cest", "origem", "cfop", "tributacao" }) Sql($"ALTER TABLE produtos DROP COLUMN {col}");
            Sql("PRAGMA user_version = 1");
        }
        using (var c = Banco.Conectar(arq))
        {
            Banco.Migrar(c);
            var s = new Servico(c);
            Assert.Equal("Antigo", Assert.Single(s.ListarProdutos()).Nome);
            Assert.Single(s.ListarVendas());
            Assert.Empty(s.ListarDevedores());
            s.RegistrarMovimentoCaixa("SUPRIMENTO", 100, "ok");        // tabelas novas funcionam
            using var cmd = c.CreateCommand(); cmd.CommandText = "PRAGMA user_version"; Assert.Equal((long)Banco.VersaoEsquema, cmd.ExecuteScalar());
        }
    }
}
