using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace GestaoComercial.Core;

public partial class Servico
{
    // ================================================================== entradas de mercadorias
    /// <summary>Registra a entrada de mercadorias de um fornecedor e soma ao estoque.</summary>
    /// <param name="data">"aaaa-mm-dd" (opcional; padrão = agora).</param>
    public long RegistrarEntrada(long? fornecedorId, IList<ItemEntradaInput> itens, string? numeroDoc = "", string? obs = "",
                                 string? data = null, bool atualizarCusto = true)
    {
        if (fornecedorId is null or 0) throw new ValidacaoException("Selecione o fornecedor da entrada.");
        if (itens == null || itens.Count == 0) throw new ValidacaoException("Adicione ao menos um produto à entrada.");
        var doc = Texto(numeroDoc, "o número do documento", false, 40);
        var observacao = Texto(obs, "a observação", false, 300);
        string dataHora;
        if (string.IsNullOrWhiteSpace(data)) dataHora = Util.AgoraIso();
        else
        {
            if (!DateTime.TryParseExact(data.Trim()[..Math.Min(10, data.Trim().Length)], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                throw new ValidacaoException("Data da entrada inválida.");
            if (d.Date > DateTime.Today) throw new ValidacaoException("A data da entrada não pode ser no futuro.");
            dataHora = d.Date == DateTime.Today ? Util.AgoraIso() : $"{d:yyyy-MM-dd} 12:00:00";
        }

        var linhas = itens.Select(i => (i.ProdutoId, Qtd: Inteiro(i.Quantidade, "a quantidade", 1), Custo: Centavos(i.CustoUnit, "o custo unitário"))).ToList();

        return Tx(() =>
        {
            var forn = UmV("SELECT nome, ativo FROM fornecedores WHERE id = ?", r => (Nome: r.S("nome"), Ativo: r.L("ativo") == 1), fornecedorId)
                       ?? throw new NaoEncontradoException("Fornecedor não encontrado.");
            if (!forn.Ativo) throw new ValidacaoException($"O fornecedor '{forn.Nome}' está inativo.");
            foreach (var l in linhas) ProdutoAtivo(l.ProdutoId);
            var total = linhas.Sum(l => l.Qtd * l.Custo);
            var eid = Inserir("INSERT INTO entradas (data, fornecedor_id, numero_doc, obs, total) VALUES (?,?,?,?,?)",
                              dataHora, fornecedorId, doc, observacao, total);
            foreach (var l in linhas)
            {
                Exec("INSERT INTO entrada_itens (entrada_id, produto_id, quantidade, custo_unit) VALUES (?,?,?,?)", eid, l.ProdutoId, l.Qtd, l.Custo);
                Exec("UPDATE produtos SET estoque = estoque + ? WHERE id = ?", l.Qtd, l.ProdutoId);
                if (atualizarCusto) Exec("UPDATE produtos SET preco_custo = ? WHERE id = ?", l.Custo, l.ProdutoId);
                Movimentar(l.ProdutoId, "ENTRADA", l.Qtd, $"Entrada {eid:000000}",
                           $"Fornecedor: {forn.Nome}" + (doc.Length > 0 ? $" | Doc: {doc}" : ""));
            }
            return eid;
        });
    }

    const string EntradaSelect =
        "SELECT e.id, e.data, e.fornecedor_id, f.nome AS fornecedor, e.numero_doc, e.obs, e.total, e.status, e.motivo_cancelamento " +
        "FROM entradas e JOIN fornecedores f ON f.id = e.fornecedor_id";

    static Entrada MapEntrada(Rd r) =>
        new(r.L("id"), r.S("data"), r.L("fornecedor_id"), r.S("fornecedor"), r.S("numero_doc"), r.S("obs"), r.L("total"),
            r.S("status"), r.S("motivo_cancelamento"), new List<EntradaItem>());

    public List<Entrada> ListarEntradas(string? inicio = null, string? fim = null, int limite = 500)
    {
        var sql = EntradaSelect + " WHERE 1=1";
        var p = new List<object?>();
        if (!string.IsNullOrEmpty(inicio)) { sql += " AND substr(e.data, 1, 10) >= ?"; p.Add(inicio); }
        if (!string.IsNullOrEmpty(fim)) { sql += " AND substr(e.data, 1, 10) <= ?"; p.Add(fim); }
        sql += " ORDER BY e.id DESC LIMIT ?"; p.Add(limite);
        return Todos(sql, MapEntrada, p.ToArray());
    }

    public Entrada ObterEntrada(long id)
    {
        var e = Um(EntradaSelect + " WHERE e.id = ?", MapEntrada, id) ?? throw new NaoEncontradoException("Entrada não encontrada.");
        e.Itens.AddRange(Todos(
            "SELECT i.id, i.produto_id, p.codigo, p.nome, p.unidade, i.quantidade, i.custo_unit FROM entrada_itens i " +
            "JOIN produtos p ON p.id = i.produto_id WHERE i.entrada_id = ? ORDER BY i.id",
            r => new EntradaItem(r.L("id"), r.L("produto_id"), r.S("codigo"), r.S("nome"), r.S("unidade"), r.L("quantidade"), r.L("custo_unit")), id));
        return e;
    }

    /// <summary>Desfaz uma entrada (só é possível se as mercadorias ainda estiverem no estoque).</summary>
    public void CancelarEntrada(long id, string? motivo)
    {
        var m = Texto(motivo, "o motivo do cancelamento", true, 200);
        Tx(() =>
        {
            var st = Um("SELECT status FROM entradas WHERE id = ?", r => r.S("status"), id) ?? throw new NaoEncontradoException("Entrada não encontrada.");
            if (st == "CANCELADA") throw new ConflitoException("Esta entrada já foi cancelada.");
            var itens = Todos("SELECT i.produto_id, i.quantidade, p.nome FROM entrada_itens i JOIN produtos p ON p.id = i.produto_id WHERE i.entrada_id = ?",
                              r => (Pid: r.L("produto_id"), Qtd: r.L("quantidade"), Nome: r.S("nome")), id);
            foreach (var it in itens)
            {
                if (Exec("UPDATE produtos SET estoque = estoque - ? WHERE id = ? AND estoque >= ?", it.Qtd, it.Pid, it.Qtd) == 0)
                {
                    var atual = Escalar("SELECT estoque FROM produtos WHERE id = ?", it.Pid);
                    throw new EstoqueException($"Não é possível cancelar: de '{it.Nome}' entraram {it.Qtd}, mas só há {atual} em estoque (parte já foi vendida ou ajustada).");
                }
                Movimentar(it.Pid, "CANCELAMENTO_ENTRADA", -it.Qtd, $"Entrada {id:000000}", m);
            }
            Exec("UPDATE entradas SET status='CANCELADA', motivo_cancelamento=? WHERE id=?", m, id);
        });
    }

    // ================================================================== vendas / notas
    /// <summary>Registra a venda, baixa o estoque e devolve o número da nota.</summary>
    public long RegistrarVenda(IList<ItemVendaInput> itens, long? clienteId = null, long desconto = 0,
                               string forma = "Dinheiro", string? obs = "")
    {
        if (!FormasPagamento.Contains(forma)) throw new ValidacaoException("Forma de pagamento inválida.");
        if (itens == null || itens.Count == 0) throw new ValidacaoException("Adicione ao menos um item à venda.");
        if (forma == Fiado && clienteId is null or 0)
            throw new ValidacaoException("Venda no fiado precisa de um cliente. Escolha o cliente (ou cadastre um novo).");
        var desc = Centavos(desconto, "o desconto");
        var observacao = Texto(obs, "a observação", false, 300);
        var linhas = itens.Select(i => (i.ProdutoId, Qtd: Inteiro(i.Quantidade, "a quantidade", 1), Preco: Centavos(i.PrecoUnit, "o preço unitário"))).ToList();

        return Tx(() =>
        {
            long? cid = null; string cnome = "Consumidor final", cdoc = "";
            if (clienteId is > 0)
            {
                var c = UmV("SELECT id, nome, documento, ativo FROM clientes WHERE id = ?", r => (Id: r.L("id"), Nome: r.S("nome"), Doc: r.S("documento"), Ativo: r.L("ativo") == 1), clienteId)
                        ?? throw new NaoEncontradoException("Cliente não encontrado.");
                if (!c.Ativo) throw new ValidacaoException($"O cliente '{c.Nome}' está inativo.");
                cid = c.Id; cnome = c.Nome; cdoc = c.Doc;
            }
            var produtos = linhas.Select(l => ProdutoAtivo(l.ProdutoId)).ToList();
            var subtotal = linhas.Sum(l => l.Qtd * l.Preco);
            if (desc > subtotal) throw new ValidacaoException("O desconto não pode ser maior que o subtotal da venda.");
            var total = subtotal - desc;
            var vid = Inserir(
                "INSERT INTO vendas (data, cliente_id, cliente_nome, cliente_doc, subtotal, desconto, total, forma_pagamento, obs, emitente_json) VALUES (?,?,?,?,?,?,?,?,?,?)",
                Util.AgoraIso(), cid, cnome, cdoc, subtotal, desc, total, forma, observacao,
                JsonSerializer.Serialize(EmpresaParaDicionario(ObterEmpresa())));
            for (int i = 0; i < linhas.Count; i++)
            {
                var (pid, qtd, preco) = linhas[i];
                var prod = produtos[i];
                if (Exec("UPDATE produtos SET estoque = estoque - ? WHERE id = ? AND estoque >= ?", qtd, pid, qtd) == 0)
                {
                    var disp = Escalar("SELECT estoque FROM produtos WHERE id = ?", pid);
                    throw new EstoqueException($"Estoque insuficiente para '{prod.Nome}': disponível {disp}, pedido {qtd}.");
                }
                Exec("INSERT INTO venda_itens (venda_id, produto_id, codigo, descricao, unidade, quantidade, preco_unit, custo_unit) VALUES (?,?,?,?,?,?,?,?)",
                     vid, pid, prod.Codigo, prod.Nome, prod.Unidade, qtd, preco, prod.PrecoCusto);
                Movimentar(pid, "VENDA", -qtd, $"Nota {vid:000000}", $"Cliente: {cnome}");
            }
            return vid;
        });
    }

    /// <summary>Cancela a venda e devolve os itens ao estoque.</summary>
    public void CancelarVenda(long id, string? motivo)
    {
        var m = Texto(motivo, "o motivo do cancelamento", true, 200);
        Tx(() =>
        {
            var st = Um("SELECT status FROM vendas WHERE id = ?", r => r.S("status"), id) ?? throw new NaoEncontradoException("Venda não encontrada.");
            if (st == "CANCELADA") throw new ConflitoException("Esta venda já foi cancelada.");
            var nf = UmV("SELECT modelo, numero FROM notas_fiscais WHERE venda_id = ? AND status = 'AUTORIZADA' LIMIT 1", r => (Mod: r.L("modelo"), Num: r.L("numero")), id);
            if (nf is { } x)
                throw new ValidacaoException($"Esta venda tem a {(x.Mod == 65 ? "NFC-e" : "NF-e")} nº {x.Num} autorizada. Cancele a nota fiscal primeiro (página Notas fiscais).");
            foreach (var it in Todos("SELECT produto_id, quantidade FROM venda_itens WHERE venda_id = ?", r => (Pid: r.L("produto_id"), Qtd: r.L("quantidade")), id))
            {
                Exec("UPDATE produtos SET estoque = estoque + ? WHERE id = ?", it.Qtd, it.Pid);
                Movimentar(it.Pid, "CANCELAMENTO_VENDA", it.Qtd, $"Nota {id:000000}", m);
            }
            Exec("UPDATE vendas SET status='CANCELADA', cancelada_em=?, motivo_cancelamento=? WHERE id=?", Util.AgoraIso(), m, id);
        });
    }

    const string VendaSelect =
        "SELECT id, data, cliente_id, cliente_nome, cliente_doc, subtotal, desconto, total, forma_pagamento, obs, status, cancelada_em, motivo_cancelamento, emitente_json FROM vendas";

    static Venda MapVenda(Rd r)
    {
        Empresa emit;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, string>>(r.S("emitente_json")) ?? new();
            emit = EmpresaDeDicionario(d);
        }
        catch (JsonException) { emit = Empresa.Padrao; }
        return new Venda(r.L("id"), r.S("data"), r.LN("cliente_id"), r.S("cliente_nome"), r.S("cliente_doc"), r.L("subtotal"),
                         r.L("desconto"), r.L("total"), r.S("forma_pagamento"), r.S("obs"), r.S("status"), r.SN("cancelada_em"),
                         r.S("motivo_cancelamento"), emit, new List<VendaItem>());
    }

    public List<Venda> ListarVendas(string? inicio = null, string? fim = null, string? status = null, string? busca = "", int limite = 1000, long? clienteId = null)
    {
        var sql = VendaSelect + " WHERE 1=1";
        var p = new List<object?>();
        if (clienteId != null) { sql += " AND cliente_id = ?"; p.Add(clienteId); }
        if (!string.IsNullOrEmpty(inicio)) { sql += " AND substr(data, 1, 10) >= ?"; p.Add(inicio); }
        if (!string.IsNullOrEmpty(fim)) { sql += " AND substr(data, 1, 10) <= ?"; p.Add(fim); }
        if (!string.IsNullOrEmpty(status)) { sql += " AND status = ?"; p.Add(status); }
        var b = (busca ?? "").Trim().TrimStart('#');
        if (b.Length > 0)
        {
            if (b.All(char.IsAsciiDigit) && long.TryParse(b, out var num))
            { sql += " AND (id = ? OR cliente_nome LIKE ? ESCAPE '\\')"; p.Add(num); p.Add(Util.LikePadrao(b)); }
            else { sql += " AND cliente_nome LIKE ? ESCAPE '\\'"; p.Add(Util.LikePadrao(b)); }
        }
        sql += " ORDER BY id DESC LIMIT ?"; p.Add(limite);
        return Todos(sql, MapVenda, p.ToArray());
    }

    public Venda ObterVenda(long id)
    {
        var v = Um(VendaSelect + " WHERE id = ?", MapVenda, id) ?? throw new NaoEncontradoException("Nota/venda não encontrada.");
        v.Itens.AddRange(Todos("SELECT id, produto_id, codigo, descricao, unidade, quantidade, preco_unit, custo_unit FROM venda_itens WHERE venda_id = ? ORDER BY id",
                               r => new VendaItem(r.L("id"), r.L("produto_id"), r.S("codigo"), r.S("descricao"), r.S("unidade"), r.L("quantidade"), r.L("preco_unit"), r.L("custo_unit")), id));
        return v;
    }

    // ================================================================== relatórios
    public ResumoVendas ResumoVendasPeriodo(string inicio, string fim)
    {
        var r = UmV("SELECT COUNT(*) AS qtd, COALESCE(SUM(total),0) AS total, COALESCE(SUM(desconto),0) AS descontos FROM vendas " +
                   "WHERE status = 'CONCLUIDA' AND substr(data,1,10) BETWEEN ? AND ?", x => (Qtd: x.L("qtd"), Total: x.L("total"), Desc: x.L("descontos")), inicio, fim)!.Value;
        var custo = Escalar("SELECT COALESCE(SUM(i.quantidade * i.custo_unit),0) FROM venda_itens i JOIN vendas v ON v.id = i.venda_id " +
                            "WHERE v.status = 'CONCLUIDA' AND substr(v.data,1,10) BETWEEN ? AND ?", inicio, fim);
        var c = UmV("SELECT COUNT(*) AS qtd, COALESCE(SUM(total),0) AS total FROM vendas WHERE status = 'CANCELADA' AND substr(data,1,10) BETWEEN ? AND ?",
                   x => (Qtd: x.L("qtd"), Total: x.L("total")), inicio, fim)!.Value;
        return new ResumoVendas(r.Qtd, r.Total, r.Desc, custo, c.Qtd, c.Total);
    }

    public List<MaisVendido> MaisVendidos(string inicio, string fim, int limite = 10) =>
        Todos("SELECT i.codigo, i.descricao, SUM(i.quantidade) AS quantidade, SUM(i.quantidade * i.preco_unit) AS receita " +
              "FROM venda_itens i JOIN vendas v ON v.id = i.venda_id WHERE v.status = 'CONCLUIDA' AND substr(v.data,1,10) BETWEEN ? AND ? " +
              "GROUP BY i.produto_id ORDER BY quantidade DESC, receita DESC LIMIT ?",
              r => new MaisVendido(r.S("codigo"), r.S("descricao"), r.L("quantidade"), r.L("receita")), inicio, fim, limite);

    public List<PorPagamento> VendasPorPagamento(string inicio, string fim) =>
        Todos("SELECT forma_pagamento, COUNT(*) AS qtd, SUM(total) AS total FROM vendas WHERE status = 'CONCLUIDA' AND substr(data,1,10) BETWEEN ? AND ? " +
              "GROUP BY forma_pagamento ORDER BY total DESC",
              r => new PorPagamento(r.S("forma_pagamento"), r.L("qtd"), r.L("total")), inicio, fim);

    /// <summary>Total vendido em cada um dos últimos <paramref name="dias"/> dias (inclui hoje e dias sem venda).</summary>
    public List<VendaDia> VendasPorDia(int dias = 14)
    {
        dias = Math.Clamp(dias, 1, 366);
        var inicio = DateTime.Today.AddDays(-(dias - 1));
        var porDia = Todos("SELECT substr(data,1,10) AS dia, COUNT(*) AS qtd, SUM(total) AS total FROM vendas WHERE status = 'CONCLUIDA' AND substr(data,1,10) >= ? GROUP BY dia",
                           r => new VendaDia(r.S("dia"), r.L("qtd"), r.L("total")), inicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                     .ToDictionary(v => v.Data);
        return Enumerable.Range(0, dias).Select(i => inicio.AddDays(i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                         .Select(d => porDia.TryGetValue(d, out var v) ? v : new VendaDia(d, 0, 0)).ToList();
    }

    public Painel ObterPainel()
    {
        var hoje = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var mes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var est = Um("SELECT COUNT(*) AS produtos, COALESCE(SUM(estoque * preco_custo),0) AS vc, COALESCE(SUM(estoque * preco_venda),0) AS vv, " +
                     "COALESCE(SUM(CASE WHEN estoque <= estoque_min THEN 1 ELSE 0 END),0) AS baixos FROM produtos WHERE ativo = 1",
                     r => new EstoquePainel(r.L("produtos"), r.L("vc"), r.L("vv"), r.L("baixos")))!;
        return new Painel(ResumoVendasPeriodo(hoje, hoje), ResumoVendasPeriodo(mes, hoje), est,
                          ListarProdutos(apenasBaixo: true).Take(50).ToList(), ListarVendas(limite: 8));
    }
}
