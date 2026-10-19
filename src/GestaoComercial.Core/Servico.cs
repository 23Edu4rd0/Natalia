using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;

namespace GestaoComercial.Core;

public enum ResultadoExclusao { Excluido, Inativado }

/// <summary>
/// Regras de negócio: cadastros, estoque, entradas, vendas e relatórios.
/// Todo valor monetário é inteiro em centavos. Toda operação que mexe em estoque roda dentro de uma
/// transação: ou grava tudo, ou não grava nada.
/// </summary>
public partial class Servico
{
    public static readonly string[] FormasPagamento =
        { "Dinheiro", "Pix", "Cartão de débito", "Cartão de crédito", "Fiado", "Transferência", "Outro" };

    readonly SqliteConnection conn;

    public Servico(SqliteConnection conexao) { conn = conexao; }

    // ------------------------------------------------------------------ infraestrutura SQL
    SqliteCommand Cmd(string sql, object?[] args)
    {
        var cmd = conn.CreateCommand();
        var sb = new StringBuilder(sql.Length + 16);
        int n = 0;
        foreach (var ch in sql)
        {
            if (ch == '?')
            {
                if (n >= args.Length) throw new InvalidOperationException("Parâmetros a menos que '?' no SQL.");
                var p = cmd.CreateParameter();
                p.ParameterName = "@p" + n;
                p.Value = args[n] ?? DBNull.Value;
                cmd.Parameters.Add(p);
                sb.Append("@p").Append(n++);
            }
            else sb.Append(ch);
        }
        if (n != args.Length) throw new InvalidOperationException("Parâmetros a mais que '?' no SQL.");
        cmd.CommandText = sb.ToString();
        return cmd;
    }

    int Exec(string sql, params object?[] a) { using var c = Cmd(sql, a); return c.ExecuteNonQuery(); }

    long Inserir(string sql, params object?[] a)
    {
        Exec(sql, a);
        return Escalar("SELECT last_insert_rowid()");
    }

    long Escalar(string sql, params object?[] a)
    {
        using var c = Cmd(sql, a);
        var r = c.ExecuteScalar();
        return r is null or DBNull ? 0 : Convert.ToInt64(r);
    }

    List<T> Todos<T>(string sql, Func<Rd, T> mapa, params object?[] a)
    {
        using var c = Cmd(sql, a);
        using var r = c.ExecuteReader();
        var rd = new Rd(r);
        var lista = new List<T>();
        while (r.Read()) lista.Add(mapa(rd));
        return lista;
    }

    T? Um<T>(string sql, Func<Rd, T> mapa, params object?[] a) where T : class
        => Todos(sql, mapa, a).FirstOrDefault();

    /// <summary>Como <see cref="Um{T}"/>, para tipos de valor (tuplas).</summary>
    T? UmV<T>(string sql, Func<Rd, T> mapa, params object?[] a) where T : struct
    {
        var l = Todos(sql, mapa, a);
        return l.Count > 0 ? l[0] : null;
    }

    /// <summary>Transação atômica: BEGIN IMMEDIATE ... COMMIT (ou ROLLBACK se algo falhar).</summary>
    T Tx<T>(Func<T> corpo)
    {
        Exec("BEGIN IMMEDIATE");
        try
        {
            var r = corpo();
            Exec("COMMIT");
            return r;
        }
        catch
        {
            try { Exec("ROLLBACK"); } catch { /* já encerrada */ }
            throw;
        }
    }

    void Tx(Action corpo) => Tx<object?>(() => { corpo(); return null; });

    static bool ViolacaoDeRestricao(SqliteException e) => e.SqliteErrorCode == 19; // SQLITE_CONSTRAINT

    // ------------------------------------------------------------------ validações
    static string Maiuscula(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    static string Texto(string? valor, string campo, bool obrigatorio = false, int max = 120)
    {
        var v = Util.LimparTexto(valor);
        if (obrigatorio && v.Length == 0) throw new ValidacaoException($"Informe {campo}.");
        if (v.Length > max) throw new ValidacaoException($"{Maiuscula(campo)} deve ter no máximo {max} caracteres.");
        return v;
    }

    static long Inteiro(long v, string campo, long min = 0, long max = Util.LimiteQuantidade)
    {
        if (v < min) throw new ValidacaoException($"{Maiuscula(campo)} deve ser no mínimo {min}.");
        if (v > max) throw new ValidacaoException($"{Maiuscula(campo)} deve ser no máximo {max}.");
        return v;
    }

    static long Centavos(long v, string campo) => Inteiro(v, campo, 0, Util.LimiteCentavos);

    // ------------------------------------------------------------------ empresa
    public Empresa ObterEmpresa()
    {
        var d = Todos("SELECT chave, valor FROM config", r => (r.S("chave"), r.S("valor"))).ToDictionary(x => x.Item1, x => x.Item2);
        return EmpresaDeDicionario(d);
    }

    internal static Empresa EmpresaDeDicionario(IDictionary<string, string> d)
    {
        var p = Empresa.Padrao;
        string G(string k, string def) => d.TryGetValue(k, out var v) ? v : def;
        return new Empresa(G("empresa_nome", p.Nome), G("empresa_documento", p.Documento), G("empresa_endereco", p.Endereco),
                           G("empresa_telefone", p.Telefone), G("empresa_rodape", p.Rodape));
    }

    internal static Dictionary<string, string> EmpresaParaDicionario(Empresa e) => new()
    {
        ["empresa_nome"] = e.Nome, ["empresa_documento"] = e.Documento, ["empresa_endereco"] = e.Endereco,
        ["empresa_telefone"] = e.Telefone, ["empresa_rodape"] = e.Rodape,
    };

    public void SalvarEmpresa(Empresa e)
    {
        var novos = EmpresaParaDicionario(new Empresa(
            Texto(e.Nome, "o nome da empresa", true, 120), Util.ValidaDocumento(e.Documento),
            Texto(e.Endereco, "o endereço", false, 200), Texto(e.Telefone, "o telefone", false, 40),
            Texto(e.Rodape, "a mensagem de rodapé", false, 200)));
        Tx(() =>
        {
            foreach (var (k, v) in novos)
                Exec("INSERT INTO config (chave, valor) VALUES (?, ?) ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor", k, v);
        });
    }

    // ------------------------------------------------------------------ categorias (tipos de produto)
    public List<Categoria> ListarCategorias()
    {
        var l = Todos("SELECT c.id, c.nome, COUNT(p.id) AS produtos FROM categorias c LEFT JOIN produtos p ON p.categoria_id = c.id GROUP BY c.id",
                      r => new Categoria(r.L("id"), r.S("nome"), r.L("produtos")));
        return Util.OrdenarPorNome(l, x => x.Nome);
    }

    public long SalvarCategoria(string? nome, long? id = null)
    {
        var n = Texto(nome, "o nome do tipo de produto", true, 60);
        if (Escalar("SELECT COUNT(*) FROM categorias WHERE nome = ? AND id IS NOT ?", n, id) > 0)
            throw new ConflitoException($"Já existe um tipo de produto chamado '{n}'.");
        return Tx(() =>
        {
            if (id is null) return Inserir("INSERT INTO categorias (nome) VALUES (?)", n);
            if (Exec("UPDATE categorias SET nome = ? WHERE id = ?", n, id) == 0) throw new NaoEncontradoException("Tipo de produto não encontrado.");
            return id.Value;
        });
    }

    public void ExcluirCategoria(long id)
    {
        var usados = Escalar("SELECT COUNT(*) FROM produtos WHERE categoria_id = ?", id);
        if (usados > 0)
            throw new ConflitoException($"Este tipo está sendo usado por {usados} produto(s). Mude a categoria desses produtos antes de excluir.");
        Tx(() => Exec("DELETE FROM categorias WHERE id = ?", id));
    }

    // ------------------------------------------------------------------ clientes / fornecedores
    static string Tabela(TipoPessoa t) => t == TipoPessoa.Cliente ? "clientes" : "fornecedores";
    static string Rotulo(TipoPessoa t) => t == TipoPessoa.Cliente ? "cliente" : "fornecedor";
    static Pessoa MapPessoa(Rd r) =>
        new(r.L("id"), r.S("nome"), r.S("documento"), r.S("telefone"), r.S("email"), r.S("endereco"), r.L("ativo") == 1);

    public List<Pessoa> ListarPessoas(TipoPessoa tipo, string busca = "", bool incluirInativos = false)
    {
        var sql = $"SELECT * FROM {Tabela(tipo)} WHERE 1=1";
        var p = new List<object?>();
        if (!incluirInativos) sql += " AND ativo = 1";
        if (!string.IsNullOrWhiteSpace(busca))
        {
            sql += " AND (nome LIKE ? ESCAPE '\\' OR documento LIKE ? ESCAPE '\\' OR telefone LIKE ? ESCAPE '\\' OR email LIKE ? ESCAPE '\\')";
            var dig = Util.SomenteDigitos(busca);
            p.AddRange(new object?[] { Util.LikePadrao(busca), Util.LikePadrao(dig.Length > 0 ? dig : busca), Util.LikePadrao(busca), Util.LikePadrao(busca) });
        }
        return Util.OrdenarPorNome(Todos(sql, MapPessoa, p.ToArray()), x => x.Nome);
    }

    public Pessoa ObterPessoa(TipoPessoa tipo, long id) =>
        Um($"SELECT * FROM {Tabela(tipo)} WHERE id = ?", MapPessoa, id)
        ?? throw new NaoEncontradoException($"{Maiuscula(Rotulo(tipo))} não encontrado(a).");

    public long SalvarPessoa(TipoPessoa tipo, PessoaDados d, long? id = null)
    {
        var t = Tabela(tipo);
        var nome = Texto(d.Nome, "o nome", true, 120);
        var doc = Util.ValidaDocumento(d.Documento);
        var tel = Texto(d.Telefone, "o telefone", false, 40);
        var email = Texto(d.Email, "o e-mail", false, 120);
        if (email.Length > 0 && !Util.ValidaEmail(email)) throw new ValidacaoException($"E-mail inválido: '{email}'.");
        var end = Texto(d.Endereco, "o endereço", false, 200);
        if (doc.Length > 0)
        {
            var dup = Um($"SELECT nome FROM {t} WHERE documento = ? AND id IS NOT ?", r => r.S("nome"), doc, id);
            if (dup != null) throw new ConflitoException($"Já existe um {Rotulo(tipo)} com este CPF/CNPJ: {dup}.");
        }
        return Tx(() =>
        {
            if (id is null)
                return Inserir($"INSERT INTO {t} (nome, documento, telefone, email, endereco, ativo) VALUES (?,?,?,?,?,?)",
                               nome, doc, tel, email, end, d.Ativo ? 1 : 0);
            if (Exec($"UPDATE {t} SET nome=?, documento=?, telefone=?, email=?, endereco=?, ativo=? WHERE id=?",
                     nome, doc, tel, email, end, d.Ativo ? 1 : 0, id) == 0)
                throw new NaoEncontradoException($"{Maiuscula(Rotulo(tipo))} não encontrado(a).");
            return id.Value;
        });
    }

    /// <summary>Exclui; se já tiver histórico, apenas inativa.</summary>
    public ResultadoExclusao ExcluirPessoa(TipoPessoa tipo, long id)
    {
        var t = Tabela(tipo);
        ObterPessoa(tipo, id);
        try { Tx(() => Exec($"DELETE FROM {t} WHERE id = ?", id)); return ResultadoExclusao.Excluido; }
        catch (SqliteException e) when (ViolacaoDeRestricao(e))
        {
            Tx(() => Exec($"UPDATE {t} SET ativo = 0 WHERE id = ?", id));
            return ResultadoExclusao.Inativado;
        }
    }

    // ------------------------------------------------------------------ backup
    public string FazerBackup(string destino)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destino))!);
        var tmp = destino + ".tmp";
        if (File.Exists(tmp)) File.Delete(tmp);
        using (var saida = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = tmp, Pooling = false }.ToString()))
        {
            saida.Open();
            conn.BackupDatabase(saida);
        }
        File.Move(tmp, destino, true);
        return destino;
    }

    /// <summary>Um backup por dia, mantendo os mais recentes. Retorna o arquivo criado (null se já existia).</summary>
    public string? BackupAutomatico(string pasta, int manter = 14)
    {
        Directory.CreateDirectory(pasta);
        var alvo = Path.Combine(pasta, $"gestao-{DateTime.Today:yyyy-MM-dd}.db");
        string? criado = null;
        if (!File.Exists(alvo)) criado = FazerBackup(alvo);
        var antigos = Directory.GetFiles(pasta, "gestao-????-??-??.db").OrderBy(x => x, StringComparer.Ordinal).ToList();
        foreach (var velho in antigos.Take(Math.Max(0, antigos.Count - manter)))
        {
            try { File.Delete(velho); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return criado;
    }
}

/// <summary>
/// Leitura por nome de coluna com os índices guardados uma vez por consulta
/// (o GetOrdinal do SQLite para .NET percorre as colunas chamando código nativo a cada célula).
/// </summary>
sealed class Rd
{
    readonly SqliteDataReader r;
    readonly Dictionary<string, int> idx;

    public Rd(SqliteDataReader r)
    {
        this.r = r;
        idx = new Dictionary<string, int>(r.FieldCount, StringComparer.Ordinal);
        for (int i = 0; i < r.FieldCount; i++) idx[r.GetName(i)] = i;
    }

    public string S(string col) { var i = idx[col]; return r.IsDBNull(i) ? "" : r.GetString(i); }
    public string? SN(string col) { var i = idx[col]; return r.IsDBNull(i) ? null : r.GetString(i); }
    public long L(string col) { var i = idx[col]; return r.IsDBNull(i) ? 0 : r.GetInt64(i); }
    public long? LN(string col) { var i = idx[col]; return r.IsDBNull(i) ? null : r.GetInt64(i); }
}
