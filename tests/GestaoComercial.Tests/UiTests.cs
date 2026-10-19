using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace GestaoComercial.Tests;

static class UiExt
{
    public static void Clicar(this Visual raiz, string texto)
    {
        var b = raiz.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => ((x.Tag as string) ?? (x.Content as string)) == texto && x.IsVisible)
                ?? throw new InvalidOperationException($"Botão '{texto}' não encontrado");
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Ambiente.Rodar();
    }

    public static void Digitar(this TextBox t, string texto) { t.Text = texto; Ambiente.Rodar(); }
}

public class UiTests
{
    // ------------------------------------------------------------------ cenário com dados
    static (long bebidas, long limpeza, long papel) Semear(Servico s)
    {
        var bebidas = s.SalvarCategoria("Bebidas"); var limpeza = s.SalvarCategoria("Limpeza"); var papel = s.SalvarCategoria("Papelaria");
        void P(string n, long c, long cu, long v, long est, long min) => s.SalvarProduto(new ProdutoDados(n, CategoriaId: c, PrecoCusto: cu, PrecoVenda: v, EstoqueInicial: est, EstoqueMin: min));
        P("Água mineral 500ml", bebidas, 120, 250, 50, 10); P("Refrigerante 2L", bebidas, 380, 650, 24, 6);
        P("Suco de uva 1L", bebidas, 420, 790, 30, 5); P("Detergente 500ml", limpeza, 150, 299, 3, 5);
        P("Sabão em pó 1kg", limpeza, 690, 1190, 8, 3); P("Caderno 96 folhas", papel, 890, 1590, 12, 4); P("Caneta azul", papel, 80, 250, 100, 20);
        s.SalvarEmpresa(new Empresa("Mercadinho Boa Compra", "11222333000181", "Rua das Flores, 123 - Centro", "(11) 4002-8922", "Obrigado pela preferência!"));
        s.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados("Distribuidora Alfa Ltda", "11222333000181", "(11) 3333-4444"));
        s.SalvarPessoa(TipoPessoa.Fornecedor, new PessoaDados("Papelaria Atacado Beta"));
        s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria Silva", "529.982.247-25", "(11) 99999-0000"));
        s.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("João Pereira"));
        return (bebidas, limpeza, papel);
    }

    static long Pid(Servico s, string busca) => s.ListarProdutos(busca)[0].Id;

    // ------------------------------------------------------------------ produtos
    [AvaloniaFact]
    public async Task Produto_novo_pelo_formulario_valida_e_salva()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var cat = a.S.SalvarCategoria("Bebidas");
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            d.Definir("nome", "Água mineral 500ml"); d.Definir("venda", "abc");
            Assert.False(d.Salvar());                                   // dado inválido: o formulário NÃO fecha
            Assert.Contains("inválido", a.P.Ultima.Texto);
            Assert.Equal(Nivel.Aviso, a.P.Ultima.Nivel);
            a.Foto("form_erro", d);
            d.Definir("venda", "2,50"); d.Definir("custo", "1,20"); d.Definir("inicial", "50"); d.Definir("minimo", "10"); d.Definir("categoria", "Bebidas");
            Assert.True(d.Salvar());
        });
        var p = Assert.Single(a.S.ListarProdutos());
        Assert.Equal(("Água mineral 500ml", 250L, 120L, 50L, cat), (p.Nome, p.PrecoVenda, p.PrecoCusto, p.Estoque, p.CategoriaId ?? 0));
        Assert.Single(t.Tabela.Linhas);
    }

    [AvaloniaFact]
    public async Task Produto_campo_obrigatorio_vazio_nao_fecha_formulario()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            Assert.False(d.Salvar());
            Assert.Contains("Nome do produto", a.P.Ultima.Texto);
            d.Close();
        });
        Assert.Empty(a.S.ListarProdutos());
    }

    [AvaloniaFact]
    public async Task Produto_editar_nao_altera_estoque_e_exclusao_com_historico_inativa()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<ProdutosTela>("Produtos");
        var id = Pid(a.S, "Caneta");
        t.Tabela.Selecionar(id.ToString());
        await a.ComDialogo<FormDialog>(t.EditarAsync, d => { d.Definir("nome", "Caneta esferográfica"); d.Definir("estoque", "9999"); Assert.True(d.Salvar()); });
        Assert.Equal(("Caneta esferográfica", 100L), (a.S.ObterProduto(id).Nome, a.S.ObterProduto(id).Estoque));
        // excluir: tem histórico (estoque inicial) -> inativa
        t.Tabela.Selecionar(id.ToString());
        a.P.Msgs.Clear();
        await t.ExcluirAsync();
        Assert.False(a.S.ObterProduto(id).Ativo);
        Assert.Equal("Inativado", a.P.Ultima.Titulo);
        Assert.DoesNotContain(t.Tabela.Linhas, l => l.Id == id.ToString());
        t.Inativos.IsChecked = true; Ambiente.Rodar();
        Assert.Contains(t.Tabela.Linhas, l => l.Id == id.ToString() && l.Estilo == "cancelado");
    }

    [AvaloniaFact]
    public async Task Produto_sem_selecao_e_filtros()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<ProdutosTela>("Produtos");
        t.Tabela.SelectedItem = null;
        await Assert.ThrowsAsync<ValidacaoException>(t.EditarAsync);
        t.Busca.Digitar("%"); Assert.Empty(t.Tabela.Linhas);
        t.Busca.Digitar("Caderno"); Assert.Single(t.Tabela.Linhas);
        t.Busca.Digitar(""); t.SelecionarTipo("Papelaria"); Ambiente.Rodar(); Assert.Equal(2, t.Tabela.Linhas.Count);
        a.Foto("produtos");
    }

    [AvaloniaFact]
    public async Task Tipos_de_produto_dialogo()
    {
        using var a = new Ambiente(); a.Janela.Show(); var (bebidas, _, _) = Semear(a.S);
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<CategoriasDialog>(t.GerenciarTiposAsync, d =>
        {
            a.Foto("categorias", d);
            d.Clicar("Excluir");                                             // nada selecionado
            Assert.Contains("Selecione", a.P.Ultima.Texto);
            var tab = d.GetVisualDescendants().OfType<Tabela>().First();
            tab.Selecionar(bebidas.ToString()); d.Clicar("Excluir");         // em uso
            Assert.Contains("sendo usado por 3 produto(s)", a.P.Ultima.Texto);
            d.Close();
        });
    }

    // ------------------------------------------------------------------ clientes / fornecedores
    [AvaloniaFact]
    public async Task Cliente_cpf_invalido_email_invalido_e_duplicado()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ClientesTela>("Clientes");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            d.Definir("nome", "Fulano"); d.Definir("documento", "111.111.111-11");
            Assert.False(d.Salvar()); Assert.Contains("CPF inválido", a.P.Ultima.Texto);
            d.Definir("documento", "529.982.247-25"); d.Definir("email", "sem-arroba");
            Assert.False(d.Salvar()); Assert.Contains("E-mail inválido", a.P.Ultima.Texto);
            d.Definir("email", "fulano@ex.com");
            Assert.True(d.Salvar());
        });
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            d.Definir("nome", "Outro"); d.Definir("documento", "52998224725");
            Assert.False(d.Salvar()); Assert.Contains("Já existe um cliente", a.P.Ultima.Texto);
            d.Close();
        });
        Assert.Single(a.S.ListarPessoas(TipoPessoa.Cliente));
        Assert.Equal("529.982.247-25", t.Tabela.Linhas[0].C[1]);
    }

    [AvaloniaFact]
    public async Task Fornecedor_com_historico_e_inativado()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var forn = a.S.ListarPessoas(TipoPessoa.Fornecedor)[0];
        a.S.RegistrarEntrada(forn.Id, new[] { new ItemEntradaInput(Pid(a.S, "Caneta"), 5, 80) });
        var t = a.Tela<FornecedoresTela>("Fornecedores");
        t.Tabela.Selecionar(forn.Id.ToString());
        await t.ExcluirAsync();
        Assert.False(a.S.ObterPessoa(TipoPessoa.Fornecedor, forn.Id).Ativo);
        Assert.Equal("Inativado", a.P.Ultima.Titulo);
    }

    [AvaloniaFact]
    public async Task Excluir_pede_confirmacao_e_respeita_o_nao()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<ClientesTela>("Clientes");
        t.Tabela.Selecionar(a.S.ListarPessoas(TipoPessoa.Cliente)[0].Id.ToString());
        a.P.RespostaConfirmar = false;
        await t.ExcluirAsync();
        Assert.Equal(2, a.S.ListarPessoas(TipoPessoa.Cliente).Count);      // nada foi excluído
        a.P.RespostaConfirmar = true;
        t.Tabela.Selecionar(a.S.ListarPessoas(TipoPessoa.Cliente)[0].Id.ToString());
        await t.ExcluirAsync();
        Assert.Single(a.S.ListarPessoas(TipoPessoa.Cliente));              // sem histórico: excluído de fato
    }

    // ------------------------------------------------------------------ entrada de mercadorias
    [AvaloniaFact]
    public async Task Entrada_de_mercadorias_completa()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<EntradasTela>("Entrada de mercadorias");
        t.Fornecedor.SelectedItem = "Distribuidora Alfa Ltda"; t.Documento.Digitar("NF 1234");
        var ed = t.Editor;
        ed.Busca.Digitar("Suco"); await ed.BuscarAsync();                       // único resultado: seleciona direto
        Assert.Equal("Suco de uva 1L", ed.ProdutoSelecionado?.Nome);
        ed.Quantidade.Digitar("30"); ed.Preco.Digitar("4,10"); await ed.AdicionarAsync();
        ed.Busca.Digitar("Detergente"); await ed.BuscarAsync(); ed.Quantidade.Digitar("20"); await ed.AdicionarAsync();
        ed.Busca.Digitar("Caneta"); await ed.BuscarAsync();
        ed.Quantidade.Digitar("abc"); await Assert.ThrowsAsync<ValidacaoException>(ed.AdicionarAsync);
        ed.Quantidade.Digitar("-3"); await Assert.ThrowsAsync<ValidacaoException>(ed.AdicionarAsync);
        ed.Quantidade.Digitar("10"); await ed.AdicionarAsync();
        Assert.Equal(3, ed.Itens.Count);
        a.Foto("entrada_antes");
        await t.RegistrarAsync();
        Assert.Equal((30L + 30, 20L + 3, 100L + 10), (a.S.ObterProduto(Pid(a.S, "Suco")).Estoque, a.S.ObterProduto(Pid(a.S, "Detergente")).Estoque, a.S.ObterProduto(Pid(a.S, "Caneta")).Estoque));
        Assert.Equal(410, a.S.ObterProduto(Pid(a.S, "Suco")).PrecoCusto);     // custo atualizado
        Assert.Empty(ed.Itens);                                                // tela limpa
    }

    [AvaloniaFact]
    public async Task Entrada_validacoes_de_tela()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<EntradasTela>("Entrada de mercadorias");
        await t.RegistrarAsync(); Assert.Equal("Selecione o fornecedor.", a.P.Ultima.Texto);
        t.Fornecedor.SelectedItem = "Distribuidora Alfa Ltda";
        await t.RegistrarAsync(); Assert.Equal("Adicione ao menos um produto.", a.P.Ultima.Texto);
        t.Editor.Busca.Digitar(Pid(a.S, "Caneta") is var id ? a.S.ObterProduto(id).Codigo : ""); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        t.Data.Digitar("31/02/2026"); await Assert.ThrowsAsync<ValidacaoException>(t.RegistrarAsync);
        t.Data.Digitar("01/01/2999"); await Assert.ThrowsAsync<ValidacaoException>(t.RegistrarAsync);
        Assert.Empty(a.S.ListarEntradas());
    }

    [AvaloniaFact]
    public async Task Entrada_historico_ver_e_cancelar()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var forn = a.S.ListarPessoas(TipoPessoa.Fornecedor)[0];
        var pid = Pid(a.S, "Detergente");
        var eid = a.S.RegistrarEntrada(forn.Id, new[] { new ItemEntradaInput(pid, 7, 20) }, "NF9");
        var t = a.Tela<EntradasTela>("Entrada de mercadorias");
        await a.ComDialogo<HistoricoEntradasDialog>(t.HistoricoAsync, d =>
        {
            var tab = d.GetVisualDescendants().OfType<Tabela>().First();
            tab.Selecionar(eid.ToString());
            a.Foto("hist_entradas", d);
            d.Clicar("Cancelar entrada");
            d.Close();
        });
        Assert.Equal("CANCELADA", a.S.ObterEntrada(eid).Status);
        Assert.Equal(3, a.S.ObterProduto(pid).Estoque);
    }

    // ------------------------------------------------------------------ venda
    [AvaloniaFact]
    public async Task Venda_completa_pela_interface_gera_nota_e_atualiza_estoque()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<VendaTela>("Nova venda");
        t.Cliente.SelectedItem = "Maria Silva"; t.Pagamento.SelectedItem = "Pix";
        var ed = t.Editor;
        ed.Busca.Digitar(a.S.ObterProduto(Pid(a.S, "Refrigerante")).Codigo); await ed.BuscarAsync();   // código exato
        ed.Quantidade.Digitar("3"); await ed.AdicionarAsync();
        ed.Busca.Digitar("Caderno"); await ed.BuscarAsync(); ed.Quantidade.Digitar("2"); await ed.AdicionarAsync();
        ed.Busca.Digitar("Água"); await ed.BuscarAsync(); ed.Quantidade.Digitar("999");
        var ex = await Assert.ThrowsAsync<ValidacaoException>(ed.AdicionarAsync);                    // estoque insuficiente (checagem na tela)
        Assert.Contains("Estoque insuficiente", ex.Message);
        ed.Busca.Digitar("zzzz"); await ed.BuscarAsync();                                            // nada encontrado
        Assert.Equal(Nivel.Info, a.P.Ultima.Nivel); Assert.Contains("Nenhum produto", a.P.Ultima.Texto);
        Assert.Null(ed.ProdutoSelecionado);                                                          // editar a busca descarta a escolha anterior
        t.Desconto.Digitar("2,00");
        Assert.Equal("TOTAL: R$ 49,30", t.TextoTotal);
        a.Foto("venda");
        a.P.Msgs.Clear(); a.P.Abertos.Clear();
        await t.FinalizarAsync();
        var v = Assert.Single(a.S.ListarVendas());
        Assert.Equal((4930L, "Maria Silva", "Pix"), (v.Total, v.ClienteNome, v.FormaPagamento));
        Assert.Equal(21, a.S.ObterProduto(Pid(a.S, "Refrigerante")).Estoque);
        Assert.Single(a.P.Abertos);                                   // nota aberta no navegador
        Assert.EndsWith($"nota-{v.Id:000000}.html", a.P.Abertos[0]);
        Assert.True(File.Exists(a.P.Abertos[0]));
        Assert.Empty(ed.Itens);                                       // carrinho limpo
    }

    [AvaloniaFact]
    public async Task Venda_estoque_zerado_no_meio_nao_grava_nada_e_mostra_aviso()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Caderno"); await t.Editor.BuscarAsync(); t.Editor.Quantidade.Digitar("1"); await t.Editor.AdicionarAsync();
        a.S.AjustarEstoque(Pid(a.S, "Caderno"), 0, "alguém zerou");            // estoque muda entre a busca e a finalização
        a.P.Msgs.Clear();
        t.Clicar("Finalizar venda e emitir nota");                            // clique real: erro vira mensagem, não exceção
        Assert.Equal(Nivel.Aviso, a.P.Ultima.Nivel);
        Assert.Contains("Estoque insuficiente para 'Caderno 96 folhas'", a.P.Ultima.Texto);
        Assert.Empty(a.S.ListarVendas());
        Assert.Single(t.Editor.Itens);                                         // carrinho preservado para o usuário corrigir
    }

    [AvaloniaFact]
    public async Task Venda_validacoes_de_tela()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<VendaTela>("Nova venda");
        await t.FinalizarAsync(); Assert.Contains("ao menos um produto", a.P.Ultima.Texto);
        await Assert.ThrowsAsync<ValidacaoException>(t.Editor.AdicionarAsync);                 // sem produto escolhido
        t.Editor.Busca.Digitar("Caneta"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        t.Desconto.Digitar("999,00"); await Assert.ThrowsAsync<ValidacaoException>(t.FinalizarAsync);
        t.Desconto.Digitar("abc"); await Assert.ThrowsAsync<ValidacaoException>(t.FinalizarAsync);
        Assert.Equal("TOTAL: R$ 2,50", t.TextoTotal);   // desconto inválido enquanto digita não quebra o total
        t.Desconto.Digitar(""); a.P.RespostaConfirmar = false; await t.FinalizarAsync();
        Assert.Empty(a.S.ListarVendas());               // usuário disse "Não"
    }

    [AvaloniaFact]
    public async Task Editor_remover_alterar_quantidade_e_mesclar_linhas()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var ed = a.Tela<VendaTela>("Nova venda").Editor;
        ed.Busca.Digitar("Caneta"); await ed.BuscarAsync(); ed.Quantidade.Digitar("2"); await ed.AdicionarAsync();
        ed.Busca.Digitar("Caneta"); await ed.BuscarAsync(); ed.Quantidade.Digitar("3"); await ed.AdicionarAsync();
        Assert.Equal(5, Assert.Single(ed.Itens).Quantidade);                 // mesmo produto e preço: soma
        ed.Tabela.Selecionar("0");
        a.P.RespostaPergunta = "200"; await Assert.ThrowsAsync<ValidacaoException>(ed.AlterarQuantidadeAsync);   // acima do estoque (100)
        a.P.RespostaPergunta = "7"; await ed.AlterarQuantidadeAsync(); Assert.Equal(7, ed.Itens[0].Quantidade);
        a.P.RespostaPergunta = null; await ed.AlterarQuantidadeAsync(); Assert.Equal(7, ed.Itens[0].Quantidade);   // cancelou
        await ed.RemoverAsync(); Assert.Empty(ed.Itens);
        await Assert.ThrowsAsync<ValidacaoException>(ed.RemoverAsync);
    }

    [AvaloniaFact]
    public async Task Selecao_de_produto_com_varios_resultados()
    {
        using var a = new Ambiente(); a.Janela.Show();
        a.S.SalvarProduto(new ProdutoDados("Parafuso", PrecoVenda: 50, EstoqueInicial: 10));
        a.S.SalvarProduto(new ProdutoDados("Parafuso Phillips", PrecoVenda: 60, EstoqueInicial: 5));
        var ed = a.Tela<VendaTela>("Nova venda").Editor;
        ed.Busca.Digitar("Paraf");
        await a.ComDialogo<SelecionarProdutoDialog>(ed.BuscarAsync, d =>
        {
            a.Foto("seletor_produto", d);
            Assert.Equal(2, d.GetVisualDescendants().OfType<Tabela>().First().Linhas.Count);
            d.GetVisualDescendants().OfType<Tabela>().First().Selecionar(a.S.ListarProdutos("Phillips")[0].Id.ToString());
            d.Escolher();
        });
        Assert.Equal("Parafuso Phillips", ed.ProdutoSelecionado?.Nome);
    }

    // ------------------------------------------------------------------ notas
    [AvaloniaFact]
    public async Task Notas_visualizar_imprimir_cancelar_exportar()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var pid = Pid(a.S, "Refrigerante");
        var vid = a.S.RegistrarVenda(new[] { new ItemVendaInput(pid, 4, 650) }, a.S.ListarPessoas(TipoPessoa.Cliente)[0].Id, 100, "Pix");
        var t = a.Tela<NotasTela>("Vendas");
        Assert.Contains("1 venda(s) concluída(s)", t.TextoRodape);
        t.Tabela.Selecionar(vid.ToString());
        await a.ComDialogo<NotaDialog>(t.VisualizarAsync, d => { a.Foto("nota_dialog", d); d.Close(); });
        // imprimir: abre HTML no navegador; se não abrir, avisa onde o arquivo está
        a.P.Abertos.Clear(); await t.ImprimirAsync();
        Assert.Contains("nota-", a.P.Abertos[0]); Assert.Contains("Mercadinho Boa Compra", File.ReadAllText(a.P.Abertos[0]));
        a.P.AbrirFunciona = false; a.P.Msgs.Clear(); await t.ImprimirAsync();
        Assert.Contains("Não foi possível abrir o navegador", a.P.Ultima.Texto); Assert.Contains("nota-", a.P.Ultima.Texto);
        // exportar CSV
        var csv = Path.Combine(a.Pasta, "vendas.csv"); a.P.ArquivoParaSalvar = csv; await t.ExportarAsync();
        Assert.Contains("João Pereira", File.ReadAllText(csv)); Assert.Contains("25,00", File.ReadAllText(csv));
        a.P.ArquivoParaSalvar = null; File.Delete(csv); await t.ExportarAsync(); Assert.False(File.Exists(csv));   // usuário cancelou o seletor
        a.Foto("notas");
        // cancelar
        a.P.RespostaPergunta = null; await t.CancelarAsync(); Assert.Equal("CONCLUIDA", a.S.ObterVenda(vid).Status);   // desistiu do motivo
        a.P.RespostaPergunta = "cliente desistiu"; await t.CancelarAsync();
        Assert.True(a.S.ObterVenda(vid).Cancelada); Assert.Equal(24, a.S.ObterProduto(pid).Estoque);
        a.P.Msgs.Clear(); await t.CancelarAsync(); Assert.Contains("já está cancelada", a.P.Ultima.Texto);
        t.Situacao.SelectedItem = "Canceladas"; Ambiente.Rodar(); Assert.Single(t.Tabela.Linhas);
        a.Foto("notas_cancelada");
    }

    [AvaloniaFact]
    public async Task Notas_filtro_de_data_invalido_mostra_mensagem()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<NotasTela>("Vendas");
        t.De.Text = "xx";
        t.Clicar("Filtrar");
        Assert.Contains("Data inicial inválida", a.P.Ultima.Texto);
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------------ estoque
    [AvaloniaFact]
    public async Task Estoque_agrupado_por_tipo_ajuste_e_selecao_de_grupo()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<EstoqueTela>("Estoque");
        Assert.Equal(3, t.Tabela.Linhas.Count(l => l.Estilo == "grupo"));
        Assert.Contains(t.Tabela.Linhas, l => l.Estilo == "alerta" && l.C[0].Contains("Detergente"));
        a.Foto("estoque");
        var pid = Pid(a.S, "Detergente");
        t.Tabela.Selecionar(pid.ToString());
        await a.ComDialogo<FormDialog>(t.AjustarAsync, d => { d.Definir("nova", "20"); d.Definir("motivo", "inventário"); Assert.True(d.Salvar()); });
        Assert.Equal(20, a.S.ObterProduto(pid).Estoque);
        t.Tabela.Selecionar("cat:Bebidas");
        await Assert.ThrowsAsync<ValidacaoException>(t.AjustarAsync);          // tipo selecionado, não produto
        t.SomenteBaixo.IsChecked = true; Ambiente.Rodar();
        Assert.All(t.Tabela.Linhas.Where(l => l.Estilo != "grupo"), l => Assert.Equal("alerta", l.Estilo));
        t.SomenteBaixo.IsChecked = false; Ambiente.Rodar();
        await a.ComDialogo<MovimentosDialog>(() => { t.Tabela.Selecionar(pid.ToString()); return t.HistoricoAsync(true); }, d => { a.Foto("movimentos", d); d.Close(); });
    }

    [AvaloniaFact]
    public async Task Ajuste_invalido_nao_fecha_formulario()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var t = a.Tela<EstoqueTela>("Estoque");
        var pid = Pid(a.S, "Caneta"); t.Tabela.Selecionar(pid.ToString());
        await a.ComDialogo<FormDialog>(t.AjustarAsync, d =>
        {
            d.Definir("nova", "100"); d.Definir("motivo", "igual");
            Assert.False(d.Salvar()); Assert.Contains("Nada a ajustar", a.P.Ultima.Texto);
            d.Definir("nova", "-1"); Assert.False(d.Salvar());
            d.Close();
        });
        Assert.Equal(100, a.S.ObterProduto(pid).Estoque);
    }

    // ------------------------------------------------------------------ painel / relatórios / config
    [AvaloniaFact]
    public void Painel_e_relatorios_mostram_numeros_corretos()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        var pid = Pid(a.S, "Caneta"); var refri = Pid(a.S, "Refrigerante");
        a.S.RegistrarVenda(new[] { new ItemVendaInput(pid, 12, 250) });
        var v2 = a.S.RegistrarVenda(new[] { new ItemVendaInput(refri, 20, 650) }, forma: "Cartão de débito");
        var v3 = a.S.RegistrarVenda(new[] { new ItemVendaInput(refri, 1, 650) }); a.S.CancelarVenda(v3, "x");
        var p = a.Tela<PainelTela>("Painel");
        Assert.Equal("R$ 160,00", p.Cartoes[0].Valor); Assert.Equal("2 venda(s)", p.Cartoes[0].Detalhe);
        a.Foto("painel");
        var r = a.Tela<RelatoriosTela>("Relatórios");
        Assert.Equal("R$ 160,00", r.Resumo("total")); Assert.Equal("R$ 80,00", r.Resumo("ticket"));
        Assert.StartsWith("1 (R$ 6,50", r.Resumo("canc"));
        a.Foto("relatorios");
        r.De.Text = "32/13/2026"; r.Clicar("Atualizar"); Assert.Contains("Data inicial inválida", a.P.Ultima.Texto);
        r.De.Text = "01/12/2026"; r.Ate.Text = "01/01/2026"; r.Clicar("Atualizar"); Assert.Contains("não pode ser maior", a.P.Ultima.Texto);
        r.Clicar("Hoje"); Assert.Equal("R$ 160,00", r.Resumo("total"));
    }

    [AvaloniaFact]
    public async Task Config_empresa_validacoes_e_backup()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var c = a.Tela<ConfigTela>("Configurações");
        Assert.Equal("Minha Empresa", c.Nome.Text);
        c.Documento.Digitar("11.222.333/0001-82"); c.Clicar("Salvar dados da empresa"); Assert.Contains("CNPJ inválido", a.P.Ultima.Texto);
        c.Documento.Digitar("11.222.333/0001-81"); c.Nome.Digitar(""); c.Clicar("Salvar dados da empresa"); Assert.Contains("Informe o nome da empresa", a.P.Ultima.Texto);
        c.Nome.Digitar("Loja Nova"); c.Clicar("Salvar dados da empresa");
        Assert.Equal("Loja Nova", a.S.ObterEmpresa().Nome); Assert.Equal("Dados da empresa salvos. As próximas notas já sairão com eles.", a.P.Ultima.Texto);
        a.Foto("config");
        var bk = Path.Combine(a.Pasta, "bk", "copia.db"); a.P.ArquivoParaSalvar = bk;
        await c.BackupAsync(); Assert.True(File.Exists(bk));
        a.P.AbrirFunciona = false; c.Clicar("Abrir pasta de dados"); Assert.Contains("Ela fica em", a.P.Ultima.Texto);
    }

    // ------------------------------------------------------------------ robustez
    [AvaloniaFact]
    public void Banco_ocupado_vira_mensagem_amigavel_sem_gravar()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        using var outro = Banco.Conectar(Path.Combine(a.Pasta, "gestao.db"));
        using (var c = outro.CreateCommand()) { c.CommandText = "BEGIN IMMEDIATE"; c.ExecuteNonQuery(); }
        using (var c = a.Conn.CreateCommand()) { c.CommandText = "PRAGMA busy_timeout = 150"; c.ExecuteNonQuery(); }
        var t = a.Tela<ClientesTela>("Clientes");
        var antes = a.S.ListarCategorias().Count;
        try
        {
            var erro = Assert.Throws<SqliteException>(() => a.S.SalvarCategoria("Nova"));
            _ = Erros.Tratar(erro, a.Janela);
        }
        finally { using var c = outro.CreateCommand(); c.CommandText = "ROLLBACK"; c.ExecuteNonQuery(); }
        Assert.Equal(Nivel.Erro, a.P.Ultima.Nivel);
        Assert.Contains("ocupado", a.P.Ultima.Texto);
        Assert.Equal(antes, a.S.ListarCategorias().Count);
    }

    [AvaloniaFact]
    public async Task Excecao_inesperada_e_tratada_e_registrada_no_log()
    {
        using var a = new Ambiente(); a.Janela.Show();
        await Erros.Tratar(new InvalidOperationException("boom"), a.Janela);
        Assert.Equal(Nivel.Erro, a.P.Ultima.Nivel);
        Assert.Contains("boom", a.P.Ultima.Texto);
        Assert.Contains("boom", File.ReadAllText(Config.ArquivoLog()));
        await Erros.Tratar(new IOException("disco"), a.Janela); Assert.Equal("Erro de arquivo", a.P.Ultima.Titulo);
        await Erros.Tratar(new UnauthorizedAccessException("negado"), a.Janela); Assert.Equal("Sem permissão", a.P.Ultima.Titulo);
    }

    [AvaloniaFact]
    public void Mensagens_de_banco_para_os_erros_mais_comuns()
    {
        static SqliteException Ex(int cod, string msg) => new(msg, cod);
        Assert.Contains("ocupado", Erros.MensagemBanco(Ex(5, "database is locked")));
        Assert.Contains("corrompido", Erros.MensagemBanco(Ex(26, "file is not a database")));
        Assert.Contains("disco está cheio", Erros.MensagemBanco(Ex(13, "database or disk is full")));
        Assert.Contains("permissão", Erros.MensagemBanco(Ex(8, "attempt to write a readonly database")));
    }

    [AvaloniaFact]
    public void Exportar_produtos_gera_csv_valido()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        a.S.SalvarProduto(new ProdutoDados("=cmd|' /C calc'!A0", PrecoVenda: 100));
        var t = a.Tela<ProdutosTela>("Produtos");
        var csv = Path.Combine(a.Pasta, "produtos.csv"); a.P.ArquivoParaSalvar = csv;
        t.ExportarAsync().GetAwaiter().GetResult();
        var txt = File.ReadAllText(csv);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(csv)[..3]);   // BOM: o Excel reconhece os acentos
        Assert.StartsWith("Código;Produto;Tipo", txt);
        Assert.Contains("'=cmd", txt);                // proteção contra injeção de fórmula
        Assert.Contains("2,50", txt);
    }

    // ------------------------------------------------------------------ capturas para conferência visual
    [AvaloniaFact]
    public void Capturas_de_todas_as_abas_com_dados()
    {
        using var a = new Ambiente(); a.Janela.Show(); Semear(a.S);
        a.S.RegistrarVenda(new[] { new ItemVendaInput(Pid(a.S, "Refrigerante"), 3, 650) }, a.S.ListarPessoas(TipoPessoa.Cliente)[0].Id, 0, "Pix");
        foreach (var nome in a.Janela.Telas.Keys.ToList()) { a.Janela.IrPara(nome); a.Foto("aba_" + nome.Replace(' ', '_')); }
        Assert.Empty(a.P.Msgs.Where(m => m.Nivel != Nivel.Info));
    }
}
