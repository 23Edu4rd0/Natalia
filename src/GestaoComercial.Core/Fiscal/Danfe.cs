using System.Globalization;
using System.Text;
using DFe.Classes.Flags;
using DFe.Utils;
using NFe.Classes;
using NFe.Classes.Protocolo;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Pagamento;
using QRCoder;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Core.Fiscal;

/// <summary>DANFE em HTML para imprimir ou salvar em PDF pelo navegador: NFC-e (cupom 80 mm) e NF-e (folha A4).</summary>
public static class Danfe
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    /// <summary>Escapa só o necessário (acentos ficam legíveis no arquivo).</summary>
    static string H(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    static string R(decimal v) => Util.FmtDinheiro((long)Math.Round(v * 100, MidpointRounding.AwayFromZero), false);
    static string Q(decimal v) => v == Math.Floor(v) ? v.ToString("0", Inv) : v.ToString("0.####", Inv).Replace('.', ',');

    /// <summary>Gera o DANFE a partir do XML guardado (nfeProc quando autorizada; NFe assinada quando não).</summary>
    public static string Html(NotaFiscal n)
    {
        NFeDoc nfe; protNFe? prot = null;
        if (n.Xml.Contains("<nfeProc"))
        {
            var proc = FuncoesXml.XmlStringParaClasse<nfeProc>(n.Xml);
            nfe = proc.NFe; prot = proc.protNFe;
        }
        else nfe = FuncoesXml.XmlStringParaClasse<NFeDoc>(n.Xml);
        var aviso = n.Status switch
        {
            "CANCELADA" => "NOTA CANCELADA",
            "AUTORIZADA" => n.Homologacao ? "EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO – SEM VALOR FISCAL" : "",
            "CONTINGENCIA" => "EMITIDA EM CONTINGÊNCIA" + (n.Homologacao ? " – AMBIENTE DE HOMOLOGAÇÃO, SEM VALOR FISCAL" : ""),
            _ => "NÃO AUTORIZADA PELA SEFAZ – SEM VALOR FISCAL",
        };
        return nfe.infNFe.ide.mod == ModeloDocumento.NFCe ? Nfce(nfe, prot, aviso, n.EmContingencia) : NfeA4(nfe, prot, aviso);
    }

    /// <summary>Número da NF-e no DANFE: 000.000.001 (no formato do .NET o ponto seria vírgula decimal).</summary>
    public static string NumeroNota(long numero) => numero.ToString("000000000").Insert(6, ".").Insert(3, ".");

    public static string ChaveFormatada(string chave) => string.Join(" ", Enumerable.Range(0, (chave.Length + 3) / 4).Select(i => chave.Substring(i * 4, Math.Min(4, chave.Length - i * 4))));

    static string Doc(string? cnpj, string? cpf) => cnpj is { Length: > 0 } ? "CNPJ " + FmtCnpj(cnpj) : cpf is { Length: > 0 } ? "CPF " + Util.FmtDocumento(cpf) : "";
    static string FmtCnpj(string c) => c.Length == 14 ? $"{c[..2]}.{c[2..5]}.{c[5..8]}/{c[8..12]}-{c[12..]}" : c;

    static string FormaPag(FormaPagamento f) => f switch
    {
        FormaPagamento.fpDinheiro => "Dinheiro",
        FormaPagamento.fpCartaoCredito => "Cartão de crédito",
        FormaPagamento.fpCartaoDebito => "Cartão de débito",
        FormaPagamento.fpCartaoDaLoja => "Crédito loja",
        FormaPagamento.fpPagamentoInstantaneoPIXDinamico or FormaPagamento.fpPagamentoInstantaneoPIXEstatico => "Pix",
        FormaPagamento.fpTransferenciabancaria => "Transferência",
        _ => "Outros",
    };

    static string QrPng(string texto)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return "data:image/png;base64," + Convert.ToBase64String(new PngByteQRCode(data).GetGraphic(6));
    }

    // ------------------------------------------------------------------ NFC-e (80 mm)
    /// <param name="pendente">Emitida em contingência e ainda não autorizada: o manual do DANFE NFC-e pede os dois avisos.</param>
    static string Nfce(NFeDoc nfe, protNFe? prot, string aviso, bool pendente = false)
    {
        var i = nfe.infNFe; var e = i.emit; var t = i.total.ICMSTot;
        var chave = i.Id[3..];
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>NFC-e</title><style>
@page { size: 80mm auto; margin: 3mm; }
body { font-family: 'Courier New', monospace; font-size: 11px; width: 74mm; margin: 0 auto; color: #000; }
.c { text-align: center; } .b { font-weight: bold; } .s { font-size: 10px; } hr { border: 0; border-top: 1px dashed #000; margin: 4px 0; }
table { width: 100%; border-collapse: collapse; } td, th { padding: 1px 0; vertical-align: top; } .r { text-align: right; }
.aviso { border: 1px solid #000; padding: 3px; margin: 4px 0; font-weight: bold; text-align: center; }
img { width: 36mm; height: 36mm; display: block; margin: 4px auto; }
@media screen { body { margin: 16px auto; } .botao { display: block; margin: 0 auto 10px; padding: 8px 16px; font: 13px sans-serif; } }
@media print { .botao { display: none; } }
</style></head><body><button class="botao" onclick="window.print()">Imprimir</button>
""");
        sb.Append($"<div class=\"c b\">{H(e.xFant ?? e.xNome)}</div><div class=\"c s\">{H(e.xNome)}<br>CNPJ {H(FmtCnpj(e.CNPJ))}  IE {H(e.IE)}<br>");
        var en = e.enderEmit;
        sb.Append($"{H(en.xLgr)}, {H(en.nro)}{(string.IsNullOrEmpty(en.xCpl) ? "" : " " + H(en.xCpl))} - {H(en.xBairro)} - {H(en.xMun)}/{en.UF}</div><hr>");
        sb.Append("<div class=\"c b s\">DANFE NFC-e – Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica</div>");
        if (aviso.Length > 0) sb.Append($"<div class=\"aviso\">{H(aviso)}</div>");
        sb.Append("<hr><table><tr class=\"b\"><td>Descrição</td><td class=\"r\">Qtde</td><td class=\"r\">Un</td><td class=\"r\">Vl unit</td><td class=\"r\">Total</td></tr>");
        foreach (var d in i.det)
            sb.Append($"<tr><td colspan=\"5\">{d.nItem:000} {H(d.prod.cProd)} {H(d.prod.xProd)}</td></tr><tr><td></td><td class=\"r\">{Q(d.prod.qCom)}</td><td class=\"r\">{H(d.prod.uCom)}</td><td class=\"r\">{R(d.prod.vUnCom)}</td><td class=\"r\">{R(d.prod.vProd)}</td></tr>");
        sb.Append("</table><hr><table>");
        sb.Append($"<tr><td>Qtde. total de itens</td><td class=\"r\">{i.det.Count}</td></tr><tr><td>Valor total R$</td><td class=\"r\">{R(t.vProd)}</td></tr>");
        if (t.vDesc > 0) sb.Append($"<tr><td>Desconto R$</td><td class=\"r\">-{R(t.vDesc)}</td></tr>");
        sb.Append($"<tr class=\"b\"><td>Valor a pagar R$</td><td class=\"r\">{R(t.vNF)}</td></tr><tr><td>FORMA DE PAGAMENTO</td><td class=\"r\">VALOR PAGO R$</td></tr>");
        foreach (var p in i.pag.SelectMany(p => p.detPag ?? new List<detPag>()))
            sb.Append($"<tr><td>{H(FormaPag(p.tPag))}</td><td class=\"r\">{R(p.vPag)}</td></tr>");
        var troco = i.pag.FirstOrDefault()?.vTroco;
        if (troco > 0) sb.Append($"<tr><td>Troco R$</td><td class=\"r\">{R(troco.Value)}</td></tr>");
        sb.Append("</table><hr>");
        sb.Append($"<div class=\"c s\">Consulte pela Chave de Acesso em<br>{H(nfe.infNFeSupl?.urlChave)}<br><span class=\"b\">{ChaveFormatada(chave)}</span></div><hr>");
        var dest = i.dest;
        sb.Append(dest == null ? "<div class=\"c b s\">CONSUMIDOR NÃO IDENTIFICADO</div>"
                               : $"<div class=\"c s\"><b>CONSUMIDOR</b> {H(Doc(dest.CNPJ, dest.CPF))}{(string.IsNullOrEmpty(dest.xNome) ? "" : "<br>" + H(dest.xNome))}</div>");
        sb.Append($"<hr><div class=\"c s\"><b>NFC-e nº {i.ide.nNF:000000000} Série {i.ide.serie:000}</b> {i.ide.dhEmi.LocalDateTime:dd/MM/yyyy HH:mm:ss}");
        if (prot?.infProt is { } ip) sb.Append($"<br>Protocolo de autorização: {H(ip.nProt)}<br>Data de autorização: {ip.dhRecbto.LocalDateTime:dd/MM/yyyy HH:mm:ss}");
        else if (pendente) sb.Append("<div class=\"aviso\">EMITIDA EM CONTINGÊNCIA<br>Pendente de autorização</div>");
        if (prot != null && i.ide.tpEmis == TipoEmissao.teOffLine) sb.Append("<br>Emitida em contingência");
        sb.Append("</div>");
        if (nfe.infNFeSupl?.qrCode is { Length: > 0 } qr) sb.Append($"<img src=\"{QrPng(qr)}\" alt=\"QR Code\">");
        if (t.vTotTrib is > 0) sb.Append($"<div class=\"c s\">Tributos totais incidentes (Lei Federal 12.741/2012): R$ {R(t.vTotTrib.Value)}</div>");
        if (!string.IsNullOrEmpty(i.infAdic?.infCpl)) sb.Append($"<hr><div class=\"s\">{H(i.infAdic!.infCpl)}</div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    // ------------------------------------------------------------------ NF-e (A4)
    static string NfeA4(NFeDoc nfe, protNFe? prot, string aviso)
    {
        var i = nfe.infNFe; var e = i.emit; var t = i.total.ICMSTot; var en = e.enderEmit; var d = i.dest;
        var chave = i.Id[3..];
        string Campo(string rotulo, string? valor, string extra = "") => $"<td{extra}><div class=\"rot\">{H(rotulo)}</div><div class=\"val\">{H(valor)}</div></td>";
        var sb = new StringBuilder();
        sb.Append("""
<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>DANFE</title><style>
@page { size: A4; margin: 8mm; }
body { font-family: Arial, Helvetica, sans-serif; font-size: 9px; color: #000; width: 194mm; margin: 0 auto; }
table { width: 100%; border-collapse: collapse; margin-top: 3px; } td { border: 1px solid #000; padding: 1px 3px; vertical-align: top; }
.rot { font-size: 6.5px; text-transform: uppercase; } .val { font-size: 9px; min-height: 11px; font-weight: bold; }
.tit { font-size: 7px; font-weight: bold; margin: 5px 0 0; text-transform: uppercase; } .c { text-align: center; } .r { text-align: right; }
.itens td { font-size: 8px; } .itens th { font-size: 6.5px; border: 1px solid #000; padding: 1px 2px; font-weight: normal; }
.aviso { position: fixed; top: 45%; left: 0; right: 0; text-align: center; font-size: 28px; color: rgba(200,0,0,.35); font-weight: bold; transform: rotate(-20deg); }
.botao { display: block; margin: 10px auto; padding: 8px 16px; font: 13px sans-serif; } @media print { .botao { display: none; } }
</style></head><body><button class="botao" onclick="window.print()">Imprimir</button>
""");
        if (aviso.Length > 0) sb.Append($"<div class=\"aviso\">{H(aviso)}</div>");
        // canhoto
        sb.Append($"<table><tr><td colspan=\"2\" style=\"font-size:7px\">RECEBEMOS DE {H(e.xNome)} OS PRODUTOS CONSTANTES DA NOTA FISCAL INDICADA AO LADO</td><td rowspan=\"2\" class=\"c\" style=\"width:30mm\"><b>NF-e</b><br>Nº {NumeroNota(i.ide.nNF)}<br>Série {i.ide.serie:000}</td></tr>");
        sb.Append("<tr><td style=\"width:35mm;height:9mm\"><div class=\"rot\">Data de recebimento</div></td><td><div class=\"rot\">Identificação e assinatura do recebedor</div></td></tr></table>");
        // cabeçalho
        sb.Append("<table><tr>");
        sb.Append($"<td style=\"width:75mm\"><div style=\"font-size:12px;font-weight:bold\">{H(e.xNome)}</div><div>{H(en.xLgr)}, {H(en.nro)} {H(en.xCpl)}<br>{H(en.xBairro)} – {H(en.xMun)}/{en.UF} – CEP {H(en.CEP)}<br>{(en.fone != null ? "Fone " + en.fone : "")}</div></td>");
        sb.Append($"<td class=\"c\" style=\"width:32mm\"><div style=\"font-size:14px;font-weight:bold\">DANFE</div><div style=\"font-size:7px\">Documento Auxiliar da Nota Fiscal Eletrônica</div><div style=\"margin-top:3px\">0 – Entrada<br>1 – Saída <b style=\"border:1px solid #000;padding:0 4px\">{(int)i.ide.tpNF}</b></div><div style=\"margin-top:3px\"><b>Nº {NumeroNota(i.ide.nNF)}<br>Série {i.ide.serie:000}</b><br>Folha 1/1</div></td>");
        sb.Append($"<td><div class=\"c\">{Code128.Svg(chave, 60)}</div><div class=\"rot\">Chave de acesso</div><div class=\"val c\">{ChaveFormatada(chave)}</div><div class=\"c\" style=\"font-size:8px\">Consulta de autenticidade no portal nacional da NF-e www.nfe.fazenda.gov.br/portal ou no site da Sefaz Autorizadora</div></td>");
        sb.Append("</tr></table>");
        sb.Append("<table><tr>" + Campo("Natureza da operação", i.ide.natOp, " style=\"width:60%\"") +
                  Campo("Protocolo de autorização de uso", prot?.infProt is { } ip ? $"{ip.nProt} – {ip.dhRecbto.LocalDateTime:dd/MM/yyyy HH:mm:ss}" : "—") + "</tr></table>");
        sb.Append("<table><tr>" + Campo("Inscrição estadual", e.IE) + Campo("Inscrição estadual do subst. trib.", "") + Campo("CNPJ", FmtCnpj(e.CNPJ)) + "</tr></table>");
        // destinatário
        sb.Append("<div class=\"tit\">Destinatário / remetente</div>");
        var ed = d?.enderDest;
        sb.Append("<table><tr>" + Campo("Nome / razão social", d?.xNome, " style=\"width:55%\"") + Campo("CNPJ / CPF", Doc(d?.CNPJ, d?.CPF).Replace("CNPJ ", "").Replace("CPF ", "")) +
                  Campo("Data da emissão", i.ide.dhEmi.LocalDateTime.ToString("dd/MM/yyyy")) + "</tr>");
        sb.Append("<tr>" + Campo("Endereço", ed == null ? "" : $"{ed.xLgr}, {ed.nro} {ed.xCpl}") + Campo("Bairro / distrito", ed?.xBairro) + Campo("CEP", ed?.CEP) + "</tr>");
        sb.Append("<tr>" + Campo("Município", ed?.xMun) + Campo("UF", ed?.UF) + Campo("Inscrição estadual", d?.IE) + "</tr></table>");
        // pagamento
        sb.Append("<div class=\"tit\">Pagamento</div><table><tr>");
        foreach (var p in i.pag.SelectMany(p => p.detPag ?? new List<detPag>())) sb.Append(Campo(FormaPag(p.tPag), "R$ " + R(p.vPag)));
        sb.Append("</tr></table>");
        // impostos
        sb.Append("<div class=\"tit\">Cálculo do imposto</div><table><tr>" + Campo("Base de cálc. do ICMS", R(t.vBC)) + Campo("Valor do ICMS", R(t.vICMS)) +
                  Campo("Base de cálc. ICMS S.T.", R(t.vBCST)) + Campo("Valor do ICMS subst.", R(t.vST)) + Campo("Valor total dos produtos", R(t.vProd)) + "</tr><tr>" +
                  Campo("Valor do frete", R(t.vFrete)) + Campo("Valor do seguro", R(t.vSeg)) + Campo("Desconto", R(t.vDesc)) + Campo("Outras despesas", R(t.vOutro)) +
                  Campo("Valor total da nota", R(t.vNF)) + "</tr></table>");
        sb.Append("<div class=\"tit\">Transportador / volumes transportados</div><table><tr>" + Campo("Frete por conta", "9 – Sem ocorrência de transporte") + "</tr></table>");
        // itens
        sb.Append("<div class=\"tit\">Dados dos produtos / serviços</div><table class=\"itens\"><tr><th>Código</th><th>Descrição</th><th>NCM</th><th>CST/CSOSN</th><th>CFOP</th><th>Un</th><th>Qtde</th><th>Vl unit</th><th>Vl desc</th><th>Vl total</th><th>BC ICMS</th><th>Vl ICMS</th><th>Alíq. ICMS</th></tr>");
        foreach (var it in i.det)
        {
            var (cst, bc, vicms, aliq) = Icms(it.imposto.ICMS.TipoICMS);
            sb.Append($"<tr><td>{H(it.prod.cProd)}</td><td>{H(it.prod.xProd)}</td><td>{H(it.prod.NCM)}</td><td class=\"c\">{cst}</td><td class=\"c\">{it.prod.CFOP}</td><td>{H(it.prod.uCom)}</td><td class=\"r\">{Q(it.prod.qCom)}</td><td class=\"r\">{R(it.prod.vUnCom)}</td><td class=\"r\">{R(it.prod.vDesc ?? 0)}</td><td class=\"r\">{R(it.prod.vProd)}</td><td class=\"r\">{R(bc)}</td><td class=\"r\">{R(vicms)}</td><td class=\"r\">{aliq:0.##}</td></tr>");
        }
        sb.Append("</table>");
        sb.Append($"<div class=\"tit\">Dados adicionais</div><table><tr><td style=\"height:22mm\"><div class=\"rot\">Informações complementares</div><div style=\"font-size:8px\">{H(i.infAdic?.infCpl)}{(t.vTotTrib is > 0 ? $"<br>Tributos aproximados (Lei 12.741/2012): R$ {R(t.vTotTrib.Value)}" : "")}</div></td></tr></table>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    static (string Cst, decimal Bc, decimal Valor, decimal Aliq) Icms(object icms)
    {
        dynamic x = icms;
        string cst;
        try { cst = ((int)x.CSOSN).ToString(Inv); }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            try { cst = ((int)x.orig).ToString(Inv) + x.CST.ToString().Replace("Cst", ""); } catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { cst = ""; }
        }
        decimal bc = 0, v = 0, a = 0;
        try { bc = (decimal)x.vBC; v = (decimal)x.vICMS; a = (decimal)x.pICMS; } catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        return (cst, bc, v, a);
    }
}

/// <summary>Código de barras Code 128 (conjunto C, só números) em SVG — usado na chave de acesso do DANFE.</summary>
public static class Code128
{
    public static readonly string[] Padroes =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213","221312","231212","112232","122132","122231","113222",
        "123122","123221","223211","221132","221231","213212","223112","312131","311222","321122","321221","312212","322112","322211","212123","212321",
        "232121","111323","131123","131321","112313","132113","132311","211313","231113","231311","112133","112331","132131","113123","113321","133121",
        "313121","211331","231131","213113","213311","213131","311123","311321","331121","312113","312311","332111","314111","221411","431111","111224",
        "111422","121124","121421","141122","141221","112214","112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141","214121","412121","111143","111341","131141","114113",
        "114311","411113","411311","113141","114131","311141","411131","211412","211214","211232","2331112",
    };

    /// <summary>Valores Code 128 (início C, pares de dígitos, verificador, fim).</summary>
    public static List<int> Codificar(string digitos)
    {
        if (digitos.Length == 0 || digitos.Length % 2 != 0 || !digitos.All(char.IsAsciiDigit)) throw new ArgumentException("Code 128-C precisa de uma quantidade par de dígitos.");
        var v = new List<int> { 105 };
        for (int i = 0; i < digitos.Length; i += 2) v.Add(int.Parse(digitos.Substring(i, 2), CultureInfo.InvariantCulture));
        var soma = v[0];
        for (int i = 1; i < v.Count; i++) soma += v[i] * i;
        v.Add(soma % 103);
        v.Add(106);
        return v;
    }

    public static string Svg(string digitos, double altura)
    {
        var modulos = new List<(bool Barra, int Largura)>();
        foreach (var c in Codificar(digitos))
            for (int k = 0; k < Padroes[c].Length; k++) modulos.Add((k % 2 == 0, Padroes[c][k] - '0'));
        var total = modulos.Sum(m => m.Largura) + 20;
        var sb = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {total} {altura.ToString(CultureInfo.InvariantCulture)}\" width=\"100%\" height=\"{altura.ToString(CultureInfo.InvariantCulture)}\" preserveAspectRatio=\"none\">");
        var x = 10;
        foreach (var (barra, larg) in modulos)
        {
            if (barra) sb.Append($"<rect x=\"{x}\" y=\"0\" width=\"{larg}\" height=\"{altura.ToString(CultureInfo.InvariantCulture)}\"/>");
            x += larg;
        }
        return sb.Append("</svg>").ToString();
    }
}
