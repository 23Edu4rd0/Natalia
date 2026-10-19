using GestaoComercial.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GestaoComercial.Tests;

public class CadastrosTests : BaseTeste
{
    [Fact]
    public void Produto_codigo_automatico_e_duplicado()
    {
        var a = Produto("A");
        Produto("B", codigo: "X1");
        Assert.Equal($"{a:00000}", S.ObterProduto(a).Codigo);
        Assert.Throws<ConflitoException>(() => Produto("C", codigo: "x1")); // case-insensitive
    }

    [Fact]
    public void Produto_validacoes()
    {
        Assert.Throws<ValidacaoException>(() => S.SalvarProduto(new ProdutoDados("  ")));
        Assert.Throws<ValidacaoException>(() => S.SalvarProduto(new ProdutoDados("X", PrecoVenda: -1)));
        Assert.Throws<ValidacaoException>(() => S.SalvarProduto(new ProdutoDados("X", EstoqueInicial: -3)));
        Assert.Throws<ValidacaoException>(() => S.SalvarProduto(new ProdutoDados(new string('X', 500))));
    }

    [Fact]
    public void Estoque_inicial_gera_movimento()
    {
        var pid = Produto(estoque: 7);
        var m = Assert.Single(S.ListarMovimentos(pid));
        Assert.Equal(("ENTRADA", 7L, 7L), (m.Tipo, m.Quantidade, m.SaldoApos));
    }

    [Fact]
    public void Editar_nao_altera_estoque()
    {
        var pid = Produto(estoque: 7);
        S.SalvarProduto(new ProdutoDados("Novo nome", EstoqueInicial: 999, PrecoVenda: 1), pid);
        Assert.Equal(7, Estoque(pid));
        Assert.Equal("Novo nome", S.ObterProduto(pid).Nome);
    }

    [Fact]
    public void Excluir_com_e_sem_historico()
    {
        var livre = Produto("Livre", estoque: 0);
        Assert.Equal(ResultadoExclusao.Excluido, S.ExcluirProduto(livre));
        var usado = Produto("Usado", estoque: 5);
        Assert.Equal(ResultadoExclusao.Inativado, S.ExcluirProduto(usado));
        Assert.False(S.ObterProduto(usado).Ativo);
        Assert.DoesNotContain(S.ListarProdutos(), p => p.Id == usado);
    }

    [Fact]
    public void Categorias()
    {
        var c = S.SalvarCategoria("Bebidas");
        Assert.Throws<ConflitoException>(() => S.SalvarCategoria("bebidas"));
        Produto("Suco", cat: c);
        Assert.Throws<ConflitoException>(() => S.ExcluirCategoria(c));
        S.SalvarCategoria("Bebidas geladas", c);
        Assert.Equal(1, S.ListarCategorias()[0].Produtos);
    }

    [Fact]
    public void Pessoas()
    {
        var cid = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Ana", "529.982.247-25"));
        Assert.Equal("52998224725", S.ObterPessoa(TipoPessoa.Cliente, cid).Documento);
        Assert.Throws<ConflitoException>(() => S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Outra", "52998224725")));
        Assert.Throws<ValidacaoException>(() => S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("X", "123")));
        Assert.Throws<ValidacaoException>(() => S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("X", Email: "naoeemail")));
        S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("B"));
        S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("C")); // sem documento pode repetir
        Assert.Single(S.ListarPessoas(TipoPessoa.Cliente, "an"));
        // cliente e fornecedor são listas independentes, mesmo CPF pode existir nas duas
        S.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados("Ana Forn", "52998224725"));
    }

    [Fact]
    public void Busca_com_caracteres_especiais()
    {
        Produto("Desconto 50%"); Produto("Outro");
        Assert.Single(S.ListarProdutos("50%"));
        Assert.Single(S.ListarProdutos("%"));
        Assert.Empty(S.ListarProdutos("_"));
        Assert.Empty(S.ListarProdutos("' OR 1=1 --"));
    }
}

public class EntradasTests : BaseTeste
{
    [Fact]
    public void Entrada_soma_estoque_e_atualiza_custo()
    {
        var f = Fornecedor(); var p = Produto(estoque: 2, custo: 100);
        var eid = S.RegistrarEntrada(f, new[] { IE(p, 8, 150) }, "NF1");
        Assert.Equal(10, Estoque(p));
        Assert.Equal(150, S.ObterProduto(p).PrecoCusto);
        var e = S.ObterEntrada(eid);
        Assert.Equal(1200, e.Total);
        Assert.Equal(8, e.Itens[0].Quantidade);
    }

    [Fact]
    public void Entrada_validacoes_e_atomicidade()
    {
        var f = Fornecedor(); var p = Produto(estoque: 1);
        Assert.Throws<ValidacaoException>(() => S.RegistrarEntrada(null, new[] { IE(p, 1, 1) }));
        Assert.Throws<ValidacaoException>(() => S.RegistrarEntrada(f, Array.Empty<ItemEntradaInput>()));
        Assert.Throws<ValidacaoException>(() => S.RegistrarEntrada(f, new[] { IE(p, 0, 1) }));
        Assert.Throws<NaoEncontradoException>(() => S.RegistrarEntrada(f, new[] { IE(p, 5, 1), IE(9999, 1, 1) }));
        Assert.Equal(1, Estoque(p));            // o primeiro item NÃO pode ter sido gravado
        Assert.Empty(S.ListarEntradas());
        Assert.Throws<ValidacaoException>(() => S.RegistrarEntrada(f, new[] { IE(p, 1, 1) }, data: "2999-01-01"));
    }

    [Fact]
    public void Cancelar_entrada()
    {
        var f = Fornecedor(); var p = Produto(estoque: 0);
        var eid = S.RegistrarEntrada(f, new[] { IE(p, 5) });
        S.CancelarEntrada(eid, "lançada errada");
        Assert.Equal(0, Estoque(p));
        Assert.Throws<ConflitoException>(() => S.CancelarEntrada(eid, "de novo"));
    }

    [Fact]
    public void Cancelar_entrada_ja_vendida_falha()
    {
        var f = Fornecedor(); var p = Produto(estoque: 0);
        var eid = S.RegistrarEntrada(f, new[] { IE(p, 5) });
        S.RegistrarVenda(new[] { IV(p, 4, 300) });
        Assert.Throws<EstoqueException>(() => S.CancelarEntrada(eid, "tentativa"));
        Assert.Equal(1, Estoque(p));
        Assert.Equal("ATIVA", S.ObterEntrada(eid).Status);
    }

    [Fact]
    public void Fornecedor_inativo()
    {
        var f = Fornecedor(); var p = Produto();
        S.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados("Forn Ltda", Ativo: false), f);
        Assert.Throws<ValidacaoException>(() => S.RegistrarEntrada(f, new[] { IE(p, 1, 1) }));
    }
}

public class VendasTests : BaseTeste
{
    [Fact]
    public void Venda_baixa_estoque_e_gera_nota()
    {
        var p = Produto(estoque: 10, custo: 100, venda: 250);
        var vid = S.RegistrarVenda(new[] { IV(p, 3, 250) }, desconto: 50, forma: "Pix");
        Assert.Equal(7, Estoque(p));
        var v = S.ObterVenda(vid);
        Assert.Equal((750L, 50L, 700L), (v.Subtotal, v.Desconto, v.Total));
        Assert.Equal("Consumidor final", v.ClienteNome);
        Assert.Equal(750, v.Itens[0].Total);
        Assert.Equal("VENDA", S.ListarMovimentos(p)[0].Tipo);
    }

    [Fact]
    public void Estoque_insuficiente_nao_grava_nada()
    {
        var a = Produto("A", estoque: 5); var b = Produto("B", estoque: 1);
        var ex = Assert.Throws<EstoqueException>(() => S.RegistrarVenda(new[] { IV(a, 2), IV(b, 2) }));
        Assert.Contains("B", ex.Message);
        Assert.Equal(5, Estoque(a)); Assert.Equal(1, Estoque(b));   // rollback completo
        Assert.Empty(S.ListarVendas());
        Assert.Single(S.ListarMovimentos(a));                          // só o estoque inicial
    }

    [Fact]
    public void Linha_duplicada_respeita_estoque_acumulado()
    {
        var p = Produto(estoque: 5);
        Assert.Throws<EstoqueException>(() => S.RegistrarVenda(new[] { IV(p, 3), IV(p, 3) }));
        Assert.Equal(5, Estoque(p));
    }

    [Fact]
    public void Validacoes_de_venda()
    {
        var p = Produto(estoque: 5);
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(Array.Empty<ItemVendaInput>()));
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 1, 100) }, desconto: 101));
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 1) }, forma: "Cheque do tio"));
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 0) }));
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 1, -1) }));
        Assert.Throws<NaoEncontradoException>(() => S.RegistrarVenda(new[] { IV(4242, 1) }));
        Assert.Throws<NaoEncontradoException>(() => S.RegistrarVenda(new[] { IV(p, 1) }, clienteId: 4242));
        Assert.Equal(5, Estoque(p));
    }

    [Fact]
    public void Produto_inativo_nao_vende()
    {
        var p = Produto(estoque: 5);
        S.SalvarProduto(new ProdutoDados("Caneta", Ativo: false), p);
        Assert.Throws<ValidacaoException>(() => S.RegistrarVenda(new[] { IV(p, 1) }));
    }

    [Fact]
    public void Cliente_e_empresa_ficam_congelados_na_nota()
    {
        var p = Produto(estoque: 5);
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria", "52998224725"));
        S.SalvarEmpresa(new Empresa("Loja A", "", "", "", ""));
        var vid = S.RegistrarVenda(new[] { IV(p, 1) }, clienteId: c);
        S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria Renomeada"), c);
        S.SalvarEmpresa(new Empresa("Loja B", "", "", "", ""));
        var v = S.ObterVenda(vid);
        Assert.Equal("Maria", v.ClienteNome);
        Assert.Equal("52998224725", v.ClienteDoc);
        Assert.Equal("Loja A", v.Emitente.Nome);
    }

    [Fact]
    public void Cancelar_venda_devolve_estoque()
    {
        var p = Produto(estoque: 5);
        var vid = S.RegistrarVenda(new[] { IV(p, 4) });
        Assert.Equal(1, Estoque(p));
        S.CancelarVenda(vid, "cliente desistiu");
        Assert.Equal(5, Estoque(p));
        Assert.True(S.ObterVenda(vid).Cancelada);
        Assert.Throws<ConflitoException>(() => S.CancelarVenda(vid, "outra vez"));
        Assert.Throws<ValidacaoException>(() => S.CancelarVenda(vid, "  "));
        Assert.Equal(5, Estoque(p));
    }

    [Fact]
    public void Preco_historico_preservado()
    {
        var p = Produto(estoque: 5, custo: 100, venda: 250);
        var vid = S.RegistrarVenda(new[] { IV(p, 1, 250) });
        S.SalvarProduto(new ProdutoDados("Caneta Gold", PrecoCusto: 999, PrecoVenda: 9999), p);
        var it = S.ObterVenda(vid).Itens[0];
        Assert.Equal(("Caneta", 250L, 100L), (it.Descricao, it.PrecoUnit, it.CustoUnit));
    }

    [Fact]
    public void Busca_de_vendas()
    {
        var p = Produto(estoque: 9);
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Joana"));
        var v1 = S.RegistrarVenda(new[] { IV(p, 1) }, clienteId: c);
        var v2 = S.RegistrarVenda(new[] { IV(p, 1) });
        Assert.Equal(new[] { v1 }, S.ListarVendas(busca: "joa").Select(v => v.Id));
        Assert.Equal(new[] { v2 }, S.ListarVendas(busca: $"#{v2}").Select(v => v.Id));
        S.CancelarVenda(v2, "teste");
        Assert.Equal(new[] { v2 }, S.ListarVendas(status: "CANCELADA").Select(v => v.Id));
    }
}

public class EstoqueRelatoriosTests : BaseTeste
{
    [Fact]
    public void Ajuste()
    {
        var p = Produto(estoque: 10);
        Assert.Equal(-3, S.AjustarEstoque(p, 7, "quebra"));
        Assert.Equal(7, Estoque(p));
        Assert.Throws<ValidacaoException>(() => S.AjustarEstoque(p, 7, "igual"));
        Assert.Throws<ValidacaoException>(() => S.AjustarEstoque(p, -1, "negativo"));
        Assert.Throws<ValidacaoException>(() => S.AjustarEstoque(p, 3, ""));
    }

    [Fact]
    public void Movimentos_consistentes_com_estoque()
    {
        var f = Fornecedor(); var p = Produto(estoque: 3);
        S.RegistrarEntrada(f, new[] { IE(p, 10, 1) });
        var v = S.RegistrarVenda(new[] { IV(p, 4, 5) });
        S.CancelarVenda(v, "x");
        S.AjustarEstoque(p, 20, "inventário");
        Assert.Equal(Estoque(p), S.ListarMovimentos(p).Sum(m => m.Quantidade));
        Assert.Equal(20, S.ListarMovimentos(p)[0].SaldoApos);
    }

    [Fact]
    public void Resumo_por_categoria_e_baixo()
    {
        var c = S.SalvarCategoria("Bebidas");
        Produto("Suco", estoque: 2, custo: 100, venda: 300, cat: c, min: 5);
        Produto("Avulso", estoque: 10, custo: 50, venda: 80);
        var res = S.ResumoPorCategoria().ToDictionary(r => r.Categoria);
        Assert.Equal(200, res["Bebidas"].ValorCusto);
        Assert.Equal(1, res["Bebidas"].Baixos);
        Assert.Equal(10, res["Sem categoria"].Unidades);
        Assert.Equal(new[] { "Suco" }, S.ListarProdutos(apenasBaixo: true).Select(p => p.Nome));
        Assert.Single(S.ListarProdutos(categoriaId: 0));
    }

    [Fact]
    public void Relatorios()
    {
        var hoje = DateTime.Today.ToString("yyyy-MM-dd");
        var p = Produto("P1", estoque: 20, custo: 100, venda: 300);
        S.RegistrarVenda(new[] { IV(p, 2, 300) }, desconto: 100, forma: "Pix");
        var v2 = S.RegistrarVenda(new[] { IV(p, 1, 300) });
        S.CancelarVenda(v2, "x");
        var r = S.ResumoVendasPeriodo(hoje, hoje);
        Assert.Equal((1L, 500L, 100L, 200L, 300L), (r.Qtd, r.Total, r.Descontos, r.Custo, r.Lucro));
        Assert.Equal((1L, 300L), (r.CanceladasQtd, r.CanceladasTotal));
        Assert.Equal(2, S.MaisVendidos(hoje, hoje)[0].Quantidade);
        Assert.Equal("Pix", S.VendasPorPagamento(hoje, hoje)[0].Forma);
        var pn = S.ObterPainel();
        Assert.Equal(1, pn.Hoje.Qtd);
        Assert.Equal(1, pn.Estoque.Produtos);
    }

    [Fact]
    public void Backup()
    {
        Produto("Salvo");
        var destino = Path.Combine(Pasta, "bk", "copia.db");
        S.FazerBackup(destino);
        using (var c = new SqliteConnection($"Data Source={destino};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM produtos";
            Assert.Equal(1L, cmd.ExecuteScalar());
        }
        var auto = Path.Combine(Pasta, "auto");
        Assert.NotNull(S.BackupAutomatico(auto));
        Assert.Null(S.BackupAutomatico(auto));   // só um por dia
    }

    [Fact]
    public void Empresa_validacoes()
    {
        Assert.Throws<ValidacaoException>(() => S.SalvarEmpresa(new Empresa("", "", "", "", "")));
        Assert.Throws<ValidacaoException>(() => S.SalvarEmpresa(new Empresa("X", "11.222.333/0001-82", "", "", "")));
        S.SalvarEmpresa(new Empresa("Loja", "11.222.333/0001-81", "Rua 1", "123", "Volte sempre"));
        Assert.Equal("11222333000181", S.ObterEmpresa().Documento);
    }
}

public class BancoTests : IDisposable
{
    readonly string pasta = Path.Combine(Path.GetTempPath(), "gc-bd-" + Guid.NewGuid().ToString("N"));
    public BancoTests() => Directory.CreateDirectory(pasta);
    public void Dispose() { try { Directory.Delete(pasta, true); } catch { } }

    [Fact]
    public void Banco_mais_novo_e_recusado()
    {
        using var c = Banco.Conectar(Path.Combine(pasta, "x.db"));
        Banco.Migrar(c);
        using (var cmd = c.CreateCommand()) { cmd.CommandText = "PRAGMA user_version = 99"; cmd.ExecuteNonQuery(); }
        Assert.Contains("mais nova", Assert.Throws<AppException>(() => Banco.Migrar(c)).Message);
    }

    [Fact]
    public void Migrar_duas_vezes_e_idempotente()
    {
        using var c = Banco.Conectar(Path.Combine(pasta, "x.db"));
        Banco.Migrar(c); Banco.Migrar(c);
    }

    [Fact]
    public void Constraint_impede_estoque_negativo()
    {
        using var c = Banco.Conectar(Path.Combine(pasta, "x.db"));
        Banco.Migrar(c);
        void Sql(string s) { using var cmd = c.CreateCommand(); cmd.CommandText = s; cmd.ExecuteNonQuery(); }
        Sql("INSERT INTO produtos (codigo, nome, estoque) VALUES ('1','a',1)");
        Assert.Throws<SqliteException>(() => Sql("UPDATE produtos SET estoque = -1"));
    }

    [Fact]
    public void Arquivo_corrompido_vira_mensagem_amigavel_ou_erro_de_banco()
    {
        var arq = Path.Combine(pasta, "lixo.db");
        File.WriteAllBytes(arq, Enumerable.Range(0, 5000).Select(i => (byte)(i * 31 % 251)).ToArray());
        Assert.ThrowsAny<Exception>(() => { using var c = Banco.Conectar(arq); Banco.Migrar(c); });
    }

    [Fact]
    public void Banco_ocupado_por_outra_conexao_gera_erro_de_lock()
    {
        var arq = Path.Combine(pasta, "lock.db");
        using var a = Banco.Conectar(arq); Banco.Migrar(a);
        using var b = Banco.Conectar(arq);
        using (var cmd = b.CreateCommand()) { cmd.CommandText = "PRAGMA busy_timeout = 200"; cmd.ExecuteNonQuery(); }
        using (var cmd = a.CreateCommand()) { cmd.CommandText = "BEGIN IMMEDIATE"; cmd.ExecuteNonQuery(); }
        var s = new Servico(b);
        var ex = Assert.Throws<SqliteException>(() => s.SalvarCategoria("X"));
        Assert.Equal(5, ex.SqliteErrorCode); // SQLITE_BUSY
        using (var cmd = a.CreateCommand()) { cmd.CommandText = "ROLLBACK"; cmd.ExecuteNonQuery(); }
        Assert.Equal(0, s.ListarCategorias().Count);   // nada foi gravado
    }
}

public class NotaTests : BaseTeste
{
    [Fact]
    public void Html_e_texto()
    {
        var p = Produto("Café <b>forte</b>", estoque: 5, venda: 1999);
        S.SalvarEmpresa(new Empresa("Loja & Cia", "11222333000181", "", "", ""));
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("José <script>"));
        var vid = S.RegistrarVenda(new[] { IV(p, 2, 1999) }, clienteId: c);
        var v = S.ObterVenda(vid);
        var html = Nota.GerarHtml(v);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<b>forte", html);
        Assert.Contains("Loja &amp; Cia", html);
        Assert.Contains("R$ 39,98", html);
        Assert.Contains("11.222.333/0001-81", html);
        var txt = Nota.GerarTexto(v);
        Assert.Contains("R$ 39,98", txt);
        Assert.Contains($"{vid:000000}", txt);
        S.CancelarVenda(vid, "teste");
        Assert.Contains("CANCELADA", Nota.GerarHtml(S.ObterVenda(vid)));
        Assert.True(File.Exists(Nota.SalvarHtml(S.ObterVenda(vid), Path.Combine(Pasta, "notas"))));
    }
}
