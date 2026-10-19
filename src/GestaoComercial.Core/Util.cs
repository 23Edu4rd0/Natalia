using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GestaoComercial.Core;

/// <summary>Dinheiro (em centavos), documentos, datas, busca e CSV.</summary>
public static class Util
{
    public const long LimiteCentavos = 100_000_000_000; // R$ 1 bilhão: evita valores absurdos por erro de digitação
    public const long LimiteQuantidade = 1_000_000;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------ dinheiro
    /// <summary>Converte "1.234,56", "12,5", "12.50", "R$ 3" em centavos.</summary>
    public static long ParseDinheiro(string? texto, string campo = "Valor")
    {
        var bruto = texto ?? "";
        var t = Regex.Replace(bruto, @"[R$\s ]", "");
        if (t.Length == 0) throw new ValidacaoException($"Informe {campo}.");
        string invalido = $"{campo} inválido: '{bruto.Trim()}'. Use apenas números, ex.: 12,50.";
        if (!Regex.IsMatch(t, @"^-?[0-9.,]+$")) throw new ValidacaoException(invalido);
        if (t.StartsWith('-')) throw new ValidacaoException($"{campo} não pode ser negativo.");
        if (t.Contains(',') && t.Contains('.'))
        {
            if (t.LastIndexOf(',') > t.LastIndexOf('.')) t = t.Replace(".", "").Replace(',', '.'); // 1.234,56
            else t = t.Replace(",", "");                                                              // 1,234.56
        }
        else if (t.Contains(',')) t = t.Replace(',', '.');
        else if (t.Contains('.') && Regex.IsMatch(t, @"^[1-9][0-9]{0,2}(\.[0-9]{3})+$")) t = t.Replace(".", ""); // 1.234 = mil...
        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint, Inv, out var valor)) throw new ValidacaoException(invalido);
        var cent = Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
        if (cent > LimiteCentavos) throw new ValidacaoException($"{campo} muito alto.");
        return (long)cent;
    }

    /// <summary>Desconto digitado como "5,00" (R$) ou "10%" (percentual do subtotal). Vazio = 0.</summary>
    public static long ParseDesconto(string? texto, long subtotal)
    {
        var t = (texto ?? "").Trim();
        if (t.Length == 0) return 0;
        if (!t.EndsWith('%')) return ParseDinheiro(t, "Desconto");
        var pct = t[..^1].Trim().Replace(',', '.');
        if (!decimal.TryParse(pct, NumberStyles.AllowDecimalPoint, Inv, out var p) || p < 0)
            throw new ValidacaoException($"Desconto inválido: '{t}'. Exemplos: 5,00 ou 10%.");
        if (p > 100) throw new ValidacaoException("O desconto em porcentagem não pode passar de 100%.");
        return (long)Math.Round(subtotal * p / 100m, MidpointRounding.AwayFromZero);
    }

    public static string FmtDinheiro(long centavos, bool simbolo = true)
    {
        var sinal = centavos < 0 ? "-" : "";
        var abs = Math.Abs(centavos);
        var reais = (abs / 100).ToString("#,##0", Inv).Replace(',', '.');
        return $"{sinal}{(simbolo ? "R$ " : "")}{reais},{abs % 100:00}";
    }

    public static long ParseInteiro(string? texto, string campo = "Quantidade", long minimo = 0, long maximo = LimiteQuantidade)
    {
        var t = (texto ?? "").Trim();
        if (t.Length == 0) throw new ValidacaoException($"Informe {campo}.");
        if (!Regex.IsMatch(t, @"^-?[0-9]+$")) throw new ValidacaoException($"{campo} deve ser um número inteiro (você digitou '{t}').");
        if (!long.TryParse(t, NumberStyles.AllowLeadingSign, Inv, out var n)) throw new ValidacaoException($"{campo} muito alto.");
        if (n < minimo) throw new ValidacaoException($"{campo} deve ser no mínimo {minimo}.");
        if (n > maximo) throw new ValidacaoException($"{campo} deve ser no máximo {maximo}.");
        return n;
    }

    // ------------------------------------------------------------------ documentos
    public static string SomenteDigitos(string? t) => new((t ?? "").Where(c => c is >= '0' and <= '9').ToArray());

    public static bool ValidaCpf(string? cpf)
    {
        var d = SomenteDigitos(cpf);
        if (d.Length != 11 || d.Distinct().Count() == 1) return false;
        for (int i = 9; i <= 10; i++)
        {
            int soma = 0;
            for (int n = 0; n < i; n++) soma += (d[n] - '0') * (i + 1 - n);
            if ((soma * 10 % 11) % 10 != d[i] - '0') return false;
        }
        return true;
    }

    public static bool ValidaCnpj(string? cnpj)
    {
        var d = SomenteDigitos(cnpj);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        static int Calc(string b)
        {
            int[] pesos = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            if (b.Length == 13) pesos = new[] { 6 }.Concat(pesos).ToArray();
            int soma = 0;
            for (int i = 0; i < b.Length; i++) soma += (b[i] - '0') * pesos[i];
            int r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }
        return Calc(d[..12]) == d[12] - '0' && Calc(d[..13]) == d[13] - '0';
    }

    public static string FmtDocumento(string? doc)
    {
        var d = SomenteDigitos(doc);
        if (d.Length == 11) return $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}";
        if (d.Length == 14) return $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}";
        return doc ?? "";
    }

    /// <summary>Valida CPF/CNPJ (vazio é aceito). Retorna só os dígitos.</summary>
    public static string ValidaDocumento(string? doc)
    {
        var d = SomenteDigitos(doc);
        if (d.Length == 0) return "";
        if (d.Length == 11) { if (!ValidaCpf(d)) throw new ValidacaoException("CPF inválido. Confira os números digitados."); }
        else if (d.Length == 14) { if (!ValidaCnpj(d)) throw new ValidacaoException("CNPJ inválido. Confira os números digitados."); }
        else throw new ValidacaoException("O documento deve ter 11 dígitos (CPF) ou 14 dígitos (CNPJ).");
        return d;
    }

    public static bool ValidaEmail(string? email) => Regex.IsMatch(email ?? "", @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    // ------------------------------------------------------------------ datas
    public static string AgoraIso() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv);
    public static string HojeBr() => DateTime.Today.ToString("dd/MM/yyyy", Inv);
    public static string InicioMesBr() => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("dd/MM/yyyy", Inv);
    public static string InicioAnoBr() => new DateTime(DateTime.Today.Year, 1, 1).ToString("dd/MM/yyyy", Inv);
    public static string DiasAtrasBr(int n) => DateTime.Today.AddDays(-n).ToString("dd/MM/yyyy", Inv);

    public static string FmtData(string? iso, bool comHora = true)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        if (iso.Length >= 19 && DateTime.TryParseExact(iso[..19], "yyyy-MM-dd HH:mm:ss", Inv, DateTimeStyles.None, out var dt))
            return dt.ToString(comHora ? "dd/MM/yyyy HH:mm" : "dd/MM/yyyy", Inv);
        if (iso.Length >= 10 && DateTime.TryParseExact(iso[..10], "yyyy-MM-dd", Inv, DateTimeStyles.None, out var d))
            return d.ToString("dd/MM/yyyy", Inv);
        return iso;
    }

    /// <summary>"dd/mm/aaaa" (ou "aaaa-mm-dd") para "aaaa-mm-dd".</summary>
    public static string ParseDataBr(string? texto, string campo = "Data")
    {
        var t = (texto ?? "").Trim();
        if (t.Length == 0) throw new ValidacaoException($"Informe {campo}.");
        foreach (var f in new[] { "dd/MM/yyyy", "yyyy-MM-dd" })
            if (DateTime.TryParseExact(t, f, Inv, DateTimeStyles.None, out var d)) return d.ToString("yyyy-MM-dd", Inv);
        throw new ValidacaoException($"{campo} inválida: '{t}'. Use o formato dd/mm/aaaa.");
    }

    // ------------------------------------------------------------------ texto / busca
    public static string LikePadrao(string? busca)
    {
        var esc = (busca ?? "").Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{esc}%";
    }

    public static string LimparTexto(string? t) => string.Join(' ', (t ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    const string Com = "àáâãäåçèéêëìíîïñòóôõöùúûüýÿÀÁÂÃÄÅÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜÝ";
    const string Sem = "aaaaaaceeeeiiiinooooouuuuyyAAAAAACEEEEIIIINOOOOOUUUUY";

    /// <summary>Chave para ordenar alfabeticamente ignorando acentos e maiúsculas.</summary>
    public static string ChaveOrdenacao(string? t)
    {
        var sb = new StringBuilder((t ?? "").Length);
        foreach (var c in t ?? "")
        {
            int i = Com.IndexOf(c);
            sb.Append(char.ToLowerInvariant(i >= 0 ? Sem[i] : c));
        }
        return sb.ToString();
    }

    /// <summary>Ordena por nome ignorando acentos/caixa, calculando a chave uma única vez por item (ordenação estável).</summary>
    public static List<T> OrdenarPorNome<T>(List<T> itens, Func<T, string> nome) =>
        itens.OrderBy(x => ChaveOrdenacao(nome(x)), StringComparer.Ordinal).ToList();

    public static int ComparaNomes(string? a, string? b) => string.CompareOrdinal(ChaveOrdenacao(a), ChaveOrdenacao(b));

    // ------------------------------------------------------------------ CSV
    static string CelulaSegura(string v) => v.Length > 0 && "=+@\t\r".Contains(v[0]) ? "'" + v : v; // evita fórmulas no Excel

    /// <summary>Grava CSV (";" e UTF-8 com BOM: abre direto no Excel pt-BR). Retorna nº de linhas.</summary>
    public static int ExportarCsv(string caminho, IEnumerable<string> cabecalho, IEnumerable<IEnumerable<object?>> linhas)
    {
        static string Q(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        int n = 0;
        using var w = new StreamWriter(caminho, false, new UTF8Encoding(true)) { NewLine = "\r\n" };
        w.WriteLine(string.Join(';', cabecalho.Select(c => Q(c))));
        foreach (var linha in linhas)
        {
            w.WriteLine(string.Join(';', linha.Select(v => Q(CelulaSegura(Convert.ToString(v, Inv) ?? "")))));
            n++;
        }
        return n;
    }
}
