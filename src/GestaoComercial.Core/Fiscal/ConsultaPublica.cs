using System.Text.Json;

namespace GestaoComercial.Core.Fiscal;

public sealed record Endereco(string Cep, string Logradouro, string Complemento, string Bairro, string Municipio, string Uf, string CodMunicipio);

public sealed record DadosCnpj(string Cnpj, string RazaoSocial, string NomeFantasia, Endereco Endereco, string Numero, string Telefone, int? CrtSugerido);

/// <summary>Consultas gratuitas para preencher formulários: CEP (ViaCEP) e CNPJ (BrasilAPI). Precisam de internet.</summary>
public static class ConsultaPublica
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<Endereco> CepAsync(string cep)
    {
        var c = Util.SomenteDigitos(cep);
        if (c.Length != 8) throw new ValidacaoException("CEP deve ter 8 números.");
        return LerViaCep(await Baixar($"https://viacep.com.br/ws/{c}/json/", "o CEP"));
    }

    public static async Task<DadosCnpj> CnpjAsync(string cnpj)
    {
        var c = DocFiscal.Limpar(cnpj).ToUpperInvariant();
        if (!DocFiscal.CnpjValido(c)) throw new ValidacaoException("CNPJ inválido.");
        return LerBrasilApi(await Baixar($"https://brasilapi.com.br/api/cnpj/v1/{c}", "o CNPJ"));
    }

    static async Task<string> Baixar(string url, string oQue)
    {
        try
        {
            using var r = await Http.GetAsync(url);
            if (r.StatusCode == System.Net.HttpStatusCode.NotFound) throw new ValidacaoException($"Não encontrei {oQue}. Confira os números ou preencha à mão.");
            if (!r.IsSuccessStatusCode) throw new ValidacaoException($"O serviço que consulta {oQue} não respondeu agora ({(int)r.StatusCode}). Preencha à mão ou tente mais tarde.");
            return await r.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException) { throw new ValidacaoException($"Sem internet para consultar {oQue}. Preencha à mão."); }
        catch (TaskCanceledException) { throw new ValidacaoException($"A consulta {(oQue == "o CEP" ? "do CEP" : "do CNPJ")} demorou demais. Preencha à mão ou tente de novo."); }
    }

    static string S(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) ? v.ValueKind switch { JsonValueKind.String => v.GetString() ?? "", JsonValueKind.Number => v.GetRawText(), _ => "" } : "";

    public static Endereco LerViaCep(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("erro", out _)) throw new ValidacaoException("CEP não encontrado. Confira os números ou preencha o endereço à mão.");
        return new Endereco(Util.SomenteDigitos(S(r, "cep")), S(r, "logradouro"), S(r, "complemento"), S(r, "bairro"), S(r, "localidade"), S(r, "uf").ToUpperInvariant(), S(r, "ibge"));
    }

    public static DadosCnpj LerBrasilApi(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        var tipo = S(r, "descricao_tipo_de_logradouro").Trim();
        var log = S(r, "logradouro").Trim();
        if (tipo.Length > 0 && !log.StartsWith(tipo, StringComparison.OrdinalIgnoreCase)) log = $"{tipo} {log}";
        int? crt = r.TryGetProperty("opcao_pelo_mei", out var mei) && mei.ValueKind == JsonValueKind.True ? 4
                 : r.TryGetProperty("opcao_pelo_simples", out var sn) && sn.ValueKind == JsonValueKind.True ? 1
                 : r.TryGetProperty("opcao_pelo_simples", out var sn2) && sn2.ValueKind == JsonValueKind.False ? 3 : null;
        var end = new Endereco(Util.SomenteDigitos(S(r, "cep")), log, S(r, "complemento").Trim(), S(r, "bairro").Trim(), S(r, "municipio").Trim(),
                               S(r, "uf").ToUpperInvariant(), S(r, "codigo_municipio_ibge"));
        return new DadosCnpj(DocFiscal.Limpar(S(r, "cnpj")), S(r, "razao_social").Trim(), S(r, "nome_fantasia").Trim(), end, S(r, "numero").Trim(),
                             Util.SomenteDigitos(S(r, "ddd_telefone_1")), crt);
    }
}
