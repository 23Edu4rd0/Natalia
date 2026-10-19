using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

public class UiNovasTests
{
    static long Prod(Servico s, string nome, long estoque = 20, long venda = 1000, long custo = 400, long min = 0, long? cat = null) =>
        s.SalvarProduto(new ProdutoDados(nome, CategoriaId: cat, PrecoCusto: custo, PrecoVenda: venda, EstoqueInicial: estoque, EstoqueMin: min));

    static async Task Vender(VendaTela t, string busca, long qtd = 1)
    {
        t.Editor.Busca.Digitar(busca); await t.Editor.BuscarAsync(); t.Editor.Quantidade.Digitar(qtd.ToString()); await t.Editor.AdicionarAsync();
    }

    // ------------------------------------------------------------------ fiado ponta a ponta
    [AvaloniaFact]
    public async Task Fiado_vender_ver_dever_receber_parcial_e_total()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Camiseta", venda: 5000);
        var maria = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria Silva", Telefone: "(11) 98888-7777"));
        var v = a.Tela<VendaTela>("Nova venda");
        await Vender(v, "Camiseta", 2);
        v.Pagamento.SelectedItem = "Fiado";
        a.P.Msgs.Clear();
        v.Clicar("Finalizar venda e emitir nota");                                  // sem cliente: bloqueia
        Assert.Contains("precisa de um cliente", a.P.Ultima.Texto);
        Assert.Empty(a.S.ListarVendas());
        v.ClienteNome = "Maria Silva";
        await v.FinalizarAsync();
        Assert.Equal(10000, a.S.SaldoCliente(maria));

        var r = a.Tela<ReceberTela>("A receber");
        Assert.Equal("R$ 100,00", r.Total.Valor);
        a.Foto("a_receber");
        r.Tabela.Selecionar(maria.ToString());
        // pagamento maior que a dívida é recusado e o formulário continua aberto
        await a.ComDialogo<FormDialog>(r.ReceberAsync, d =>
        {
            d.Definir("valor", "150,00"); Assert.False(d.Salvar()); Assert.Contains("maior que a dívida", d.Erro);
            d.Definir("valor", "40,00"); d.Definir("forma", "Pix"); Assert.True(d.Salvar());
        });
        Assert.Equal(6000, a.S.SaldoCliente(maria)); Assert.Equal("R$ 60,00", r.Total.Valor);
        r.Tabela.Selecionar(maria.ToString());
        await a.ComDialogo<FormDialog>(r.ReceberAsync, d => Assert.True(d.Salvar()));       // valor padrão = saldo inteiro
        Assert.Equal(0, a.S.SaldoCliente(maria));
        Assert.Empty(r.Tabela.Linhas);
    }

    [AvaloniaFact]
    public async Task Extrato_cobranca_whatsapp_e_impressao()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Vestido", venda: 20000);
        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Joana Costa", Telefone: "(21) 99999-1234"));
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 20000) }, c, forma: "Fiado");
        a.S.RegistrarRecebimento(c, 5000, "Dinheiro");
        var r = a.Tela<ReceberTela>("A receber"); r.Tabela.Selecionar(c.ToString());
        await a.ComDialogo<HistoricoClienteDialog>(r.ExtratoAsync, d =>
        {
            Assert.Equal("R$ 150,00", d.TextoSaldo);
            Assert.Equal(2, d.Fiado.Linhas.Count); Assert.Single(d.Compras.Linhas);
            a.Foto("cliente_historico", d);
            d.Close();
        });
        await r.CobrarAsync();
        Assert.Contains("https://wa.me/5521999991234?text=", a.P.Abertos[^1]);
        Assert.Contains("150%2C00", a.P.Abertos[^1]);                            // valor da dívida na mensagem
        a.P.Abertos.Clear(); await r.ImprimirAsync();
        Assert.Contains("extrato-fiado-", a.P.Abertos[0]); Assert.Contains("Joana Costa", File.ReadAllText(a.P.Abertos[0]));
        a.P.AbrirFunciona = false; a.P.Msgs.Clear(); await r.CobrarAsync();
        Assert.Contains("Não foi possível abrir o WhatsApp", a.P.Ultima.Texto);   // plano B: mostra o endereço
    }

    [AvaloniaFact]
    public async Task Receber_sem_selecao_e_cliente_sem_divida()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var r = a.Tela<ReceberTela>("A receber");
        await Assert.ThrowsAsync<ValidacaoException>(r.ReceberAsync);
        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Sem dívida"));
        a.P.Msgs.Clear();
        Assert.False(await Fluxos.ReceberPagamentoAsync(a.Janela, a.S, c));
        Assert.Contains("não tem nada a pagar", a.P.Ultima.Texto);
    }

    // ------------------------------------------------------------------ venda: troco, %, atalhos, cliente rápido, teclas
    [AvaloniaFact]
    public async Task Troco_e_desconto_em_porcentagem()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Bolsa", venda: 10000);
        var v = a.Tela<VendaTela>("Nova venda");
        await Vender(v, "Bolsa", 1);
        Assert.True(v.TrocoVisivel);                                       // Dinheiro é o padrão
        v.Desconto.Digitar("10%");
        Assert.Equal("TOTAL: R$ 90,00", v.TextoTotal); Assert.Contains("Desconto: -R$ 10,00", v.TextoSubtotal);
        v.ValorRecebido.Digitar("100"); Assert.Equal("R$ 10,00", v.TextoTroco);
        v.ValorRecebido.Digitar("50"); Assert.Equal("faltam R$ 40,00", v.TextoTroco);
        v.ValorRecebido.Digitar("abc"); Assert.Equal("valor inválido", v.TextoTroco);
        v.Pagamento.SelectedItem = "Pix"; Ambiente.Rodar(); Assert.False(v.TrocoVisivel);
        v.Desconto.Digitar("200%"); await Assert.ThrowsAsync<ValidacaoException>(v.FinalizarAsync);
        v.Desconto.Digitar("10%"); v.Pagamento.SelectedItem = "Dinheiro"; v.ValorRecebido.Digitar("100");
        a.Foto("venda_troco");
        a.P.Confirmacoes.Clear(); await v.FinalizarAsync();
        Assert.Equal(9000, Assert.Single(a.S.ListarVendas()).Total);
        Assert.Contains("Troco R$ 10,00", a.P.UltimoToast.Texto);              // aparece no aviso da venda
    }

    [AvaloniaFact]
    public async Task Atalhos_dos_mais_vendidos_adicionam_um_clique()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var x = Prod(a.S, "Blusa Azul", venda: 7990); var y = Prod(a.S, "Calça Jeans", venda: 12990); Prod(a.S, "Nunca vendido");
        a.S.RegistrarVenda(new[] { new ItemVendaInput(y, 5, 12990), new ItemVendaInput(x, 2, 7990) });
        var v = a.Tela<VendaTela>("Nova venda");
        Assert.Equal(new[] { "Calça Jeans", "Blusa Azul" }, v.Editor.NomesAtalhos);      // ordem do mais vendido
        a.Foto("venda_atalhos");
        var chip = v.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as string) == "Calça Jeans");
        chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Ambiente.Rodar();
        var item = Assert.Single(v.Editor.Itens);
        Assert.Equal(("Calça Jeans", 1L, 12990L), (item.Nome, item.Quantidade, item.Preco));
        chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Ambiente.Rodar();
        Assert.Equal(2, v.Editor.Itens[0].Quantidade);
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task Novo_cliente_na_hora_da_venda()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var v = a.Tela<VendaTela>("Nova venda");
        await a.ComDialogo<FormDialog>(v.NovoClienteAsync, d =>
        {
            d.Definir("nome", ""); Assert.False(d.Salvar());                         // nome obrigatório
            d.Definir("nome", "Cliente Novo"); d.Definir("telefone", "(11) 90000-0000"); Assert.True(d.Salvar());
        });
        Assert.Equal("Cliente Novo", v.ClienteNome);                        // já vem selecionado
        Assert.Equal("(11) 90000-0000", a.S.ListarPessoas(TipoPessoa.Cliente)[0].Telefone);
    }

    [AvaloniaFact]
    public void Teclas_de_atalho_F2_e_F9()
    {
        using var a = new Ambiente(); a.Janela.Show();
        a.Janela.IrPara("Painel");
        a.Janela.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None); Ambiente.Rodar();
        Assert.Equal("Nova venda", a.Janela.PaginaAtual);
        a.Janela.IrPara("Produtos");
        a.Janela.KeyPressQwerty(PhysicalKey.F1, RawInputModifiers.None); Ambiente.Rodar();
        Assert.Equal("Sobre", a.P.Ultima.Titulo);
        // F9 numa venda vazia: avisa que falta produto (e não quebra)
        a.Janela.IrPara("Nova venda"); a.P.Msgs.Clear();
        a.Janela.KeyPressQwerty(PhysicalKey.F9, RawInputModifiers.None); Ambiente.Rodar();
        Assert.Contains("Adicione ao menos um produto", a.P.UltimoToast.Texto);
    }

    // ------------------------------------------------------------------ caixa
    [AvaloniaFact]
    public async Task Caixa_troco_retirada_resumo_e_fechamento()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item", venda: 3000);
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 2, 3000) }, forma: "Dinheiro");
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 3000) }, forma: "Pix");
        var c = a.Tela<CaixaTela>("Caixa");
        await a.ComDialogo<FormDialog>(() => c.RegistrarAsync("SUPRIMENTO"), d => { d.Definir("valor", "100,00"); d.Definir("motivo", "troco inicial"); Assert.True(d.Salvar()); });
        await a.ComDialogo<FormDialog>(() => c.RegistrarAsync("SANGRIA"), d =>
        {
            d.Definir("valor", "20,00"); Assert.False(d.Salvar()); Assert.Contains("Motivo", d.Erro);   // retirada exige motivo
            d.Definir("motivo", "pagar entregador"); Assert.True(d.Salvar());
        });
        Assert.Equal("R$ 140,00", c.Gaveta.Valor);          // 100 + 60 (dinheiro) - 20
        Assert.Equal("R$ 90,00", c.Vendido.Valor);
        Assert.Equal(2, c.Lancamentos.Linhas.Count);
        a.Foto("caixa");
        c.Lancamentos.Selecionar(c.Resumo!.Movimentos[1].Id.ToString());
        await c.ExcluirAsync(); Assert.Equal("R$ 160,00", c.Gaveta.Valor);
        a.P.Abertos.Clear(); await c.ImprimirAsync();
        Assert.Contains("fechamento-caixa-", a.P.Abertos[0]); Assert.Contains("R$ 160,00", File.ReadAllText(a.P.Abertos[0]));
        c.Data.Digitar("01/01/2020"); c.Clicar("Ver");
        Assert.Equal("R$ 0,00", c.Gaveta.Valor);
        c.Data.Digitar("31/02/2026"); a.P.Msgs.Clear(); c.Clicar("Ver"); Assert.Contains("inválida", a.P.Ultima.Texto);
    }

    // ------------------------------------------------------------------ produtos / estoque / clientes
    [AvaloniaFact]
    public async Task Reajuste_de_precos_pela_tela()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var cat = a.S.SalvarCategoria("Roupas");
        var p1 = Prod(a.S, "Camisa", venda: 10000, cat: cat); var p2 = Prod(a.S, "Chaveiro", venda: 1000);
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.ReajustarAsync, d =>
        {
            d.Definir("pct", "abc"); Assert.False(d.Salvar()); Assert.Contains("inválida", d.Erro);
            d.Definir("pct", "0"); Assert.False(d.Salvar());
            d.Definir("tipo", "Roupas"); d.Definir("pct", "10"); Assert.True(d.Salvar());
        });
        Assert.Equal(11000, a.S.ObterProduto(p1).PrecoVenda); Assert.Equal(1000, a.S.ObterProduto(p2).PrecoVenda);   // só Roupas
        Assert.Contains("Reajustar 1 produto(s) em +10%", a.P.Confirmacoes[^1]);
        // usuário desiste na confirmação
        a.P.RespostaConfirmar = false;
        await a.ComDialogo<FormDialog>(t.ReajustarAsync, d => { d.Definir("pct", "-50"); Assert.True(d.Salvar()); });
        Assert.Equal(11000, a.S.ObterProduto(p1).PrecoVenda);
    }

    [AvaloniaFact]
    public async Task Lista_de_reposicao_imprime_e_exporta()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Pouco", estoque: 2, min: 10); Prod(a.S, "Muito", estoque: 100, min: 10);
        var t = a.Tela<EstoqueTela>("Estoque");
        await a.ComDialogo<ReposicaoDialog>(t.ReposicaoAsync, d =>
        {
            Assert.Single(d.Tabela.Linhas); Assert.Contains("18", d.Tabela.Linhas[0].C[4]);
            a.Foto("reposicao", d);
            d.ImprimirAsync().GetAwaiter().GetResult(); Assert.Contains("reposicao-estoque.html", a.P.Abertos[^1]);
            var csv = Path.Combine(a.Pasta, "rep.csv"); a.P.ArquivoParaSalvar = csv; d.ExportarAsync().GetAwaiter().GetResult(); Assert.Contains("Pouco", File.ReadAllText(csv));
            d.Close();
        });
    }

    [AvaloniaFact]
    public async Task Clientes_historico_e_botoes()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item"); var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Ana Paula", Telefone: "11 97777-6666"));
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 1000) }, c);
        var t = a.Tela<ClientesTela>("Clientes");
        await Assert.ThrowsAsync<ValidacaoException>(t.HistoricoAsync);
        t.Tabela.Selecionar(c.ToString());
        await a.ComDialogo<HistoricoClienteDialog>(t.HistoricoAsync, d =>
        {
            Assert.Single(d.Compras.Linhas); Assert.Equal("R$ 0,00", d.TextoSaldo);
            d.Clicar("Cobrar no WhatsApp"); Assert.Contains("não tem nada a pagar", a.P.Ultima.Texto);
            d.Close();
        });
    }

    // ------------------------------------------------------------------ notas
    [AvaloniaFact]
    public async Task Cupom_80mm_e_whatsapp_da_nota()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item", venda: 2500);
        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Lúcia", Telefone: "(31) 98888-0000"));
        var vid = a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 2, 2500) }, c, forma: "Pix");
        var t = a.Tela<NotasTela>("Vendas"); t.Tabela.Selecionar(vid.ToString());
        a.P.Abertos.Clear(); await t.CupomAsync();
        Assert.Contains($"cupom-{vid:000000}.html", a.P.Abertos[0]); Assert.Contains("class=\"cupom\"", File.ReadAllText(a.P.Abertos[0]));
        a.P.Abertos.Clear(); await t.WhatsAppAsync();
        Assert.StartsWith("https://wa.me/5531988880000?text=", a.P.Abertos[0]); Assert.Contains("R%24%2050%2C00", a.P.Abertos[0]);
        a.Foto("vendas");
    }

    // ------------------------------------------------------------------ navegação e visual
    [AvaloniaFact]
    public void Menu_lateral_navega_e_destaca_pagina_ativa()
    {
        using var a = new Ambiente(); a.Janela.Show();
        foreach (var nome in a.Janela.Telas.Keys.ToList())
        {
            a.Janela.IrPara(nome); Ambiente.Rodar();
            Assert.Equal(nome, a.Janela.PaginaAtual);
            var botoes = a.Janela.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("nav")).ToList();
            Assert.Equal(a.Janela.Telas.Count, botoes.Count);
            Assert.Equal(nome, Assert.Single(botoes, b => b.Classes.Contains("ativo")).Tag);
        }
        Assert.Empty(a.P.Msgs);
    }

    [AvaloniaFact]
    public async Task Fiado_aparece_no_painel_e_nas_vendas_com_etiqueta()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item", venda: 4000); var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Rita"));
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 4000) }, c, forma: "Fiado");
        var painel = a.Tela<PainelTela>("Painel");
        Assert.Equal("R$ 40,00", painel.Cartoes[3].Valor); Assert.Contains("1 cliente(s) devendo", painel.Cartoes[3].Detalhe);
        a.Foto("painel_fiado");
        await Task.CompletedTask;
    }
}
