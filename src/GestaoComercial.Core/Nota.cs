using System.Text;

namespace GestaoComercial.Core;

/// <summary>
/// Geração da nota (HTML para imprimir/salvar em PDF e texto para visualizar no programa).
/// IMPORTANTE: é um comprovante interno de venda, SEM valor fiscal. Não substitui NF-e/NFC-e/SAT.
/// </summary>
public static class Nota
{
    public const string Aviso = "DOCUMENTO SEM VALOR FISCAL - comprovante interno de venda";

    public static string Numero(Venda v) => $"{v.Id:000000}";

    static string Esc(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    /// <param name="cupom">true = formato estreito (80 mm) para impressora térmica.</param>
    public static string GerarHtml(Venda v, bool cupom = false)
    {
        var e = v.Emitente;
        var linhas = new StringBuilder();
        int n = 1;
        foreach (var it in v.Itens)
            linhas.Append($"<tr><td>{n++}</td><td>{Esc(it.Codigo)}</td><td>{Esc(it.Descricao)}</td><td class='n'>{it.Quantidade} {Esc(it.Unidade)}</td>" +
                          $"<td class='n'>{Util.FmtDinheiro(it.PrecoUnit)}</td><td class='n'>{Util.FmtDinheiro(it.Total)}</td></tr>");
        var docEmit = e.Documento.Length > 0 ? $"CNPJ/CPF: {Esc(Util.FmtDocumento(e.Documento))}<br>" : "";
        var endEmit = e.Endereco.Length > 0 ? $"{Esc(e.Endereco)}<br>" : "";
        var telEmit = e.Telefone.Length > 0 ? $"Tel.: {Esc(e.Telefone)}<br>" : "";
        var docCli = v.ClienteDoc.Length > 0 ? $" &ndash; {Esc(Util.FmtDocumento(v.ClienteDoc))}" : "";
        var desconto = v.Desconto > 0 ? $"<tr><td>Desconto</td><td class='n'>- {Util.FmtDinheiro(v.Desconto)}</td></tr>" : "";
        var obs = v.Obs.Length > 0 ? $"<p><b>Observações:</b> {Esc(v.Obs)}</p>" : "";
        var cancel = v.Cancelada
            ? $"<div class='cancel'>NOTA CANCELADA em {Util.FmtData(v.CanceladaEm)}<br><small>Motivo: {Esc(v.MotivoCancelamento.Length > 0 ? v.MotivoCancelamento : "-")}</small></div>"
            : "";
        return $$"""
<!DOCTYPE html>
<html lang="pt-BR"><head><meta charset="utf-8">
<title>Nota {{Numero(v)}}</title>
<style>
 body { font-family: Arial, Helvetica, sans-serif; color:#111; max-width: 780px; margin: 24px auto; padding: 0 16px; }
 header { display:flex; justify-content:space-between; gap:24px; border-bottom:2px solid #111; padding-bottom:12px; }
 h1 { font-size: 20px; margin: 0 0 4px; }
 .nota { text-align:right; } .nota b { font-size: 20px; }
 table { width:100%; border-collapse: collapse; margin-top:14px; font-size: 14px; }
 th, td { padding:6px 8px; border-bottom:1px solid #ccc; text-align:left; }
 th { background:#f0f0f0; } .n { text-align:right; white-space:nowrap; }
 .totais { width: 280px; margin-left:auto; }
 .totais tr:last-child td { font-weight:bold; font-size:17px; border-top:2px solid #111; }
 .aviso { margin-top:22px; padding:8px; border:1px dashed #888; text-align:center; font-size:12px; color:#444; }
 .cancel { margin:14px 0; padding:12px; border:3px solid #c00; color:#c00; font-weight:bold; font-size:20px; text-align:center; }
 .cancel small { font-size:13px; font-weight:normal; }
 footer { text-align:center; margin-top:16px; font-size:13px; }
 @media print { body { margin:0; } }

 .cupom { max-width: 300px; font-size: 12px; margin: 4px auto; padding: 0 4px; }
 .cupom header { display:block; text-align:center; border-bottom:1px dashed #000; }
 .cupom .nota { text-align:center; margin-top:6px; }
 .cupom table { font-size: 12px; } .cupom th, .cupom td { padding:3px 2px; }
 .cupom th:nth-child(1), .cupom td:nth-child(1), .cupom th:nth-child(2), .cupom td:nth-child(2) { display:none; }
 .cupom .totais { width:100%; }
</style></head><body class="{{(cupom ? "cupom" : "")}}">
<header>
  <div><h1>{{Esc(e.Nome)}}</h1><small>{{docEmit}}{{endEmit}}{{telEmit}}</small></div>
  <div class="nota">NOTA Nº<br><b>{{Numero(v)}}</b><br><small>{{Util.FmtData(v.Data)}}</small></div>
</header>
{{cancel}}
<p><b>Cliente:</b> {{Esc(v.ClienteNome)}}{{docCli}}<br>
<b>Pagamento:</b> {{Esc(v.FormaPagamento)}}</p>
<table>
<thead><tr><th>#</th><th>Código</th><th>Descrição</th><th class="n">Qtd.</th><th class="n">Preço unit.</th><th class="n">Total</th></tr></thead>
<tbody>{{linhas}}</tbody>
</table>
<table class="totais">
<tr><td>Subtotal</td><td class="n">{{Util.FmtDinheiro(v.Subtotal)}}</td></tr>
{{desconto}}
<tr><td>TOTAL</td><td class="n">{{Util.FmtDinheiro(v.Total)}}</td></tr>
</table>
{{obs}}
<div class="aviso">{{Aviso}}</div>
<footer>{{Esc(e.Rodape)}}</footer>
</body></html>
""";
    }

    static string Centro(string s, int w) => s.Length >= w ? s : new string(' ', (w - s.Length) / 2) + s;

    public static string GerarTexto(Venda v, int largura = 72)
    {
        var e = v.Emitente;
        var tr = new string('=', largura);
        var fino = new string('-', largura);
        var o = new List<string> { tr, Centro(e.Nome, largura) };
        if (e.Documento.Length > 0) o.Add(Centro($"CNPJ/CPF: {Util.FmtDocumento(e.Documento)}", largura));
        if (e.Endereco.Length > 0) o.Add(Centro(e.Endereco, largura));
        if (e.Telefone.Length > 0) o.Add(Centro($"Tel.: {e.Telefone}", largura));
        o.Add(tr); o.Add(Centro($"NOTA Nº {Numero(v)}", largura)); o.Add(Centro(Util.FmtData(v.Data), largura));
        if (v.Cancelada)
        {
            o.Add(Centro("*** NOTA CANCELADA ***", largura));
            o.Add(Centro($"Motivo: {(v.MotivoCancelamento.Length > 0 ? v.MotivoCancelamento : "-")}", largura));
        }
        var cli = v.ClienteNome + (v.ClienteDoc.Length > 0 ? $"  ({Util.FmtDocumento(v.ClienteDoc)})" : "");
        o.Add(fino); o.Add($"Cliente:   {cli}"); o.Add($"Pagamento: {v.FormaPagamento}"); o.Add(fino);
        o.Add($"{"#",-4}{"Descrição",-30}{"Qtd",6}{"Unit.",14}{"Total",16}"); o.Add(fino);
        int n = 1;
        foreach (var it in v.Itens)
        {
            var d = it.Descricao.Length > 29 ? it.Descricao[..28] + "…" : it.Descricao;
            o.Add($"{n++,-4}{d,-30}{it.Quantidade,6}{Util.FmtDinheiro(it.PrecoUnit),14}{Util.FmtDinheiro(it.Total),16}");
        }
        o.Add(fino);
        o.Add($"{"Subtotal:",56}{Util.FmtDinheiro(v.Subtotal),16}");
        if (v.Desconto > 0) o.Add($"{"Desconto:",56}{"- " + Util.FmtDinheiro(v.Desconto),16}");
        o.Add($"{"TOTAL:",56}{Util.FmtDinheiro(v.Total),16}");
        if (v.Obs.Length > 0) { o.Add(fino); o.Add($"Obs.: {v.Obs}"); }
        o.Add(tr); o.Add(Centro(Aviso, largura));
        if (e.Rodape.Length > 0) o.Add(Centro(e.Rodape, largura));
        o.Add(tr);
        return string.Join("\n", o);
    }

    public static string SalvarHtml(Venda v, string pasta, bool cupom = false)
    {
        Directory.CreateDirectory(pasta);
        var caminho = Path.Combine(pasta, cupom ? $"cupom-{Numero(v)}.html" : $"nota-{Numero(v)}.html");
        File.WriteAllText(caminho, GerarHtml(v, cupom), new UTF8Encoding(false));
        return caminho;
    }

    /// <summary>Mensagem curta com o resumo da compra, pronta para enviar por WhatsApp.</summary>
    public static string TextoWhatsApp(Venda v)
    {
        var sb = new StringBuilder();
        sb.Append($"*{v.Emitente.Nome}*\nNota nº {Numero(v)} - {Util.FmtData(v.Data)}\n\n");
        foreach (var it in v.Itens) sb.Append($"{it.Quantidade}x {it.Descricao} - {Util.FmtDinheiro(it.Total)}\n");
        if (v.Desconto > 0) sb.Append($"\nDesconto: -{Util.FmtDinheiro(v.Desconto)}");
        sb.Append($"\n*Total: {Util.FmtDinheiro(v.Total)}* ({v.FormaPagamento})\n\n{(v.Emitente.Rodape.Length > 0 ? v.Emitente.Rodape : "Obrigado pela preferência!")}");
        return sb.ToString();
    }

    /// <summary>Endereço wa.me que abre o WhatsApp (com o contato, se houver telefone) e a mensagem pronta.</summary>
    public static string LinkWhatsApp(string? telefone, string mensagem)
    {
        var d = Util.SomenteDigitos(telefone);
        if (d.Length is 10 or 11) d = "55" + d;           // DDD + número: acrescenta o código do Brasil
        return $"https://wa.me/{(d.Length >= 12 ? d : "")}?text={Uri.EscapeDataString(mensagem)}";
    }
}
