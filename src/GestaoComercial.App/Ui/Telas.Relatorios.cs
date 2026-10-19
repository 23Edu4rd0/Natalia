using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

public sealed class RelatoriosTela : Tela
{
    public TextBox De { get; }
    public TextBox Ate { get; }
    readonly Dictionary<string, TextBlock> campos = new();
    readonly Tabela tTop, tPag;
    public string Resumo(string chave) => campos[chave].Text ?? "";

    public override string Titulo => "Relatórios";
    public override string Subtitulo => "Quanto você vendeu, lucrou e o que mais sai.";

    public RelatoriosTela(MainWindow app) : base(app)
    {
        De = K.Caixa(Util.InicioMesBr(), 110); Ate = K.Caixa(Util.HojeBr(), 110);
        void Periodo(string ini, string fim) { De.Text = ini; Ate.Text = fim; Seguro(Atualizar)(); }
        var filtros = K.Linha(10, K.Campo("De", De), K.Campo("Até", Ate), K.Botao("Atualizar", Seguro(Atualizar), classe: "primario"),
                              K.Botao("Hoje", () => Periodo(Util.HojeBr(), Util.HojeBr())), K.Botao("Últimos 30 dias", () => Periodo(Util.DiasAtrasBr(30), Util.HojeBr())),
                              K.Botao("Mês atual", () => Periodo(Util.InicioMesBr(), Util.HojeBr())), K.Botao("Ano atual", () => Periodo(Util.InicioAnoBr(), Util.HojeBr())));
        foreach (var c in filtros.Children.Skip(2)) c.VerticalAlignment = VerticalAlignment.Bottom;

        var itens = new[] { ("qtd", "Vendas concluídas"), ("total", "Faturamento"), ("ticket", "Ticket médio"), ("descontos", "Descontos dados"),
                            ("custo", "Custo dos itens vendidos"), ("lucro", "Lucro bruto"), ("canc", "Vendas canceladas") };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        for (int i = 0; i < itens.Length; i++)
        {
            var (k, rot) = itens[i];
            var lbl = K.Rotulo("-", true); campos[k] = lbl;
            K.ColocarNaGrade(g, K.Margem(K.Rotulo(rot + ":"), 0, 4, 8, 4), i / 4, (i % 4) * 2);
            K.ColocarNaGrade(g, K.Margem(lbl, 0, 4, 30, 4), i / 4, (i % 4) * 2 + 1);
        }
        var resumo = K.Cartao(g); resumo.Margin = new Thickness(0, 16, 0, 16);

        tTop = new Tabela(new Col("Código", 90), new Col("Produto", 220, Expande: true), new Col("Qtd.", 70, true), new Col("Receita", 110, true));
        tPag = new Tabela(new Col("Forma", 150, Expande: true), new Col("Vendas", 80, true), new Col("Total", 120, true));
        var baixo = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,2*"), RowDefinitions = new RowDefinitions("Auto,*") };
        K.ColocarNaGrade(baixo, K.H2("Produtos mais vendidos"), 0, 0);
        K.ColocarNaGrade(baixo, K.Margem(K.H2("Por forma de pagamento"), 10, 0, 0, 0), 0, 1);
        tTop.Margin = new Thickness(0, 6, 10, 0); tPag.Margin = new Thickness(10, 6, 0, 0);
        K.ColocarNaGrade(baixo, tTop, 1, 0); K.ColocarNaGrade(baixo, tPag, 1, 1);
        var nota = K.Discreto("Para exportar vendas, produtos ou estoque em CSV (Excel), use o botão 'Exportar CSV' das abas Notas, Produtos e Estoque.");
        nota.Margin = new Thickness(0, 8, 0, 0);
        Content = K.Doca(new StackPanel { Children = { filtros, resumo } }, baixo, nota);
    }

    public override void Atualizar()
    {
        var ini = Util.ParseDataBr(De.Text, "Data inicial");
        var fim = Util.ParseDataBr(Ate.Text, "Data final");
        if (string.CompareOrdinal(ini, fim) > 0) throw new ValidacaoException("A data inicial não pode ser maior que a data final.");
        var r = Svc.ResumoVendasPeriodo(ini, fim);
        campos["qtd"].Text = r.Qtd.ToString();
        campos["total"].Text = Util.FmtDinheiro(r.Total);
        campos["ticket"].Text = Util.FmtDinheiro(r.TicketMedio);
        campos["descontos"].Text = Util.FmtDinheiro(r.Descontos);
        campos["custo"].Text = Util.FmtDinheiro(r.Custo);
        campos["lucro"].Text = Util.FmtDinheiro(r.Lucro);
        campos["canc"].Text = $"{r.CanceladasQtd} ({Util.FmtDinheiro(r.CanceladasTotal)})";
        tTop.Definir(Svc.MaisVendidos(ini, fim).Select((p, i) => new Linha { Id = i.ToString(), C = new[] { p.Codigo, p.Descricao, p.Quantidade.ToString(), Util.FmtDinheiro(p.Receita) } }));
        tPag.Definir(Svc.VendasPorPagamento(ini, fim).Select((p, i) => new Linha { Id = i.ToString(), C = new[] { p.Forma, p.Qtd.ToString(), Util.FmtDinheiro(p.Total) } }));
    }
}

public sealed class ConfigTela : Tela
{
    public override string Titulo => "Configurações";
    public override string Subtitulo => "Dados da sua loja, backup e informações do programa.";

    public TextBox Nome { get; } = K.Caixa("");
    public TextBox Documento { get; } = K.Caixa("");
    public TextBox Endereco { get; } = K.Caixa("");
    public TextBox Telefone { get; } = K.Caixa("");
    public TextBox Rodape { get; } = K.Caixa("");

    public ComboBox Modo { get; }
    static readonly (string Valor, string Rotulo)[] OpcoesModo =
    {
        ("simples", "Simples — só o necessário para vender (recomendado)"),
        ("avancado", "Avançado — caixa, fornecedores, entradas, estoque, relatórios, custos e mais"),
    };
    public ComboBox AposVenda { get; }
    static readonly (string Valor, string Rotulo)[] OpcoesAposVenda =
    {
        ("aviso", "Mostrar um aviso com botões (Imprimir, Cupom, WhatsApp, Desfazer)"),
        ("cupom", "Abrir o cupom 80 mm automaticamente"),
        ("perguntar", "Perguntar se quero imprimir"),
        ("nada", "Não fazer nada (só o aviso com Desfazer)"),
    };
    bool carregando;

    public ConfigTela(MainWindow app) : base(app)
    {
        Modo = K.Combo(OpcoesModo.Select(o => o.Rotulo), double.NaN, OpcoesModo[0].Rotulo); Modo.HorizontalAlignment = HorizontalAlignment.Stretch;
        Modo.SelectionChanged += (_, _) =>
        {
            if (carregando || Modo.SelectedIndex < 0) return;
            var avancado = OpcoesModo[Modo.SelectedIndex].Valor == "avancado";
            if (avancado == App.Avancado) return;
            Seguro(() => App.DefinirModo(avancado))();      // refaz o menu e as telas (inclusive esta)
        };
        AposVenda = K.Combo(OpcoesAposVenda.Select(o => o.Rotulo), double.NaN, OpcoesAposVenda[0].Rotulo); AposVenda.HorizontalAlignment = HorizontalAlignment.Stretch;
        AposVenda.SelectionChanged += (_, _) =>
        {
            if (carregando || AposVenda.SelectedIndex < 0) return;
            Seguro(() => { Svc.SalvarConfig(VendaTela.ConfigAposVenda, OpcoesAposVenda[AposVenda.SelectedIndex].Valor); Sucesso("Preferência salva."); })();
        };
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto") };
        var campos = new (string, TextBox)[] { ("Nome / Razão social *", Nome), ("CNPJ / CPF", Documento), ("Endereço", Endereco), ("Telefone", Telefone), ("Mensagem no rodapé da nota", Rodape) };
        for (int i = 0; i < campos.Length; i++)
        {
            K.ColocarNaGrade(g, K.Margem(K.Rotulo(campos[i].Item1), 0, 0, 16, 0), i, 0);
            campos[i].Item2.Margin = new Thickness(0, 4);
            K.ColocarNaGrade(g, campos[i].Item2, i, 1);
        }
        var salvar = K.Botao("Salvar dados da empresa", SeguroAsync(SalvarEmpresaAsync), icone: "ok", classe: "primario"); salvar.HorizontalAlignment = HorizontalAlignment.Right; salvar.Margin = new Thickness(0, 8, 0, 0);
        K.ColocarNaGrade(g, salvar, 5, 1);
        var empresa = Grupo("Dados da empresa (aparecem no cabeçalho das notas)", g);

        var bk = new StackPanel { Spacing = 8 };
        bk.Children.Add(new TextBlock { Text = "Um backup automático é feito 1 vez por dia ao abrir o programa (os últimos 14 são mantidos).\nFaça também uma cópia para um pendrive/nuvem de vez em quando.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        bk.Children.Add(K.Linha(K.Botao("Fazer backup agora…", SeguroAsync(BackupAsync)), K.Botao("Abrir pasta de backups", Seguro(() => AbrirPasta("backups"))),
                                K.Botao("Abrir pasta de dados", Seguro(() => AbrirPasta(null))),
                                K.Botao("Abrir pasta de notas", Seguro(() => AbrirPasta("notas"))), K.Botao("Sobre o programa", Seguro(App.Sobre))));
        bk.Children.Insert(2, K.Botao("Restaurar um backup…", SeguroAsync(RestaurarAsync), icone: "pasta", classe: "perigo"));
        bk.Children.Add(K.Discreto($"Pasta de dados: {Config.PastaDados()}"));
        var backup = Grupo("Backup e dados", bk);

        var modo = Grupo("Modo de uso", new StackPanel
        {
            Spacing = 8,
            Children = { Modo, K.Discreto("O modo simples esconde o que uma venda do dia a dia não usa. Seus dados não mudam: dá para trocar quando quiser.") },
        });
        var venda = Grupo("Ao finalizar uma venda", AposVenda);

        var imp = Grupo("Importante", new TextBlock
        {
            Text = "As notas emitidas por este programa são comprovantes internos de venda e NÃO têm valor fiscal.\nPara emitir NF-e / NFC-e / cupom fiscal é necessário um emissor autorizado pela SEFAZ.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        Content = new ScrollViewer { Content = new StackPanel { Spacing = 14, MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Left, Children = { modo, empresa, venda, backup, imp } } };
    }

    static Control Grupo(string titulo, Control conteudo) =>
        K.Cartao(new StackPanel { Spacing = 12, Children = { K.H2(titulo), conteudo } });

    public override void Atualizar()
    {
        carregando = true;
        Modo.SelectedIndex = App.Avancado ? 1 : 0;
        var idx = Array.FindIndex(OpcoesAposVenda, o => o.Valor == Svc.ObterConfig(VendaTela.ConfigAposVenda, "aviso"));
        AposVenda.SelectedIndex = idx < 0 ? 0 : idx;
        carregando = false;
        var e = Svc.ObterEmpresa();
        Nome.Text = e.Nome; Documento.Text = Util.FmtDocumento(e.Documento); Endereco.Text = e.Endereco; Telefone.Text = e.Telefone; Rodape.Text = e.Rodape;
    }

    public async Task SalvarEmpresaAsync()
    {
        Svc.SalvarEmpresa(new Empresa(Nome.Text ?? "", Documento.Text ?? "", Endereco.Text ?? "", Telefone.Text ?? "", Rodape.Text ?? ""));
        Atualizar();
        Sucesso("Dados da loja salvos. As próximas notas já saem com eles.");
    }

    /// <summary>Volta os dados para um backup antigo. Antes, guarda uma cópia de segurança do que existe agora.</summary>
    public async Task RestaurarAsync()
    {
        var arquivo = await Plat.Atual.AbrirArquivo(Janela, "Escolha o arquivo de backup", "Backup (.db)", new[] { "db" });
        if (arquivo == null) return;
        if (!await Confirmar("Restaurar este backup?\n\nOs dados de hoje serão SUBSTITUÍDOS pelos do arquivo escolhido. " +
                             "Uma cópia de segurança dos dados atuais é guardada antes, então dá para voltar atrás.", "Restaurar backup")) return;
        var seguranca = Svc.RestaurarBackup(arquivo, Config.Subpasta("backups"));
        AtualizarTudo();
        Sucesso("Backup restaurado. Os dados anteriores ficaram guardados em uma cópia de segurança.", ("Abrir pasta", () => Plat.Atual.Abrir(Path.GetDirectoryName(seguranca)!)));
    }

    public async Task BackupAsync()
    {
        var caminho = await Plat.Atual.SalvarArquivo(Janela, "Salvar backup", Config.Subpasta("backups"), $"gestao-backup-{DateTime.Now:yyyy-MM-dd_HHmm}.db", "Banco de dados", "db");
        if (caminho == null) return;
        Svc.FazerBackup(caminho);
        Sucesso("Backup salvo.", ("Abrir pasta", () => Plat.Atual.Abrir(Path.GetDirectoryName(caminho)!)));
    }

    void AbrirPasta(string? sub)
    {
        var pasta = sub != null ? Config.Subpasta(sub) : Config.PastaDados();
        if (!Plat.Atual.Abrir(pasta)) _ = Info($"Não foi possível abrir a pasta automaticamente.\nEla fica em:\n{pasta}", "Pasta");
    }
}
