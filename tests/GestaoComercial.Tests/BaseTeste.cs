using GestaoComercial.Core;
using Microsoft.Data.Sqlite;

namespace GestaoComercial.Tests;

public abstract class BaseTeste : IDisposable
{
    protected readonly string Pasta = Path.Combine(Path.GetTempPath(), "gc-test-" + Guid.NewGuid().ToString("N"));
    protected readonly SqliteConnection Conn;
    protected readonly Servico S;

    protected BaseTeste()
    {
        Directory.CreateDirectory(Pasta);
        Conn = Banco.Conectar(Path.Combine(Pasta, "t.db"));
        Banco.Migrar(Conn);
        S = new Servico(Conn);
    }

    public void Dispose() { Conn.Dispose(); try { Directory.Delete(Pasta, true); } catch { } }

    protected long Produto(string nome = "Caneta", long estoque = 10, long custo = 100, long venda = 250, long? cat = null, string? codigo = null, long min = 0)
        => S.SalvarProduto(new ProdutoDados(nome, codigo, cat, null, custo, venda, min, estoque));
    protected long Fornecedor(string nome = "Forn Ltda") => S.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados(nome));
    protected long Estoque(long pid) => S.ObterProduto(pid).Estoque;
    protected static ItemVendaInput IV(long pid, long q, long p = 100) => new(pid, q, p);
    protected static ItemEntradaInput IE(long pid, long q, long c = 100) => new(pid, q, c);
}
