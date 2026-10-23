using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using GestaoComercial.App.Ui;
using GestaoComercial.Core;
using Xunit;

namespace GestaoComercial.Tests;

public class UiMelhoriasTests
{
    static long Prod(Servico s, string nome, long estoque = 20, long venda = 1000, string? codigo = null) =>
        s.SalvarProduto(new ProdutoDados(nome, codigo, PrecoCusto: venda / 2, PrecoVenda: venda, EstoqueInicial: estoque));

    // ------------------------------------------------------------------ avisos rápidos (toast) de verdade
    [AvaloniaFact]
    public void Toast_real_aparece_executa_botao_e_fecha()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var real = new PlataformaReal(); var chamou = 0;
        real.Toast(a.Janela, "Tudo certo", Nivel.Info, new (string, Action)[] { ("Fazer algo", () => chamou++) }, 5);
        Ambiente.Rodar();
        Assert.Equal(1, a.Janela.ToastsAbertos);
        a.Foto("toast", a.Janela);
        a.Janela.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as string) == "Fazer algo").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(1, chamou); Assert.Equal(0, a.Janela.ToastsAbertos);                 // clicar executa e fecha
        real.Toast(a.Janela, "some sozinho", Nivel.Info, null, 0);                       // sem botões e com tempo 0: some logo
        var fimToast = DateTime.UtcNow.AddSeconds(3);
        while (a.Janela.ToastsAbertos > 0 && DateTime.UtcNow < fimToast) { Thread.Sleep(20); Ambiente.Rodar(); }
        Assert.Equal(0, a.Janela.ToastsAbertos);
        for (int i = 0; i < 7; i++) real.Toast(a.Janela, $"aviso {i}", Nivel.Aviso, null, 60);
        Assert.Equal(4, a.Janela.ToastsAbertos);                                         // nunca empilha mais de 4
        // botão que falha não derruba o programa: vira mensagem
        real.Toast(a.Janela, "x", Nivel.Info, new (string, Action)[] { ("Quebra", () => throw new ValidacaoException("não deu")) });
        a.Janela.GetVisualDescendants().OfType<Button>().First(b => (b.Tag as string) == "Quebra").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Contains("não deu", a.P.Ultima.Texto);
    }

    // ------------------------------------------------------------------ velocidade: páginas sob demanda e cache
    [AvaloniaFact]
    public void Paginas_so_sao_criadas_quando_abertas_e_nao_recarregam_sem_mudanca()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Ambiente.Rodar();
        Assert.True(a.Janela.Telas.JaCriada("Painel"));
        Assert.False(a.Janela.Telas.JaCriada("Relatórios"));                      // ainda nem foi construída
        Assert.False(a.Janela.Telas.JaCriada("Estoque"));
        Prod(a.S, "Item");
        var t = a.Tela<ProdutosTela>("Produtos");
        var antes = t.Tabela.Linhas;
        a.Janela.IrPara("Clientes"); a.Janela.IrPara("Produtos");
        Assert.Same(antes, t.Tabela.Linhas);                                       // nada mudou: não consultou de novo
        Prod(a.S, "Outro");
        a.Janela.IrPara("Clientes"); a.Janela.IrPara("Produtos");
        Assert.NotSame(antes, t.Tabela.Linhas);                                    // mudou: recarregou
        Assert.Equal(2, t.Tabela.Linhas.Count);
        a.Janela.AtualizarTudo();                                                  // forçar sempre recarrega
        Assert.NotSame(t.Tabela.Linhas, antes);
        Assert.Equal(a.Janela.Telas.Count, a.Janela.Telas.Keys.Count());
    }

    [AvaloniaFact]
    public void Busca_com_atraso_so_consulta_quando_para_de_digitar()
    {
        K.DebounceMs = 0; var n = 0;
        var imediato = K.Debounce(() => n++); imediato(); imediato(); Assert.Equal(2, n);
        K.DebounceMs = 120;
        try
        {
            n = 0; var atrasado = K.Debounce(() => n++);
            for (int i = 0; i < 5; i++) atrasado();                                // 5 teclas seguidas
            Assert.Equal(0, n);
            var fim = DateTime.UtcNow.AddSeconds(3);
            while (n == 0 && DateTime.UtcNow < fim) { Thread.Sleep(30); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(1, n);                                                    // uma única consulta no fim
        }
        finally { K.DebounceMs = 0; }
    }

    // ------------------------------------------------------------------ venda mais rápida
    [AvaloniaFact]
    public async Task Quantidade_antes_do_nome_e_leitor_de_codigo_de_barras()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Caneta Azul", codigo: "7891234567890"); Prod(a.S, "Caderno", codigo: "7890000000011");
        var ed = a.Tela<VendaTela>("Nova venda").Editor;
        ed.Busca.Digitar("3x caneta"); await ed.BuscarAsync();
        Assert.Equal("3", ed.Quantidade.Text); Assert.Equal("Caneta Azul", ed.ProdutoSelecionado?.Nome); Assert.Empty(ed.Itens);   // por nome: seleciona
        await ed.AdicionarAsync(); Assert.Equal(3, ed.Itens[0].Quantidade);
        ed.Quantidade.Digitar("1");
        for (int i = 0; i < 3; i++) { ed.Busca.Digitar("7890000000011"); await ed.BuscarAsync(); }       // leitor "bipando" 3x
        Assert.Equal(("Caderno", 3L), (ed.Itens[1].Nome, ed.Itens[1].Quantidade));
        ed.Busca.Digitar("2*7891234567890"); await ed.BuscarAsync();                                       // 2 unidades pelo código
        Assert.Equal(5, ed.Itens[0].Quantidade);
    }

    [AvaloniaFact]
    public async Task Produto_inexistente_pode_ser_cadastrado_sem_sair_da_venda()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var ed = a.Tela<VendaTela>("Nova venda").Editor;
        ed.Busca.Digitar("7899998887776");
        await a.ComDialogo<FormDialog>(ed.BuscarAsync, d =>
        {
            Assert.Equal("7899998887776", d.Texto("codigo"));                    // o que foi bipado já vem preenchido
            d.Definir("nome", "Sabonete"); d.Definir("venda", "4,50"); d.Definir("inicial", "10");
            Assert.True(d.Salvar());
        });
        var p = Assert.Single(a.S.ListarProdutos());
        Assert.Equal(("Sabonete", "7899998887776", 450L), (p.Nome, p.Codigo, p.PrecoVenda));
        Assert.Equal(("Sabonete", 1L), (ed.Itens[0].Nome, ed.Itens[0].Quantidade));    // já entrou na venda
        // por nome (não parece código): o nome vem preenchido
        ed.Busca.Digitar("Esponja");
        await a.ComDialogo<FormDialog>(ed.BuscarAsync, d => { Assert.Equal("Esponja", d.Texto("nome")); Assert.Equal("", d.Texto("codigo")); d.Close(); });
        Assert.Single(a.S.ListarProdutos());                                           // cancelou: nada criado
    }

    [AvaloniaFact]
    public async Task Margem_de_lucro_calcula_o_preco_ao_vivo()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            d.Definir("custo", "10,00"); d.Definir("margem", "50");
            Assert.Equal("15,00", d.Texto("venda"));                                   // custo + 50%
            d.Definir("margem", "100,5"); Assert.Equal("20,05", d.Texto("venda"));
            d.Definir("venda", "30,00"); Assert.Equal("200", d.Texto("margem"));       // preço digitado -> margem
            d.Definir("custo", "20,00"); Assert.Equal("60,00", d.Texto("venda"));      // mudou o custo: preço acompanha a margem
            d.Definir("nome", "X"); Assert.True(d.Salvar());
        });
        Assert.Equal((2000L, 6000L), (a.S.ListarProdutos()[0].PrecoCusto, a.S.ListarProdutos()[0].PrecoVenda));
    }

    [AvaloniaFact]
    public async Task Venda_sem_confirmacao_com_desfazer_e_botoes_no_aviso()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Blusa", estoque: 5, venda: 5000);
        var c = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Ana Lima", Telefone: "(11) 91111-2222"));
        var t = a.Tela<VendaTela>("Nova venda");
        t.ClienteNome = "ana"; t.Editor.Busca.Digitar("Blusa"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();   // "ana" acha a Ana Lima
        await t.FinalizarAsync();
        var v = Assert.Single(a.S.ListarVendas());
        Assert.Equal("Ana Lima", v.ClienteNome); Assert.Equal(4, a.S.ObterProduto(p).Estoque);
        var rotulos = a.P.UltimoToast.Acoes.Select(x => x.Rotulo).ToList();
        Assert.Equal(new[] { "Nota fiscal", "Imprimir nota", "Cupom 80 mm", "WhatsApp", "Desfazer" }, rotulos);   // WhatsApp porque o cliente tem telefone
        a.P.ClicarToast("WhatsApp"); Assert.StartsWith("https://wa.me/5511911112222?text=", a.P.Abertos[^1]);
        a.P.ClicarToast("Cupom 80 mm"); Assert.Contains("cupom-", a.P.Abertos[^1]);
        a.P.ClicarToast("Desfazer");                                                    // "errei!" -> um clique desfaz
        Assert.True(a.S.ObterVenda(v.Id).Cancelada); Assert.Equal(5, a.S.ObterProduto(p).Estoque);
        Assert.Contains("desfeita", a.P.UltimoToast.Texto);
    }

    [AvaloniaFact]
    public async Task Desfazer_duas_vezes_avisa_sem_estragar_o_estoque()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item", estoque: 5);
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync(); await t.FinalizarAsync();
        var toast = a.P.UltimoToast;
        toast.Acoes.First(x => x.Rotulo == "Desfazer").Acao();
        Assert.Throws<ConflitoException>(() => toast.Acoes.First(x => x.Rotulo == "Desfazer").Acao());   // (no app real vira mensagem)
        Assert.Equal(5, a.S.ObterProduto(p).Estoque);
    }

    [AvaloniaTheory]
    [InlineData("cupom", true, false, false)]
    [InlineData("perguntar", false, true, false)]
    [InlineData("nada", false, false, true)]
    public async Task Preferencia_de_pos_venda_muda_o_que_acontece(string modo, bool abreCupom, bool pergunta, bool soAviso)
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Item", estoque: 5); a.S.SalvarConfig(VendaTela.ConfigAposVenda, modo);
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        a.P.Abertos.Clear(); a.P.Confirmacoes.Clear(); a.P.RespostaConfirmar = false;
        await t.FinalizarAsync();
        Assert.Equal(abreCupom, a.P.Abertos.Any(x => x.Contains("cupom-")));
        Assert.Equal(pergunta, a.P.Confirmacoes.Any(x => x.Contains("imprimir")));
        if (soAviso) Assert.Equal(new[] { "Desfazer" }, a.P.UltimoToast.Acoes.Select(x => x.Rotulo));
    }

    [AvaloniaFact]
    public void Config_pos_venda_salva_a_escolha()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var c = a.Tela<ConfigTela>("Configurações");
        Assert.Equal(0, c.AposVenda.SelectedIndex);
        c.AposVenda.SelectedIndex = 1;
        Assert.Equal("cupom", a.S.ObterConfig(VendaTela.ConfigAposVenda)); Assert.Equal("Preferência salva.", a.P.UltimoToast.Texto);
        a.Janela.IrPara("Painel"); c = a.Tela<ConfigTela>("Configurações"); Assert.Equal(1, c.AposVenda.SelectedIndex);   // lembra ao reabrir
    }

    [AvaloniaFact]
    public async Task Cliente_por_digitacao_com_aviso_de_divida_e_erro_claro()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = Prod(a.S, "Item", venda: 4000);
        var jose = a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("José Antônio"));
        a.S.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados("Maria José"));
        a.S.RegistrarVenda(new[] { new ItemVendaInput(p, 1, 4000) }, jose, forma: "Fiado");
        var t = a.Tela<VendaTela>("Nova venda");
        void Def(string nome) { t.ClienteNome = nome; Ambiente.Rodar(); }   // a caixa de texto aplica a digitação em seguida (fila do dispatcher)
        Def("José Antônio");
        Assert.Equal("Esse cliente já deve R$ 40,00 no fiado.", t.TextoInfoCliente);
        Def("xyz"); Assert.Contains("não encontrado", t.TextoInfoCliente);
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        var ex = await Assert.ThrowsAsync<ValidacaoException>(t.FinalizarAsync); Assert.Contains("Não encontrei o cliente “xyz”", ex.Message);
        Def("jose"); ex = await Assert.ThrowsAsync<ValidacaoException>(t.FinalizarAsync);          // "jose" bate com dois nomes (acento ignorado): precisa escolher
        Def("antonio"); t.Pagamento.SelectedItem = "Fiado"; await t.FinalizarAsync();               // só um bate: resolve sozinho
        Assert.Equal(8000, a.S.SaldoCliente(jose));
        Def(""); t.Pagamento.SelectedItem = "Fiado"; Assert.Contains("escolha o cliente", t.TextoInfoCliente);
    }

    [AvaloniaFact]
    public async Task Atalhos_de_desconto_e_valor_recebido()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Item", venda: 8000);
        var t = a.Tela<VendaTela>("Nova venda");
        t.Editor.Busca.Digitar("Item"); await t.Editor.BuscarAsync(); await t.Editor.AdicionarAsync();
        t.Clicar("10%"); Assert.Equal("TOTAL: R$ 72,00", t.TextoTotal);
        t.Clicar("Valor exato"); Assert.Equal("R$ 0,00", t.TextoTroco);
        t.Clicar("R$ 100"); Assert.Equal("R$ 28,00", t.TextoTroco);
        t.Clicar("R$ 50"); Assert.Equal("faltam R$ 22,00", t.TextoTroco);
        t.Clicar("Sem desconto"); Assert.Equal("TOTAL: R$ 80,00", t.TextoTotal);
    }

    // ------------------------------------------------------------------ erros dentro do formulário
    [AvaloniaFact]
    public async Task Erro_aparece_no_formulario_some_ao_digitar_e_nao_abre_janela()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ClientesTela>("Clientes");
        await a.ComDialogo<FormDialog>(t.NovoAsync, d =>
        {
            d.Definir("nome", "Fulano"); d.Definir("documento", "111.111.111-11");
            Assert.False(d.Salvar()); Assert.Contains("CPF inválido", d.Erro);
            d.Definir("documento", "529.982.247-25"); Assert.Equal("", d.Erro);          // digitou: o erro antigo some
            Assert.True(d.Salvar());
        });
        Assert.Empty(a.P.Msgs);                                                          // nenhuma caixa de mensagem foi aberta
    }

    // ------------------------------------------------------------------ importar planilha
    [AvaloniaFact]
    public async Task Importar_planilha_pela_tela_com_resultado_claro()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        var csv = Path.Combine(a.Pasta, "lista.csv");
        File.WriteAllText(csv, "Nome;Código;Tipo;Preço;Estoque\nVestido floral;V1;Roupas;129,90;4\nBlusa lisa;B1;Roupas;59,90;10\n;;;;\nCinto;C1;Acessórios;abc;3\n", new UTF8Encoding(true));
        a.P.ArquivoParaAbrir = csv;
        await a.ComDialogo<ImportarProdutosDialog>(t.ImportarAsync, d =>
        {
            d.BaixarModeloAsync().GetAwaiter().GetResult();
            Assert.EndsWith("modelo-produtos.csv", a.P.Abertos[^1]); Assert.Contains("Preço de venda", File.ReadAllText(a.P.Abertos[^1]));
            a.Foto("importar", d);
            d.ImportarAsync().GetAwaiter().GetResult();
            Assert.Equal((2, 0, 1), (d.Resultado!.Criados, d.Resultado.Atualizados, d.Resultado.Ignorados));
            Assert.Contains("2 produto(s) criado(s)", d.TextoResultado); Assert.Contains("Linha 5", d.TextoResultado);
            d.Close();
        });
        Assert.Equal(2, t.Tabela.Linhas.Count);                                           // a lista já foi atualizada atrás da janela
        Assert.Equal(new[] { "Roupas" }, a.S.ListarCategorias().Select(c => c.Nome));
        a.P.ArquivoParaAbrir = null;                                                     // cancelou o seletor: nada acontece
        await a.ComDialogo<ImportarProdutosDialog>(t.ImportarAsync, d => { d.ImportarAsync().GetAwaiter().GetResult(); Assert.Null(d.Resultado); d.Close(); });
    }

    // ------------------------------------------------------------------ restaurar backup
    [AvaloniaFact]
    public async Task Restaurar_backup_pela_tela_com_confirmacao_e_copia_de_seguranca()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Original");
        var bk = a.S.FazerBackup(Path.Combine(a.Pasta, "bk.db"));
        Prod(a.S, "Depois do backup");
        var c = a.Tela<ConfigTela>("Configurações");
        a.P.ArquivoParaAbrir = bk; a.P.RespostaConfirmar = false;
        await c.RestaurarAsync();
        Assert.Equal(2, a.S.ListarProdutos().Count);                                      // disse "Não": nada mudou
        a.P.RespostaConfirmar = true;
        await c.RestaurarAsync();
        Assert.Equal(new[] { "Original" }, a.S.ListarProdutos().Select(p => p.Nome));
        Assert.Contains("Backup restaurado", a.P.UltimoToast.Texto);
        Assert.Contains(Directory.GetFiles(Path.Combine(Config.PastaDados(), "backups")), f => f.Contains("antes-de-restaurar"));
        a.P.ArquivoParaAbrir = Path.Combine(a.Pasta, "bk.db.inexistente");
        await Assert.ThrowsAsync<ValidacaoException>(c.RestaurarAsync);
    }

    // ------------------------------------------------------------------ iniciantes
    [AvaloniaFact]
    public void Primeiros_passos_aparecem_so_enquanto_nao_ha_produtos()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var p = a.Tela<PainelTela>("Painel");
        Assert.True(p.PrimeirosPassosVisivel);
        a.Foto("primeiros_passos");
        Prod(a.S, "Primeiro");
        a.Janela.IrPara("Clientes"); p = a.Tela<PainelTela>("Painel");
        Assert.False(p.PrimeirosPassosVisivel);
    }

    [AvaloniaFact]
    public void Listas_vazias_explicam_o_que_fazer_e_a_busca_sem_resultado_diz_isso()
    {
        using var a = new Ambiente(); a.Janela.Show();
        var t = a.Tela<ProdutosTela>("Produtos");
        Assert.True(t.Tabela.VazioVisivel); Assert.Contains("Importar planilha", t.Tabela.TextoVazio);
        a.Foto("lista_vazia");
        Prod(a.S, "Camisa");
        a.Janela.IrPara("Clientes"); t = a.Tela<ProdutosTela>("Produtos");
        Assert.False(t.Tabela.VazioVisivel);
        t.Busca.Digitar("zzz"); Assert.True(t.Tabela.VazioVisivel); Assert.Contains("Nada encontrado para “zzz”", t.Tabela.TextoVazio);
        Assert.True(a.Tela<ReceberTela>("A receber").Tabela.VazioVisivel);
        Assert.True(a.Tela<NotasTela>("Vendas").Tabela.VazioVisivel);
    }

    [AvaloniaFact]
    public async Task Exportar_mostra_aviso_com_botao_para_abrir_o_arquivo()
    {
        using var a = new Ambiente(); a.Janela.Show();
        Prod(a.S, "Item");
        var t = a.Tela<ProdutosTela>("Produtos");
        var csv = Path.Combine(a.Pasta, "p.csv"); a.P.ArquivoParaSalvar = csv;
        await t.ExportarAsync();
        Assert.Empty(a.P.Msgs);                                                           // sem janela para fechar
        Assert.Contains("exportada", a.P.UltimoToast.Texto);
        a.P.ClicarToast("Abrir arquivo"); Assert.Equal(csv, a.P.Abertos[^1]);
    }
}
