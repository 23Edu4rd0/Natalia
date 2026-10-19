using Microsoft.Data.Sqlite;

namespace GestaoComercial.Core;

public partial class Servico
{
    const string ProdutoSelect =
        "SELECT p.id, p.codigo, p.nome, p.categoria_id, COALESCE(c.nome, 'Sem categoria') AS categoria, p.unidade, " +
        "p.preco_custo, p.preco_venda, p.estoque, p.estoque_min, p.ativo " +
        "FROM produtos p LEFT JOIN categorias c ON c.id = p.categoria_id";

    static Produto MapProduto(Rd r) =>
        new(r.L("id"), r.S("codigo"), r.S("nome"), r.LN("categoria_id"), r.S("categoria"), r.S("unidade"),
            r.L("preco_custo"), r.L("preco_venda"), r.L("estoque"), r.L("estoque_min"), r.L("ativo") == 1);

    string ProximoCodigo()
    {
        var n = Escalar("SELECT COALESCE(MAX(id), 0) + 1 FROM produtos");
        while (Escalar("SELECT COUNT(*) FROM produtos WHERE codigo = ?", $"{n:00000}") > 0) n++;
        return $"{n:00000}";
    }

    /// <param name="categoriaId">null = todas; 0 = somente sem categoria.</param>
    public List<Produto> ListarProdutos(string busca = "", long? categoriaId = null, bool incluirInativos = false, bool apenasBaixo = false)
    {
        var sql = ProdutoSelect + " WHERE 1=1";
        var p = new List<object?>();
        if (!incluirInativos) sql += " AND p.ativo = 1";
        if (categoriaId == 0) sql += " AND p.categoria_id IS NULL";
        else if (categoriaId != null) { sql += " AND p.categoria_id = ?"; p.Add(categoriaId); }
        if (apenasBaixo) sql += " AND p.estoque <= p.estoque_min";
        if (!string.IsNullOrWhiteSpace(busca))
        {
            sql += " AND (p.nome LIKE ? ESCAPE '\\' OR p.codigo LIKE ? ESCAPE '\\')";
            p.Add(Util.LikePadrao(busca)); p.Add(Util.LikePadrao(busca));
        }
        return Util.OrdenarPorNome(Todos(sql, MapProduto, p.ToArray()), x => x.Nome);
    }

    public Produto ObterProduto(long id) =>
        Um(ProdutoSelect + " WHERE p.id = ?", MapProduto, id) ?? throw new NaoEncontradoException("Produto não encontrado.");

    /// <summary>Busca exata por código (ou código de barras) entre os produtos ativos; null se não achar.</summary>
    public Produto? ObterProdutoPorCodigo(string? codigo)
    {
        var c = (codigo ?? "").Trim();
        return c.Length == 0 ? null : Um(ProdutoSelect + " WHERE p.codigo = ? AND p.ativo = 1", MapProduto, c);
    }

    Produto ProdutoAtivo(long id)
    {
        var p = Um(ProdutoSelect + " WHERE p.id = ?", MapProduto, id) ?? throw new NaoEncontradoException("Um dos produtos não existe mais.");
        if (!p.Ativo) throw new ValidacaoException($"O produto '{p.Nome}' está inativo e não pode ser movimentado.");
        return p;
    }

    public long SalvarProduto(ProdutoDados d, long? id = null)
    {
        var nome = Texto(d.Nome, "o nome do produto", true, 120);
        var codigo = Texto(d.Codigo, "o código", false, 40);
        var unidade = Texto(d.Unidade, "a unidade", false, 8);
        if (unidade.Length == 0) unidade = "un";
        long? categoriaId = d.CategoriaId is > 0 ? d.CategoriaId : null;
        if (categoriaId != null && Escalar("SELECT COUNT(*) FROM categorias WHERE id = ?", categoriaId) == 0)
            throw new ValidacaoException("O tipo de produto selecionado não existe mais.");
        var custo = Centavos(d.PrecoCusto, "o preço de custo");
        var venda = Centavos(d.PrecoVenda, "o preço de venda");
        var minimo = Inteiro(d.EstoqueMin, "o estoque mínimo");
        var inicial = id is null ? Inteiro(d.EstoqueInicial, "o estoque inicial") : 0;

        if (codigo.Length > 0)
        {
            var dup = Um("SELECT nome FROM produtos WHERE codigo = ? AND id IS NOT ?", r => r.S("nome"), codigo, id);
            if (dup != null) throw new ConflitoException($"Já existe um produto com o código '{codigo}': {dup}.");
        }

        return Tx(() =>
        {
            if (id is null)
            {
                if (codigo.Length == 0) codigo = ProximoCodigo();
                var pid = Inserir("INSERT INTO produtos (codigo, nome, categoria_id, unidade, preco_custo, preco_venda, estoque, estoque_min, ativo) VALUES (?,?,?,?,?,?,?,?,?)",
                                  codigo, nome, categoriaId, unidade, custo, venda, inicial, minimo, d.Ativo ? 1 : 0);
                if (inicial > 0) Movimentar(pid, "ENTRADA", inicial, "Cadastro", "Estoque inicial");
                return pid;
            }
            if (Escalar("SELECT COUNT(*) FROM produtos WHERE id = ?", id) == 0) throw new NaoEncontradoException("Produto não encontrado.");
            if (codigo.Length == 0) codigo = Um("SELECT codigo FROM produtos WHERE id = ?", r => r.S("codigo"), id)!;
            Exec("UPDATE produtos SET codigo=?, nome=?, categoria_id=?, unidade=?, preco_custo=?, preco_venda=?, estoque_min=?, ativo=? WHERE id=?",
                 codigo, nome, categoriaId, unidade, custo, venda, minimo, d.Ativo ? 1 : 0, id);
            return id.Value;
        });
    }

    /// <summary>Exclui; se já houver movimentação, apenas inativa.</summary>
    public ResultadoExclusao ExcluirProduto(long id)
    {
        ObterProduto(id);
        try { Tx(() => Exec("DELETE FROM produtos WHERE id = ?", id)); return ResultadoExclusao.Excluido; }
        catch (SqliteException e) when (ViolacaoDeRestricao(e))
        {
            Tx(() => Exec("UPDATE produtos SET ativo = 0 WHERE id = ?", id));
            return ResultadoExclusao.Inativado;
        }
    }

    void Movimentar(long produtoId, string tipo, long quantidade, string referencia = "", string obs = "")
    {
        var saldo = Escalar("SELECT estoque FROM produtos WHERE id = ?", produtoId);
        Exec("INSERT INTO movimentos (data, produto_id, tipo, quantidade, saldo_apos, referencia, obs) VALUES (?,?,?,?,?,?,?)",
             Util.AgoraIso(), produtoId, tipo, quantidade, saldo, referencia, obs);
    }

    /// <summary>Define o estoque para uma contagem real (inventário). Retorna a diferença aplicada.</summary>
    public long AjustarEstoque(long produtoId, long novaQuantidade, string? motivo)
    {
        var nova = Inteiro(novaQuantidade, "a nova quantidade");
        var m = Texto(motivo, "o motivo do ajuste", true, 200);
        return Tx(() =>
        {
            var p = UmV("SELECT nome, estoque FROM produtos WHERE id = ?", r => (r.S("nome"), r.L("estoque")), produtoId);
            if (p is null) throw new NaoEncontradoException("Produto não encontrado.");
            var dif = nova - p.Value.Item2;
            if (dif == 0) throw new ValidacaoException($"O estoque de '{p.Value.Item1}' já é {nova}. Nada a ajustar.");
            Exec("UPDATE produtos SET estoque = ? WHERE id = ?", nova, produtoId);
            Movimentar(produtoId, "AJUSTE", dif, "Ajuste manual", m);
            return dif;
        });
    }

    // ------------------------------------------------------------------ estoque (consultas)
    public List<ResumoCategoria> ResumoPorCategoria()
    {
        var l = Todos(
            "SELECT COALESCE(c.nome, 'Sem categoria') AS categoria, COUNT(p.id) AS produtos, COALESCE(SUM(p.estoque),0) AS unidades, " +
            "COALESCE(SUM(p.estoque * p.preco_custo),0) AS valor_custo, COALESCE(SUM(p.estoque * p.preco_venda),0) AS valor_venda, " +
            "COALESCE(SUM(CASE WHEN p.estoque <= p.estoque_min THEN 1 ELSE 0 END),0) AS baixos " +
            "FROM produtos p LEFT JOIN categorias c ON c.id = p.categoria_id WHERE p.ativo = 1 GROUP BY c.id",
            r => new ResumoCategoria(r.S("categoria"), r.L("produtos"), r.L("unidades"), r.L("valor_custo"), r.L("valor_venda"), r.L("baixos")));
        return Util.OrdenarPorNome(l, x => x.Categoria);
    }

    public List<Movimento> ListarMovimentos(long? produtoId = null, int limite = 500) =>
        Todos("SELECT m.id, m.data, m.produto_id, p.codigo, p.nome, m.tipo, m.quantidade, m.saldo_apos, m.referencia, m.obs " +
              "FROM movimentos m JOIN produtos p ON p.id = m.produto_id" + (produtoId != null ? " WHERE m.produto_id = ?" : "") +
              " ORDER BY m.id DESC LIMIT ?",
              r => new Movimento(r.L("id"), r.S("data"), r.L("produto_id"), r.S("codigo"), r.S("nome"), r.S("tipo"),
                                 r.L("quantidade"), r.L("saldo_apos"), r.S("referencia"), r.S("obs")),
              produtoId != null ? new object?[] { produtoId, limite } : new object?[] { limite });
}
