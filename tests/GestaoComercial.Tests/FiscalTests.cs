using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DFe.Classes.Flags;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;
using NFe.Classes.Servicos.Autorizacao;
using NFe.Classes.Servicos.Tipos;
using NFe.Utils;
using NFe.Utils.Autorizacao;
using NFe.Utils.Validacao;
using Xunit;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Tests;

/// <summary>SEFAZ de mentira: responde como a real e confere o lote nos esquemas oficiais antes, como a biblioteca faz.</summary>
public sealed class SefazFalsa : ITransmissorSefaz
{
    public Func<NFeDoc, RespostaSefaz> AoAutorizar { get; set; } = nfe => Aprovar(nfe);
    public Func<string, RespostaSefaz> AoConsultar { get; set; } = _ => new RespostaSefaz(217, "Rejeição: NF-e não consta na base de dados da SEFAZ");
    public bool SemInternet { get; set; }
    public int Envios, Consultas, Cancelamentos, Inutilizacoes, Tentativas;
    /// <summary>Chave da NFC-e substituta no último cancelamento por substituição (110112).</summary>
    public string? UltimaSubstituta;
    public List<int> NumerosInutilizados { get; } = new();
    public NFeDoc? Ultima;
    public string UltimoLote = "";

    public static RespostaSefaz Aprovar(NFeDoc nfe, int cStat = 100)
    {
        var prot = new NFe.Classes.Protocolo.protNFe
        {
            versao = "4.00",
            infProt = new NFe.Classes.Protocolo.infProt
            {
                tpAmb = TipoAmbiente.Homologacao, verAplic = "SP_NFE_PL009_V4", chNFe = nfe.infNFe.Id[3..], dhRecbto = DateTimeOffset.Now,
                nProt = "135260000012345", digVal = nfe.Signature.SignedInfo.Reference.DigestValue, cStat = cStat, xMotivo = "Autorizado o uso da NF-e",
            },
        };
        return new RespostaSefaz(cStat, "Autorizado o uso da NF-e", "135260000012345", prot, DateTimeOffset.Now);
    }

    void Rede() { if (SemInternet) throw new SemComunicacaoException("Sem comunicação com a SEFAZ (teste)."); }

    public RespostaSefaz Autorizar(NFeDoc nfe, ConfiguracaoServico conf, X509Certificate2 cert)
    {
        var lote = new enviNFe4("4.00", 1, IndicadorSincronizacao.Sincrono, new List<NFeDoc> { nfe }).ObterXmlString();
        Validador.Valida(ServicoNFe.NFeAutorizacao, VersaoServico.Versao400, lote, true, conf);   // como a biblioteca faz antes de enviar
        UltimoLote = lote;
        Tentativas++;
        Rede();
        Envios++; Ultima = nfe;
        return AoAutorizar(nfe);
    }

    public RespostaSefaz Consultar(string chave, ConfiguracaoServico conf, X509Certificate2 cert) { Rede(); Consultas++; return AoConsultar(chave); }

    public (RespostaSefaz, string) Cancelar(string chave, string protocolo, string justificativa, string cnpj, ConfiguracaoServico conf, X509Certificate2 cert,
                                            string? chaveSubstituta = null)
    {
        Rede(); Cancelamentos++; UltimaSubstituta = chaveSubstituta;
        return (new RespostaSefaz(135, "Evento registrado e vinculado a NF-e", "135260000099999"), $"<procEventoNFe><chNFe>{chave}</chNFe><xJust>{justificativa}</xJust></procEventoNFe>");
    }

    public RespostaSefaz Inutilizar(string cnpj, int ano, int modelo, int serie, int numero, string justificativa, ConfiguracaoServico conf, X509Certificate2 cert)
    { Rede(); Inutilizacoes++; NumerosInutilizados.Add(numero); return new RespostaSefaz(102, "Inutilização de número homologado"); }

    public RespostaSefaz Status(ConfiguracaoServico conf, X509Certificate2 cert) { Rede(); return new RespostaSefaz(107, "Serviço em Operação"); }
}

public class FiscalTests : BaseTeste
{
    readonly SefazFalsa sefaz = new();

    public FiscalTests()
    {
        EmissorNFe.PastaBaseSchemas = Path.Combine(Path.GetTempPath(), "gc-schemas-teste");
        S.PastaFiscal = Path.Combine(Pasta, "notas-fiscais");
        S.Transmissor = sefaz;
    }

    /// <summary>Certificado A1 autoassinado (só serve para testar a assinatura; a SEFAZ real recusaria).</summary>
    internal static string CriarCertificado(string pasta, string senha = "1234")
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=LOJA TESTE LTDA:11222333000181", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
        var arq = Path.Combine(pasta, "certificado-teste.pfx");
        File.WriteAllBytes(arq, cert.Export(X509ContentType.Pfx, senha));
        return arq;
    }

    internal static ConfigFiscal ConfigCompleta(string pasta, int crt = 1) => new()
    {
        Cnpj = "11.222.333/0001-81", RazaoSocial = "Natália Modas Ltda", NomeFantasia = "Natália Modas", IE = "111.111.111.111", Crt = crt,
        Logradouro = "Rua das Flores", Numero = "123", Bairro = "Centro", CodMunicipio = "3550308", Municipio = "São Paulo", Uf = "SP", Cep = "01001-000",
        Telefone = "(11) 98888-7777", Ambiente = 2, CscId = "000001", Csc = "0123456789ABCDEF0123456789ABCDEF",
        CertificadoArquivo = CriarCertificado(pasta), CertificadoSenha = "1234",
        TributacaoPadrao = crt is 1 or 4 ? "102" : "00", AliquotaIcms = crt is 1 or 4 ? 0 : 18, CstPisCofins = crt is 1 or 4 ? "99" : "01",
        AliquotaPis = crt is 1 or 4 ? 0 : 0.65m, AliquotaCofins = crt is 1 or 4 ? 0 : 3m, PercentualTributos = 8.5m,
        RespTecCnpj = "11222333000181", RespTecContato = "Suporte", RespTecEmail = "suporte@exemplo.com", RespTecFone = "11999990000",
    };

    long Venda(string forma = "Dinheiro", long desconto = 0, long? cliente = null, string ncm = "61091000")
    {
        var p1 = S.SalvarProduto(new ProdutoDados("Blusa de alcinha", S.ObterProdutoPorCodigo("7891000100011") == null ? "7891000100011" : null, PrecoVenda: 5990, EstoqueInicial: 10));
        var p2 = S.SalvarProduto(new ProdutoDados("Calça jeans", null, PrecoVenda: 12990, EstoqueInicial: 10));
        S.SalvarFiscalProduto(new FiscalProduto(p1, ncm, "", 0, "", ""));
        S.SalvarFiscalProduto(new FiscalProduto(p2, ncm.Length > 0 ? "62034200" : "", "", 0, "", ""));
        return S.RegistrarVenda(new[] { new ItemVendaInput(p1, 2, 5990), new ItemVendaInput(p2, 1, 12990) }, cliente, desconto, forma);
    }

    void Configurar(int crt = 1, bool contingencia = true)
    {
        var c = ConfigCompleta(Pasta, crt); c.ContingenciaAutomatica = contingencia;
        S.SalvarConfigFiscal(c);
    }

    // ------------------------------------------------------------------ configuração
    [Fact]
    public void Config_fiscal_salva_e_le_com_a_senha_protegida()
    {
        Configurar();
        var c = S.ObterConfigFiscal();
        Assert.Equal(("11222333000181", "SP", "1234"), (c.Cnpj, c.Uf, c.CertificadoSenha));
        Assert.DoesNotContain("1234\"", S.ObterConfig("fiscal"));                  // a senha não fica no JSON
        Assert.DoesNotContain("1234", S.ObterConfig("fiscal_cert_senha"));        // nem em texto puro
        Assert.Empty(c.Pendencias(65));
        c.Csc = ""; Assert.Contains(c.Pendencias(65), m => m.Contains("CSC"));    // NFC-e precisa do CSC...
        Assert.DoesNotContain(c.Pendencias(55), m => m.Contains("CSC"));          // ...NF-e não
        c.CertificadoSenha = "errada"; Assert.Contains(c.Pendencias(55), m => m.Contains("senha"));
    }

    [Fact]
    public void Sem_configuracao_lista_tudo_que_falta()
    {
        var v = Venda();
        var ex = Assert.Throws<PendenciasException>(() => S.EmitirNota(S.PrepararNota(v)));
        Assert.Contains(ex.Pendencias, p => p.Mensagem.Contains("CNPJ"));
        Assert.Contains(ex.Pendencias, p => p.Mensagem.Contains("Certificado"));
        Assert.Equal(0, sefaz.Envios);
        Assert.Empty(S.ListarNotasFiscais());                                      // nada foi reservado
    }

    // ------------------------------------------------------------------ emissão
    [Fact]
    public void NFCe_autorizada_guarda_xml_protocolo_e_avanca_a_numeracao()
    {
        Configurar();
        var v = Venda(desconto: 1000);
        var d = S.PrepararNota(v);
        Assert.Equal(("61091000", "5102", "102"), (d.Itens[0].Ncm, d.Itens[0].Cfop, d.Itens[0].Tributacao));
        var r = S.EmitirNota(d);
        Assert.True(r.Autorizada, r.Mensagem);
        var n = S.ObterNotaFiscal(r.NotaId);
        Assert.Equal(("AUTORIZADA", 65, 1L, "135260000012345"), (n.Status, n.Modelo, n.Numero, n.Protocolo));
        Assert.StartsWith("<?xml", n.Xml); Assert.Contains("<nfeProc", n.Xml); Assert.Contains("<protNFe", n.Xml);
        Assert.Equal(44, n.Chave.Length);
        Assert.Contains("/qrcode?p=" + n.Chave + "|2|2|1|", n.Xml.Replace("&amp;", "&"));   // QR Code v2 com CSC
        Assert.Equal(2, S.ProximoNumeroNota(65));
        Assert.True(File.Exists(Directory.GetFiles(S.PastaFiscal!, "*procNFe.xml", SearchOption.AllDirectories).Single()));

        // desconto rateado: a soma dos itens bate com a venda
        var nfe = sefaz.Ultima!;
        Assert.Equal(10.00m, nfe.infNFe.det.Sum(x => x.prod.vDesc ?? 0));
        Assert.Equal(S.ObterVenda(v).Total / 100m, nfe.infNFe.total.ICMSTot.vNF);
        Assert.Equal(EmissorNFe.TextoHomologacao, nfe.infNFe.det[0].prod.xProd);  // exigência da homologação
        Assert.Equal("7891000100011", nfe.infNFe.det[0].prod.cEAN);                // código de barras válido vira GTIN
        Assert.Equal("SEM GTIN", nfe.infNFe.det[1].prod.cEAN);
        Assert.Null(nfe.infNFe.dest);                                              // consumidor não identificado
        Assert.NotNull(nfe.infNFe.infRespTec);
        Assert.Contains("SIMPLES NACIONAL", nfe.infNFe.infAdic!.infCpl);
        Assert.True(nfe.infNFe.total.ICMSTot.vTotTrib > 0);

        // DANFE (cupom)
        var html = Danfe.Html(n);
        Assert.Contains("DANFE NFC-e", html);
        Assert.Contains(Danfe.ChaveFormatada(n.Chave), html);
        Assert.Contains("data:image/png;base64,", html);                           // QR Code desenhado
        Assert.Contains("HOMOLOGAÇÃO", html);
        Assert.Contains("CONSUMIDOR NÃO IDENTIFICADO", html);

        // a mesma venda não ganha segunda nota
        Assert.Throws<ValidacaoException>(() => S.EmitirNota(S.PrepararNota(v)));
    }

    [Fact]
    public void Produto_sem_NCM_aparece_como_pendencia_e_o_que_o_usuario_digita_fica_salvo()
    {
        Configurar();
        var v = Venda(ncm: "");
        var d = S.PrepararNota(v);
        var ex = Assert.Throws<PendenciasException>(() => S.EmitirNota(d));
        Assert.Equal(new[] { "item:0:ncm", "item:1:ncm" }, ex.Pendencias.Select(p => p.Campo));
        d.Itens[0].Ncm = "6109.10.00"; d.Itens[1].Ncm = "62034200";
        Assert.True(S.EmitirNota(d).Autorizada);
        Assert.Equal("61091000", S.ObterFiscalProduto(d.Itens[0].ProdutoId).Ncm);   // da próxima vez já vem preenchido
    }

    [Fact]
    public void Cliente_com_CPF_na_NFCe()
    {
        Configurar();
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria Silva", "529.982.247-25", Email: "maria@email.com"));
        var v = Venda("Pix", cliente: c);
        var d = S.PrepararNota(v);
        Assert.Equal("529.982.247-25", d.DestDocumento);
        Assert.True(S.EmitirNota(d).Autorizada);
        var dest = sefaz.Ultima!.infNFe.dest!;
        Assert.Equal(("52998224725", EmissorNFe.DestHomologacao), (dest.CPF, dest.xNome));
        Assert.Equal(NFe.Classes.Informacoes.Pagamento.FormaPagamento.fpPagamentoInstantaneoPIXEstatico, sefaz.Ultima.infNFe.pag[0].detPag[0].tPag);
        d.DestDocumento = "111.111.111-11";
        Assert.Contains(RegrasNota.Verificar(d, S.ObterConfigFiscal(), false), p => p.Campo == "dest.doc");
    }

    [Fact]
    public void Sem_internet_fica_pendente_e_o_reenvio_usa_o_mesmo_numero()
    {
        Configurar(contingencia: false);                                            // com a contingência ligada, sairia em contingência

        var v = Venda("Cartão de crédito");
        sefaz.SemInternet = true;
        var r = S.EmitirNota(S.PrepararNota(v));
        Assert.Equal("PENDENTE", r.Status); Assert.Contains("Sem comunicação", r.Mensagem);
        var pendente = S.ObterNotaFiscal(r.NotaId);
        Assert.True(pendente.Xml.Length > 0);                                       // a nota assinada fica guardada
        sefaz.SemInternet = false;
        var r2 = S.EmitirNota(S.PrepararNota(v));
        Assert.True(r2.Autorizada);
        Assert.Equal((r.NotaId, 1L), (r2.NotaId, S.ObterNotaFiscal(r2.NotaId).Numero));
        Assert.Equal(1, sefaz.Consultas);                                           // perguntou antes de reenviar
        Assert.NotNull(sefaz.Ultima!.infNFe.pag[0].detPag[0].card);                 // cartão leva o grupo "card"
        Assert.Single(S.ListarNotasFiscais());
    }

    [Fact]
    public void Resposta_perdida_e_recuperada_pela_consulta_sem_reenviar()
    {
        Configurar(contingencia: false);
        var v = Venda();
        NFeDoc? enviada = null;
        sefaz.AoAutorizar = nfe => { enviada = nfe; throw new SemComunicacaoException("caiu a internet depois de enviar"); };
        Assert.Equal("PENDENTE", S.EmitirNota(S.PrepararNota(v)).Status);
        sefaz.AoConsultar = _ => SefazFalsa.Aprovar(enviada!);
        var r = S.EmitirNota(S.PrepararNota(v));
        Assert.True(r.Autorizada);
        Assert.Equal(1, sefaz.Envios);                                              // não mandou duas vezes
        Assert.Equal(enviada!.infNFe.Id[3..], r.Chave);
    }

    [Fact]
    public void Rejeicao_mostra_o_motivo_e_corrigir_reaproveita_o_numero()
    {
        Configurar();
        var v = Venda();
        sefaz.AoAutorizar = _ => new RespostaSefaz(778, "Rejeição: Informado NCM inexistente");
        var r = S.EmitirNota(S.PrepararNota(v));
        Assert.Equal("REJEITADA", r.Status); Assert.Contains("NCM inexistente", r.Mensagem); Assert.Contains("778", r.Mensagem);
        sefaz.AoAutorizar = nfe => SefazFalsa.Aprovar(nfe);
        var r2 = S.EmitirNota(S.PrepararNota(v));
        Assert.True(r2.Autorizada);
        Assert.Equal(1L, S.ObterNotaFiscal(r2.NotaId).Numero);
        Assert.Equal(2, S.ProximoNumeroNota(65));
    }

    [Fact]
    public void Duplicidade_confirma_pela_consulta()
    {
        Configurar();
        var v = Venda();
        sefaz.AoAutorizar = _ => new RespostaSefaz(204, "Rejeição: Duplicidade de NF-e");
        sefaz.AoConsultar = _ => SefazFalsa.Aprovar(sefaz.Ultima!);
        Assert.True(S.EmitirNota(S.PrepararNota(v)).Autorizada);
    }

    [Fact]
    public void Denegada_gasta_o_numero()
    {
        Configurar();
        var v = Venda();
        sefaz.AoAutorizar = _ => new RespostaSefaz(302, "Uso Denegado: Irregularidade fiscal do destinatário");
        var r = S.EmitirNota(S.PrepararNota(v));
        Assert.Equal("DENEGADA", r.Status);
        sefaz.AoAutorizar = nfe => SefazFalsa.Aprovar(nfe);
        var r2 = S.EmitirNota(S.PrepararNota(v));
        Assert.Equal(2L, S.ObterNotaFiscal(r2.NotaId).Numero);
    }

    [Fact]
    public void Cancelar_nota_e_so_depois_a_venda()
    {
        Configurar();
        var v = Venda();
        var id = S.EmitirNota(S.PrepararNota(v)).NotaId;
        Assert.Throws<ValidacaoException>(() => S.CancelarVenda(v, "cliente desistiu"));      // nota autorizada segura a venda
        Assert.Throws<ValidacaoException>(() => S.CancelarNota(id, "curta"));
        S.CancelarNota(id, "Cliente desistiu da compra no caixa");
        var n = S.ObterNotaFiscal(id);
        Assert.Equal("CANCELADA", n.Status); Assert.Contains("procEventoNFe", n.XmlEvento);
        Assert.Contains("NOTA CANCELADA", Danfe.Html(n));
        S.CancelarVenda(v, "cliente desistiu");
        Assert.True(S.ObterVenda(v).Cancelada);
        Assert.Throws<ValidacaoException>(() => S.CancelarNota(id, "Cliente desistiu da compra no caixa"));
    }

    [Fact]
    public void Inutiliza_numero_de_nota_rejeitada()
    {
        Configurar();
        var v = Venda();
        sefaz.AoAutorizar = _ => new RespostaSefaz(539, "Rejeição: algo");
        var id = S.EmitirNota(S.PrepararNota(v)).NotaId;
        S.InutilizarNota(id, "Nota rejeitada e abandonada no caixa");
        Assert.Equal("INUTILIZADA", S.ObterNotaFiscal(id).Status);
        Assert.Equal(1, sefaz.Inutilizacoes);
    }

    [Fact]
    public void Numeracao_pode_continuar_de_outro_programa()
    {
        Configurar();
        S.DefinirProximoNumeroNota(65, 500);
        Assert.Equal(500L, S.ObterNotaFiscal(S.EmitirNota(S.PrepararNota(Venda())).NotaId).Numero);
        Assert.Equal(1, S.ProximoNumeroNota(55));                                  // NF-e tem numeração própria
    }

    [Fact]
    public void NFe_para_empresa_de_outro_estado()
    {
        Configurar();
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Boutique Rio Ltda", "11.222.333/0001-81"));
        var v = Venda("Transferência", cliente: c);
        var d = S.PrepararNota(v, 55);
        var ex = Assert.Throws<PendenciasException>(() => S.EmitirNota(d));
        Assert.Contains(ex.Pendencias, p => p.Campo == "dest.logradouro");
        Assert.Contains(ex.Pendencias, p => p.Campo == "dest.uf");
        d.DestLogradouro = "Av. Atlântica"; d.DestNumero = "1000"; d.DestBairro = "Copacabana"; d.DestCodMunicipio = "3304557";
        d.DestMunicipio = "Rio de Janeiro"; d.DestUf = "RJ"; d.DestCep = "22021-000"; d.DestIndIE = 1; d.DestIE = "12345678";
        var r = S.EmitirNota(d);
        Assert.True(r.Autorizada, r.Mensagem);
        var nfe = sefaz.Ultima!;
        Assert.Equal((ModeloDocumento.NFe, NFe.Classes.Informacoes.Identificacao.Tipos.DestinoOperacao.doInterestadual), (nfe.infNFe.ide.mod, nfe.infNFe.ide.idDest));
        Assert.All(nfe.infNFe.det, x => Assert.Equal(6102, x.prod.CFOP));          // contribuinte de outro estado
        Assert.Equal(EmissorNFe.DestHomologacao, nfe.infNFe.dest!.xNome);
        Assert.Contains("DANFE", Danfe.Html(S.ObterNotaFiscal(r.NotaId)));
        Assert.Contains("<svg", Danfe.Html(S.ObterNotaFiscal(r.NotaId)));            // código de barras da chave
        Assert.Equal("RJ", S.ObterFiscalCliente(c)!.Uf);                           // endereço lembrado para a próxima
        Assert.Equal("Av. Atlântica", S.PrepararNota(Venda(cliente: c), 55).DestLogradouro);
    }

    [Fact]
    public void Regime_normal_calcula_ICMS_PIS_e_COFINS()
    {
        Configurar(crt: 3);
        var r = S.EmitirNota(S.PrepararNota(Venda()));
        Assert.True(r.Autorizada, r.Mensagem);
        var t = sefaz.Ultima!.infNFe.total.ICMSTot;
        Assert.InRange(Math.Abs(t.vICMS - t.vNF * 0.18m), 0m, 0.02m);           // soma do ICMS de cada item (arredondado por item)
        Assert.Equal(t.vNF, t.vBC);
        Assert.True(t.vPIS > 0 && t.vCOFINS > 0);
    }

    [Fact]
    public void Status_da_sefaz()
    {
        Configurar();
        Assert.Equal("107 – Serviço em Operação", S.StatusSefaz());
        sefaz.SemInternet = true;
        Assert.Throws<SemComunicacaoException>(() => S.StatusSefaz());
    }

    // ------------------------------------------------------------------ peças
    [Fact]
    public void Documentos_e_codigos()
    {
        Assert.True(DocFiscal.CnpjValido("11.222.333/0001-81"));
        Assert.True(DocFiscal.CnpjValido("12.ABC.345/01DE-35"));                   // CNPJ alfanumérico (exemplo oficial)
        Assert.False(DocFiscal.CnpjValido("12.ABC.345/01DE-36"));
        Assert.True(DocFiscal.GtinValido("7891000100011")); Assert.False(DocFiscal.GtinValido("7891000100012")); Assert.False(DocFiscal.GtinValido("00001"));
        Assert.True(DocFiscal.NcmValido("6109.10.00")); Assert.False(DocFiscal.NcmValido("610910"));
        Assert.True(DocFiscal.CodMunicipioValido("3550308", "SP")); Assert.False(DocFiscal.CodMunicipioValido("3304557", "SP"));
        Assert.Equal("6108", RegrasNota.AjustarCfop("5102", true, false));
        Assert.Equal("6102", RegrasNota.AjustarCfop("5102", true, true));
        Assert.Equal("5102", RegrasNota.AjustarCfop("5102", false, false));
        Assert.Equal(new long[] { 333, 333, 334 }, EmissorNFe.Ratear(1000, new List<long> { 100, 100, 100 }));
        Assert.Equal(1000, EmissorNFe.Ratear(1000, new List<long> { 5990 * 2, 12990 }).Sum());
    }

    [Fact]
    public void Codigo_de_barras_da_chave_segue_o_padrao_Code128()
    {
        Assert.Equal(107, Code128.Padroes.Length);
        Assert.Equal(107, Code128.Padroes.Distinct().Count());
        Assert.All(Code128.Padroes.Take(106), p => Assert.Equal(11, p.Sum(c => c - '0')));
        Assert.Equal(13, Code128.Padroes[106].Sum(c => c - '0'));
        Assert.All(Code128.Padroes.Take(106), p => Assert.Equal(0, (p[0] - '0' + p[2] - '0' + p[4] - '0') % 2));   // barras somam par
        var v = Code128.Codificar(new string('1', 44));
        Assert.Equal(25, v.Count); Assert.Equal(105, v[0]); Assert.Equal(106, v[^1]);
    }

    [Fact]
    public void Consulta_de_CEP_e_CNPJ_le_as_respostas()
    {
        var e = ConsultaPublica.LerViaCep("{\"cep\":\"01001-000\",\"logradouro\":\"Praça da Sé\",\"complemento\":\"lado ímpar\",\"bairro\":\"Sé\",\"localidade\":\"São Paulo\",\"uf\":\"SP\",\"ibge\":\"3550308\"}");
        Assert.Equal(("01001000", "Praça da Sé", "São Paulo", "3550308"), (e.Cep, e.Logradouro, e.Municipio, e.CodMunicipio));
        Assert.Throws<ValidacaoException>(() => ConsultaPublica.LerViaCep("{\"erro\": true}"));
        var c = ConsultaPublica.LerBrasilApi("{\"cnpj\":\"11222333000181\",\"razao_social\":\"NATALIA MODAS LTDA\",\"nome_fantasia\":\"NATALIA MODAS\",\"descricao_tipo_de_logradouro\":\"RUA\",\"logradouro\":\"DAS FLORES\",\"numero\":\"123\",\"complemento\":\"\",\"bairro\":\"CENTRO\",\"municipio\":\"SAO PAULO\",\"uf\":\"SP\",\"cep\":\"01001000\",\"codigo_municipio_ibge\":3550308,\"ddd_telefone_1\":\"11 988887777\",\"opcao_pelo_simples\":true,\"opcao_pelo_mei\":false}");
        Assert.Equal(("NATALIA MODAS LTDA", "RUA DAS FLORES", "3550308", 1), (c.RazaoSocial, c.Endereco.Logradouro, c.Endereco.CodMunicipio, c.CrtSugerido!.Value));
    }

    [Fact]
    public void Segredo_guarda_e_devolve_a_senha()
    {
        var g = Segredo.Proteger("minha senha");
        Assert.DoesNotContain("minha", g);
        Assert.Equal("minha senha", Segredo.Abrir(g));
        Assert.Equal("", Segredo.Abrir("lixo"));
    }
}
