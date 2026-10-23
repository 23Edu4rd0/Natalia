using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace GestaoComercial.Core.Fiscal;

/// <summary>
/// Tudo que a loja precisa informar uma vez para emitir NF-e/NFC-e: dados do emitente, certificado digital,
/// CSC (NFC-e), numeração e a tributação padrão. Fica guardado (em JSON) na tabela de configurações.
/// </summary>
public sealed class ConfigFiscal
{
    // ---- emitente
    public string Cnpj { get; set; } = "";
    public string RazaoSocial { get; set; } = "";
    public string NomeFantasia { get; set; } = "";
    public string IE { get; set; } = "";
    /// <summary>1 = Simples Nacional, 2 = Simples (excesso de sublimite), 3 = Regime normal, 4 = MEI.</summary>
    public int Crt { get; set; } = 1;
    public string Logradouro { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Complemento { get; set; } = "";
    public string Bairro { get; set; } = "";
    public string CodMunicipio { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Cep { get; set; } = "";
    public string Telefone { get; set; } = "";

    // ---- ambiente e numeração
    /// <summary>1 = Produção (vale de verdade), 2 = Homologação (teste, sem valor).</summary>
    public int Ambiente { get; set; } = 2;
    public int SerieNfce { get; set; } = 1;
    public int SerieNfe { get; set; } = 1;

    // ---- NFC-e
    public string CscId { get; set; } = "";
    public string Csc { get; set; } = "";
    /// <summary>2 = QR Code com CSC (padrão); 3 = QR Code versão 3 (sem CSC, quando a SEFAZ do estado já exigir).</summary>
    public int VersaoQrCode { get; set; } = 2;

    // ---- certificado digital A1 (arquivo .pfx copiado para a pasta de dados)
    public string CertificadoArquivo { get; set; } = "";
    /// <summary>A senha não vai para este JSON: fica protegida à parte (ver <see cref="Segredo"/>).</summary>
    [System.Text.Json.Serialization.JsonIgnore] public string CertificadoSenha { get; set; } = "";

    // ---- tributação padrão (cada produto pode ter a sua)
    public string NaturezaOperacao { get; set; } = "VENDA DE MERCADORIA";
    public string CfopPadrao { get; set; } = "5102";
    /// <summary>CSOSN (Simples/MEI: 102, 103, 300, 400, 500) ou CST (regime normal: 00, 40, 41, 60).</summary>
    public string TributacaoPadrao { get; set; } = "102";
    public decimal AliquotaIcms { get; set; }
    /// <summary>CST de PIS/COFINS no regime normal (01, 04, 06, 07, 08, 09, 49, 99). Simples/MEI usa 99 sem valores.</summary>
    public string CstPisCofins { get; set; } = "99";
    public decimal AliquotaPis { get; set; }
    public decimal AliquotaCofins { get; set; }
    /// <summary>Percentual aproximado de tributos (Lei 12.741/2012, tabela IBPT). 0 = não informar.</summary>
    public decimal PercentualTributos { get; set; }
    /// <summary>Pix recebido por chave/QR fixo (20 = estático) ou pelo QR da maquininha/banco a cada venda (17 = dinâmico).</summary>
    public string TipoPix { get; set; } = "20";
    public string InfoComplementar { get; set; } = "";

    // ---- responsável técnico (exigido por várias SEFAZ): quem fez/mantém o programa
    public string RespTecCnpj { get; set; } = "";
    public string RespTecContato { get; set; } = "";
    public string RespTecEmail { get; set; } = "";
    public string RespTecFone { get; set; } = "";

    public bool Simples => Crt is 1 or 4;
    public bool Producao => Ambiente == 1;

    public ConfigFiscal Copia()
    {
        var x = JsonSerializer.Deserialize<ConfigFiscal>(JsonSerializer.Serialize(this))!;
        x.CertificadoSenha = CertificadoSenha;
        return x;
    }

    /// <summary>
    /// O que ainda falta configurar para emitir. Lista vazia = pronto.
    /// <paramref name="modelo"/> 65 também exige o CSC (QR Code v2).
    /// </summary>
    public List<string> Pendencias(int modelo, bool verificarCertificado = true)
    {
        var p = new List<string>();
        if (!DocFiscal.CnpjValido(Cnpj)) p.Add("CNPJ da loja inválido ou não informado.");
        if (RazaoSocial.Trim().Length < 2) p.Add("Razão social da loja não informada.");
        if (!DocFiscal.IeValida(IE)) p.Add("Inscrição estadual (IE) da loja não informada (use só números).");
        if (Crt is < 1 or > 4) p.Add("Regime tributário não escolhido.");
        if (Logradouro.Trim().Length == 0 || Numero.Trim().Length == 0 || Bairro.Trim().Length == 0) p.Add("Endereço da loja incompleto (rua, número e bairro).");
        if (!Ufs.Codigos.ContainsKey(Uf)) p.Add("Estado (UF) da loja não informado.");
        else if (!DocFiscal.CodMunicipioValido(CodMunicipio, Uf)) p.Add("Código IBGE da cidade da loja inválido (7 números; use “Buscar CEP” para preencher).");
        if (Municipio.Trim().Length == 0) p.Add("Cidade da loja não informada.");
        if (Util.SomenteDigitos(Cep).Length != 8) p.Add("CEP da loja inválido.");
        if (CertificadoArquivo.Length == 0 || !File.Exists(CertificadoArquivo)) p.Add("Certificado digital A1 (.pfx) não escolhido.");
        else if (verificarCertificado && ProblemaCertificado(CertificadoArquivo) is string erroCert) p.Add(erroCert);
        if (modelo == 65 && VersaoQrCode == 2 && (CscId.Trim().Length == 0 || Csc.Trim().Length == 0))
            p.Add("CSC da NFC-e (código e identificador) não informado. Ele é gerado no site da SEFAZ do seu estado.");
        if (!Tributacao.Validas(Crt).Contains(TributacaoPadrao)) p.Add("Tributação padrão (CSOSN/CST) não combina com o regime escolhido.");
        if (!DocFiscal.CfopValido(CfopPadrao)) p.Add("CFOP padrão inválido (ex.: 5102).");
        return p;
    }

    // ------------------------------------------------------------------ persistência
    const string Chave = "fiscal", ChaveSenha = "fiscal_cert_senha";

    public static ConfigFiscal Carregar(Servico svc)
    {
        var json = svc.ObterConfig(Chave);
        ConfigFiscal c;
        try { c = json.Length > 0 ? JsonSerializer.Deserialize<ConfigFiscal>(json) ?? new() : new(); }
        catch (JsonException) { c = new(); }
        c.CertificadoSenha = Segredo.Abrir(svc.ObterConfig(ChaveSenha));
        return c;
    }

    public void Salvar(Servico svc)
    {
        svc.SalvarConfig(Chave, JsonSerializer.Serialize(this));
        svc.SalvarConfig(ChaveSenha, Segredo.Proteger(CertificadoSenha));
    }

    /// <summary>Abre o certificado A1. Erros viram mensagens claras (senha errada, arquivo danificado).</summary>
    public static X509Certificate2 AbrirCertificado(string arquivo, string senha)
    {
        if (!File.Exists(arquivo)) throw new ValidacaoException("O arquivo do certificado digital não foi encontrado. Escolha o .pfx de novo.");
        try
        {
            var flags = OperatingSystem.IsWindows()
                ? X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
                : X509KeyStorageFlags.Exportable;
            var cert = new X509Certificate2(File.ReadAllBytes(arquivo), senha, flags);
            if (!cert.HasPrivateKey) throw new ValidacaoException("Este arquivo não tem a chave privada. Use o certificado A1 completo (.pfx com senha).");
            return cert;
        }
        catch (CryptographicException)
        {
            throw new ValidacaoException("Não foi possível abrir o certificado: a senha está errada ou o arquivo não é um certificado A1 (.pfx).");
        }
    }

    /// <summary>Problema do certificado configurado (senha, validade), ou null se está tudo certo.</summary>
    public string? ProblemaCertificado(string arquivo)
    {
        try
        {
            using var c = AbrirCertificado(arquivo, CertificadoSenha);
            if (c.NotAfter < DateTime.Now) return $"O certificado digital venceu em {c.NotAfter:dd/MM/yyyy}. Renove com a sua certificadora.";
            return null;
        }
        catch (ValidacaoException ex) { return ex.Message; }
    }
}

/// <summary>Guarda a senha do certificado de forma protegida (Windows: DPAPI do usuário; outros sistemas: cifrada com chave da máquina).</summary>
public static class Segredo
{
    static readonly byte[] Entropia = Encoding.UTF8.GetBytes("GestaoComercial.Fiscal.v1");

    public static string Proteger(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        var dados = Encoding.UTF8.GetBytes(texto);
        if (OperatingSystem.IsWindows())
            return "dp:" + Convert.ToBase64String(ProtectedData.Protect(dados, Entropia, DataProtectionScope.CurrentUser));
        using var aes = Aes.Create();
        aes.Key = ChaveMaquina();
        aes.GenerateIV();
        var cifra = aes.EncryptCbc(dados, aes.IV);
        return "ae:" + Convert.ToBase64String(aes.IV.Concat(cifra).ToArray());
    }

    public static string Abrir(string guardado)
    {
        try
        {
            if (guardado.StartsWith("dp:") && OperatingSystem.IsWindows())
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(guardado[3..]), Entropia, DataProtectionScope.CurrentUser));
            if (guardado.StartsWith("ae:"))
            {
                var tudo = Convert.FromBase64String(guardado[3..]);
                using var aes = Aes.Create();
                aes.Key = ChaveMaquina();
                return Encoding.UTF8.GetString(aes.DecryptCbc(tudo[16..], tudo[..16]));
            }
        }
        catch (CryptographicException) { /* outra máquina/usuário: pede a senha de novo */ }
        catch (FormatException) { }
        return "";
    }

    static byte[] ChaveMaquina()
    {
        string id = Environment.MachineName + "|" + Environment.UserName;
        foreach (var f in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            try { if (File.Exists(f)) { id += "|" + File.ReadAllText(f).Trim(); break; } } catch (IOException) { }
        return SHA256.HashData(Encoding.UTF8.GetBytes(id + "|GestaoComercial"));
    }
}

/// <summary>Códigos IBGE dos estados (cUF).</summary>
public static class Ufs
{
    public static readonly IReadOnlyDictionary<string, int> Codigos = new Dictionary<string, int>
    {
        ["AC"] = 12, ["AL"] = 27, ["AP"] = 16, ["AM"] = 13, ["BA"] = 29, ["CE"] = 23, ["DF"] = 53, ["ES"] = 32, ["GO"] = 52,
        ["MA"] = 21, ["MT"] = 51, ["MS"] = 50, ["MG"] = 31, ["PA"] = 15, ["PB"] = 25, ["PR"] = 41, ["PE"] = 26, ["PI"] = 22,
        ["RJ"] = 33, ["RN"] = 24, ["RS"] = 43, ["RO"] = 11, ["RR"] = 14, ["SC"] = 42, ["SP"] = 35, ["SE"] = 28, ["TO"] = 17,
    };
    public static IReadOnlyList<string> Siglas => Codigos.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();
}

/// <summary>Opções de tributação por regime.</summary>
public static class Tributacao
{
    public static readonly (string Codigo, string Descricao)[] Simples =
    {
        ("102", "102 – Tributada pelo Simples sem permissão de crédito (mais comum)"),
        ("103", "103 – Isenção do ICMS no Simples para faixa de receita"),
        ("300", "300 – Imune"),
        ("400", "400 – Não tributada pelo Simples"),
        ("500", "500 – ICMS já cobrado antes por substituição tributária"),
    };

    public static readonly (string Codigo, string Descricao)[] Normal =
    {
        ("00", "00 – Tributada integralmente (usa a alíquota de ICMS)"),
        ("40", "40 – Isenta"),
        ("41", "41 – Não tributada"),
        ("60", "60 – ICMS já cobrado antes por substituição tributária"),
    };

    public static (string Codigo, string Descricao)[] Opcoes(int crt) => crt is 1 or 4 ? Simples : Normal;
    public static HashSet<string> Validas(int crt) => Opcoes(crt).Select(o => o.Codigo).ToHashSet();
}

/// <summary>Validações de documentos e códigos usados na nota.</summary>
public static class DocFiscal
{
    /// <summary>CNPJ numérico ou alfanumérico (novo formato a partir de julho/2026).</summary>
    public static bool CnpjValido(string? cnpj)
    {
        var c = Limpar(cnpj).ToUpperInvariant();
        if (c.Length != 14 || !c[..12].All(ch => char.IsAsciiDigit(ch) || char.IsAsciiLetterUpper(ch)) || !c[12..].All(char.IsAsciiDigit)) return false;
        if (c.All(char.IsAsciiDigit)) return Util.ValidaCnpj(c);
        static int Dv(string b)
        {
            int[] pesos = b.Length == 12 ? new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 } : new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int soma = 0;
            for (int i = 0; i < b.Length; i++) soma += (b[i] - '0') * pesos[i];
            var r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }
        return Dv(c[..12]) == c[12] - '0' && Dv(c[..13]) == c[13] - '0';
    }

    /// <summary>Tira pontuação de CNPJ/CPF mantendo letras (CNPJ alfanumérico).</summary>
    public static string Limpar(string? doc) => new((doc ?? "").Where(char.IsAsciiLetterOrDigit).ToArray());

    public static bool IeValida(string? ie)
    {
        var t = (ie ?? "").Trim().ToUpperInvariant();
        if (t == "ISENTO") return true;
        var d = Util.SomenteDigitos(t);
        return d.Length is >= 2 and <= 14 && d.Length == Limpar(t).Length;
    }

    public static bool NcmValido(string? ncm) => Util.SomenteDigitos(ncm).Length == 8 && Util.SomenteDigitos(ncm) == (ncm ?? "").Trim().Replace(".", "");
    public static bool CfopValido(string? cfop) => Util.SomenteDigitos(cfop) is { Length: 4 } d && d[0] is '5' or '6' or '7';
    public static bool CestValido(string? cest) => (cest ?? "").Trim().Length == 0 || Util.SomenteDigitos(cest).Length == 7;

    public static bool CodMunicipioValido(string? cod, string uf) =>
        Util.SomenteDigitos(cod) is { Length: 7 } d && Ufs.Codigos.TryGetValue(uf, out var cuf) && d.StartsWith(cuf.ToString());

    /// <summary>GTIN (EAN-8/12/13/14) com dígito verificador correto; senão a nota vai como "SEM GTIN".</summary>
    public static bool GtinValido(string? codigo)
    {
        var d = (codigo ?? "").Trim();
        if (d.Length is not (8 or 12 or 13 or 14) || !d.All(char.IsAsciiDigit)) return false;
        int soma = 0;
        for (int i = d.Length - 2, peso = 3; i >= 0; i--, peso = peso == 3 ? 1 : 3) soma += (d[i] - '0') * peso;
        return (10 - soma % 10) % 10 == d[^1] - '0';
    }
}
