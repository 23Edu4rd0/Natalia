namespace GestaoComercial.Core.Fiscal;

/// <summary>Um produto da venda com os dados fiscais que vão para a nota (o usuário completa o que faltar).</summary>
public sealed class ItemNota
{
    public long ProdutoId { get; init; }
    public string Codigo { get; init; } = "";
    public string Descricao { get; init; } = "";
    public string Unidade { get; init; } = "UN";
    public long Quantidade { get; init; }
    public long PrecoUnit { get; init; }
    public long Total => Quantidade * PrecoUnit;
    public string Ncm { get; set; } = "";
    public string Cest { get; set; } = "";
    public string Cfop { get; set; } = "";
    public int Origem { get; set; }
    public string Tributacao { get; set; } = "";
}

/// <summary>
/// Rascunho da nota de uma venda: vem pré-preenchido (cliente, produtos, padrões da configuração)
/// e a tela deixa o usuário completar/corrigir antes de emitir.
/// </summary>
public sealed class DadosNota
{
    public long VendaId { get; init; }
    /// <summary>65 = NFC-e (consumidor no balcão), 55 = NF-e (empresa, outro estado, entrega).</summary>
    public int Modelo { get; set; } = 65;
    public long? ClienteId { get; set; }
    public string DestDocumento { get; set; } = "";
    public string DestNome { get; set; } = "";
    public string DestEmail { get; set; } = "";
    /// <summary>1 = contribuinte do ICMS (tem IE), 2 = isento de IE, 9 = não contribuinte (pessoa física, a maioria).</summary>
    public int DestIndIE { get; set; } = 9;
    public string DestIE { get; set; } = "";
    public string DestLogradouro { get; set; } = "";
    public string DestNumero { get; set; } = "";
    public string DestComplemento { get; set; } = "";
    public string DestBairro { get; set; } = "";
    public string DestCodMunicipio { get; set; } = "";
    public string DestMunicipio { get; set; } = "";
    public string DestUf { get; set; } = "";
    public string DestCep { get; set; } = "";
    public List<ItemNota> Itens { get; set; } = new();
    public long Desconto { get; init; }
    public long Total { get; init; }
    public string FormaPagamento { get; init; } = "Dinheiro";
    public string InfoComplementar { get; set; } = "";
    /// <summary>Guarda NCM/CFOP/tributação digitados no cadastro dos produtos (e o endereço no cadastro do cliente).</summary>
    public bool SalvarNosCadastros { get; set; } = true;

    public bool TemDestinatario => DocFiscal.Limpar(DestDocumento).Length > 0;
    public bool DestCnpj => DocFiscal.Limpar(DestDocumento).Length == 14;
}

/// <summary>Algo que precisa ser preenchido/corrigido. <see cref="Campo"/> diz onde (ex.: "dest.doc", "item:2:ncm", "config").</summary>
public sealed record Pendencia(string Campo, string Mensagem);

/// <summary>Resultado de uma emissão.</summary>
public sealed record ResultadoNota(long NotaId, string Status, string Mensagem, string Chave)
{
    public bool Autorizada => Status == "AUTORIZADA";
    /// <summary>Emitida sem internet (contingência offline): vale o cupom; vai para a SEFAZ depois.</summary>
    public bool Contingencia => Status == "CONTINGENCIA";
}

/// <summary>Entrada em contingência offline da NFC-e: quando começou e o motivo (vai na nota).</summary>
public sealed record Contingencia(DateTimeOffset Inicio, string Justificativa);

/// <summary>Resultado do envio das notas guardadas (contingência) e da conferência das tentativas sem resposta.</summary>
public sealed record ResultadoEnvio(int Autorizadas, int Rejeitadas, int Restantes, bool SemComunicacao, string Mensagem)
{
    public bool NadaAFazer => Autorizadas == 0 && Rejeitadas == 0 && Restantes == 0 && !SemComunicacao && Mensagem.Length == 0;
}

public static class RegrasNota
{
    /// <summary>Confere o rascunho e devolve tudo o que falta, de uma vez (para a tela mostrar os campos em vermelho).</summary>
    public static List<Pendencia> Verificar(DadosNota d, ConfigFiscal cfg, bool verificarCertificado = true)
    {
        var p = cfg.Pendencias(d.Modelo, verificarCertificado).Select(m => new Pendencia("config", m)).ToList();
        var doc = DocFiscal.Limpar(d.DestDocumento);
        if (doc.Length > 0)
        {
            var ok = doc.Length == 11 ? Util.ValidaCpf(doc) : doc.Length == 14 && DocFiscal.CnpjValido(doc);
            if (!ok) p.Add(new("dest.doc", "CPF/CNPJ do cliente inválido."));
        }
        if (d.Modelo == 55)
        {
            if (doc.Length == 0) p.Add(new("dest.doc", "NF-e precisa do CPF ou CNPJ do cliente."));
            if (d.DestNome.Trim().Length < 2) p.Add(new("dest.nome", "NF-e precisa do nome do cliente."));
            if (d.DestLogradouro.Trim().Length == 0) p.Add(new("dest.logradouro", "Informe a rua do cliente."));
            if (d.DestNumero.Trim().Length == 0) p.Add(new("dest.numero", "Informe o número do endereço do cliente (ou S/N)."));
            if (d.DestBairro.Trim().Length == 0) p.Add(new("dest.bairro", "Informe o bairro do cliente."));
            if (!Ufs.Codigos.ContainsKey(d.DestUf)) p.Add(new("dest.uf", "Informe o estado (UF) do cliente."));
            else if (!DocFiscal.CodMunicipioValido(d.DestCodMunicipio, d.DestUf)) p.Add(new("dest.cmun", "Código IBGE da cidade do cliente inválido (use “Buscar CEP”)."));
            if (d.DestMunicipio.Trim().Length == 0) p.Add(new("dest.municipio", "Informe a cidade do cliente."));
            if (d.DestCep.Trim().Length > 0 && Util.SomenteDigitos(d.DestCep).Length != 8) p.Add(new("dest.cep", "CEP do cliente inválido."));
            if (d.DestIndIE == 1 && !DocFiscal.IeValida(d.DestIE)) p.Add(new("dest.ie", "Cliente contribuinte do ICMS: informe a inscrição estadual."));
            if (d.DestIndIE == 1 && doc.Length != 14) p.Add(new("dest.ie", "Só empresa (CNPJ) pode ser contribuinte do ICMS."));
            var outroEstado = Ufs.Codigos.ContainsKey(d.DestUf) && d.DestUf != cfg.Uf;
            if (outroEstado && !cfg.Simples && d.DestIndIE == 9)
                p.Add(new("dest.uf", "Venda para consumidor de outro estado no regime normal exige o cálculo do DIFAL, que este programa não faz. Peça orientação ao seu contador."));
        }
        if (d.Itens.Count == 0) p.Add(new("itens", "A venda não tem produtos."));
        var validas = Tributacao.Validas(cfg.Crt);
        for (int i = 0; i < d.Itens.Count; i++)
        {
            var it = d.Itens[i];
            if (!DocFiscal.NcmValido(it.Ncm)) p.Add(new($"item:{i}:ncm", $"“{it.Descricao}”: falta o NCM (código de 8 números da classificação do produto)."));
            if (!DocFiscal.CestValido(it.Cest)) p.Add(new($"item:{i}:cest", $"“{it.Descricao}”: CEST deve ter 7 números (ou ficar em branco)."));
            if (!DocFiscal.CfopValido(it.Cfop)) p.Add(new($"item:{i}:cfop", $"“{it.Descricao}”: CFOP inválido (ex.: 5102)."));
            if (!validas.Contains(it.Tributacao)) p.Add(new($"item:{i}:trib", $"“{it.Descricao}”: tributação {it.Tributacao} não combina com o regime da loja."));
            if (it.Origem is < 0 or > 8) p.Add(new($"item:{i}:origem", $"“{it.Descricao}”: origem da mercadoria inválida."));
        }
        return p;
    }

    /// <summary>CFOP da venda: dentro do estado 5xxx; para outro estado 6xxx (6108 quando o cliente não é contribuinte).</summary>
    public static string AjustarCfop(string cfop, bool outroEstado, bool contribuinte)
    {
        var d = Util.SomenteDigitos(cfop);
        if (d.Length != 4) return d;
        if (!outroEstado) return d[0] == '6' ? "5" + d[1..] : d;
        if (d is "5102" or "6102" or "6108") return contribuinte ? "6102" : "6108";
        return d[0] == '5' ? "6" + d[1..] : d;
    }
}
