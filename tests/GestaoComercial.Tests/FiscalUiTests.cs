using Avalonia.Headless.XUnit;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;
using Xunit;

namespace GestaoComercial.Tests;

/// <summary>Telas da nota fiscal: emitir completando o que falta, configurar, lista de notas e NFC-e logo depois da venda.</summary>
public class FiscalUiTests
{
    static (Ambiente A, SefazFalsa Sefaz) Preparar(bool configurar = true, bool simples = false)
    {
        var a = new Ambiente(simples);
        var sefaz = new SefazFalsa();
        EmissorNFe.PastaBaseSchemas = Path.Combine(Path.GetTempPath(), "gc-schemas-teste");
        a.S.PastaFiscal = Path.Combine(a.Pasta, "notas-fiscais");
        a.S.Transmissor = sefaz;
        EmitirNotaDialog.EmSegundoPlano = false;
        if (configurar) a.S.SalvarConfigFiscal(FiscalTests.ConfigCompleta(a.Pasta));
        a.Janela.Show();
        return (a, sefaz);
    }

    static long Venda(Ambiente a, string ncm = "", long? cliente = null)
    {
        var p1 = a.S.SalvarProduto(new ProdutoDados("Vestido longo floral", PrecoVenda: 15990, EstoqueInicial: 10));
        var p2 = a.S.SalvarProduto(new ProdutoDados("Cinto de couro", PrecoVenda: 4990, EstoqueInicial: 10));
        if (ncm.Length > 0) { a.S.SalvarFiscalProduto(new FiscalProduto(p1, ncm, "", 0, "", "")); a.S.SalvarFiscalProduto(new FiscalProduto(p2, "42033000", "", 0, "", "")); }
        return a.S.RegistrarVenda(new[] { new ItemVendaInput(p1, 1, 15990), new ItemVendaInput(p2, 2, 4990) }, cliente, 0, "Pix");
    }

    /// <summary>Emitir com o modelo de teste ligado é síncrono: dá para chamar dentro do diálogo.</summary>
    static ResultadoNota? Emitir(EmitirNotaDialog d) => d.EmitirAsync().GetAwaiter().GetResult();

    [AvaloniaFact]
    public async Task NFCe_pela_tela_pede_o_NCM_que_falta_e_depois_lembra()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var v = Venda(a);
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v), d =>
        {
            Assert.Equal(65, d.Modelo);
            Assert.Equal("", d.TextoAviso);                                       // configuração completa: nada a avisar
            Assert.Null(Emitir(d));                                              // falta o NCM dos dois produtos
            Assert.True(d.CampoComErro("item:0:ncm")); Assert.True(d.CampoComErro("item:1:ncm"));
            Assert.Contains("NCM", d.TextoAviso);
            Assert.Equal(0, sefaz.Envios);
            a.Foto("nf_emitir_faltando", d);
            d.CamposNcm[0].Text = "6204.44.00"; d.CamposNcm[1].Text = "42033000";
            var r = Emitir(d);
            Assert.True(r!.Autorizada);
        });
        var n = a.S.NotasDaVenda(v).Single();
        Assert.Equal(("AUTORIZADA", 65, 1L), (n.Status, n.Modelo, n.Numero));
        Assert.Contains(a.P.Abertos, x => x.Contains("danfe-" + n.Chave));        // abre o cupom para imprimir
        Assert.Equal("62044400", a.S.ObterFiscalProduto(a.S.ListarProdutos("Vestido").Single().Id).Ncm);   // fica no cadastro

        // pedir a nota de novo da mesma venda: só reabre o cupom, não emite outra
        a.P.Abertos.Clear();
        await Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v);
        Assert.Single(a.P.Abertos); Assert.Equal(1, sefaz.Envios);
    }

    [AvaloniaFact]
    public async Task Cliente_com_CNPJ_vai_para_NFe_e_pede_o_endereco()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Boutique Bela Ltda", "11.222.333/0001-81"));
        var v = Venda(a, "62044400", c);
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v), d =>
        {
            Assert.Equal(55, d.Modelo);                                          // CNPJ: sugere NF-e
            Assert.Equal("11.222.333/0001-81", d.Doc.Text);
            Assert.Null(Emitir(d));
            foreach (var campo in new[] { "dest.logradouro", "dest.numero", "dest.bairro", "dest.uf", "dest.municipio" }) Assert.True(d.CampoComErro(campo), campo);
            Assert.False(d.CampoComErro("dest.doc"));
            a.Foto("nf_emitir_nfe_endereco", d);
            d.Cep.Text = "20040-002"; d.Logradouro.Text = "Avenida Rio Branco"; d.Numero.Text = "100"; d.Bairro.Text = "Centro";
            d.Municipio.Text = "Rio de Janeiro"; d.Uf.SelectedItem = "RJ"; d.CodMunicipio.Text = "3304557";
            Assert.True(Emitir(d)!.Autorizada);
        });
        var n = a.S.NotasDaVenda(v).Single();
        Assert.Equal((55, "AUTORIZADA"), (n.Modelo, n.Status));
        Assert.Contains("<CFOP>6108</CFOP>", n.Xml);                            // outro estado, não contribuinte
        Assert.Equal("Avenida Rio Branco", a.S.ObterFiscalCliente(c)!.Logradouro);

        // trocar o modelo na tela: NFC-e esconde o endereço e não o exige
        var v2 = Venda(a, "62044400", c);
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v2), d =>
        {
            Assert.Equal("Avenida Rio Branco", d.Logradouro.Text);                // da última nota
            d.TrocarModelo(65); Assert.Equal(65, d.Modelo);
            d.Close();
        });
        Assert.Equal(1, sefaz.Envios);
    }

    [AvaloniaFact]
    public async Task Sem_configuracao_avisa_e_nao_reserva_numero()
    {
        var (a, sefaz) = Preparar(configurar: false); using var _ = a;
        var v = Venda(a, "62044400");
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v), d =>
        {
            Assert.Contains("configure a nota fiscal", d.TextoAviso);
            Assert.Contains("e mais", d.TextoAviso);                              // resumido: a lista completa está na configuração
            Assert.Null(Emitir(d));
            a.Foto("nf_emitir_sem_config", d);
            d.Close();
        });
        Assert.Empty(a.S.ListarNotasFiscais()); Assert.Equal(0, sefaz.Envios);
    }

    [AvaloniaFact]
    public async Task Sem_internet_fica_pendente_e_tentar_de_novo_autoriza_com_o_mesmo_numero()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var v = Venda(a, "62044400");
        sefaz.SemInternet = true;
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v), d =>
        {
            var r = Emitir(d)!;
            Assert.Equal("PENDENTE", r.Status);
            Assert.Contains("pendente", d.TextoAviso);
            sefaz.SemInternet = false;
            Assert.True(Emitir(d)!.Autorizada);
        });
        var n = a.S.NotasDaVenda(v).Single();
        Assert.Equal(("AUTORIZADA", 1L), (n.Status, n.Numero));
    }

    [AvaloniaFact]
    public async Task Tela_de_notas_lista_reimprime_e_cancela_a_nota_e_a_venda()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var v = Venda(a, "62044400");
        Assert.True(a.S.EmitirNota(a.S.PrepararNota(v)).Autorizada);
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        Assert.Contains("HOMOLOGAÇÃO", t.TextoConfig);
        var linha = t.Tabela.Linhas.Single();
        Assert.Equal(new[] { "1", "NFC-e", "Autorizada" }, new[] { linha.C[0], linha.C[1], linha.C[5] });
        Assert.StartsWith("Teste · Protocolo 1352", linha.C[6]);
        a.Foto("nf_lista");

        t.Tabela.Selecionar(linha.Id);
        a.P.Abertos.Clear();
        await t.VerAsync();
        Assert.Contains(a.P.Abertos, x => x.Contains("danfe-"));

        a.P.RespostaPergunta = "Cliente desistiu da compra"; a.P.RespostaConfirmar = true;
        t.Tabela.Selecionar(linha.Id);
        await t.CancelarAsync();
        Assert.Equal(1, sefaz.Cancelamentos);
        Assert.Equal("CANCELADA", a.S.NotasDaVenda(v).Single().Status);
        Assert.Equal("CANCELADA", a.S.ObterVenda(v).Status);                     // e a venda também, com estoque devolvido
        t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        Assert.Equal("Cancelada", t.Tabela.Linhas.Single().C[5]);
    }

    [AvaloniaFact]
    public async Task Venda_com_nota_autorizada_so_cancela_depois_da_nota()
    {
        var (a, _) = Preparar(); using var amb = a;
        var v = Venda(a, "62044400");
        a.S.EmitirNota(a.S.PrepararNota(v));
        var ex = Assert.Throws<ValidacaoException>(() => a.S.CancelarVenda(v, "teste"));
        Assert.Contains("nota fiscal", ex.Message);
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public void Sem_configuracao_a_lista_de_notas_convida_a_configurar()
    {
        var (a, _) = Preparar(configurar: false, simples: true); using var amb = a;
        Assert.Contains("Notas fiscais", a.Janela.MenuVisivel);
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        Assert.Contains("ainda não está configurada", t.TextoConfig);
        Assert.Empty(t.Tabela.Linhas);
        a.Foto("nf_lista_vazia");
    }

    [AvaloniaFact]
    public async Task Configurar_salva_incompleto_mostra_o_que_falta_e_depois_completa()
    {
        var (a, _) = Preparar(configurar: false); using var amb = a;
        var cert = FiscalTests.CriarCertificado(a.Pasta, "4321");
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        await a.ComDialogo<ConfigFiscalDialog>(t.ConfigurarAsync, d =>
        {
            Assert.Contains("CNPJ", d.TextoPendencias);
            Assert.Equal("Nenhum certificado escolhido.", d.TextoCertificado);
            Assert.Equal(2, d.Ambiente);                                         // começa em homologação
            a.Foto("nf_config_vazia", d);
            d.Cnpj.Text = "11.222.333/0001-81"; d.Razao.Text = "Natália Modas Ltda";
            d.SalvarAsync().GetAwaiter().GetResult();                            // salva mesmo incompleto
            Assert.Contains(a.P.Msgs, m => m.Titulo == "Salvo, mas ainda falta");
            Assert.DoesNotContain("CNPJ inválido", d.TextoPendencias);
            d.Close();
        });
        Assert.Equal("Natália Modas Ltda", a.S.ObterConfigFiscal().RazaoSocial);

        a.P.ArquivoParaAbrir = cert;
        await a.ComDialogo<ConfigFiscalDialog>(t.ConfigurarAsync, d =>
        {
            Assert.Equal("11.222.333/0001-81", d.Cnpj.Text);                     // o que foi salvo volta
            d.EscolherCertificadoAsync().GetAwaiter().GetResult();
            d.Senha.Text = "errada"; Ambiente.Rodar(); Assert.Contains("senha", d.TextoCertificado.ToLowerInvariant());
            Assert.NotEqual("Digite a senha do certificado.", d.TextoCertificado);
            d.Senha.Text = "4321"; Ambiente.Rodar(); Assert.StartsWith("Válido até", d.TextoCertificado);
            d.IE.Text = "111.111.111.111"; d.Regime.SelectedIndex = 0;
            d.Cep.Text = "01001-000"; d.Logradouro.Text = "Praça da Sé"; d.Numero.Text = "10"; d.Bairro.Text = "Sé";
            d.Municipio.Text = "São Paulo"; d.Uf.SelectedItem = "SP"; d.CodMunicipio.Text = "3550308";
            d.CscId.Text = "000001"; d.Csc.Text = "0123456789ABCDEF0123456789ABCDEF";
            d.ProximoNfce.Text = "120";                                          // vinha de outro programa
            Ambiente.Rodar();
            Assert.Equal("", d.TextoPendencias);                                 // a lista do que falta some conforme preenche
            a.Foto("nf_config_completa", d);
            d.SalvarAsync().GetAwaiter().GetResult();
        });
        var cfg = a.S.ObterConfigFiscal();
        Assert.Empty(cfg.Pendencias(65));
        Assert.Equal(Path.Combine(a.Pasta, "certificado", "certificado.pfx"), cfg.CertificadoArquivo);   // cópia guardada com os dados
        Assert.Equal(120, a.S.ProximoNumeroNota(65));
        Assert.Equal(1, a.S.ProximoNumeroNota(55));
        Assert.Contains("HOMOLOGAÇÃO", a.Tela<NotasFiscaisTela>("Notas fiscais").TextoConfig);      // configurada: só lembra que é teste
    }

    [AvaloniaFact]
    public async Task Numeracao_de_teste_nao_passa_para_producao_ao_salvar()
    {
        var (a, _) = Preparar(); using var amb = a;
        a.S.DefinirProximoNumeroNota(65, 57);                                    // homologação já no 57
        var cfg = a.S.ObterConfigFiscal(); cfg.Ambiente = 1; a.S.SalvarConfigFiscal(cfg);
        Assert.Equal(1, a.S.ProximoNumeroNota(65));
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        await a.ComDialogo<ConfigFiscalDialog>(t.ConfigurarAsync, d =>
        {
            Assert.Equal(1, d.Ambiente); Assert.Equal("1", d.ProximoNfce.Text);
            d.SalvarAsync().GetAwaiter().GetResult();
        });
        Assert.Equal(1, a.S.ProximoNumeroNota(65));
        cfg = a.S.ObterConfigFiscal(); cfg.Ambiente = 2; a.S.SalvarConfigFiscal(cfg);
        Assert.Equal(57, a.S.ProximoNumeroNota(65));
    }

    [AvaloniaFact]
    public async Task Apos_a_venda_emite_a_NFCe_sozinha_quando_nao_falta_nada()
    {
        var (a, sefaz) = Preparar(); using var amb = a;
        a.S.SalvarConfig(VendaTela.ConfigAposVenda, "nfce");
        var p = a.S.SalvarProduto(new ProdutoDados("Blusa", PrecoVenda: 7990, EstoqueInicial: 5));
        a.S.SalvarFiscalProduto(new FiscalProduto(p, "61061000", "", 0, "", ""));
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Blusa"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        a.P.Abertos.Clear();
        await t.FinalizarAsync();
        Assert.Equal(1, sefaz.Envios);
        Assert.Equal("AUTORIZADA", a.S.ListarNotasFiscais().Single().Status);
        Assert.Contains(a.P.Abertos, x => x.Contains("danfe-"));
        Assert.Contains("Imprimir de novo", a.P.UltimoToast.Acoes.Select(x => x.Rotulo));

        // produto sem NCM: abre a tela para completar em vez de falhar
        var q = a.S.SalvarProduto(new ProdutoDados("Saia", PrecoVenda: 6990, EstoqueInicial: 5));
        t.Editor.Busca.Digitar("Saia"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        await a.ComDialogo<EmitirNotaDialog>(t.FinalizarAsync, d =>
        {
            Assert.Equal("", d.CamposNcm[0].Text);
            d.CamposNcm[0].Text = "62045200";
            Assert.True(Emitir(d)!.Autorizada);
        });
        Assert.Equal(2, sefaz.Envios);
        Assert.Equal("62045200", a.S.ObterFiscalProduto(q).Ncm);
    }

    [AvaloniaFact]
    public async Task Lista_de_vendas_tem_o_botao_de_nota_fiscal()
    {
        var (a, _) = Preparar(); using var amb = a;
        var v = Venda(a, "62044400");
        var t = a.Tela<NotasTela>("Vendas");
        t.Tabela.Selecionar(v.ToString());
        await a.ComDialogo<EmitirNotaDialog>(t.NotaFiscalAsync, d => Assert.True(Emitir(d)!.Autorizada));
        Assert.Single(a.S.NotasDaVenda(v));
    }

    [AvaloniaFact]
    public void DANFE_da_NFCe_e_da_NFe_vira_HTML_com_QR_e_codigo_de_barras()
    {
        var (a, _) = Preparar(); using var amb = a;
        var v = Venda(a, "62044400");
        var nfce = a.S.ObterNotaFiscal(a.S.EmitirNota(a.S.PrepararNota(v)).NotaId);
        var html = Danfe.Html(nfce);
        Assert.Contains("data:image/png;base64,", html);                          // QR Code
        Assert.Contains("SEM VALOR FISCAL", html.ToUpperInvariant());

        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Boutique Bela Ltda", "11.222.333/0001-81"));
        var v2 = Venda(a, "62044400", c);
        var d = a.S.PrepararNota(v2, 55);
        d.DestLogradouro = "Rua A"; d.DestNumero = "1"; d.DestBairro = "Centro"; d.DestMunicipio = "São Paulo"; d.DestUf = "SP"; d.DestCodMunicipio = "3550308"; d.DestCep = "01001000";
        d.DestIndIE = 9;
        var nfe = a.S.ObterNotaFiscal(a.S.EmitirNota(d).NotaId);
        var html2 = Danfe.Html(nfe);
        Assert.Contains("<svg", html2);                                          // código de barras da chave
        Assert.Contains(Danfe.ChaveFormatada(nfe.Chave), html2);
        Assert.Contains("Nº 000.000.001", html2);
        Assert.Equal("123.456.789", Danfe.NumeroNota(123456789));
        var pasta = Environment.GetEnvironmentVariable("GC_SHOT_DIR");
        if (!string.IsNullOrEmpty(pasta))
        {
            File.WriteAllText(Path.Combine(pasta, "danfe_nfce.html"), html);
            File.WriteAllText(Path.Combine(pasta, "danfe_nfe.html"), html2);
        }
    }
}
