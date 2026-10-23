using System.Security.Cryptography.X509Certificates;
using DFe.Classes.Flags;
using DFe.Utils;
using NFe.Classes.Protocolo;
using NFe.Classes.Servicos.Tipos;
using NFe.Servicos;
using NFe.Utils;
using NFe.Utils.Excecoes;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Core.Fiscal;

/// <summary>Resposta da SEFAZ para uma nota (autorização ou consulta).</summary>
public sealed record RespostaSefaz(int CStat, string Motivo, string Protocolo = "", protNFe? Prot = null, DateTimeOffset? Recebido = null)
{
    /// <summary>100 = autorizada; 150 = autorizada fora do prazo.</summary>
    public bool Autorizada => CStat is 100 or 150;
    /// <summary>Uso denegado (o número fica gasto e a nota não vale).</summary>
    public bool Denegada => CStat is 110 or 301 or 302 or 303;
}

/// <summary>Comunicação com a SEFAZ. A implementação real usa a biblioteca ZeusFiscal; os testes usam uma falsa.</summary>
public interface ITransmissorSefaz
{
    RespostaSefaz Autorizar(NFeDoc nfe, ConfiguracaoServico conf, X509Certificate2 cert);
    RespostaSefaz Consultar(string chave, ConfiguracaoServico conf, X509Certificate2 cert);
    /// <summary>Cancelamento: devolve o XML do evento registrado (procEventoNFe) quando aceito.</summary>
    (RespostaSefaz Resposta, string XmlEvento) Cancelar(string chave, string protocolo, string justificativa, string cnpj, ConfiguracaoServico conf, X509Certificate2 cert);
    RespostaSefaz Inutilizar(string cnpj, int ano, int modelo, int serie, int numero, string justificativa, ConfiguracaoServico conf, X509Certificate2 cert);
    RespostaSefaz Status(ConfiguracaoServico conf, X509Certificate2 cert);
}

/// <summary>Sem internet, SEFAZ fora do ar, certificado recusado na conexão...</summary>
public sealed class SemComunicacaoException(string mensagem) : AppException(mensagem);

public sealed class TransmissorSefaz : ITransmissorSefaz
{
    static T Chamar<T>(Func<T> f)
    {
        try { return f(); }
        catch (ComunicacaoException ex) { throw new SemComunicacaoException("Sem comunicação com a SEFAZ. Confira a internet e tente de novo em alguns minutos.\n(" + ex.Message.Trim() + ")"); }
        catch (System.Net.WebException ex) { throw new SemComunicacaoException("Sem comunicação com a SEFAZ: " + ex.Message); }
        catch (System.Net.Http.HttpRequestException ex) { throw new SemComunicacaoException("Sem comunicação com a SEFAZ: " + ex.Message); }
        catch (TimeoutException) { throw new SemComunicacaoException("A SEFAZ demorou demais para responder. Tente de novo."); }
    }

    public RespostaSefaz Autorizar(NFeDoc nfe, ConfiguracaoServico conf, X509Certificate2 cert) => Chamar(() =>
    {
        using var s = new ServicosNFe(conf, cert);
        var ret = s.NFeAutorizacao(1, IndicadorSincronizacao.Sincrono, new List<NFeDoc> { nfe }).Retorno;
        if (ret.protNFe?.infProt is { } p1) return Resposta(ret.protNFe);
        if (ret.cStat == 103 && ret.infRec?.nRec is { Length: > 0 } recibo)
        {
            // lote recebido para processamento depois (alguns estados): pergunta o resultado algumas vezes
            for (int i = 0; i < 6; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Max(2, (double)ret.tMed)));
                var rec = s.NFeRetAutorizacao(recibo).Retorno;
                if (rec.protNFe?.FirstOrDefault() is { } p2) return Resposta(p2);
                if (rec.cStat != 105) return new RespostaSefaz(rec.cStat, rec.xMotivo);   // 105 = ainda em processamento
            }
            return new RespostaSefaz(105, "A SEFAZ ainda está processando a nota. Use “Consultar” daqui a pouco.");
        }
        return new RespostaSefaz(ret.cStat, ret.xMotivo);
    });

    public RespostaSefaz Consultar(string chave, ConfiguracaoServico conf, X509Certificate2 cert) => Chamar(() =>
    {
        using var s = new ServicosNFe(conf, cert);
        var ret = s.NfeConsultaProtocolo(chave).Retorno;
        if (ret.protNFe?.infProt != null) return Resposta(ret.protNFe);
        return new RespostaSefaz(ret.cStat, ret.xMotivo);
    });

    public (RespostaSefaz, string) Cancelar(string chave, string protocolo, string justificativa, string cnpj, ConfiguracaoServico conf, X509Certificate2 cert) => Chamar(() =>
    {
        using var s = new ServicosNFe(conf, cert);
        var ret = s.RecepcaoEventoCancelamento(1, 1, protocolo, chave, justificativa, cnpj, DateTimeOffset.Now);
        var inf = ret.Retorno.retEvento?.FirstOrDefault()?.infEvento;
        var xml = ret.ProcEventosNFe?.FirstOrDefault() is { } proc ? FuncoesXml.ClasseParaXmlString(proc) : "";
        return (inf != null ? new RespostaSefaz(inf.cStat, inf.xMotivo, inf.nProt ?? "") : new RespostaSefaz(ret.Retorno.cStat, ret.Retorno.xMotivo), xml);
    });

    public RespostaSefaz Inutilizar(string cnpj, int ano, int modelo, int serie, int numero, string justificativa, ConfiguracaoServico conf, X509Certificate2 cert) => Chamar(() =>
    {
        using var s = new ServicosNFe(conf, cert);
        var ret = s.NfeInutilizacao(cnpj, ano % 100, modelo == 65 ? ModeloDocumento.NFCe : ModeloDocumento.NFe, serie, numero, numero, justificativa).Retorno;
        return new RespostaSefaz(ret.infInut.cStat, ret.infInut.xMotivo, ret.infInut.nProt ?? "");
    });

    public RespostaSefaz Status(ConfiguracaoServico conf, X509Certificate2 cert) => Chamar(() =>
    {
        using var s = new ServicosNFe(conf, cert);
        var ret = s.NfeStatusServico().Retorno;
        return new RespostaSefaz(ret.cStat, ret.xMotivo);
    });

    static RespostaSefaz Resposta(protNFe p) =>
        new(p.infProt.cStat, p.infProt.xMotivo, p.infProt.nProt ?? "", p, p.infProt.dhRecbto);
}
