using System.Text;

namespace GestaoComercial.Core;

/// <summary>Documentos simples em HTML para imprimir ou salvar em PDF pelo navegador.</summary>
public static class Impressos
{
    static string Esc(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    const string Css = """
<style>
 body { font-family: Arial, Helvetica, sans-serif; color:#111; max-width: 760px; margin: 24px auto; padding: 0 16px; }
 h1 { font-size: 20px; margin: 0 0 2px; } h2 { font-size: 15px; margin: 22px 0 6px; } .sub { color:#555; font-size: 13px; }
 table { width:100%; border-collapse: collapse; font-size: 14px; } th, td { padding:6px 8px; border-bottom:1px solid #ddd; text-align:left; }
 th { background:#f0f0f0; } .n { text-align:right; white-space:nowrap; } .forte td { font-weight:bold; border-top:2px solid #111; }
 .caixa { margin-top:10px; padding:12px; border:2px solid #111; font-size:16px; } @media print { body { margin:0; } }
</style>
""";

    static string Pagina(string titulo, string corpo) =>
        $"<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><title>{Esc(titulo)}</title>{Css}</head><body>{corpo}</body></html>";

    static string Cab(Empresa e, string titulo, string sub) =>
        $"<h1>{Esc(e.Nome)}</h1><div class=\"sub\">{Esc(titulo)} - {Esc(sub)}</div>";

    public static string FechamentoCaixa(Empresa e, ResumoCaixa r)
    {
        var sb = new StringBuilder(Cab(e, "Fechamento de caixa", Util.FmtData(r.Data, false)));
        sb.Append("<h2>Vendas do dia</h2><table><tr><th>Forma de pagamento</th><th class=\"n\">Vendas</th><th class=\"n\">Total</th></tr>");
        foreach (var f in r.VendasPorForma) sb.Append($"<tr><td>{Esc(f.Forma)}</td><td class=\"n\">{f.Qtd}</td><td class=\"n\">{Util.FmtDinheiro(f.Total)}</td></tr>");
        sb.Append($"<tr class=\"forte\"><td>Total vendido</td><td class=\"n\">{r.VendasQtd}</td><td class=\"n\">{Util.FmtDinheiro(r.VendasTotal)}</td></tr></table>");
        if (r.FiadoLancado > 0) sb.Append($"<p class=\"sub\">Do total vendido, {Util.FmtDinheiro(r.FiadoLancado)} ficou no fiado (a receber).</p>");
        sb.Append("<h2>Pagamentos de fiado recebidos</h2>");
        if (r.RecebimentosPorForma.Count == 0) sb.Append("<p class=\"sub\">Nenhum.</p>");
        else
        {
            sb.Append("<table>");
            foreach (var f in r.RecebimentosPorForma) sb.Append($"<tr><td>{Esc(f.Forma)}</td><td class=\"n\">{Util.FmtDinheiro(f.Total)}</td></tr>");
            sb.Append("</table>");
        }
        sb.Append("<h2>Entradas e retiradas do caixa</h2>");
        if (r.Movimentos.Count == 0) sb.Append("<p class=\"sub\">Nenhuma.</p>");
        else
        {
            sb.Append("<table><tr><th>Hora</th><th>Tipo</th><th>Motivo</th><th class=\"n\">Valor</th></tr>");
            foreach (var m in r.Movimentos)
                sb.Append($"<tr><td>{Esc(Util.FmtData(m.Data).Split(' ').LastOrDefault())}</td><td>{(m.Tipo == "SANGRIA" ? "Retirada" : "Entrada (troco)")}</td><td>{Esc(m.Motivo)}</td><td class=\"n\">{(m.Tipo == "SANGRIA" ? "-" : "")}{Util.FmtDinheiro(m.Valor)}</td></tr>");
            sb.Append("</table>");
        }
        sb.Append($"<div class=\"caixa\"><b>Dinheiro que deve ter na gaveta: {Util.FmtDinheiro(r.DinheiroEsperado)}</b><br><span class=\"sub\">entradas de troco + vendas e pagamentos em dinheiro - retiradas</span></div>");
        sb.Append("<p style=\"margin-top:40px\">Conferido por: ______________________________</p>");
        return Pagina("Fechamento de caixa", sb.ToString());
    }

    public static string Reposicao(Empresa e, IEnumerable<ItemReposicao> itens)
    {
        var sb = new StringBuilder(Cab(e, "Lista de reposição de estoque", DateTime.Today.ToString("dd/MM/yyyy")));
        sb.Append("<h2>Produtos para comprar</h2><table><tr><th>Código</th><th>Produto</th><th class=\"n\">Estoque</th><th class=\"n\">Mínimo</th><th class=\"n\">Sugestão</th></tr>");
        foreach (var i in itens)
            sb.Append($"<tr><td>{Esc(i.Produto.Codigo)}</td><td>{Esc(i.Produto.Nome)}</td><td class=\"n\">{i.Produto.Estoque}</td><td class=\"n\">{i.Produto.EstoqueMin}</td><td class=\"n\">{i.Sugestao} {Esc(i.Produto.Unidade)}</td></tr>");
        sb.Append("</table>");
        return Pagina("Lista de reposição", sb.ToString());
    }

    public static string ExtratoFiado(Empresa e, string cliente, IEnumerable<LinhaExtrato> linhas, long saldo)
    {
        var sb = new StringBuilder(Cab(e, "Extrato do fiado", cliente));
        sb.Append("<h2>Movimentação</h2><table><tr><th>Data</th><th>Descrição</th><th class=\"n\">Valor</th><th class=\"n\">Saldo</th></tr>");
        foreach (var l in linhas)
            sb.Append($"<tr><td>{Util.FmtData(l.Data)}</td><td>{Esc(l.Descricao)}</td><td class=\"n\">{(l.Valor < 0 ? "-" : "")}{Util.FmtDinheiro(Math.Abs(l.Valor))}</td><td class=\"n\">{Util.FmtDinheiro(l.Saldo)}</td></tr>");
        sb.Append($"<tr class=\"forte\"><td colspan=\"3\">Total a pagar</td><td class=\"n\">{Util.FmtDinheiro(saldo)}</td></tr></table>");
        return Pagina("Extrato do fiado", sb.ToString());
    }

    /// <summary>Mensagem de cobrança gentil para WhatsApp.</summary>
    public static string MensagemCobranca(Empresa e, string cliente, long saldo) =>
        $"Olá, {cliente.Split(' ')[0]}! Tudo bem? Aqui é da {e.Nome}. Passando para lembrar que você tem um saldo de {Util.FmtDinheiro(saldo)} em aberto conosco. " +
        "Quando puder, é só nos avisar. Obrigada! 😊";
}
