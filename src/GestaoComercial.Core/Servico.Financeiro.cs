using System.Globalization;

namespace GestaoComercial.Core;

public partial class Servico
{
    public const string Fiado = "Fiado";

    /// <summary>Formas aceitas para receber um pagamento de fiado (tudo, menos o próprio fiado).</summary>
    public static IEnumerable<string> FormasRecebimento => FormasPagamento.Where(f => f != Fiado);

    // ================================================================== fiado / contas a receber
    const string SqlSaldo =
        "(SELECT COALESCE(SUM(v.total), 0) FROM vendas v WHERE v.cliente_id = c.id AND v.forma_pagamento = 'Fiado' AND v.status = 'CONCLUIDA') " +
        "- (SELECT COALESCE(SUM(r.valor), 0) FROM recebimentos r WHERE r.cliente_id = c.id AND r.status = 'ATIVO')";

    /// <summary>Quanto o cliente deve (compras fiado concluídas − pagamentos). Negativo = crédito do cliente.</summary>
    public long SaldoCliente(long clienteId) =>
        Escalar($"SELECT {SqlSaldo} FROM clientes c WHERE c.id = ?", clienteId);

    public List<Devedor> ListarDevedores()
    {
        var l = Todos(
            $"SELECT * FROM (SELECT c.id, c.nome, c.telefone, {SqlSaldo} AS saldo, " +
            "(SELECT MAX(v.data) FROM vendas v WHERE v.cliente_id = c.id AND v.forma_pagamento = 'Fiado' AND v.status = 'CONCLUIDA') AS ultima " +
            "FROM clientes c) WHERE saldo <> 0",
            r => new Devedor(r.L("id"), r.S("nome"), r.S("telefone"), r.L("saldo"), r.SN("ultima")));
        return l.OrderByDescending(d => d.Saldo).ThenBy(d => Util.ChaveOrdenacao(d.Nome), StringComparer.Ordinal).ToList();
    }

    public long TotalAReceber() => ListarDevedores().Where(d => d.Saldo > 0).Sum(d => d.Saldo);

    public long RegistrarRecebimento(long clienteId, long valor, string forma, string? obs = "")
    {
        if (!FormasRecebimento.Contains(forma)) throw new ValidacaoException("Escolha como o cliente pagou (dinheiro, Pix, cartão...).");
        var v = Centavos(valor, "o valor");
        if (v <= 0) throw new ValidacaoException("Informe um valor maior que zero.");
        var o = Texto(obs, "a observação", false, 200);
        return Tx(() =>
        {
            var nome = Um("SELECT nome FROM clientes WHERE id = ?", r => r.S("nome"), clienteId) ?? throw new NaoEncontradoException("Cliente não encontrado.");
            var saldo = SaldoCliente(clienteId);
            if (saldo <= 0) throw new ValidacaoException($"{nome} não tem nada a pagar.");
            if (v > saldo) throw new ValidacaoException($"O valor é maior que a dívida de {nome} ({Util.FmtDinheiro(saldo)}).");
            return Inserir("INSERT INTO recebimentos (data, cliente_id, valor, forma, obs) VALUES (?,?,?,?,?)", Util.AgoraIso(), clienteId, v, forma, o);
        });
    }

    public void EstornarRecebimento(long id, string? motivo)
    {
        var m = Texto(motivo, "o motivo", true, 200);
        Tx(() =>
        {
            var st = Um("SELECT status FROM recebimentos WHERE id = ?", r => r.S("status"), id) ?? throw new NaoEncontradoException("Pagamento não encontrado.");
            if (st == "ESTORNADO") throw new ConflitoException("Este pagamento já foi estornado.");
            Exec("UPDATE recebimentos SET status = 'ESTORNADO', motivo_estorno = ? WHERE id = ?", m, id);
        });
    }

    public List<Recebimento> ListarRecebimentos(long? clienteId = null, string? inicio = null, string? fim = null, int limite = 500)
    {
        var sql = "SELECT r.id, r.data, r.cliente_id, c.nome AS cliente, r.valor, r.forma, r.obs, r.status, r.motivo_estorno " +
                  "FROM recebimentos r JOIN clientes c ON c.id = r.cliente_id WHERE 1=1";
        var p = new List<object?>();
        if (clienteId != null) { sql += " AND r.cliente_id = ?"; p.Add(clienteId); }
        if (!string.IsNullOrEmpty(inicio)) { sql += " AND substr(r.data,1,10) >= ?"; p.Add(inicio); }
        if (!string.IsNullOrEmpty(fim)) { sql += " AND substr(r.data,1,10) <= ?"; p.Add(fim); }
        sql += " ORDER BY r.id DESC LIMIT ?"; p.Add(limite);
        return Todos(sql, r => new Recebimento(r.L("id"), r.S("data"), r.L("cliente_id"), r.S("cliente"), r.L("valor"), r.S("forma"), r.S("obs"),
                                               r.S("status"), r.S("motivo_estorno")), p.ToArray());
    }

    /// <summary>Extrato do fiado do cliente: compras (+) e pagamentos (−) em ordem, com saldo corrente.</summary>
    public List<LinhaExtrato> ExtratoCliente(long clienteId)
    {
        var eventos = new List<(string Data, long Ordem, string Desc, long Valor)>();
        eventos.AddRange(Todos("SELECT id, data, total FROM vendas WHERE cliente_id = ? AND forma_pagamento = 'Fiado' AND status = 'CONCLUIDA'",
                               r => (r.S("data"), r.L("id"), $"Compra fiado - nota {r.L("id"):000000}", r.L("total")), clienteId));
        eventos.AddRange(Todos("SELECT id, data, valor, forma FROM recebimentos WHERE cliente_id = ? AND status = 'ATIVO'",
                               r => (r.S("data"), r.L("id"), $"Pagamento ({r.S("forma")})", -r.L("valor")), clienteId));
        long saldo = 0;
        var saida = new List<LinhaExtrato>();
        foreach (var e in eventos.OrderBy(x => x.Data, StringComparer.Ordinal).ThenBy(x => x.Ordem))
        {
            saldo += e.Valor;
            saida.Add(new LinhaExtrato(e.Data, e.Desc, e.Valor, saldo));
        }
        return saida;
    }

    // ================================================================== caixa do dia
    /// <param name="tipo">"SUPRIMENTO" (entrada de dinheiro, ex.: troco inicial) ou "SANGRIA" (retirada).</param>
    public long RegistrarMovimentoCaixa(string tipo, long valor, string? motivo)
    {
        if (tipo is not ("SUPRIMENTO" or "SANGRIA")) throw new ValidacaoException("Tipo de lançamento inválido.");
        var v = Centavos(valor, "o valor");
        if (v <= 0) throw new ValidacaoException("Informe um valor maior que zero.");
        var m = Texto(motivo, "o motivo", tipo == "SANGRIA", 200);
        return Tx(() => Inserir("INSERT INTO caixa_movs (data, tipo, valor, motivo) VALUES (?,?,?,?)", Util.AgoraIso(), tipo, v, m));
    }

    public void ExcluirMovimentoCaixa(long id)
    {
        Tx(() =>
        {
            var data = Um("SELECT data FROM caixa_movs WHERE id = ?", r => r.S("data"), id) ?? throw new NaoEncontradoException("Lançamento não encontrado.");
            if (data[..10] != DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                throw new ValidacaoException("Só é possível excluir lançamentos de hoje (o caixa de dias anteriores já foi fechado).");
            Exec("DELETE FROM caixa_movs WHERE id = ?", id);
        });
    }

    public ResumoCaixa ObterResumoCaixa(string dataIso)
    {
        var porForma = VendasPorPagamento(dataIso, dataIso);
        var vendas = ResumoVendasPeriodo(dataIso, dataIso);
        var movs = Todos("SELECT id, data, tipo, valor, motivo FROM caixa_movs WHERE substr(data,1,10) = ? ORDER BY id",
                         r => new MovCaixa(r.L("id"), r.S("data"), r.S("tipo"), r.L("valor"), r.S("motivo")), dataIso);
        var receb = Todos("SELECT forma, COUNT(*) AS qtd, SUM(valor) AS total FROM recebimentos WHERE status = 'ATIVO' AND substr(data,1,10) = ? GROUP BY forma ORDER BY total DESC",
                          r => new PorPagamento(r.S("forma"), r.L("qtd"), r.L("total")), dataIso);
        long Soma(string t) => movs.Where(m => m.Tipo == t).Sum(m => m.Valor);
        long din(IEnumerable<PorPagamento> l) => l.Where(x => x.Forma == "Dinheiro").Sum(x => x.Total);
        var sup = Soma("SUPRIMENTO"); var san = Soma("SANGRIA");
        return new ResumoCaixa(dataIso, sup, san, vendas.Qtd, vendas.Total, porForma.Where(x => x.Forma == Fiado).Sum(x => x.Total),
                               porForma, receb.Sum(x => x.Total), receb, sup - san + din(porForma) + din(receb), movs);
    }

    // ================================================================== preços e reposição
    /// <summary>Reajusta o preço de venda (e opcionalmente o custo) em porcentagem. categoriaId: null = todos; 0 = sem categoria.</summary>
    public int ReajustarPrecos(long? categoriaId, decimal percentual, bool incluirCusto = false)
    {
        if (percentual == 0) throw new ValidacaoException("Informe a porcentagem do reajuste (ex.: 5 para aumentar 5%, -10 para reduzir 10%).");
        if (percentual < -90 || percentual > 500) throw new ValidacaoException("A porcentagem deve ficar entre -90% e 500%.");
        var fator = 1m + percentual / 100m;
        long Novo(long v) => Math.Min((long)Math.Round(v * fator, MidpointRounding.AwayFromZero), Util.LimiteCentavos);
        return Tx(() =>
        {
            var itens = ListarProdutos(categoriaId: categoriaId);
            foreach (var p in itens)
                Exec("UPDATE produtos SET preco_venda = ?, preco_custo = ? WHERE id = ?", Novo(p.PrecoVenda), incluirCusto ? Novo(p.PrecoCusto) : p.PrecoCusto, p.Id);
            return itens.Count;
        });
    }

    /// <summary>Produtos no estoque mínimo (ou abaixo), com uma sugestão de quanto comprar (dobro do mínimo).</summary>
    public List<ItemReposicao> ListarReposicao() =>
        ListarProdutos(apenasBaixo: true).Select(p => new ItemReposicao(p, Math.Max(p.EstoqueMin * 2 - p.Estoque, 1))).ToList();

    /// <summary>Os produtos mais vendidos nos últimos 90 dias que ainda têm estoque (atalhos da tela de venda).</summary>
    public List<Produto> ProdutosRapidos(int quantidade = 8)
    {
        var desde = DateTime.Today.AddDays(-90).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var ids = Todos("SELECT i.produto_id AS id FROM venda_itens i JOIN vendas v ON v.id = i.venda_id JOIN produtos p ON p.id = i.produto_id " +
                        "WHERE v.status = 'CONCLUIDA' AND substr(v.data,1,10) >= ? AND p.ativo = 1 AND p.estoque > 0 " +
                        "GROUP BY i.produto_id ORDER BY SUM(i.quantidade) DESC LIMIT ?", r => r.L("id"), desde, quantidade);
        return ids.Select(ObterProduto).ToList();
    }
}
