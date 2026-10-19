using System.Text;
using GestaoComercial.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GestaoComercial.Tests;

public class ImportacaoTests : BaseTeste
{
    string Csv(string conteudo, Encoding? enc = null)
    {
        var p = Path.Combine(Pasta, Guid.NewGuid().ToString("N") + ".csv");
        File.WriteAllBytes(p, (enc ?? new UTF8Encoding(true)).GetPreamble().Concat((enc ?? new UTF8Encoding(true)).GetBytes(conteudo)).ToArray());
        return p;
    }

    [Fact]
    public void Importa_planilha_do_modelo_criando_tipos_e_produtos()
    {
        var r = S.ImportarProdutosCsv(Csv(Servico.ModeloCsvProdutos));
        Assert.Equal((2, 0, 0), (r.Criados, r.Atualizados, r.Ignorados));
        var camisa = S.ListarProdutos("Camiseta")[0];
        Assert.Equal(("7891000100103", "Roupas", 2500L, 4990L, 30L, 5L), (camisa.Codigo, camisa.Categoria, camisa.PrecoCusto, camisa.PrecoVenda, camisa.Estoque, camisa.EstoqueMin));
        Assert.Equal(2, S.ListarCategorias().Count);
        Assert.Equal("00002", S.ListarProdutos("Garrafa")[0].Codigo);          // sem código: gera automático
        Assert.Equal(30, S.ListarMovimentos(camisa.Id)[0].Quantidade);          // estoque inicial vira movimento
    }

    [Fact]
    public void Reimportar_atualiza_pelo_codigo_e_ajusta_estoque_com_historico()
    {
        S.ImportarProdutosCsv(Csv(Servico.ModeloCsvProdutos));
        var r = S.ImportarProdutosCsv(Csv("Nome;Código;Preço de venda;Estoque\nCamiseta básica branca;7891000100103;59,90;12\n"));
        Assert.Equal((0, 1), (r.Criados, r.Atualizados));
        var p = S.ObterProdutoPorCodigo("7891000100103")!;
        Assert.Equal((5990L, 12L), (p.PrecoVenda, p.Estoque));
        Assert.Equal(-18, S.ListarMovimentos(p.Id)[0].Quantidade);
        Assert.Equal("Roupas", p.Categoria);                                   // tipo mantido (coluna ausente na 2ª planilha)
    }

    [Fact]
    public void Excel_brasileiro_ansi_virgula_e_aspas()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Encoding.GetEncoding(1252);
        var p = Csv("Produto,Categoria,Preço,Qtd\r\n\"Sabão, em pó 1kg\",Limpeza,\"R$ 11,90\",8\r\n\"Água \"\"gelada\"\"\",Bebidas,2.50,3\r\n", ansi);
        var r = S.ImportarProdutosCsv(p);
        Assert.Equal(2, r.Criados);
        Assert.Equal(1190, S.ListarProdutos("Sabão")[0].PrecoVenda);
        Assert.Equal("Água \"gelada\"", S.ListarProdutos("gelada")[0].Nome);
        Assert.Equal(250, S.ListarProdutos("gelada")[0].PrecoVenda);
    }

    [Fact]
    public void Linhas_com_erro_sao_puladas_e_listadas_sem_perder_as_boas()
    {
        var r = S.ImportarProdutosCsv(Csv("Nome;Preço;Estoque\nBom;10,00;5\n;5,00;1\nRuim;abc;2\nOutro bom;3,00;-4\nÓtimo;7,00;9\n"));
        Assert.Equal((2, 3), (r.Criados, r.Ignorados));
        Assert.Contains(r.Erros, e => e.StartsWith("Linha 3") && e.Contains("sem nome"));
        Assert.Contains(r.Erros, e => e.StartsWith("Linha 4") && e.Contains("inválido"));
        Assert.Contains(r.Erros, e => e.StartsWith("Linha 5"));
        Assert.Equal(new[] { "Bom", "Ótimo" }, S.ListarProdutos().Select(x => x.Nome));
    }

    [Fact]
    public void Planilha_invalida_da_mensagem_clara()
    {
        Assert.Contains("coluna com o nome", Assert.Throws<ValidacaoException>(() => S.ImportarProdutosCsv(Csv("A;B\n1;2\n"))).Message);
        Assert.Throws<ValidacaoException>(() => S.ImportarProdutosCsv(Csv("")));
    }

    [Fact]
    public void Importa_milhares_de_linhas_rapido()
    {
        var sb = new StringBuilder("Nome;Código;Custo;Preço;Estoque\n");
        for (int i = 1; i <= 5000; i++) sb.Append($"Produto {i};C{i};1,00;2,00;3\n");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = S.ImportarProdutosCsv(Csv(sb.ToString()));
        Assert.Equal(5000, r.Criados);
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"importação lenta: {sw.Elapsed.TotalSeconds:F1}s");
    }
}

public class VersaoBackupConfigTests : BaseTeste
{
    [Fact]
    public void Versao_so_muda_quando_grava()
    {
        var v0 = S.Versao;
        S.ListarProdutos(); S.ObterPainel(); S.ListarVendas();
        Assert.Equal(v0, S.Versao);                                            // leitura não muda
        var p = Produto();
        Assert.True(S.Versao > v0);
        var v1 = S.Versao;
        Assert.Throws<EstoqueException>(() => S.RegistrarVenda(new[] { IV(p, 999) }));   // falha: rollback, nada mudou
        Assert.Equal(v1, S.Versao);
        S.RegistrarVenda(new[] { IV(p, 1) });
        Assert.True(S.Versao > v1);
    }

    [Fact]
    public void Configuracao_simples()
    {
        Assert.Equal("aviso", S.ObterConfig("apos_venda", "aviso"));
        S.SalvarConfig("apos_venda", "cupom"); S.SalvarConfig("apos_venda", "nada");
        Assert.Equal("nada", S.ObterConfig("apos_venda", "aviso"));
        Assert.Equal("Minha Empresa", S.ObterEmpresa().Nome);                  // não interfere nos dados da loja
    }

    [Fact]
    public void Restaurar_backup_volta_os_dados_e_guarda_copia_de_seguranca()
    {
        var p = Produto("Antes", estoque: 5);
        var bk = S.FazerBackup(Path.Combine(Pasta, "bk", "b.db"));
        S.SalvarProduto(new ProdutoDados("Depois", PrecoVenda: 1), null);
        S.RegistrarVenda(new[] { IV(p, 2) });
        var v = S.Versao;
        var seg = S.RestaurarBackup(bk, Path.Combine(Pasta, "seg"));
        Assert.True(File.Exists(seg));
        Assert.Equal(new[] { "Antes" }, S.ListarProdutos().Select(x => x.Nome));
        Assert.Empty(S.ListarVendas());
        Assert.Equal(5, Estoque(p));
        Assert.True(S.Versao > v);
        // a cópia de segurança contém o estado de antes de restaurar (com "Depois" e a venda)
        using var c = new SqliteConnection($"Data Source={seg};Pooling=False"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM produtos"; Assert.Equal(2L, cmd.ExecuteScalar());
    }

    [Fact]
    public void Restaurar_arquivo_invalido_nao_mexe_nos_dados()
    {
        Produto("Intacto");
        var lixo = Path.Combine(Pasta, "lixo.db"); File.WriteAllBytes(lixo, Enumerable.Range(0, 3000).Select(i => (byte)(i % 251)).ToArray());
        Assert.ThrowsAny<AppException>(() => S.RestaurarBackup(lixo, Path.Combine(Pasta, "seg")));
        var outro = Path.Combine(Pasta, "outro.db");
        using (var c = new SqliteConnection($"Data Source={outro};Pooling=False")) { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE x(a)"; cmd.ExecuteNonQuery(); }
        Assert.Contains("não parece ser um backup", Assert.Throws<ValidacaoException>(() => S.RestaurarBackup(outro, Path.Combine(Pasta, "seg"))).Message);
        Assert.Throws<ValidacaoException>(() => S.RestaurarBackup(Path.Combine(Pasta, "nao-existe.db"), Path.Combine(Pasta, "seg")));
        Assert.Equal(new[] { "Intacto" }, S.ListarProdutos().Select(x => x.Nome));
    }

    [Fact]
    public void Backup_em_segundo_plano_usa_conexao_propria()
    {
        Produto("Dado");
        var pasta = Path.Combine(Pasta, "auto");
        var t = Task.Run(() => Servico.BackupAutomaticoDeArquivo(Path.Combine(Pasta, "t.db"), pasta));
        Produto("Outro");                                                       // a conexão principal segue usável enquanto o backup roda
        Assert.NotNull(t.GetAwaiter().GetResult());
        Assert.Null(Servico.BackupAutomaticoDeArquivo(Path.Combine(Pasta, "t.db"), pasta));
        using var c = new SqliteConnection($"Data Source={Directory.GetFiles(pasta, "*.db")[0]};Pooling=False"); c.Open();
        using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM produtos"; Assert.True((long)cmd.ExecuteScalar()! >= 1);
    }
}
