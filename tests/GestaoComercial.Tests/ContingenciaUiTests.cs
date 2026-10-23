using Avalonia.Headless.XUnit;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;
using Xunit;

namespace GestaoComercial.Tests;

/// <summary>Contingência nas telas: sem internet o cupom sai, a lista avisa o que falta enviar e o envio acontece quando a internet volta.</summary>
public class ContingenciaUiTests
{
    static (Ambiente A, SefazFalsa Sefaz) Preparar()
    {
        var a = new Ambiente();
        var sefaz = new SefazFalsa();
        EmissorNFe.PastaBaseSchemas = Path.Combine(Path.GetTempPath(), "gc-schemas-teste");
        a.S.PastaFiscal = Path.Combine(a.Pasta, "notas-fiscais");
        a.S.Transmissor = sefaz;
        EmitirNotaDialog.EmSegundoPlano = false;
        a.S.SalvarConfigFiscal(FiscalTests.ConfigCompleta(a.Pasta));
        a.Janela.Show();
        return (a, sefaz);
    }

    static long Venda(Ambiente a, long preco = 12990)
    {
        var p = a.S.SalvarProduto(new ProdutoDados("Saia midi " + Guid.NewGuid().ToString("N")[..4], PrecoVenda: preco, EstoqueInicial: 10));
        a.S.SalvarFiscalProduto(new FiscalProduto(p, "62045200", "", 0, "", ""));
        return a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, preco) }, null, 0, "Dinheiro");
    }

    [AvaloniaFact]
    public async Task Sem_internet_a_NFCe_sai_em_contingencia_e_vai_quando_a_internet_volta()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var v = Venda(a);
        sefaz.SemInternet = true;
        a.P.Abertos.Clear();
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v), d =>
        {
            var r = d.EmitirAsync().GetAwaiter().GetResult();
            Assert.True(r!.Contingencia, r.Mensagem);
        });
        var nota = a.S.NotasDaVenda(v).First(n => n.EmContingencia);
        Assert.Contains(a.P.Abertos, x => x.Contains("danfe-" + nota.Chave));       // o cupom abre para imprimir
        Assert.Contains("CONTINGÊNCIA", a.P.UltimoToast.Texto);
        Assert.Equal(Nivel.Aviso, a.P.UltimoToast.Nivel);
        Assert.Equal("1", a.Janela.TextoSeloNotas);                                // selo no menu

        // pedir a nota de novo: só reabre o cupom
        a.P.Abertos.Clear();
        await Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v);
        Assert.Single(a.P.Abertos);

        // a próxima venda já mostra que está em contingência e não espera a SEFAZ
        var v2 = Venda(a, 7990);
        await a.ComDialogo<EmitirNotaDialog>(() => Fluxos.NotaFiscalAsync(a.Janela, a.Janela, v2), d =>
        {
            Assert.Contains("CONTINGÊNCIA", d.TextoAviso);
            a.Foto("contingencia_emitir", d);
            Assert.True(d.EmitirAsync().GetAwaiter().GetResult()!.Contingencia);
        });
        Assert.Equal("2", a.Janela.TextoSeloNotas);

        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        Assert.Contains("2 NFC-e emitida(s) sem internet", t.TextoContingencia);
        Assert.Contains("Contingência", t.Tabela.Linhas.Select(l => l.C[5]));
        Assert.Contains("Substituída", t.Tabela.Linhas.Select(l => l.C[5]));       // a tentativa que ficou sem resposta
        a.Foto("contingencia_lista");

        // ainda sem internet: "Enviar agora" avisa e nada muda
        await t.EnviarAgoraAsync();
        Assert.Contains("sem comunicação", a.P.UltimoToast.Texto.ToLowerInvariant());
        Assert.Equal(2, a.S.NotasEmContingencia().Count);

        // a internet volta: o envio automático manda tudo e avisa
        sefaz.SemInternet = false;
        var res = await a.Janela.EnviarNotasPendentesAsync();
        Assert.Equal(2, res!.Autorizadas);
        Assert.Contains("autorizadas", a.P.UltimoToast.Texto);
        Assert.Equal("", a.Janela.TextoSeloNotas);
        Assert.Null(a.S.ContingenciaAtiva());
        t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        Assert.Equal("", t.TextoContingencia);
        Assert.Equal(2, t.Tabela.Linhas.Count(l => l.C[5] == "Autorizada"));
        Assert.Contains("Inutilizada", t.Tabela.Linhas.Select(l => l.C[5]));
        a.Foto("contingencia_enviada");
    }

    [AvaloniaFact]
    public async Task Envio_automatico_sem_internet_fica_quieto()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        sefaz.SemInternet = true;
        a.S.EmitirNota(a.S.PrepararNota(Venda(a)));
        var avisos = a.P.Toasts.Count;
        var r = await a.Janela.EnviarNotasPendentesAsync();
        Assert.True(r!.SemComunicacao);
        Assert.Equal(avisos, a.P.Toasts.Count);                                    // não incomoda a cada tentativa
    }

    [AvaloniaFact]
    public async Task Apos_a_venda_sem_internet_a_NFCe_sai_em_contingencia_sozinha()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        a.S.SalvarConfig(VendaTela.ConfigAposVenda, "nfce");
        var p = a.S.SalvarProduto(new ProdutoDados("Bolsa", PrecoVenda: 15990, EstoqueInicial: 5));
        a.S.SalvarFiscalProduto(new FiscalProduto(p, "42022210", "", 0, "", ""));
        sefaz.SemInternet = true;
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Bolsa"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        a.P.Abertos.Clear();
        await t.FinalizarAsync();
        Assert.Single(a.S.NotasEmContingencia());
        Assert.Contains(a.P.Abertos, x => x.Contains("danfe-"));
    }

    [AvaloniaFact]
    public async Task Venda_em_contingencia_nao_cancela_e_a_nota_espera_autorizacao()
    {
        var (a, sefaz) = Preparar(); using var _ = a;
        var v = Venda(a);
        a.S.EntrarEmContingencia();
        a.S.EmitirNota(a.S.PrepararNota(v));
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        t.Tabela.Selecionar(t.Tabela.Linhas.Single().Id!);
        await t.CancelarAsync();
        Assert.Equal(0, sefaz.Cancelamentos);
        Assert.Contains("contingência", a.P.UltimoToast.Texto);
        Assert.Throws<ValidacaoException>(() => a.S.CancelarVenda(v, "cliente desistiu"));
    }

    [AvaloniaFact]
    public async Task Configuracao_permite_desligar_a_contingencia()
    {
        var (a, _) = Preparar(); using var amb = a;
        var t = a.Tela<NotasFiscaisTela>("Notas fiscais");
        await a.ComDialogo<ConfigFiscalDialog>(t.ConfigurarAsync, d =>
        {
            Assert.True(d.Contingencia.IsChecked);
            d.Contingencia.IsChecked = false;
            d.SalvarAsync().GetAwaiter().GetResult();
        });
        Assert.False(a.S.ObterConfigFiscal().ContingenciaAutomatica);
    }
}
