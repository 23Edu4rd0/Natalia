using System.Text;
using Microsoft.Data.Sqlite;

namespace GestaoComercial.Core;

public record ResultadoImportacao(int Criados, int Atualizados, int Ignorados, List<string> Erros)
{
    public int Total => Criados + Atualizados;
}

/// <summary>Importação de produtos a partir de planilha CSV (Excel: Salvar como "CSV").</summary>
public partial class Servico
{
    public const string ModeloCsvProdutos =
        "Nome;Código;Tipo;Unidade;Custo;Preço de venda;Estoque;Estoque mínimo\r\n" +
        "Camiseta básica branca;7891000100103;Roupas;un;25,00;49,90;30;5\r\n" +
        "Garrafa de água 500ml;;Bebidas;un;1,20;3,50;100;20\r\n";

    static readonly Dictionary<string, string> Apelidos = new()
    {
        ["nome"] = "nome", ["produto"] = "nome", ["descricao"] = "nome", ["item"] = "nome",
        ["codigo"] = "codigo", ["cod"] = "codigo", ["codigodebarras"] = "codigo", ["codbarras"] = "codigo", ["barras"] = "codigo", ["ean"] = "codigo", ["sku"] = "codigo", ["referencia"] = "codigo",
        ["tipo"] = "tipo", ["categoria"] = "tipo", ["grupo"] = "tipo", ["departamento"] = "tipo",
        ["unidade"] = "unidade", ["un"] = "unidade", ["und"] = "unidade",
        ["custo"] = "custo", ["precocusto"] = "custo", ["precodecusto"] = "custo", ["valorcusto"] = "custo", ["compra"] = "custo",
        ["venda"] = "venda", ["preco"] = "venda", ["precovenda"] = "venda", ["precodevenda"] = "venda", ["valor"] = "venda", ["valorvenda"] = "venda",
        ["estoque"] = "estoque", ["quantidade"] = "estoque", ["qtd"] = "estoque", ["saldo"] = "estoque", ["qtde"] = "estoque",
        ["minimo"] = "minimo", ["estoqueminimo"] = "minimo", ["min"] = "minimo",
    };

    static string Chave(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in Util.ChaveOrdenacao(s)) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>Lê o arquivo aceitando UTF-8 (com ou sem BOM) e o ANSI do Excel em português (Windows-1252).</summary>
    public static string LerTextoPlanilha(string caminho)
    {
        var bytes = File.ReadAllBytes(caminho);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        try { return new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    /// <summary>Divide o CSV em linhas de campos (aspas, delimitador ; , ou tab, quebras de linha dentro de aspas).</summary>
    public static List<List<string>> LerCsv(string texto)
    {
        var primeira = texto.Split('\n', 2)[0];
        char delim = new[] { ';', ',', '\t' }.OrderByDescending(d => primeira.Count(c => c == d)).First();
        var linhas = new List<List<string>>();
        var campo = new StringBuilder(); var atual = new List<string>(); bool aspas = false;
        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            if (aspas)
            {
                if (c == '"') { if (i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; } else aspas = false; }
                else campo.Append(c);
            }
            else if (c == '"') aspas = true;
            else if (c == delim) { atual.Add(campo.ToString()); campo.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                atual.Add(campo.ToString()); campo.Clear();
                linhas.Add(atual);   // linhas em branco ficam: o número da linha nos erros bate com o da planilha
                atual = new List<string>();
            }
            else campo.Append(c);
        }
        atual.Add(campo.ToString());
        if (atual.Any(x => x.Trim().Length > 0)) linhas.Add(atual);
        return linhas;
    }

    /// <summary>
    /// Cria ou atualiza produtos a partir da planilha. Produtos com o mesmo código são atualizados.
    /// Linhas com problema são puladas e listadas; as demais são gravadas juntas (rápido, mesmo com milhares de linhas).
    /// </summary>
    public ResultadoImportacao ImportarProdutosCsv(string caminho)
    {
        var linhas = LerCsv(LerTextoPlanilha(caminho));
        static bool Vazia(List<string> l) => l.All(x => x.Trim().Length == 0);
        var cab = linhas.FindIndex(l => !Vazia(l));
        if (cab < 0) throw new ValidacaoException("A planilha está vazia.");
        var colunas = new Dictionary<string, int>();
        for (int i = 0; i < linhas[cab].Count; i++)
            if (Apelidos.TryGetValue(Chave(linhas[cab][i]), out var campo) && !colunas.ContainsKey(campo)) colunas[campo] = i;
        if (!colunas.ContainsKey("nome"))
            throw new ValidacaoException("Não encontrei a coluna com o nome dos produtos. A primeira linha da planilha precisa ter os títulos (ex.: Nome, Preço de venda, Estoque). Use o modelo para conferir.");

        string Cel(List<string> l, string k) => colunas.TryGetValue(k, out var i) && i < l.Count ? l[i].Trim() : "";
        var erros = new List<string>(); int criados = 0, atualizados = 0, ignorados = 0;

        return Tx(() =>
        {
            var tipos = ListarCategorias().ToDictionary(c => Util.ChaveOrdenacao(c.Nome), c => c.Id);
            for (int n = cab + 1; n < linhas.Count; n++)
            {
                var l = linhas[n]; int numero = n + 1;
                if (Vazia(l)) continue;
                try
                {
                    var nome = Cel(l, "nome");
                    if (nome.Length == 0) { ignorados++; erros.Add($"Linha {numero}: sem nome, ignorada."); continue; }
                    long Din(string k, string rot) { var t = Cel(l, k); return t.Length == 0 ? 0 : Util.ParseDinheiro(t, rot); }
                    long Int(string k, string rot) { var t = Cel(l, k); return t.Length == 0 ? 0 : Util.ParseInteiro(t.Replace(".", ""), rot); }
                    // valida os números ANTES de criar o tipo: linha rejeitada não deixa tipo "órfão" para trás
                    long vCusto = Din("custo", "o custo"), vVenda = Din("venda", "o preço de venda"), vMin = Int("minimo", "o estoque mínimo"), vEstoque = Int("estoque", "o estoque");
                    long? tipoId = null;
                    var tipo = Cel(l, "tipo");
                    if (tipo.Length > 0)
                    {
                        var chaveTipo = Util.ChaveOrdenacao(Util.LimparTexto(tipo));
                        if (!tipos.TryGetValue(chaveTipo, out var tid)) tipos[chaveTipo] = tid = Inserir("INSERT INTO categorias (nome) VALUES (?)", Texto(tipo, "o tipo", true, 60));
                        tipoId = tid;
                    }
                    var codigo = Cel(l, "codigo");
                    long? existente = codigo.Length > 0 ? UmV("SELECT id FROM produtos WHERE codigo = ?", r => r.L("id"), codigo) : null;
                    var temEstoque = colunas.ContainsKey("estoque");
                    var estoque = vEstoque;
                    var dados = new ProdutoDados(nome, codigo, tipoId, Cel(l, "unidade"), vCusto, vVenda, vMin, estoque, true);
                    if (existente is long eid)
                    {
                        // ao atualizar, coluna ausente ou célula vazia mantém o que já está cadastrado
                        var ant = ObterProduto(eid);
                        dados = dados with
                        {
                            CategoriaId = tipoId ?? ant.CategoriaId,
                            Unidade = Cel(l, "unidade").Length > 0 ? Cel(l, "unidade") : ant.Unidade,
                            PrecoCusto = Cel(l, "custo").Length > 0 ? dados.PrecoCusto : ant.PrecoCusto,
                            PrecoVenda = Cel(l, "venda").Length > 0 ? dados.PrecoVenda : ant.PrecoVenda,
                            EstoqueMin = Cel(l, "minimo").Length > 0 ? dados.EstoqueMin : ant.EstoqueMin,
                        };
                        GravarProduto(dados, eid);
                        var atual = Escalar("SELECT estoque FROM produtos WHERE id = ?", eid);
                        if (temEstoque && Cel(l, "estoque").Length > 0 && atual != estoque)
                        {
                            Exec("UPDATE produtos SET estoque = ? WHERE id = ?", estoque, eid);
                            Movimentar(eid, "AJUSTE", estoque - atual, "Importação", "Estoque atualizado pela planilha");
                        }
                        atualizados++;
                    }
                    else { GravarProduto(dados, null); criados++; }
                }
                catch (AppException ex) { ignorados++; erros.Add($"Linha {numero}: {ex.Message}"); }
            }
            return new ResultadoImportacao(criados, atualizados, ignorados, erros);
        });
    }

    // ================================================================== restaurar backup
    /// <summary>
    /// Substitui os dados atuais pelo conteúdo de um arquivo de backup. Antes, guarda uma cópia de segurança dos dados atuais.
    /// Retorna o caminho da cópia de segurança.
    /// </summary>
    public string RestaurarBackup(string arquivo, string pastaSeguranca)
    {
        if (!File.Exists(arquivo)) throw new ValidacaoException("Arquivo de backup não encontrado.");
        var cs = new SqliteConnectionStringBuilder { DataSource = arquivo, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var origem = new SqliteConnection(cs);
        try
        {
            origem.Open();
            using var c = origem.CreateCommand();
            c.CommandText = "PRAGMA quick_check";
            if ((c.ExecuteScalar() as string) != "ok") throw new ValidacaoException("Este arquivo de backup está danificado e não pode ser usado.");
            c.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('produtos','vendas','clientes')";
            if (Convert.ToInt64(c.ExecuteScalar()) < 3) throw new ValidacaoException("Este arquivo não parece ser um backup do Gestão Comercial.");
            c.CommandText = "PRAGMA user_version";
            if (Convert.ToInt64(c.ExecuteScalar()) > Banco.VersaoEsquema) throw new ValidacaoException("Este backup foi feito por uma versão mais nova do programa.");
        }
        catch (SqliteException) { throw new ValidacaoException("Este arquivo não é um backup válido do Gestão Comercial."); }

        var seguranca = FazerBackup(Path.Combine(pastaSeguranca, $"antes-de-restaurar-{DateTime.Now:yyyy-MM-dd_HHmmss}.db"));
        origem.BackupDatabase(conn);
        Banco.Migrar(conn);       // backup de versão antiga sobe para o esquema atual
        Mudou();
        return seguranca;
    }
}
