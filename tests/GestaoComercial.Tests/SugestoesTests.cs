using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

/// <summary>Lista de sugestões que aparece enquanto se digita o produto (venda e entrada).</summary>
public class SugestoesTests
{
    static void Tecla(TextBox t, Key k)
    {
        t.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = k, Source = t });
        Ambiente.Rodar();
    }

    static (Ambiente a, VendaTela t) Venda()
    {
        var a = new Ambiente(simples: true); a.Janela.Show();
        GaleriaTests.SemearLoja(a.S, a.Conn);
        return (a, a.Tela<VendaTela>("Nova venda"));
    }

    [AvaloniaFact]
    public void Digitar_mostra_sugestoes_sem_ligar_para_acento_e_maiusculas()
    {
        var (a, t) = Venda(); using var _ = a;
        var ed = t.Editor;
        ed.Busca.Digitar("calca");
        Assert.True(ed.SugestoesAbertas);
        Assert.Equal("Calça jeans skinny", ed.Sugestoes[0].Nome);
        ed.Busca.Digitar("BLUSA"); Assert.Equal("Blusa de alcinha branca", ed.Sugestoes.Single().Nome);
        ed.Busca.Digitar("a");                                                           // muitos resultados: no máximo 7, começando pelos que iniciam com "a"
        Assert.InRange(ed.Sugestoes.Count, 1, 7);
        ed.Busca.Digitar("de");                                                          // palavra no meio do nome também vale
        Assert.Contains(ed.Sugestoes, p => p.Nome == "Vestido floral midi" || p.Nome.Contains(" de "));
        ed.Busca.Digitar(""); Assert.False(ed.SugestoesAbertas);
        ed.Busca.Digitar("xyzw"); Assert.True(ed.SugestoesAbertas); Assert.Empty(ed.Sugestoes);   // avisa que não achou
        Tecla(ed.Busca, Key.Escape); Assert.False(ed.SugestoesAbertas);
    }

    [AvaloniaFact]
    public void Setas_e_Enter_adicionam_a_sugestao_destacada()
    {
        var (a, t) = Venda(); using var _ = a;
        var ed = t.Editor;
        ed.Busca.Digitar("o");
        var segunda = ed.Sugestoes[1];
        Tecla(ed.Busca, Key.Down); Assert.Equal(1, ed.IndiceSugestao);
        Tecla(ed.Busca, Key.Up); Tecla(ed.Busca, Key.Down);
        a.Foto("sugestoes_produto", TopLevel.GetTopLevel(ed.Sugestoes.Count > 0 ? (Visual?)FindPopupChild(ed) : null));
        Tecla(ed.Busca, Key.Enter);
        Assert.Equal(segunda.Id, ed.Itens.Single().ProdutoId);
        Assert.Equal(1, ed.Itens.Single().Quantidade);
        Assert.False(ed.SugestoesAbertas);
        Assert.Equal("", ed.Busca.Text);                                                  // pronto para o próximo
        a.Foto("venda_com_itens");
    }

    static Visual? FindPopupChild(EditorItens ed) =>
        ed.GetType().GetField("popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(ed) is Avalonia.Controls.Primitives.Popup p ? p.Child : null;

    [AvaloniaFact]
    public async Task Quantidade_no_texto_clique_e_codigo_de_barras()
    {
        var (a, t) = Venda(); using var _ = a;
        var ed = t.Editor;
        ed.Busca.Digitar("2x short");
        Assert.Equal("Short jeans", ed.Sugestoes.Single().Nome);
        Tecla(ed.Busca, Key.Enter);
        Assert.Equal(2, ed.Itens.Single().Quantidade);

        ed.Busca.Digitar("brinco"); await ed.EscolherSugestaoAsync(0);                   // clique na sugestão
        Assert.Equal(2, ed.Itens.Count);

        ed.Busca.Digitar("7891000100011");                                                // leitor de código de barras + Enter
        Tecla(ed.Busca, Key.Enter);
        Assert.Contains(ed.Itens, i => i.Nome == "Blusa de alcinha branca");
        Assert.Equal(3, ed.Itens.Count);
    }

    [AvaloniaFact]
    public void Produto_sem_estoque_aparece_avisado_e_nao_entra()
    {
        var (a, t) = Venda(); using var _ = a;
        var ed = t.Editor;
        ed.Busca.Digitar("base liquida");
        Assert.Equal(0, ed.Sugestoes.Single().Estoque);
        Tecla(ed.Busca, Key.Enter);
        Assert.Empty(ed.Itens);
        Assert.Contains("sem estoque", a.P.Ultima.Texto);
    }

    [AvaloniaFact]
    public void Enter_sem_resultado_oferece_cadastrar()
    {
        var (a, t) = Venda(); using var _ = a;
        var ed = t.Editor;
        a.P.RespostaConfirmar = false;
        ed.Busca.Digitar("Saia longa");
        Tecla(ed.Busca, Key.Enter);
        Assert.Contains("Quer cadastrar", a.P.Confirmacoes.Last());
        Assert.Empty(ed.Itens);
    }

    [AvaloniaFact]
    public void Na_entrada_a_sugestao_so_seleciona_para_digitar_quantidade_e_custo()
    {
        using var a = new Ambiente(); a.Janela.Show();
        GaleriaTests.SemearLoja(a.S, a.Conn);
        var t = a.Tela<EntradasTela>("Entrada de mercadorias");
        var ed = t.Editor;
        ed.Busca.Digitar("colar");
        Tecla(ed.Busca, Key.Enter);
        Assert.Equal("Colar de pérolas", ed.ProdutoSelecionado?.Nome);
        Assert.Empty(ed.Itens);
        Assert.Equal("18,00", ed.Preco.Text);                                             // custo do produto
    }
}
