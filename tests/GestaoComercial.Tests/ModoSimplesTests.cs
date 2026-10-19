using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

/// <summary>Modo simples (padrão) × modo avançado (escolhido em Configurações).</summary>
public class ModoSimplesTests
{
    static long Prod(Servico s, string nome, long estoque = 20, long venda = 1000) =>
        s.SalvarProduto(new ProdutoDados(nome, PrecoCusto: venda / 2, PrecoVenda: venda, EstoqueInicial: estoque, Unidade: "kg", EstoqueMin: 3));

    static readonly string[] Simples = { "Painel", "Nova venda", "Vendas", "A receber", "Clientes", "Produtos", "Configurações" };

    [AvaloniaFact]
    public void Padrao_e_simples_e_o_menu_so_tem_o_necessario()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        Assert.False(a.Janela.Avancado);
        Assert.Equal(Simples, a.Janela.MenuVisivel);
        a.Tela<PainelTela>("Painel"); a.Foto("modo_simples_painel");
    }

    [AvaloniaFact]
    public void Troca_de_modo_pela_configuracao_mostra_tudo_e_lembra_a_escolha()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var c = a.Tela<ConfigTela>("Configurações");
        Assert.Equal(0, c.Modo.SelectedIndex);
        a.Foto("config_modo");
        c.Modo.SelectedIndex = 1; Ambiente.Rodar();
        Assert.True(a.Janela.Avancado);
        Assert.Equal("avancado", a.S.ObterConfig(MainWindow.ConfigModo));
        Assert.Equal(12, a.Janela.MenuVisivel.Count);
        Assert.Contains("Entrada de mercadorias", a.Janela.MenuVisivel);
        Assert.Equal("Configurações", a.Janela.PaginaAtual);                       // continua na mesma página
        Assert.Equal(1, a.Tela<ConfigTela>("Configurações").Modo.SelectedIndex);   // a tela refeita já mostra o modo novo
        Assert.Contains("Modo avançado", a.P.UltimoToast.Texto);
        // reabrir o programa com o mesmo banco: continua avançado
        var b = new MainWindow(a.S); Assert.True(b.Avancado);
        a.Tela<ConfigTela>("Configurações").Modo.SelectedIndex = 0; Ambiente.Rodar();
        Assert.False(a.Janela.Avancado); Assert.Equal(Simples, a.Janela.MenuVisivel);
    }

    [AvaloniaFact]
    public async Task Formulario_de_produto_simples_tem_so_o_essencial_e_nao_perde_os_dados_escondidos()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            Assert.Equal(new[] { "nome", "categoria", "venda", "inicial" }, d.Chaves);
            a.Foto("form_produto_simples", d);
            d.Definir("nome", "Sabonete"); d.Definir("venda", "4,50"); d.Definir("inicial", "12");
            Assert.True(d.Salvar());
        });
        var p = a.S.ListarProdutos().Single();
        Assert.Equal((450L, 12L, "un"), (p.PrecoVenda, p.Estoque, p.Unidade));
        Assert.StartsWith("", p.Codigo); Assert.NotEmpty(p.Codigo);               // código gerado sozinho

        // editar no modo simples não zera custo, unidade, mínimo nem código que vieram do avançado
        var q = Prod(a.S, "Arroz", venda: 2000); var antes = a.S.ObterProduto(q);
        t = a.Tela<ProdutosTela>("Produtos"); t.Tabela.Selecionar(q.ToString());
        await a.ComDialogo<FormDialog>(t.EditarAsync, d =>
        {
            Assert.DoesNotContain("custo", d.Chaves);
            d.Definir("venda", "21,00"); Assert.True(d.Salvar());
        });
        var depois = a.S.ObterProduto(q);
        Assert.Equal(2100, depois.PrecoVenda);
        Assert.Equal((antes.Codigo, antes.PrecoCusto, antes.Unidade, antes.EstoqueMin, antes.Estoque), (depois.Codigo, depois.PrecoCusto, depois.Unidade, depois.EstoqueMin, depois.Estoque));
    }

    [AvaloniaFact]
    public async Task Avancado_continua_com_o_formulario_completo()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            Assert.Equal(new[] { "nome", "codigo", "categoria", "unidade", "custo", "margem", "venda", "minimo", "inicial" }, d.Chaves);
            d.Close();
        });
    }

    [AvaloniaFact]
    public void Lista_de_produtos_simples_tem_menos_colunas_e_botoes_de_estoque()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var q = Prod(a.S, "Arroz", estoque: 2, venda: 2000);
        var t = a.Tela<ProdutosTela>("Produtos");
        Assert.Equal(new[] { "Arroz", "Sem categoria", "R$ 20,00", "2", "Ativo" }, t.Tabela.Linhas.Single().C);
        Assert.Equal("alerta", t.Tabela.Linhas.Single().Estilo);                  // 2 <= mínimo 3: continua destacado
        Assert.NotNull(t.FindBotao("Chegou mercadoria")); Assert.NotNull(t.FindBotao("Corrigir estoque"));
        Assert.Null(t.FindBotao("Reajustar preços…"));
        a.Foto("produtos_simples");
    }

    [AvaloniaFact]
    public async Task Chegou_mercadoria_soma_ao_estoque_sem_fornecedor_e_corrigir_estoque_acerta()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var q = Prod(a.S, "Arroz", estoque: 2);
        var t = a.Tela<ProdutosTela>("Produtos"); t.Tabela.Selecionar(q.ToString());
        await a.ComDialogo<FormDialog>(t.ChegouMercadoriaAsync, d =>
        {
            Assert.Equal(q, d.Valor("produto"));                                  // já vem com o produto selecionado
            d.Definir("qtd", "0"); Assert.False(d.Salvar()); Assert.NotEmpty(d.Erro);
            d.Definir("qtd", "10"); Assert.True(d.Salvar());
        });
        Assert.Equal(12, a.S.ObterProduto(q).Estoque);
        Assert.Contains("agora tem 12", a.P.UltimoToast.Texto);
        var mov = a.S.ListarMovimentos(q).First();
        Assert.Equal(("ENTRADA", 10L), (mov.Tipo, mov.Quantidade));

        t = a.Tela<ProdutosTela>("Produtos"); t.Tabela.Selecionar(q.ToString());
        await a.ComDialogo<FormDialog>(t.CorrigirEstoqueAsync, d => { d.Definir("nova", "7"); Assert.True(d.Salvar()); });
        Assert.Equal(7, a.S.ObterProduto(q).Estoque);
        // nada selecionado: mensagem clara
        t = a.Tela<ProdutosTela>("Produtos"); t.Tabela.Selecionar("-1");
        await Assert.ThrowsAsync<ValidacaoException>(t.CorrigirEstoqueAsync);
    }

    [AvaloniaFact]
    public void Receber_mercadoria_valida_quantidade_e_produto_inativo()
    {
        using var a = new Ambiente(simples: true);
        var q = Prod(a.S, "Arroz", estoque: 2);
        Assert.Throws<ValidacaoException>(() => a.S.ReceberMercadoria(q, 0));
        Assert.Throws<ValidacaoException>(() => a.S.ReceberMercadoria(q, -3));
        Assert.Throws<NaoEncontradoException>(() => a.S.ReceberMercadoria(9999, 1));
        Assert.Equal(5, a.S.ReceberMercadoria(q, 3));
        a.S.ExcluirProduto(q);
        Assert.Equal(5, a.S.ObterProduto(q).Estoque);
    }

    [AvaloniaFact]
    public void Painel_simples_nao_mostra_lucro_nem_caixa_e_tem_chegou_mercadoria()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var t = a.Tela<PainelTela>("Painel");
        Assert.NotNull(t.FindBotao("Chegou mercadoria")); Assert.Null(t.FindBotao("Fechar caixa")); Assert.Null(t.FindBotao("Entrada de mercadorias"));
        Assert.DoesNotContain(t.Cartoes.Where(c => c.Parent != null), c => c == t.Cartoes[2]);   // cartão "Lucro no mês" fora da tela
        var b = new Ambiente(); b.Janela.Show();
        var tb = b.Tela<PainelTela>("Painel");
        Assert.NotNull(tb.FindBotao("Fechar caixa")); Assert.NotNull(tb.FindBotao("Entrada de mercadorias"));
        Assert.All(tb.Cartoes, c => Assert.NotNull(c.Parent));
        b.Dispose();
    }

    [AvaloniaFact]
    public async Task Venda_simples_esconde_desconto_troco_e_observacao_ate_pedir()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        Prod(a.S, "Item", venda: 8000);
        var t = a.Tela<VendaTela>("Nova venda");
        Assert.False(t.ExtrasVisiveis);
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        t.Clicar("＋ Desconto, troco ou observação");
        Assert.True(t.ExtrasVisiveis);
        a.Foto("venda_simples_extras");
        t.Clicar("10%"); Assert.Equal("TOTAL: R$ 72,00", t.TextoTotal);
        await t.FinalizarAsync();
        Assert.False(t.ExtrasVisiveis);                                           // depois da venda volta a ficar limpo
        Assert.Equal(7200, a.S.ListarVendas().Single().Total);
        // venda comum, sem abrir nada
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        await t.FinalizarAsync();
        Assert.Equal(2, a.S.ListarVendas().Count);
        a.Foto("venda_simples");
    }

    [AvaloniaFact]
    public async Task Venda_avancada_mostra_tudo_de_cara()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<VendaTela>("Nova venda");
        Assert.True(t.ExtrasVisiveis);
        Assert.Null(t.FindBotao("＋ Desconto, troco ou observação"));
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Cadastro_rapido_de_produto_dentro_da_venda_usa_o_formulario_simples()
    {
        using var a = new Ambiente(simples: true); a.Janela.Show();
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Caneta");
        a.P.RespostaConfirmar = true;
        await a.ComDialogo<FormDialog>(t.Editor.BuscarAsync, d =>
        {
            Assert.DoesNotContain("custo", d.Chaves);
            d.Definir("venda", "3,00"); d.Definir("inicial", "5"); Assert.True(d.Salvar());
        });
        Assert.Single(t.Editor.Itens);                                           // recém-cadastrado já entra na venda
        Assert.Equal(300, a.S.ListarProdutos("Caneta").Single().PrecoVenda);
    }
}
