using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;
using Microsoft.Data.Sqlite;
using NFe.Utils.NFe;
using Xunit;
using NFeDoc = NFe.Classes.NFe;

namespace GestaoComercial.Tests;

/// <summary>Contingência offline da NFC-e: sem internet o cupom sai mesmo assim e a nota vai para a SEFAZ depois.</summary>
public class ContingenciaTests : BaseTeste
{
    readonly SefazFalsa sefaz = new();

    public ContingenciaTests()
    {
        EmissorNFe.PastaBaseSchemas = Path.Combine(Path.GetTempPath(), "gc-schemas-teste");
        S.PastaFiscal = Path.Combine(Pasta, "notas-fiscais");
        S.Transmissor = sefaz;
    }

    void Configurar(Action<ConfigFiscal>? ajuste = null)
    {
        var c = FiscalTests.ConfigCompleta(Pasta);
        ajuste?.Invoke(c);
        S.SalvarConfigFiscal(c);
    }

    long Venda(long preco = 8990, string ncm = "61091000")
    {
        var p = S.SalvarProduto(new ProdutoDados("Camiseta " + Guid.NewGuid().ToString("N")[..6], PrecoVenda: preco, EstoqueInicial: 50));
        S.SalvarFiscalProduto(new FiscalProduto(p, ncm, "", 0, "", ""));
        return S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, preco) }, null, 0, "Dinheiro");
    }

    ResultadoNota Emitir(long venda) => S.EmitirNota(S.PrepararNota(venda));

    static string Tag(string xml, string tag)
    {
        var i = xml.IndexOf($"<{tag}>", StringComparison.Ordinal);
        if (i < 0) return "";
        i += tag.Length + 2;
        return xml[i..xml.IndexOf($"</{tag}>", i, StringComparison.Ordinal)];
    }

    [Fact]
    public void Sem_internet_a_NFCe_sai_em_contingencia_e_o_cupom_vale()
    {
        Configurar();
        var v = Venda();
        sefaz.SemInternet = true;
        var r = Emitir(v);
        Assert.True(r.Contingencia, r.Mensagem);
        Assert.Contains("CONTINGÊNCIA", r.Mensagem);

        var n = S.ObterNotaFiscal(r.NotaId);
        Assert.Equal(("CONTINGENCIA", 2L), (n.Status, n.Numero));
        Assert.True(n.Offline);
        Assert.Equal('9', n.Chave[34]);                                             // o tipo de emissão faz parte da chave
        Assert.Equal(Servico.JustificativaContingencia, Tag(n.Xml, "xJust"));
        Assert.NotEqual("", Tag(n.Xml, "dhCont"));
        // QR Code offline (v2): chave|2|ambiente|dia|valor|digest|idCSC|hash
        Assert.Equal(8, Tag(n.Xml, "qrCode").Split("p=")[1].Split('|').Length);

        // a tentativa normal que ficou sem resposta não é reaproveitada: fica para conferir depois
        var tentativa = S.ListarNotasFiscais().Single(x => x.Id != n.Id);
        Assert.Equal(("SUBSTITUIDA", 1L, n.Id), (tentativa.Status, tentativa.Numero, tentativa.SubstitutaId));
        Assert.NotNull(S.ContingenciaAtiva());

        var html = Danfe.Html(n);
        Assert.Contains("EMITIDA EM CONTINGÊNCIA", html);
        Assert.Contains("Pendente de autorização", html);
        if (Environment.GetEnvironmentVariable("GC_SHOT_DIR") is { Length: > 0 } fotos) File.WriteAllText(Path.Combine(fotos, "danfe_contingencia.html"), html);

        // a venda já tem a nota: não emite outra nem cancela antes de a nota ir para a SEFAZ
        Assert.Contains("contingência", Assert.Throws<ValidacaoException>(() => Emitir(v)).Message);
        Assert.Contains("contingência", Assert.Throws<ValidacaoException>(() => S.CancelarVenda(v, "desistiu")).Message);
    }

    [Fact]
    public void Ja_em_contingencia_as_proximas_saem_na_hora_sem_esperar_a_SEFAZ()
    {
        Configurar();
        sefaz.SemInternet = true;
        Emitir(Venda());
        var antes = sefaz.Tentativas;
        var r = Emitir(Venda());
        Assert.True(r.Contingencia);
        Assert.Equal(antes, sefaz.Tentativas);                                     // nem tentou
        Assert.Equal(3, S.ObterNotaFiscal(r.NotaId).Numero);
        Assert.Single(S.ListarNotasFiscais(), x => x.Status == "SUBSTITUIDA");    // só a primeira tentativa ficou para conferir
        Assert.Equal(2, S.NotasEmContingencia().Count);
    }

    [Fact]
    public void Internet_volta_envia_tudo_e_inutiliza_o_numero_que_a_SEFAZ_nunca_recebeu()
    {
        Configurar();
        sefaz.SemInternet = true;
        var r1 = Emitir(Venda());
        var r2 = Emitir(Venda(4990));

        // continua sem internet: nada muda
        var tentativa = S.EnviarNotasPendentes();
        Assert.True(tentativa.SemComunicacao);
        Assert.Equal(2, tentativa.Restantes);
        Assert.NotNull(S.ContingenciaAtiva());

        sefaz.SemInternet = false;
        var res = S.EnviarNotasPendentes();
        Assert.Equal((2, 0, 0, false), (res.Autorizadas, res.Rejeitadas, res.Restantes, res.SemComunicacao));
        Assert.Contains("autorizadas", res.Mensagem);
        Assert.Null(S.ContingenciaAtiva());                                        // voltou ao normal
        foreach (var r in new[] { r1, r2 })
        {
            var n = S.ObterNotaFiscal(r.NotaId);
            Assert.Equal("AUTORIZADA", n.Status);
            Assert.True(n.Offline);
            Assert.Equal(r.Chave, n.Chave);                                       // a chave do cupom entregue não muda
            Assert.Contains("Emitida em contingência", Danfe.Html(n));
        }
        // a nº 1 (sem resposta) não estava na SEFAZ (217): número inutilizado
        Assert.Equal(new[] { 1 }, sefaz.NumerosInutilizados);
        Assert.Equal("INUTILIZADA", S.ListarNotasFiscais().Single(x => x.Numero == 1).Status);

        // a próxima volta a ser normal
        var r3 = Emitir(Venda());
        Assert.True(r3.Autorizada);
        Assert.False(S.ObterNotaFiscal(r3.NotaId).Offline);
    }

    [Fact]
    public void Se_a_tentativa_sem_resposta_tinha_sido_autorizada_cancela_por_substituicao()
    {
        Configurar();
        NFeDoc? perdida = null;
        sefaz.AoAutorizar = nfe => { perdida = nfe; throw new SemComunicacaoException("caiu a internet depois de enviar"); };
        var r = Emitir(Venda());
        Assert.True(r.Contingencia);

        sefaz.AoAutorizar = nfe => SefazFalsa.Aprovar(nfe);
        var chavePerdida = perdida!.infNFe.Id[3..];
        sefaz.AoConsultar = chave => chave == chavePerdida ? SefazFalsa.Aprovar(perdida) : new RespostaSefaz(217, "Rejeição: NF-e não consta na base de dados da SEFAZ");
        var res = S.EnviarNotasPendentes();
        Assert.Equal(1, res.Autorizadas);
        Assert.Equal(r.Chave, sefaz.UltimaSubstituta);                              // aponta a NFC-e que o cliente levou
        var antiga = S.ListarNotasFiscais().Single(x => x.Numero == 1);
        Assert.Equal("CANCELADA", antiga.Status);
        Assert.Contains("substituição", antiga.Motivo);
        Assert.Equal("AUTORIZADA", S.ObterNotaFiscal(r.NotaId).Status);
        Assert.Empty(sefaz.NumerosInutilizados);
    }

    [Fact]
    public void Contingencia_recusada_e_corrigida_com_o_mesmo_numero_chave_e_data()
    {
        Configurar();
        S.EntrarEmContingencia();
        var v = Venda();
        var r = Emitir(v);
        Assert.True(r.Contingencia);
        Assert.Equal(0, sefaz.Tentativas);
        Assert.Equal(1, S.ObterNotaFiscal(r.NotaId).Numero);                       // entrou direto: sem tentativa perdida
        var dhEmi = Tag(S.ObterNotaFiscal(r.NotaId).Xml, "dhEmi");

        sefaz.AoAutorizar = _ => new RespostaSefaz(778, "Rejeição: Informado NCM inexistente");
        var res = S.EnviarNotasPendentes();
        Assert.Equal((0, 1), (res.Autorizadas, res.Rejeitadas));
        Assert.Contains("recusada", res.Mensagem);
        Assert.Equal("REJEITADA", S.ObterNotaFiscal(r.NotaId).Status);

        // corrige o NCM e emite de novo: continua sendo a mesma nota em contingência
        var d = S.PrepararNota(v); d.Itens[0].Ncm = "61091000";
        sefaz.AoAutorizar = nfe => SefazFalsa.Aprovar(nfe);
        var r2 = S.EmitirNota(d);
        Assert.True(r2.Autorizada, r2.Mensagem);
        var n = S.ObterNotaFiscal(r2.NotaId);
        Assert.Equal((r.NotaId, r.Chave, 1L), (n.Id, n.Chave, n.Numero));
        Assert.True(n.Offline);
        Assert.Equal(dhEmi, Tag(n.Xml, "dhEmi"));
    }

    [Fact]
    public void Correcao_de_contingencia_sem_internet_continua_em_contingencia()
    {
        Configurar();
        S.EntrarEmContingencia();
        var v = Venda();
        var r = Emitir(v);
        sefaz.AoAutorizar = _ => new RespostaSefaz(778, "Rejeição: NCM");
        S.EnviarNotasPendentes();
        sefaz.SemInternet = true;
        var r2 = Emitir(v);
        Assert.True(r2.Contingencia);
        Assert.Equal((r.NotaId, r.Chave), (r2.NotaId, r2.Chave));
    }

    [Fact]
    public void SEFAZ_paralisada_vai_para_contingencia_com_o_mesmo_numero()
    {
        Configurar();
        sefaz.AoAutorizar = _ => new RespostaSefaz(108, "Serviço Paralisado Momentaneamente (curto prazo)");
        var r = Emitir(Venda());
        Assert.True(r.Contingencia);
        var n = S.ListarNotasFiscais().Single();                                   // não perdeu número: a SEFAZ não processou
        Assert.Equal((1L, "CONTINGENCIA"), (n.Numero, n.Status));
    }

    [Fact]
    public void NFe_e_contingencia_desligada_continuam_pendentes()
    {
        Configurar(c => c.ContingenciaAutomatica = false);
        sefaz.SemInternet = true;
        Assert.Equal("PENDENTE", Emitir(Venda()).Status);
        Assert.Null(S.ContingenciaAtiva());

        Configurar();
        var c = S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Loja Parceira Ltda", "11.222.333/0001-81"));
        var p = S.SalvarProduto(new ProdutoDados("Vestido", PrecoVenda: 19990, EstoqueInicial: 5));
        S.SalvarFiscalProduto(new FiscalProduto(p, "62044400", "", 0, "", ""));
        var v = S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 19990) }, c, 0, "Pix");
        var d = S.PrepararNota(v, 55);
        d.DestLogradouro = "Rua A"; d.DestNumero = "1"; d.DestBairro = "Centro"; d.DestMunicipio = "São Paulo"; d.DestUf = "SP"; d.DestCodMunicipio = "3550308";
        Assert.Equal("PENDENTE", S.EmitirNota(d).Status);                         // NF-e não tem contingência offline
        Assert.Throws<ValidacaoException>(() => EmissorNFe.Montar(S.ObterConfigFiscal(), d, 9, DateTimeOffset.Now, new Contingencia(DateTimeOffset.Now, Servico.JustificativaContingencia)));
    }

    [Fact]
    public void QR_Code_versao_3_em_contingencia_e_assinado_com_o_certificado()
    {
        Configurar(c => c.VersaoQrCode = 3);
        S.EntrarEmContingencia();
        var r = Emitir(Venda());
        Assert.True(r.Contingencia, r.Mensagem);
        var qr = Tag(S.ObterNotaFiscal(r.NotaId).Xml, "qrCode").Split("p=")[1].Split('|');
        Assert.Equal("3", qr[1]);
        Assert.True(qr[^1].Length > 100);                                          // assinatura RSA em base64
        sefaz.AoAutorizar = nfe => SefazFalsa.Aprovar(nfe);
        Assert.Equal(1, S.EnviarNotasPendentes().Autorizadas);                    // e passa no layout oficial
    }

    [Fact]
    public void Sem_nada_para_enviar_confere_a_SEFAZ_e_sai_da_contingencia()
    {
        Configurar();
        S.EntrarEmContingencia();
        sefaz.SemInternet = true;
        Assert.True(S.EnviarNotasPendentes().SemComunicacao);
        Assert.NotNull(S.ContingenciaAtiva());
        Assert.True(S.TemEnvioPendente());
        sefaz.SemInternet = false;
        Assert.False(S.EnviarNotasPendentes().SemComunicacao);
        Assert.Null(S.ContingenciaAtiva());
        Assert.False(S.TemEnvioPendente());
        Assert.True(S.EnviarNotasPendentes().NadaAFazer);
    }

    [Fact]
    public void Banco_da_versao_anterior_ganha_as_situacoes_novas_sem_perder_notas()
    {
        var arq = Path.Combine(Pasta, "v3.db");
        using (var c = Banco.Conectar(arq))
        {
            Banco.Migrar(c);
            using var cmd = c.CreateCommand();
            // volta a tabela para o formato da 2.5 (sem as situações novas) com uma nota
            cmd.CommandText = """
                INSERT INTO vendas (data, cliente_nome, subtotal, desconto, total, forma_pagamento) VALUES ('2026-10-01 10:00:00', 'Consumidor', 1000, 0, 1000, 'Dinheiro');
                DROP TABLE notas_fiscais;
                CREATE TABLE notas_fiscais (
                    id INTEGER PRIMARY KEY, venda_id INTEGER NOT NULL REFERENCES vendas(id), modelo INTEGER NOT NULL CHECK (modelo IN (55, 65)),
                    serie INTEGER NOT NULL, numero INTEGER NOT NULL, ambiente INTEGER NOT NULL CHECK (ambiente IN (1, 2)),
                    status TEXT NOT NULL CHECK (status IN ('PENDENTE', 'AUTORIZADA', 'REJEITADA', 'DENEGADA', 'CANCELADA', 'INUTILIZADA')),
                    chave TEXT NOT NULL DEFAULT '', protocolo TEXT NOT NULL DEFAULT '', cstat INTEGER NOT NULL DEFAULT 0, motivo TEXT NOT NULL DEFAULT '',
                    criada_em TEXT NOT NULL, emitida_em TEXT NOT NULL DEFAULT '', dest_doc TEXT NOT NULL DEFAULT '', dest_nome TEXT NOT NULL DEFAULT '',
                    total INTEGER NOT NULL DEFAULT 0, xml TEXT NOT NULL DEFAULT '', xml_evento TEXT NOT NULL DEFAULT '', UNIQUE (modelo, serie, numero, ambiente));
                INSERT INTO notas_fiscais (venda_id, modelo, serie, numero, ambiente, status, chave, protocolo, criada_em, total)
                    VALUES (last_insert_rowid(), 65, 1, 7, 2, 'AUTORIZADA', 'CHAVE', '1352', '2026-10-01 10:00:00', 1000);
                PRAGMA user_version = 3;
                """;
            cmd.ExecuteNonQuery();
        }
        using (var c = Banco.Conectar(arq))
        {
            Banco.Migrar(c);
            var s = new Servico(c);
            var n = s.ListarNotasFiscais().Single();
            Assert.Equal((7L, "AUTORIZADA", "CHAVE", "1352"), (n.Numero, n.Status, n.Chave, n.Protocolo));
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"UPDATE notas_fiscais SET status = 'CONTINGENCIA', substituta_id = NULL WHERE id = {n.Id}";
            cmd.ExecuteNonQuery();                                                  // a situação nova é aceita
            cmd.CommandText = "PRAGMA user_version";
            Assert.Equal((long)Banco.VersaoEsquema, cmd.ExecuteScalar());
        }
        SqliteConnection.ClearAllPools();
    }
}
