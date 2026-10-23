using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

public sealed class RelatoriosTela : Tela
{
    public TextBox De { get; }
    public TextBox Ate { get; }
    readonly Dictionary<string, string> valores = new();
    readonly Tabela tTop;
    readonly StackPanel barrasPagamento = new() { Spacing = 14 };
    readonly K.Indicador kTotal = new("Faturamento", "dinheiro", "#4F46E5", "#EEF2FF");
    readonly K.Indicador kLucro = new("Lucro bruto", "subindo", "#059669", "#ECFDF5");
    readonly K.Indicador kTicket = new("Ticket médio", "etiqueta", "#0891B2", "#ECFEFF");
    readonly K.Indicador kQtd = new("Vendas concluídas", "venda", "#7C3AED", "#F5F3FF");
    readonly List<Button> periodos = new();
    public string Resumo(string chave) => valores.GetValueOrDefault(chave, "");

    public override string Titulo => "Relatórios";
    public override string Subtitulo => "Quanto você vendeu, lucrou e o que mais sai.";

    public RelatoriosTela(MainWindow app) : base(app)
    {
        De = K.Data(Util.InicioMesBr()); Ate = K.Data(Util.HojeBr());
        Button Periodo(string rotulo, Func<string> ini)
        {
            var b = K.Botao(rotulo, () => { De.Text = ini(); Ate.Text = Util.HojeBr(); Seguro(Atualizar)(); }, classe: "chip");
            periodos.Add(b);
            return b;
        }
        var atalhos = K.Linha(6, Periodo("Hoje", Util.HojeBr), Periodo("Últimos 30 dias", () => Util.DiasAtrasBr(30)),
                                 Periodo("Mês atual", Util.InicioMesBr), Periodo("Ano atual", Util.InicioAnoBr));
        var datas = K.Linha(8, De, K.Rotulo("até", false, 13, Cores.Texto2), Ate, K.Botao("Atualizar", Seguro(Atualizar), classe: "primario"));
        K.AoApertarEnter(De, Seguro(Atualizar)); K.AoApertarEnter(Ate, Seguro(Atualizar));
        var filtros = K.Ferramentas(atalhos, datas);
        filtros.Margin = new Thickness(0, 0, 0, 20);

        var cartoes = K.Colunas(18, kTotal, kLucro, kTicket, kQtd);
        cartoes.Margin = new Thickness(0, 0, 0, 20);

        tTop = new Tabela(new Col("#", 54), new Col("Produto", 200, Expande: true), new Col("Qtd.", 80, true), new Col("Receita", 130, true));
        var top = K.Lista(tTop, K.Ferramentas(K.H2("Produtos mais vendidos"), null), vazio: "Nenhuma venda no período.", iconeVazio: "relatorios");
        var pag = K.Secao("Por forma de pagamento", new ScrollViewer { Content = barrasPagamento }, preencher: true);

        var baixo = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,20,2*") };
        K.ColocarNaGrade(baixo, top, 0, 0); K.ColocarNaGrade(baixo, pag, 0, 2);
        var nota = K.Discreto("Para levar os dados ao Excel, use “Exportar” nas listas de Vendas, Produtos e Estoque."); nota.Margin = new Thickness(0, 14, 0, 0); nota.FontSize = 12.5;
        Content = K.Rolagem(K.Doca(new StackPanel { Children = { filtros, cartoes } }, baixo, nota), 640);
    }

    public override void Atualizar()
    {
        var ini = Util.ParseDataBr(De.Text, "Data inicial");
        var fim = Util.ParseDataBr(Ate.Text, "Data final");
        if (string.CompareOrdinal(ini, fim) > 0) throw new ValidacaoException("A data inicial não pode ser maior que a data final.");
        // destaca o atalho de período que corresponde às datas
        var deTxt = De.Text?.Trim(); var hoje = Util.HojeBr();
        var marcado = Ate.Text?.Trim() != hoje ? -1 : deTxt == hoje ? 0 : deTxt == Util.DiasAtrasBr(30) ? 1 : deTxt == Util.InicioMesBr() ? 2 : deTxt == Util.InicioAnoBr() ? 3 : -1;
        for (int i = 0; i < periodos.Count; i++) { if (i == marcado) periodos[i].Classes.Add("ativo"); else periodos[i].Classes.Remove("ativo"); }

        var r = Svc.ResumoVendasPeriodo(ini, fim);
        valores["qtd"] = r.Qtd.ToString();
        valores["total"] = Util.FmtDinheiro(r.Total);
        valores["ticket"] = Util.FmtDinheiro(r.TicketMedio);
        valores["descontos"] = Util.FmtDinheiro(r.Descontos);
        valores["custo"] = Util.FmtDinheiro(r.Custo);
        valores["lucro"] = Util.FmtDinheiro(r.Lucro);
        valores["canc"] = $"{r.CanceladasQtd} ({Util.FmtDinheiro(r.CanceladasTotal)})";
        kTotal.Definir(valores["total"], r.Descontos > 0 ? $"já com {valores["descontos"]} de descontos" : "sem descontos no período");
        var margem = r.Total > 0 ? (decimal)r.Lucro / r.Total * 100 : 0;
        kLucro.Definir(valores["lucro"], r.Total > 0 ? $"margem de {margem:0.#}% · custo {valores["custo"]}" : "vendas − custo dos itens");
        kTicket.Definir(valores["ticket"], "valor médio por venda");
        kQtd.Definir(valores["qtd"], r.CanceladasQtd > 0 ? $"{valores["canc"]} cancelada(s)" : "nenhuma cancelada", r.CanceladasQtd > 0 ? "#B91C1C" : null);

        tTop.Definir(Svc.MaisVendidos(ini, fim).Select((p, i) => new Linha { Id = i.ToString(), C = new[] { $"{i + 1}º", p.Descricao, p.Quantidade.ToString(), Util.FmtDinheiro(p.Receita) } }));

        // barras horizontais por forma de pagamento
        barrasPagamento.Children.Clear();
        var formas = Svc.VendasPorPagamento(ini, fim);
        var max = Math.Max(1, formas.Select(f => f.Total).DefaultIfEmpty(0).Max());
        var soma = Math.Max(1, formas.Sum(f => f.Total));
        var cores = new[] { "#4F46E5", "#0891B2", "#059669", "#D97706", "#7C3AED", "#DB2777", "#64748B" };
        for (int i = 0; i < formas.Count; i++)
        {
            var f = formas[i];
            var topo = new DockPanel();
            var valor = new TextBlock { Text = $"{Util.FmtDinheiro(f.Total)}  ·  {(decimal)f.Total / soma * 100:0}%", FontSize = 13, FontWeight = Avalonia.Media.FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto) };
            DockPanel.SetDock(valor, Dock.Right); topo.Children.Add(valor);
            topo.Children.Add(new TextBlock { Text = $"{f.Forma}  ({f.Qtd})", FontSize = 13.5, Foreground = Cores.P(Cores.Corpo) });
            var fr = (double)f.Total / max;
            var trilho = new Grid { ColumnDefinitions = new ColumnDefinitions($"{fr}*,{1 - fr}*"), Height = 8 };
            trilho.Children.Add(new Border { Background = Cores.P(Cores.CinzaFundo), CornerRadius = new CornerRadius(4), [Grid.ColumnSpanProperty] = 2 });
            trilho.Children.Add(new Border { Background = Cores.P(cores[i % cores.Length]), CornerRadius = new CornerRadius(4) });
            barrasPagamento.Children.Add(new StackPanel { Spacing = 6, Children = { topo, trilho } });
        }
        if (formas.Count == 0) barrasPagamento.Children.Add(K.Discreto("Nenhuma venda no período."));
    }
}

public sealed class ConfigTela : Tela
{
    public override string Titulo => "Configurações";
    public override string Subtitulo => "Dados da sua loja, jeito de usar o programa e backup.";

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
        ("nfce", "Emitir a NFC-e (cupom fiscal) na hora — pede só o que faltar"),
    };
    bool carregando;

    public ConfigTela(MainWindow app) : base(app)
    {
        Modo = K.Combo(OpcoesModo.Select(o => o.Rotulo), double.NaN, OpcoesModo[0].Rotulo); Modo.HorizontalAlignment = HorizontalAlignment.Stretch;
        Modo.SelectionChanged += (_, _) =>
        {
            if (opcaoSimples != null) MarcarModo();
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
        // ---- sua loja
        Nome.Watermark = "Nome que aparece nas notas"; Documento.Watermark = "Opcional"; Endereco.Watermark = "Rua, número, bairro, cidade"; Telefone.Watermark = "(11) 99999-0000";
        Rodape.Watermark = "Ex.: Obrigada pela preferência!";
        var duas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        K.ColocarNaGrade(duas, K.Campo("CNPJ / CPF", Documento), 0, 0); K.ColocarNaGrade(duas, K.Campo("Telefone / WhatsApp", Telefone), 0, 2);
        var salvar = K.Botao("Salvar dados da empresa", SeguroAsync(SalvarEmpresaAsync), icone: "ok", classe: "primario"); salvar.HorizontalAlignment = HorizontalAlignment.Right;
        var loja = K.Secao("Sua loja", new StackPanel
        {
            Spacing = 16,
            Children = { K.Campo("Nome da loja *", Nome), duas, K.Campo("Endereço", Endereco), K.Campo("Mensagem no rodapé da nota", Rodape), salvar },
        }, subtitulo: "Aparece no cabeçalho das notas e nas mensagens de WhatsApp.", icone: "loja");

        var aviso = new Border
        {
            Background = Cores.P("#FFFBEB"), BorderBrush = Cores.P("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(18, 14),
            Child = new DockPanel
            {
                Children =
                {
                    K.Margem(Icones.Criar("info", 20, "#D97706"), 0, 0, 12, 0),
                    new TextBlock
                    {
                        Text = "O comprovante de venda (botão “Nota”/“Cupom”) é interno e NÃO tem valor fiscal. A nota fiscal de verdade é a NFC-e / NF-e, emitida pelo botão “Nota fiscal” depois de configurar ao lado.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 13, Foreground = Cores.P("#92400E"),
                    },
                },
            },
        };
        DockPanel.SetDock(((DockPanel)aviso.Child).Children[0], Dock.Left);

        // ---- modo de uso: dois cartões para escolher (a lista "Modo" fica por trás, guardando a escolha)
        Button Opcao(int indice, string titulo, string texto, string icone)
        {
            var b = new Button { Tag = "Modo: " + titulo, Padding = new Thickness(14, 12) };
            b.Classes.Add("bloco");
            var bolha = K.Bolha(icone, Cores.Primaria, Cores.PrimariaSuave, 36);
            var textos = new StackPanel
            {
                Spacing = 2, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = titulo, FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 14, Foreground = Cores.P(Cores.Texto) },
                    new TextBlock { Text = texto, FontSize = 12.5, Foreground = Cores.P(Cores.Texto2), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                },
            };
            var d = new DockPanel(); DockPanel.SetDock(bolha, Dock.Left); d.Children.Add(bolha); d.Children.Add(textos);
            b.Content = d;
            b.Click += (_, _) => Modo.SelectedIndex = indice;
            return b;
        }
        opcaoSimples = Opcao(0, "Simples (recomendado)", "Só o necessário para vender: vendas, fiado, clientes e produtos.", "raio");
        opcaoAvancada = Opcao(1, "Avançado", "Tudo: caixa, fornecedores, entradas, estoque, relatórios, custos e margem.", "relatorios");
        var modo = K.Secao("Modo de uso", new StackPanel { Spacing = 10, Children = { opcaoSimples, opcaoAvancada } },
                           subtitulo: "Seus dados não mudam: dá para trocar quando quiser.", icone: "ajustes");

        var venda = K.Secao("Ao finalizar uma venda", AposVenda, subtitulo: "O que acontece depois de apertar “Finalizar venda”.", icone: "venda");

        var bk = new StackPanel { Spacing = 14 };
        bk.Children.Add(new TextBlock { Text = "Um backup automático é feito 1 vez por dia ao abrir o programa (os últimos 14 ficam guardados). De vez em quando, copie também para um pendrive ou para a nuvem.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 13.5, Foreground = Cores.P(Cores.Texto2) });
        bk.Children.Add(K.Fila(8, K.Botao("Fazer backup agora…", SeguroAsync(BackupAsync), icone: "nuvem"),
                                  K.Botao("Restaurar um backup…", SeguroAsync(RestaurarAsync), icone: "pasta", classe: "perigo")));
        bk.Children.Add(K.Fila(4, K.Botao("Abrir pasta de backups", Seguro(() => AbrirPasta("backups")), classe: "fantasma"),
                                  K.Botao("Abrir pasta de dados", Seguro(() => AbrirPasta(null)), classe: "fantasma"),
                                  K.Botao("Abrir pasta de notas", Seguro(() => AbrirPasta("notas")), classe: "fantasma"),
                                  K.Botao("Sobre o programa", Seguro(App.Sobre), classe: "fantasma")));
        var pasta = K.Discreto($"Pasta de dados: {Config.PastaDados()}"); pasta.FontSize = 12; pasta.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        bk.Children.Add(pasta);
        var backup = K.Secao("Backup e dados", bk, icone: "nuvem");

        var esquerda = new StackPanel { Spacing = 20, Children = { loja, aviso } };
        lblFiscal = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 13.5 };
        var fiscal = K.Secao("Nota fiscal eletrônica (NF-e / NFC-e)", new StackPanel
        {
            Spacing = 12,
            Children = { lblFiscal, K.Linha(8, K.Botao("Configurar nota fiscal…", SeguroAsync(ConfigurarFiscalAsync), icone: "ajustes", classe: "primario"), K.Botao("Ver notas emitidas", () => App.IrPara("Notas fiscais"), classe: "fantasma")) },
        }, subtitulo: "Notas de verdade, enviadas à SEFAZ. Precisa do certificado digital A1 da empresa.", icone: "nf");
        var direita = new StackPanel { Spacing = 20, Children = { fiscal, modo, venda, backup } };
        var colunas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20,*"), MaxWidth = 1500, HorizontalAlignment = HorizontalAlignment.Left };
        K.ColocarNaGrade(colunas, esquerda, 0, 0); K.ColocarNaGrade(colunas, direita, 0, 2);
        Content = new ScrollViewer { Content = colunas, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    Button opcaoSimples = null!, opcaoAvancada = null!;
    TextBlock lblFiscal = null!;
    public string TextoFiscal => lblFiscal.Text ?? "";

    public async Task ConfigurarFiscalAsync()
    {
        if (await ConfigFiscalDialog.Abrir(Janela, Svc)) Atualizar();
    }

    void MarcarModo()
    {
        foreach (var (b, ativo) in new[] { (opcaoSimples, Modo.SelectedIndex == 0), (opcaoAvancada, Modo.SelectedIndex == 1) })
            if (ativo) b.Classes.Add("ativo"); else b.Classes.Remove("ativo");
    }

    public override void Atualizar()
    {
        carregando = true;
        Modo.SelectedIndex = App.Avancado ? 1 : 0;
        MarcarModo();
        var idx = Array.FindIndex(OpcoesAposVenda, o => o.Valor == Svc.ObterConfig(VendaTela.ConfigAposVenda, "aviso"));
        AposVenda.SelectedIndex = idx < 0 ? 0 : idx;
        carregando = false;
        var cf = Svc.ObterConfigFiscal();
        var pend = cf.Pendencias(65, verificarCertificado: false);
        lblFiscal.Text = pend.Count > 0 ? $"Ainda não configurada: falta {pend.Count} item(ns)."
                       : cf.Producao ? $"Pronta, em PRODUÇÃO (notas com valor fiscal). CNPJ {Util.FmtDocumento(cf.Cnpj)} – {cf.Uf}."
                       : $"Pronta, em HOMOLOGAÇÃO (notas de teste). CNPJ {Util.FmtDocumento(cf.Cnpj)} – {cf.Uf}.";
        if (pend.Count == 0 && Svc.ContingenciaAtiva() is { } cont)
            lblFiscal.Text += $"\nEm CONTINGÊNCIA (sem comunicação com a SEFAZ) desde {cont.Inicio.LocalDateTime:dd/MM HH:mm}: {Svc.NotasEmContingencia().Count} NFC-e esperando envio.";
        lblFiscal.Foreground = Cores.P(pend.Count > 0 ? "#92400E" : cf.Producao ? "#065F46" : Cores.Corpo);
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
