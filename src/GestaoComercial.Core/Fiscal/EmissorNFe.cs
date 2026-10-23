using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DFe.Classes.Entidades;
using DFe.Classes.Flags;
using DFe.Utils;
using NFe.Classes.Informacoes;
using NFe.Classes.Informacoes.Destinatario;
using NFe.Classes.Informacoes.Detalhe;
using NFe.Classes.Informacoes.Detalhe.Tributacao;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Estadual.Tipos;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal;
using NFe.Classes.Informacoes.Detalhe.Tributacao.Federal.Tipos;
using NFe.Classes.Informacoes.Emitente;
using NFe.Classes.Informacoes.Identificacao;
using NFe.Classes.Informacoes.Identificacao.Tipos;
using NFe.Classes.Informacoes.Observacoes;
using Shared.NFe.Classes.Informacoes.InfRespTec;
using NFe.Classes.Informacoes.Pagamento;
using NFe.Classes.Informacoes.Total;
using NFe.Classes.Informacoes.Transporte;
using NFe.Classes.Servicos.Tipos;
using NFe.Utils;
using NFe.Utils.Excecoes;
using NFe.Utils.InformacoesSuplementares;
using NFe.Utils.NFe;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Core.Fiscal;

/// <summary>Monta, assina e valida a NF-e/NFC-e de uma venda (sem falar com a SEFAZ).</summary>
public static class EmissorNFe
{
    public const string TextoHomologacao = "NOTA FISCAL EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
    public const string DestHomologacao = "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Configuração da biblioteca para a loja/modelo (não usa a instância global).</summary>
    public static ConfiguracaoServico Configuracao(ConfigFiscal cfg, int modelo)
    {
        var c = new ConfiguracaoServico
        {
            DiretorioSchemas = PastaSchemas(),
            ValidarSchemas = true,
            TimeOut = 30000,
            ProtocoloDeSeguranca = System.Net.SecurityProtocolType.Tls12,
            RemoverAcentos = false,
        };
        c.VersaoLayout = VersaoServico.Versao400;
        c.cUF = EstadoDe(cfg.Uf);
        c.tpAmb = cfg.Producao ? TipoAmbiente.Producao : TipoAmbiente.Homologacao;
        c.ModeloDocumento = modelo == 65 ? ModeloDocumento.NFCe : ModeloDocumento.NFe;
        c.tpEmis = TipoEmissao.teNormal;
        return c;
    }

    public static Estado EstadoDe(string uf) =>
        Ufs.Codigos.TryGetValue(uf, out var cod) ? (Estado)cod : throw new ValidacaoException("Estado (UF) da loja não configurado.");

    // ------------------------------------------------------------------ esquemas oficiais (XSD)
    static readonly object travaSchemas = new();
    /// <summary>Pasta base dos XSD (padrão: schemas-nfe na pasta de dados). Os testes apontam para uma pasta temporária.</summary>
    public static string? PastaBaseSchemas { get; set; }

    /// <summary>Extrai os XSD embutidos para a pasta de dados (uma vez por versão do programa).</summary>
    public static string PastaSchemas()
    {
        lock (travaSchemas)
        {
            var asm = typeof(EmissorNFe).Assembly;
            var pasta = Path.Combine(PastaBaseSchemas ?? Config.Subpasta("schemas-nfe"), asm.GetName().Version?.ToString() ?? "0");
            var marca = Path.Combine(pasta, ".ok");
            if (File.Exists(marca)) return pasta;
            Directory.CreateDirectory(pasta);
            foreach (var nome in asm.GetManifestResourceNames().Where(n => n.StartsWith("schemas/")))
            {
                using var s = asm.GetManifestResourceStream(nome)!;
                using var f = File.Create(Path.Combine(pasta, nome["schemas/".Length..]));
                s.CopyTo(f);
            }
            File.WriteAllText(marca, DateTime.Now.ToString("O", Inv));
            return pasta;
        }
    }

    // ------------------------------------------------------------------ montagem
    /// <param name="contingencia">NFC-e emitida sem internet (tpEmis 9): quando entrou em contingência e por quê.</param>
    /// <param name="codigoNumerico">cNF da chave. Ao corrigir uma nota em contingência rejeitada, mantém o da nota já entregue ao cliente (a chave não muda).</param>
    public static NFeDoc Montar(ConfigFiscal cfg, DadosNota d, int numero, DateTimeOffset dhEmi, Contingencia? contingencia = null, string? codigoNumerico = null)
    {
        if (contingencia != null && d.Modelo != 65) throw new ValidacaoException("A contingência offline só existe para a NFC-e.");
        var nfce = d.Modelo == 65;
        var homolog = !cfg.Producao;
        var serie = nfce ? cfg.SerieNfce : cfg.SerieNfe;
        var doc = DocFiscal.Limpar(d.DestDocumento).ToUpperInvariant();
        var outroEstado = !nfce && Ufs.Codigos.ContainsKey(d.DestUf) && d.DestUf != cfg.Uf;
        var contribuinte = !nfce && d.DestIndIE == 1;

        var ide = new ide
        {
            cUF = EstadoDe(cfg.Uf), natOp = Limitar(cfg.NaturezaOperacao.Trim().Length > 0 ? cfg.NaturezaOperacao : "VENDA DE MERCADORIA", 60),
            mod = nfce ? ModeloDocumento.NFCe : ModeloDocumento.NFe, serie = serie, nNF = numero, tpNF = TipoNFe.tnSaida,
            cMunFG = long.Parse(Util.SomenteDigitos(cfg.CodMunicipio)), tpEmis = TipoEmissao.teNormal,
            tpImp = nfce ? TipoImpressao.tiNFCe : TipoImpressao.tiRetrato, cNF = codigoNumerico is { Length: 8 } ? codigoNumerico : CodigoNumerico(numero),
            tpAmb = homolog ? TipoAmbiente.Homologacao : TipoAmbiente.Producao, finNFe = FinalidadeNFe.fnNormal,
            verProc = Limitar($"GestaoComercial {Config.Versao}", 20), idDest = outroEstado ? DestinoOperacao.doInterestadual : DestinoOperacao.doInterna,
            dhEmi = dhEmi, procEmi = ProcessoEmissao.peAplicativoContribuinte,
            indFinal = contribuinte ? ConsumidorFinal.cfNao : ConsumidorFinal.cfConsumidorFinal,
            indPres = PresencaComprador.pcPresencial,
        };
        if (contingencia != null)
        {
            ide.tpEmis = TipoEmissao.teOffLine;
            ide.dhCont = contingencia.Inicio <= dhEmi ? contingencia.Inicio : dhEmi;
            ide.xJust = Limitar(contingencia.Justificativa, 256);
        }

        var emit = new emit
        {
            xNome = Limitar(cfg.RazaoSocial, 60), xFant = Vazio(Limitar(cfg.NomeFantasia, 60)),
            IE = cfg.IE.Trim().ToUpperInvariant() == "ISENTO" ? "ISENTO" : Util.SomenteDigitos(cfg.IE), CRT = (CRT)cfg.Crt,
            enderEmit = new enderEmit
            {
                xLgr = Limitar(cfg.Logradouro, 60), nro = Limitar(cfg.Numero, 60), xCpl = Vazio(Limitar(cfg.Complemento, 60)), xBairro = Limitar(cfg.Bairro, 60),
                cMun = long.Parse(Util.SomenteDigitos(cfg.CodMunicipio)), xMun = Limitar(cfg.Municipio, 60), UF = EstadoDe(cfg.Uf),
                CEP = Util.SomenteDigitos(cfg.Cep), cPais = 1058, xPais = "BRASIL",
                fone = Util.SomenteDigitos(cfg.Telefone) is { Length: >= 6 and <= 14 } f ? long.Parse(f) : null,
            },
        };
        emit.CNPJ = DocFiscal.Limpar(cfg.Cnpj).ToUpperInvariant();

        dest? dest = null;
        if (doc.Length > 0 || !nfce)
        {
            dest = new dest(VersaoServico.Versao400);
            if (doc.Length == 14) dest.CNPJ = doc; else dest.CPF = doc;
            var nome = homolog ? DestHomologacao : Limitar(d.DestNome, 60);
            if (nome.Length > 0) dest.xNome = nome;
            dest.indIEDest = nfce ? indIEDest.NaoContribuinte : (indIEDest)d.DestIndIE;
            if (!nfce && d.DestIndIE == 1) dest.IE = Util.SomenteDigitos(d.DestIE);
            if (Util.ValidaEmail(d.DestEmail)) dest.email = Limitar(d.DestEmail.Trim(), 60);
            if (!nfce)
                dest.enderDest = new enderDest
                {
                    xLgr = Limitar(d.DestLogradouro, 60), nro = Limitar(d.DestNumero, 60), xCpl = Vazio(Limitar(d.DestComplemento, 60)),
                    xBairro = Limitar(d.DestBairro, 60), cMun = long.Parse(Util.SomenteDigitos(d.DestCodMunicipio)), xMun = Limitar(d.DestMunicipio, 60),
                    UF = d.DestUf, CEP = Vazio(Util.SomenteDigitos(d.DestCep)), cPais = 1058, xPais = "BRASIL",
                };
        }

        var inf = new infNFe { versao = "4.00", ide = ide, emit = emit, dest = dest, transp = new transp { modFrete = ModalidadeFrete.mfSemFrete } };

        // desconto da venda rateado pelos itens (o último leva a sobra dos centavos)
        var descontos = Ratear(d.Desconto, d.Itens.Select(i => i.Total).ToList());
        decimal vBC = 0, vICMS = 0, vPIS = 0, vCOFINS = 0, vTotTrib = 0;
        for (int i = 0; i < d.Itens.Count; i++)
        {
            var it = d.Itens[i];
            var bruto = Reais(it.Total);
            var desc = Reais(descontos[i]);
            var liquido = bruto - desc;
            var gtin = DocFiscal.GtinValido(it.Codigo) ? it.Codigo : "SEM GTIN";
            var cfop = RegrasNota.AjustarCfop(it.Cfop, outroEstado, contribuinte);
            var prod = new prod
            {
                cProd = Limitar(it.Codigo.Length > 0 ? it.Codigo : it.ProdutoId.ToString(Inv), 60), cEAN = gtin, cEANTrib = gtin,
                xProd = homolog && i == 0 ? TextoHomologacao : Limitar(it.Descricao, 120), NCM = Util.SomenteDigitos(it.Ncm),
                CEST = Vazio(Util.SomenteDigitos(it.Cest)), CFOP = int.Parse(cfop),
                uCom = Unidade(it.Unidade), qCom = it.Quantidade, vUnCom = Reais(it.PrecoUnit), vProd = bruto,
                uTrib = Unidade(it.Unidade), qTrib = it.Quantidade, vUnTrib = Reais(it.PrecoUnit),
                vDesc = desc > 0 ? desc : null, indTot = IndicadorTotal.ValorDoItemCompoeTotalNF,
            };

            var imposto = new imposto { ICMS = new ICMS { TipoICMS = Icms(cfg, it, liquido, out var bcItem, out var icmsItem) } };
            vBC += bcItem; vICMS += icmsItem;
            imposto.PIS = new PIS { TipoPIS = Pis(cfg, liquido, out var pisItem) };
            imposto.COFINS = new COFINS { TipoCOFINS = Cofins(cfg, liquido, out var cofinsItem) };
            vPIS += pisItem; vCOFINS += cofinsItem;
            if (cfg.PercentualTributos > 0)
            {
                imposto.vTotTrib = Math.Round(liquido * cfg.PercentualTributos / 100m, 2, MidpointRounding.AwayFromZero);
                vTotTrib += imposto.vTotTrib.Value;
            }
            inf.det.Add(new det { nItem = i + 1, prod = prod, imposto = imposto });
        }

        var vProd = inf.det.Sum(x => x.prod.vProd);
        var vDesc = inf.det.Sum(x => x.prod.vDesc ?? 0);
        inf.total = new total
        {
            ICMSTot = new ICMSTot
            {
                vBC = vBC, vICMS = vICMS, vICMSDeson = 0, vFCP = 0, vFCPST = 0, vFCPSTRet = 0, vIPIDevol = 0,
                vProd = vProd, vDesc = vDesc, vPIS = vPIS, vCOFINS = vCOFINS, vNF = vProd - vDesc,
                vTotTrib = vTotTrib > 0 ? vTotTrib : null,
            },
        };

        var vNF = inf.total.ICMSTot.vNF;
        var detPag = new detPag { tPag = FormaPag(d.FormaPagamento, cfg), vPag = vNF };
        if (detPag.tPag == FormaPagamento.fpOutro) detPag.xPag = Limitar(d.FormaPagamento, 60);
        if (detPag.tPag is FormaPagamento.fpCartaoCredito or FormaPagamento.fpCartaoDebito or FormaPagamento.fpPagamentoInstantaneoPIXDinamico)
            detPag.card = new card { tpIntegra = TipoIntegracaoPagamento.TipNaoIntegrado };
        inf.pag = new List<pag> { new pag { detPag = new List<detPag> { detPag } } };

        var obs = new List<string>();
        if (cfg.Simples) obs.Add("DOCUMENTO EMITIDO POR ME OU EPP OPTANTE PELO SIMPLES NACIONAL.");
        if (vTotTrib > 0) obs.Add($"Valor aproximado dos tributos: {Util.FmtDinheiro((long)Math.Round(vTotTrib * 100))} ({cfg.PercentualTributos.ToString("0.##", Inv).Replace('.', ',')}%). Fonte: IBPT.");
        if (cfg.InfoComplementar.Trim().Length > 0) obs.Add(cfg.InfoComplementar.Trim());
        if (d.InfoComplementar.Trim().Length > 0) obs.Add(d.InfoComplementar.Trim());
        if (obs.Count > 0) inf.infAdic = new infAdic { infCpl = Limitar(string.Join(" ", obs), 5000) };

        if (DocFiscal.CnpjValido(cfg.RespTecCnpj))
            inf.infRespTec = new infRespTec
            {
                CNPJ = DocFiscal.Limpar(cfg.RespTecCnpj).ToUpperInvariant(), xContato = Limitar(cfg.RespTecContato, 60),
                email = Limitar(cfg.RespTecEmail, 60), fone = Util.SomenteDigitos(cfg.RespTecFone),
            };

        return new NFeDoc { infNFe = inf };
    }

    /// <summary>Assina, coloca o QR Code (NFC-e) e valida contra os esquemas oficiais. Lança <see cref="ValidacaoException"/> com o problema.</summary>
    public static NFeDoc Preparar(NFeDoc nfe, ConfigFiscal cfg, ConfiguracaoServico conf, X509Certificate2 cert)
    {
        nfe.Assina(conf, cert);
        if (nfe.infNFe.ide.mod == ModeloDocumento.NFCe)
        {
            var versao = cfg.VersaoQrCode == 3 ? VersaoQrCode.QrCodeVersao3 : VersaoQrCode.QrCodeVersao2;
            if (versao == VersaoQrCode.QrCodeVersao3 && nfe.infNFe.ide.tpEmis == TipoEmissao.teOffLine)
            {
                // QR Code v3 em contingência é assinado com o certificado: a biblioteca o abre pela configuração
                conf.Certificado.TipoCertificado = TipoCertificado.A1Arquivo;
                conf.Certificado.Arquivo = cfg.CertificadoArquivo;
                conf.Certificado.Senha = cfg.CertificadoSenha;
                conf.Certificado.KeyStorageFlags = X509KeyStorageFlags.Exportable;
            }
            nfe.infNFeSupl = new NFe.Classes.infNFeSupl();
            nfe.infNFeSupl.urlChave = nfe.infNFeSupl.ObterUrlConsulta(nfe, versao);
            nfe.infNFeSupl.qrCode = nfe.infNFeSupl.ObterUrlQrCode(nfe, versao, cfg.CscId.Trim().TrimStart('0') is { Length: > 0 } id ? id : "0", cfg.Csc.Trim(), conf.Certificado);
        }
        try { nfe.Valida(conf); }
        catch (ValidacaoSchemaException ex)
        {
            throw new ValidacaoException("A nota não passou na conferência do layout oficial, então não foi enviada:\n" + ResumirErroSchema(ex.Message));
        }
        return nfe;
    }

    /// <summary>Mensagens de XSD são técnicas: mostra só as linhas úteis.</summary>
    static string ResumirErroSchema(string msg) =>
        string.Join("\n", msg.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).Take(4));

    // ------------------------------------------------------------------ impostos
    static ICMSBasico Icms(ConfigFiscal cfg, ItemNota it, decimal liquido, out decimal bc, out decimal valor)
    {
        bc = 0; valor = 0;
        var orig = (OrigemMercadoria)it.Origem;
        if (cfg.Simples)
            return it.Tributacao switch
            {
                "500" => new ICMSSN500 { orig = orig, CSOSN = Csosnicms.Csosn500 },
                "103" => new ICMSSN102 { orig = orig, CSOSN = Csosnicms.Csosn103 },
                "300" => new ICMSSN102 { orig = orig, CSOSN = Csosnicms.Csosn300 },
                "400" => new ICMSSN102 { orig = orig, CSOSN = Csosnicms.Csosn400 },
                _ => new ICMSSN102 { orig = orig, CSOSN = Csosnicms.Csosn102 },
            };
        switch (it.Tributacao)
        {
            case "40": return new ICMS40 { orig = orig, CST = Csticms.Cst40 };
            case "41": return new ICMS40 { orig = orig, CST = Csticms.Cst41 };
            case "60": return new ICMS60 { orig = orig, CST = Csticms.Cst60 };
            default:
                bc = liquido;
                valor = Math.Round(liquido * cfg.AliquotaIcms / 100m, 2, MidpointRounding.AwayFromZero);
                return new ICMS00 { orig = orig, CST = Csticms.Cst00, modBC = DeterminacaoBaseIcms.DbiValorOperacao, vBC = bc, pICMS = cfg.AliquotaIcms, vICMS = valor };
        }
    }

    static PISBasico Pis(ConfigFiscal cfg, decimal liquido, out decimal valor)
    {
        valor = 0;
        var cst = cfg.Simples ? "99" : cfg.CstPisCofins;
        switch (cst)
        {
            case "01": case "02":
                valor = Math.Round(liquido * cfg.AliquotaPis / 100m, 2, MidpointRounding.AwayFromZero);
                return new PISAliq { CST = cst == "01" ? CSTPIS.pis01 : CSTPIS.pis02, vBC = liquido, pPIS = cfg.AliquotaPis, vPIS = valor };
            case "04": case "05": case "06": case "07": case "08": case "09":
                return new PISNT { CST = Enum.Parse<CSTPIS>("pis" + cst) };
            default:
                return new PISOutr { CST = cst == "49" ? CSTPIS.pis49 : CSTPIS.pis99, vBC = 0, pPIS = 0, vPIS = 0 };
        }
    }

    static COFINSBasico Cofins(ConfigFiscal cfg, decimal liquido, out decimal valor)
    {
        valor = 0;
        var cst = cfg.Simples ? "99" : cfg.CstPisCofins;
        switch (cst)
        {
            case "01": case "02":
                valor = Math.Round(liquido * cfg.AliquotaCofins / 100m, 2, MidpointRounding.AwayFromZero);
                return new COFINSAliq { CST = cst == "01" ? CSTCOFINS.cofins01 : CSTCOFINS.cofins02, vBC = liquido, pCOFINS = cfg.AliquotaCofins, vCOFINS = valor };
            case "04": case "05": case "06": case "07": case "08": case "09":
                return new COFINSNT { CST = Enum.Parse<CSTCOFINS>("cofins" + cst) };
            default:
                return new COFINSOutr { CST = cst == "49" ? CSTCOFINS.cofins49 : CSTCOFINS.cofins99, vBC = 0, pCOFINS = 0, vCOFINS = 0 };
        }
    }

    static FormaPagamento FormaPag(string forma, ConfigFiscal cfg) => forma switch
    {
        "Dinheiro" => FormaPagamento.fpDinheiro,
        "Pix" => cfg.TipoPix == "17" ? FormaPagamento.fpPagamentoInstantaneoPIXDinamico : FormaPagamento.fpPagamentoInstantaneoPIXEstatico,
        "Cartão de débito" => FormaPagamento.fpCartaoDebito,
        "Cartão de crédito" => FormaPagamento.fpCartaoCredito,
        "Fiado" => FormaPagamento.fpCartaoDaLoja,
        "Transferência" => FormaPagamento.fpTransferenciabancaria,
        _ => FormaPagamento.fpOutro,
    };

    // ------------------------------------------------------------------ utilidades
    static decimal Reais(long centavos) => centavos / 100m;

    /// <summary>Divide <paramref name="total"/> proporcionalmente; a soma bate exatamente.</summary>
    public static List<long> Ratear(long total, List<long> pesos)
    {
        var r = pesos.Select(_ => 0L).ToList();
        var soma = pesos.Sum();
        if (total <= 0 || soma <= 0) return r;
        long usado = 0;
        for (int i = 0; i < pesos.Count; i++)
        {
            r[i] = i == pesos.Count - 1 ? total - usado : total * pesos[i] / soma;
            usado += r[i];
        }
        return r;
    }

    static string CodigoNumerico(int numero)
    {
        string c;
        do c = RandomNumberGenerator.GetInt32(10000000, 99999999).ToString(Inv);
        while (c == numero.ToString("D8", Inv));
        return c;
    }

    static string Unidade(string? u)
    {
        var t = (u ?? "").Trim();
        return Limitar(t.Length == 0 ? "UN" : t.ToUpperInvariant(), 6);
    }

    static string Limitar(string? s, int max)
    {
        var t = string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return t.Length <= max ? t : t[..max];
    }

    static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
