using GestaoComercial.Core.Fiscal;
using NFe.Classes;
using NFe.Classes.Protocolo;
using NFe.Utils.NFe;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Core;

/// <summary>Faltam dados para emitir: a lista diz quais (a tela destaca cada campo).</summary>
public sealed class PendenciasException(List<Pendencia> pendencias)
    : ValidacaoException("Para emitir a nota, complete:\n• " + string.Join("\n• ", pendencias.Select(p => p.Mensagem)))
{
    public List<Pendencia> Pendencias { get; } = pendencias;
}

public partial class Servico
{
    /// <summary>Quem fala com a SEFAZ (os testes trocam por um simulado).</summary>
    public ITransmissorSefaz Transmissor { get; set; } = new TransmissorSefaz();
    /// <summary>Onde ficam os XML das notas (padrão: notas-fiscais na pasta de dados).</summary>
    public string? PastaFiscal { get; set; }

    // ================================================================== configuração
    public ConfigFiscal ObterConfigFiscal() => ConfigFiscal.Carregar(this);

    public void SalvarConfigFiscal(ConfigFiscal c)
    {
        if (c.SerieNfce is < 0 or > 999 || c.SerieNfe is < 0 or > 999) throw new ValidacaoException("A série deve ser um número de 0 a 999.");
        if (c.Ambiente is not (1 or 2)) throw new ValidacaoException("Escolha o ambiente (homologação ou produção).");
        if (c.AliquotaIcms is < 0 or > 100 || c.AliquotaPis is < 0 or > 100 || c.AliquotaCofins is < 0 or > 100 || c.PercentualTributos is < 0 or > 100)
            throw new ValidacaoException("Alíquotas e percentuais devem ficar entre 0 e 100.");
        c.Uf = c.Uf.Trim().ToUpperInvariant();
        c.Cnpj = DocFiscal.Limpar(c.Cnpj).ToUpperInvariant();
        c.Salvar(this);
        Mudou();
    }

    string ChaveNumeracao(int modelo, int serie, int ambiente) => $"fiscal_prox_{modelo}_{serie}_{ambiente}";

    /// <summary>Próximo número da nota (por modelo, série e ambiente): nunca repete um número já usado.</summary>
    public long ProximoNumeroNota(int modelo)
    {
        var c = ObterConfigFiscal();
        return ProximoNumeroNota(modelo, modelo == 65 ? c.SerieNfce : c.SerieNfe, c.Ambiente);
    }

    public long ProximoNumeroNota(int modelo, int serie, int ambiente)
    {
        long.TryParse(ObterConfig(ChaveNumeracao(modelo, serie, ambiente), "1"), out var cfgNum);
        var maior = Escalar("SELECT COALESCE(MAX(numero), 0) FROM notas_fiscais WHERE modelo = ? AND serie = ? AND ambiente = ?", modelo, serie, ambiente);
        return Math.Max(Math.Max(cfgNum, 1), maior + 1);
    }

    /// <summary>Para quem já emitia notas em outro programa: continua a partir do número informado.</summary>
    public void DefinirProximoNumeroNota(int modelo, long numero)
    {
        if (numero is < 1 or > 999_999_999) throw new ValidacaoException("O número da nota deve ficar entre 1 e 999.999.999.");
        var c = ObterConfigFiscal();
        SalvarConfig(ChaveNumeracao(modelo, modelo == 65 ? c.SerieNfce : c.SerieNfe, c.Ambiente), numero.ToString());
    }

    // ================================================================== dados fiscais de produtos e clientes
    public FiscalProduto ObterFiscalProduto(long produtoId) =>
        Um("SELECT id, ncm, cest, origem, cfop, tributacao FROM produtos WHERE id = ?",
           r => new FiscalProduto(r.L("id"), r.S("ncm"), r.S("cest"), (int)r.L("origem"), r.S("cfop"), r.S("tributacao")), produtoId)
        ?? throw new NaoEncontradoException("Produto não encontrado.");

    public void SalvarFiscalProduto(FiscalProduto f)
    {
        var ncm = Util.SomenteDigitos(f.Ncm);
        if (ncm.Length != 0 && ncm.Length != 8) throw new ValidacaoException("NCM deve ter 8 números.");
        if (!DocFiscal.CestValido(f.Cest)) throw new ValidacaoException("CEST deve ter 7 números (ou ficar em branco).");
        if (f.Cfop.Trim().Length > 0 && !DocFiscal.CfopValido(f.Cfop)) throw new ValidacaoException("CFOP inválido (ex.: 5102).");
        if (f.Origem is < 0 or > 8) throw new ValidacaoException("Origem da mercadoria inválida.");
        Tx(() => Exec("UPDATE produtos SET ncm = ?, cest = ?, origem = ?, cfop = ?, tributacao = ? WHERE id = ?",
                      ncm, Util.SomenteDigitos(f.Cest), f.Origem, Util.SomenteDigitos(f.Cfop), f.Tributacao.Trim(), f.ProdutoId));
    }

    public FiscalCliente? ObterFiscalCliente(long clienteId) =>
        Um("SELECT * FROM clientes_fiscal WHERE cliente_id = ?",
           r => new FiscalCliente(r.L("cliente_id"), r.S("ie"), (int)r.L("ind_ie"), r.S("logradouro"), r.S("numero"), r.S("complemento"), r.S("bairro"),
                                  r.S("cod_municipio"), r.S("municipio"), r.S("uf"), r.S("cep")), clienteId);

    public void SalvarFiscalCliente(FiscalCliente f) => Tx(() => Exec(
        "INSERT INTO clientes_fiscal (cliente_id, ie, ind_ie, logradouro, numero, complemento, bairro, cod_municipio, municipio, uf, cep) VALUES (?,?,?,?,?,?,?,?,?,?,?) " +
        "ON CONFLICT(cliente_id) DO UPDATE SET ie = excluded.ie, ind_ie = excluded.ind_ie, logradouro = excluded.logradouro, numero = excluded.numero, " +
        "complemento = excluded.complemento, bairro = excluded.bairro, cod_municipio = excluded.cod_municipio, municipio = excluded.municipio, uf = excluded.uf, cep = excluded.cep",
        f.ClienteId, f.IE.Trim(), f.IndIE, f.Logradouro.Trim(), f.Numero.Trim(), f.Complemento.Trim(), f.Bairro.Trim(),
        Util.SomenteDigitos(f.CodMunicipio), f.Municipio.Trim(), f.Uf.Trim().ToUpperInvariant(), Util.SomenteDigitos(f.Cep)));

    // ================================================================== notas
    const string NotaSelect = "SELECT * FROM notas_fiscais";

    static NotaFiscal MapNota(Rd r) =>
        new(r.L("id"), r.L("venda_id"), (int)r.L("modelo"), (int)r.L("serie"), r.L("numero"), (int)r.L("ambiente"), r.S("status"), r.S("chave"),
            r.S("protocolo"), (int)r.L("cstat"), r.S("motivo"), r.S("criada_em"), r.S("emitida_em"), r.S("dest_doc"), r.S("dest_nome"), r.L("total"),
            r.S("xml"), r.S("xml_evento"));

    public List<NotaFiscal> ListarNotasFiscais(string? inicio = null, string? fim = null, int limite = 1000)
    {
        var sql = NotaSelect + " WHERE 1=1";
        var p = new List<object?>();
        if (!string.IsNullOrEmpty(inicio)) { sql += " AND substr(criada_em, 1, 10) >= ?"; p.Add(inicio); }
        if (!string.IsNullOrEmpty(fim)) { sql += " AND substr(criada_em, 1, 10) <= ?"; p.Add(fim); }
        sql += " ORDER BY id DESC LIMIT ?"; p.Add(limite);
        return Todos(sql, MapNota, p.ToArray());
    }

    public NotaFiscal ObterNotaFiscal(long id) => Um(NotaSelect + " WHERE id = ?", MapNota, id) ?? throw new NaoEncontradoException("Nota fiscal não encontrada.");

    public List<NotaFiscal> NotasDaVenda(long vendaId) => Todos(NotaSelect + " WHERE venda_id = ? ORDER BY id DESC", MapNota, vendaId);

    /// <summary>Rascunho da nota de uma venda, já com o que o programa sabe (cliente, produtos, padrões).</summary>
    public DadosNota PrepararNota(long vendaId, int modelo = 65)
    {
        var v = ObterVenda(vendaId);
        var cfg = ObterConfigFiscal();
        var d = new DadosNota
        {
            VendaId = v.Id, Modelo = modelo, ClienteId = v.ClienteId, DestDocumento = Util.FmtDocumento(v.ClienteDoc),
            DestNome = v.ClienteId != null ? v.ClienteNome : "", Desconto = v.Desconto, Total = v.Total, FormaPagamento = v.FormaPagamento,
        };
        if (v.ClienteId is long cid)
        {
            try { d.DestEmail = ObterPessoa(TipoPessoa.Cliente, cid).Email; } catch (NaoEncontradoException) { }
            if (ObterFiscalCliente(cid) is { } f)
            {
                d.DestIE = f.IE; d.DestIndIE = f.IndIE; d.DestLogradouro = f.Logradouro; d.DestNumero = f.Numero; d.DestComplemento = f.Complemento;
                d.DestBairro = f.Bairro; d.DestCodMunicipio = f.CodMunicipio; d.DestMunicipio = f.Municipio; d.DestUf = f.Uf; d.DestCep = f.Cep;
            }
            else if (Util.SomenteDigitos(v.ClienteDoc).Length == 14) d.DestIndIE = 9;
        }
        var validas = Tributacao.Validas(cfg.Crt);
        foreach (var it in v.Itens)
        {
            FiscalProduto? f = null;
            try { f = ObterFiscalProduto(it.ProdutoId); } catch (NaoEncontradoException) { }
            d.Itens.Add(new ItemNota
            {
                ProdutoId = it.ProdutoId, Codigo = it.Codigo, Descricao = it.Descricao, Unidade = it.Unidade, Quantidade = it.Quantidade, PrecoUnit = it.PrecoUnit,
                Ncm = f?.Ncm ?? "", Cest = f?.Cest ?? "", Origem = f?.Origem ?? 0,
                Cfop = f is { Cfop.Length: 4 } ? f.Cfop : cfg.CfopPadrao,
                Tributacao = f != null && validas.Contains(f.Tributacao) ? f.Tributacao : cfg.TributacaoPadrao,
            });
        }
        return d;
    }

    /// <summary>
    /// Emite a NF-e/NFC-e da venda: confere os dados, reserva o número, monta, assina, valida e envia à SEFAZ.
    /// Sem internet a nota fica PENDENTE (dá para reenviar); rejeitada fica REJEITADA com o motivo (corrige e reenvia com o mesmo número).
    /// </summary>
    public ResultadoNota EmitirNota(DadosNota d)
    {
        var venda = ObterVenda(d.VendaId);
        if (venda.Cancelada) throw new ValidacaoException("Esta venda foi cancelada: não dá para emitir nota fiscal.");
        if (NotasDaVenda(venda.Id).FirstOrDefault(n => n.Autorizada) is { } jaTem)
            throw new ValidacaoException($"Esta venda já tem a {jaTem.Tipo} nº {jaTem.Numero} autorizada.");
        var cfg = ObterConfigFiscal();
        var pend = RegrasNota.Verificar(d, cfg);
        if (pend.Count > 0) throw new PendenciasException(pend);

        if (d.SalvarNosCadastros) GuardarDadosDaNota(d);
        using var cert = ConfigFiscal.AbrirCertificado(cfg.CertificadoArquivo, cfg.CertificadoSenha);
        var conf = EmissorNFe.Configuracao(cfg, d.Modelo);

        // tentativa anterior desta venda que não foi aceita: reaproveita o número
        var anterior = NotasDaVenda(venda.Id).FirstOrDefault(n => n.Modelo == d.Modelo && n.Ambiente == cfg.Ambiente && n.PodeReenviar);
        if (anterior is { Status: "PENDENTE", Chave.Length: > 0, Xml.Length: > 0 })
        {
            // pode ter sido autorizada e a resposta se perdeu: pergunta antes de mandar de novo
            RespostaSefaz r0;
            try { r0 = Transmissor.Consultar(anterior.Chave, conf, cert); }
            catch (SemComunicacaoException ex) { return new ResultadoNota(anterior.Id, "PENDENTE", ex.Message, anterior.Chave); }
            if (r0.Autorizada) return Registrar(anterior.Id, new NFeDoc().CarregarDeXmlString(anterior.Xml), r0, cfg);
        }

        long notaId, numero;
        if (anterior != null) { notaId = anterior.Id; numero = anterior.Numero; }
        else
        {
            var serie = d.Modelo == 65 ? cfg.SerieNfce : cfg.SerieNfe;
            (notaId, numero) = Tx(() =>
            {
                var n = ProximoNumeroNota(d.Modelo);
                var id = Inserir("INSERT INTO notas_fiscais (venda_id, modelo, serie, numero, ambiente, status, criada_em, dest_doc, dest_nome, total) VALUES (?,?,?,?,?,?,?,?,?,?)",
                                 venda.Id, d.Modelo, serie, n, cfg.Ambiente, "PENDENTE", Util.AgoraIso(), DocFiscal.Limpar(d.DestDocumento), d.DestNome.Trim(), venda.Total);
                Exec("INSERT INTO config (chave, valor) VALUES (?, ?) ON CONFLICT(chave) DO UPDATE SET valor = excluded.valor", ChaveNumeracao(d.Modelo, serie, cfg.Ambiente), (n + 1).ToString());
                return (id, n);
            });
        }

        NFeDoc nfe;
        try { nfe = EmissorNFe.Preparar(EmissorNFe.Montar(cfg, d, (int)numero, DateTimeOffset.Now), cfg, conf, cert); }
        catch (ValidacaoException ex)
        {
            Tx(() => Exec("UPDATE notas_fiscais SET status = 'REJEITADA', cstat = 0, motivo = ? WHERE id = ?", ex.Message, notaId));
            throw;
        }
        var chave = nfe.infNFe.Id[3..];
        Tx(() => Exec("UPDATE notas_fiscais SET status = 'PENDENTE', chave = ?, xml = ?, cstat = 0, motivo = ?, dest_doc = ?, dest_nome = ? WHERE id = ?",
                      chave, nfe.ObterXmlString(), "Enviando para a SEFAZ...", DocFiscal.Limpar(d.DestDocumento), d.DestNome.Trim(), notaId));

        RespostaSefaz r;
        try
        {
            r = Transmissor.Autorizar(nfe, conf, cert);
            if (r.CStat is 204 or 539)   // duplicidade: a SEFAZ já tem esta nota; confirma pela chave
            {
                var c = Transmissor.Consultar(chave, conf, cert);
                if (c.Autorizada) r = c;
            }
        }
        catch (SemComunicacaoException ex)
        {
            Tx(() => Exec("UPDATE notas_fiscais SET motivo = ? WHERE id = ?", ex.Message, notaId));
            return new ResultadoNota(notaId, "PENDENTE", ex.Message, chave);
        }
        return Registrar(notaId, nfe, r, cfg);
    }

    void GuardarDadosDaNota(DadosNota d)
    {
        foreach (var it in d.Itens)
            try { SalvarFiscalProduto(new FiscalProduto(it.ProdutoId, it.Ncm, it.Cest, it.Origem, it.Cfop, it.Tributacao)); }
            catch (NaoEncontradoException) { }
        if (d.ClienteId is long cid && (d.Modelo == 55 || d.DestLogradouro.Trim().Length > 0))
            SalvarFiscalCliente(new FiscalCliente(cid, d.DestIE, d.DestIndIE, d.DestLogradouro, d.DestNumero, d.DestComplemento, d.DestBairro,
                                                  d.DestCodMunicipio, d.DestMunicipio, d.DestUf, d.DestCep));
    }

    /// <summary>Grava o resultado da SEFAZ. Autorizada: guarda o XML completo (nfeProc) no banco e na pasta notas-fiscais.</summary>
    ResultadoNota Registrar(long notaId, NFeDoc nfe, RespostaSefaz r, ConfigFiscal cfg)
    {
        var chave = nfe.infNFe.Id[3..];
        if (r.Autorizada)
        {
            var proc = new nfeProc { versao = "4.00", NFe = nfe, protNFe = r.Prot ?? throw new InvalidOperationException("Autorização sem protocolo.") };
            var xml = DFe.Utils.FuncoesXml.ClasseParaXmlString(proc);
            if (!xml.StartsWith("<?xml")) xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + xml;
            var quando = (r.Recebido ?? DateTimeOffset.Now).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
            Tx(() => Exec("UPDATE notas_fiscais SET status = 'AUTORIZADA', cstat = ?, motivo = ?, protocolo = ?, emitida_em = ?, xml = ? WHERE id = ?",
                          r.CStat, r.Motivo, r.Protocolo, quando, xml, notaId));
            SalvarArquivoXml(ObterNotaFiscal(notaId), xml, "procNFe");
            var tipo = nfe.infNFe.ide.mod == DFe.Classes.Flags.ModeloDocumento.NFCe ? "NFC-e" : "NF-e";
            return new ResultadoNota(notaId, "AUTORIZADA", $"{tipo} nº {nfe.infNFe.ide.nNF} autorizada pela SEFAZ{(cfg.Producao ? "" : " (homologação: sem valor fiscal)")}.", chave);
        }
        var status = r.Denegada ? "DENEGADA" : r.CStat == 105 ? "PENDENTE" : "REJEITADA";
        var msg = r.Denegada ? $"Uso denegado pela SEFAZ ({r.CStat}): {r.Motivo}. Este número não pode mais ser usado."
                 : r.CStat == 105 ? r.Motivo
                 : $"A SEFAZ recusou a nota ({r.CStat}): {r.Motivo}";
        Tx(() => Exec("UPDATE notas_fiscais SET status = ?, cstat = ?, motivo = ? WHERE id = ?", status, r.CStat, msg, notaId));
        return new ResultadoNota(notaId, status, msg, chave);
    }

    /// <summary>Pergunta à SEFAZ a situação de uma nota pendente (resposta perdida, lote em processamento).</summary>
    public ResultadoNota ConsultarNota(long notaId)
    {
        var n = ObterNotaFiscal(notaId);
        if (n.Chave.Length == 0 || n.Xml.Length == 0) throw new ValidacaoException("Esta nota ainda não foi montada. Use “Emitir” de novo.");
        var cfg = ObterConfigFiscal();
        using var cert = ConfigFiscal.AbrirCertificado(cfg.CertificadoArquivo, cfg.CertificadoSenha);
        var conf = EmissorNFe.Configuracao(cfg, n.Modelo);
        var r = Transmissor.Consultar(n.Chave, conf, cert);
        if (n.Autorizada || n.Status == "CANCELADA") return new ResultadoNota(n.Id, n.Status, $"{r.CStat} – {r.Motivo}", n.Chave);
        if (r.Autorizada) return Registrar(n.Id, new NFeDoc().CarregarDeXmlString(n.Xml), r, cfg);
        Tx(() => Exec("UPDATE notas_fiscais SET motivo = ? WHERE id = ?", $"Consulta: {r.CStat} – {r.Motivo}", n.Id));
        return new ResultadoNota(n.Id, n.Status, $"{r.CStat} – {r.Motivo}", n.Chave);
    }

    /// <summary>Cancela na SEFAZ uma nota autorizada (prazo curto: NFC-e ~30 min e NF-e 24 h na maioria dos estados).</summary>
    public void CancelarNota(long notaId, string justificativa)
    {
        var j = (justificativa ?? "").Trim();
        if (j.Length < 15) throw new ValidacaoException("A justificativa do cancelamento precisa ter pelo menos 15 letras.");
        if (j.Length > 255) j = j[..255];
        var n = ObterNotaFiscal(notaId);
        if (!n.Autorizada) throw new ValidacaoException("Só dá para cancelar nota autorizada.");
        var cfg = ObterConfigFiscal();
        using var cert = ConfigFiscal.AbrirCertificado(cfg.CertificadoArquivo, cfg.CertificadoSenha);
        var conf = EmissorNFe.Configuracao(cfg, n.Modelo);
        var (r, xml) = Transmissor.Cancelar(n.Chave, n.Protocolo, j, DocFiscal.Limpar(cfg.Cnpj).ToUpperInvariant(), conf, cert);
        if (r.CStat is not (135 or 155 or 573))
            throw new ValidacaoException($"A SEFAZ não aceitou o cancelamento ({r.CStat}): {r.Motivo}");
        Tx(() => Exec("UPDATE notas_fiscais SET status = 'CANCELADA', motivo = ?, xml_evento = ? WHERE id = ?", $"Cancelada: {j}", xml, notaId));
        if (xml.Length > 0) SalvarArquivoXml(n, xml, "procEventoCanc");
    }

    /// <summary>Número que ficou sem nota autorizada (rejeitada e abandonada): avisa a SEFAZ que ele não será usado.</summary>
    public void InutilizarNota(long notaId, string justificativa)
    {
        var j = (justificativa ?? "").Trim();
        if (j.Length < 15) throw new ValidacaoException("A justificativa precisa ter pelo menos 15 letras.");
        var n = ObterNotaFiscal(notaId);
        if (!n.PodeReenviar) throw new ValidacaoException("Só dá para inutilizar o número de uma nota rejeitada ou pendente.");
        var cfg = ObterConfigFiscal();
        using var cert = ConfigFiscal.AbrirCertificado(cfg.CertificadoArquivo, cfg.CertificadoSenha);
        var conf = EmissorNFe.Configuracao(cfg, n.Modelo);
        if (n.Chave.Length > 0 && Transmissor.Consultar(n.Chave, conf, cert) is { Autorizada: true } r0)
        {
            Registrar(n.Id, new NFeDoc().CarregarDeXmlString(n.Xml), r0, cfg);
            throw new ValidacaoException("Esta nota já tinha sido autorizada pela SEFAZ (a resposta tinha se perdido). Ela foi atualizada como autorizada.");
        }
        var r = Transmissor.Inutilizar(DocFiscal.Limpar(cfg.Cnpj).ToUpperInvariant(), DateTime.Now.Year, n.Modelo, n.Serie, (int)n.Numero, j, conf, cert);
        if (r.CStat != 102) throw new ValidacaoException($"A SEFAZ não aceitou a inutilização ({r.CStat}): {r.Motivo}");
        Tx(() => Exec("UPDATE notas_fiscais SET status = 'INUTILIZADA', motivo = ?, protocolo = ? WHERE id = ?", $"Número inutilizado: {j}", r.Protocolo, notaId));
    }

    /// <summary>Teste rápido: a SEFAZ do estado está respondendo com este certificado?</summary>
    public string StatusSefaz(int modelo = 65)
    {
        var cfg = ObterConfigFiscal();
        if (!Ufs.Codigos.ContainsKey(cfg.Uf)) throw new ValidacaoException("Informe o estado (UF) da loja primeiro.");
        using var cert = ConfigFiscal.AbrirCertificado(cfg.CertificadoArquivo, cfg.CertificadoSenha);
        var r = Transmissor.Status(EmissorNFe.Configuracao(cfg, modelo), cert);
        return $"{r.CStat} – {r.Motivo}";
    }

    /// <summary>XML na pasta notas-fiscais/AAAA-MM (o contador costuma pedir estes arquivos todo mês).</summary>
    public string SalvarArquivoXml(NotaFiscal n, string xml, string sufixo)
    {
        var mes = n.CriadaEm.Length >= 7 ? n.CriadaEm[..7] : DateTime.Now.ToString("yyyy-MM");
        var pasta = Path.Combine(PastaFiscal ?? Config.Subpasta("notas-fiscais"), (n.Homologacao ? "homologacao-" : "") + mes);
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, $"{n.Chave}-{sufixo}.xml");
        File.WriteAllText(arquivo, xml, new System.Text.UTF8Encoding(false));
        return arquivo;
    }
}
