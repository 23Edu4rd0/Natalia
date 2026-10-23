using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Início: números do dia, gráfico de vendas e atalhos para o que a vendedora mais faz.</summary>
public sealed class PainelTela : Tela
{
    readonly K.Indicador hoje = new("Vendas hoje", "venda", "#4F46E5", "#EEF2FF");
    readonly K.Indicador mes = new("Vendas no mês", "relatorios", "#0891B2", "#ECFEFF");
    readonly K.Indicador lucro = new("Lucro no mês", "subindo", "#059669", "#ECFDF5");
    readonly K.Indicador receber = new("A receber", "receber", "#D97706", "#FFFBEB");
    readonly K.Indicador baixo = new("Estoque baixo", "alerta", "#DC2626", "#FEF2F2");
    readonly Tabela tBaixo, tVendas;
    readonly GraficoBarras grafico = new() { Height = 230 };
    readonly TextBlock resumoGrafico = K.Discreto("");
    readonly Border contaBaixo = K.Selo("0", Cores.VermelhoFundo, "#991B1B");
    readonly Control primeirosPassos;
    public bool PrimeirosPassosVisivel => primeirosPassos.IsVisible;
    public IReadOnlyList<K.Indicador> Cartoes => new[] { hoje, mes, lucro, receber, baixo };
    public GraficoBarras Grafico => grafico;

    public override string Titulo => $"{Saudacao()}!";
    static readonly string[] Dias = { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
    static readonly string[] DiasCurtos = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
    static readonly string[] Meses = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    public override string Subtitulo { get { var h = DateTime.Today; return $"{char.ToUpperInvariant(Dias[(int)h.DayOfWeek][0])}{Dias[(int)h.DayOfWeek][1..]}, {h.Day} de {Meses[h.Month - 1]}. Veja como está sua loja hoje."; } }

    static string Saudacao() => DateTime.Now.Hour switch { < 12 => "Bom dia", < 18 => "Boa tarde", _ => "Boa noite" };

    public PainelTela(MainWindow app) : base(app)
    {
        // indicadores (sem custo cadastrado no modo simples, o "lucro" não seria confiável: o cartão some)
        var cartoes = K.Colunas(18, Cartoes.Where(c => App.Avancado || c != lucro).Cast<Control>().ToArray());
        cartoes.Margin = new Thickness(0, 0, 0, 20);

        // gráfico
        var cartaoGrafico = K.Secao("Vendas dos últimos 14 dias", grafico, textoVivo: resumoGrafico);
        cartaoGrafico.Margin = new Thickness(0, 0, 0, 20);

        // últimas vendas
        tVendas = new Tabela(new Col("Nota", 104), new Col("Quando", 136), new Col("Cliente", 140, Expande: true), new Col("Pagamento", 140), new Col("Total", 116, true));
        var verTodas = K.Botao("Ver todas", () => App.IrPara("Vendas"), classe: "fantasma");
        var listaVendas = K.Lista(tVendas, K.Ferramentas(K.H2("Últimas vendas"), verTodas), vazio: "Nenhuma venda ainda.\nAs vendas aparecem aqui assim que forem feitas.", iconeVazio: "venda");

        var esquerda = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        K.ColocarNaGrade(esquerda, cartaoGrafico, 0, 0); K.ColocarNaGrade(esquerda, listaVendas, 1, 0);

        // atalhos
        Button Atalho(string texto, string detalhe, string icone, string cor, string fundo, Action acao)
        {
            var b = new Button { Tag = texto, Padding = new Thickness(10, 14) };
            b.Classes.Add("bloco");
            var bolha = K.Bolha(icone, cor, fundo, 40); bolha.HorizontalAlignment = HorizontalAlignment.Center;
            b.Content = new StackPanel
            {
                Spacing = 3,
                Children =
                {
                    bolha,
                    new TextBlock { Text = texto, FontWeight = FontWeight.SemiBold, FontSize = 13.5, Foreground = Cores.P(Cores.Texto), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
                    new TextBlock { Text = detalhe, FontSize = 12, Foreground = Cores.P(Cores.Texto2), TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                },
            };
            b.Click += (_, _) => acao();
            return b;
        }
        var atalhos = new UniformGrid { Columns = 2 };
        void Add(Button b) { b.Margin = atalhos.Children.Count % 2 == 0 ? new Thickness(0, 0, 6, 12) : new Thickness(6, 0, 0, 12); atalhos.Children.Add(b); }
        Add(Atalho("Nova venda", "Atalho F2", "venda", "#4F46E5", "#EEF2FF", () => App.IrPara("Nova venda")));
        Add(Atalho("Receber fiado", "Quem está devendo", "receber", "#D97706", "#FFFBEB", () => App.IrPara("A receber")));
        if (App.Avancado)
        {
            Add(Atalho("Entrada de mercadorias", "Do fornecedor", "entrada", "#0891B2", "#ECFEFF", () => App.IrPara("Entrada de mercadorias")));
            Add(Atalho("Fechar caixa", "Conferir o dia", "caixa", "#059669", "#ECFDF5", () => App.IrPara("Caixa")));
        }
        else
        {
            Add(Atalho("Chegou mercadoria", "Somar ao estoque", "entrada", "#0891B2", "#ECFEFF", () => { App.IrPara("Produtos"); _ = ((ProdutosTela)App.Telas["Produtos"]).ChegouMercadoriaAsync(); }));
            Add(Atalho("Novo produto", "Cadastrar", "produtos", "#7C3AED", "#F5F3FF", () => { App.IrPara("Produtos"); _ = ((ProdutosTela)App.Telas["Produtos"]).NovoAsync(); }));
        }
        var cartaoAtalhos = K.Secao("Atalhos", K.Margem(atalhos, 0, 0, 0, -12));
        cartaoAtalhos.Margin = new Thickness(0, 0, 0, 20);

        // para repor
        tBaixo = App.Avancado
            ? new Tabela(new Col("Produto", 140, Expande: true), new Col("Tem", 72, true), new Col("Mín.", 72, true))
            : new Tabela(new Col("Produto", 180, Expande: true), new Col("Tem", 80, true));
        var cabBaixo = K.Linha(10, K.H2("Para repor"), contaBaixo);
        var listaBaixo = K.Lista(tBaixo, K.Ferramentas(cabBaixo, K.Botao("Ver produtos", () => App.IrPara(App.Avancado ? "Estoque" : "Produtos"), classe: "fantasma")),
                                 vazio: "Tudo abastecido!\nNenhum produto acabando.", iconeVazio: "ok");

        var direita = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        K.ColocarNaGrade(direita, cartaoAtalhos, 0, 0); K.ColocarNaGrade(direita, listaBaixo, 1, 0);

        var principal = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,20,1*") };
        K.ColocarNaGrade(principal, esquerda, 0, 0); K.ColocarNaGrade(principal, direita, 0, 2);

        primeirosPassos = MontarPrimeirosPassos();
        Content = K.Rolagem(K.Doca(new StackPanel { Children = { primeirosPassos, cartoes } }, principal), 820);
    }

    Control MontarPrimeirosPassos()
    {
        Control Passo(string n, string texto, params Control[] botoes)
        {
            var num = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Cores.P(Cores.PrimariaSuave), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = n, FontWeight = FontWeight.Bold, Foreground = Cores.P(Cores.Primaria), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 4) };
            K.ColocarNaGrade(linha, num, 0, 0);
            var t = K.Rotulo(texto, false, 14); t.Margin = new Thickness(12, 0); K.ColocarNaGrade(linha, t, 0, 1);
            K.ColocarNaGrade(linha, K.Linha(8, botoes), 0, 2);
            return linha;
        }
        var corpo = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                Passo("1", "Coloque o nome e os dados da sua loja (aparecem nas notas).", K.Botao("Dados da loja", () => App.IrPara("Configurações"))),
                Passo("2", "Cadastre seus produtos — ou traga a lista do Excel de uma vez.", K.Botao("Cadastrar produtos", () => App.IrPara("Produtos")), K.Botao("Importar planilha", () => _ = ((ProdutosTela)App.Telas["Produtos"]).ImportarAsync())),
                Passo("3", "Faça sua primeira venda.", K.Botao("Nova venda", () => App.IrPara("Nova venda"), classe: "primario")),
            },
        };
        var cartao = K.Secao("Bem-vinda! Vamos começar em 3 passos", corpo, subtitulo: "Leva poucos minutos e depois é só vender.", icone: "raio");
        cartao.Margin = new Thickness(0, 0, 0, 20); cartao.IsVisible = false;
        return cartao;
    }

    public override void Atualizar()
    {
        primeirosPassos.IsVisible = !Svc.TemProdutos();
        var d = Svc.ObterPainel();
        hoje.Definir(Util.FmtDinheiro(d.Hoje.Total), $"{d.Hoje.Qtd} venda(s)");
        mes.Definir(Util.FmtDinheiro(d.Mes.Total), $"{d.Mes.Qtd} venda(s)");
        lucro.Definir(Util.FmtDinheiro(d.Mes.Lucro), "vendas − custo", Cores.Texto2);
        var devedores = Svc.ListarDevedores().Where(x => x.Saldo > 0).ToList();
        receber.Definir(Util.FmtDinheiro(devedores.Sum(x => x.Saldo)), $"{devedores.Count} cliente(s) devendo");
        baixo.Definir(d.Estoque.Baixos.ToString(), "abaixo do mínimo", d.Estoque.Baixos > 0 ? "#B91C1C" : null);

        var dias = Svc.VendasPorDia(14);
        grafico.Definir(dias.Select(v =>
        {
            var dt = DateTime.ParseExact(v.Data, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var ehHoje = dt == DateTime.Today;
            return new GraficoBarras.Ponto(ehHoje ? "Hoje" : DiasCurtos[(int)dt.DayOfWeek], $"{dt:dd/MM}", v.Total,
                                           $"{dt:dd/MM}: {Util.FmtDinheiro(v.Total)} em {v.Qtd} venda(s)", ehHoje);
        }).ToList());
        resumoGrafico.Text = $"{Util.FmtDinheiro(dias.Sum(x => x.Total))} em {dias.Sum(x => x.Qtd)} venda(s) no período";

        ((TextBlock)contaBaixo.Child!).Text = d.BaixoEstoque.Count.ToString();
        contaBaixo.IsVisible = d.BaixoEstoque.Count > 0;
        tBaixo.Definir(d.BaixoEstoque.Select(p => new Linha
        {
            Id = p.Id.ToString(), Estilo = p.Estoque == 0 ? "alerta" : "",
            C = App.Avancado ? new[] { p.Nome, p.Estoque.ToString(), p.EstoqueMin.ToString() } : new[] { p.Nome, p.Estoque.ToString() },
        }));
        tVendas.Definir(d.Recentes.Select(v => new Linha
        {
            Id = v.Id.ToString(), Estilo = v.Cancelada ? "cancelado" : "",
            C = new[] { $"#{v.Id:000000}", Quando(v.Data), v.ClienteNome, v.FormaPagamento, Util.FmtDinheiro(v.Total) },
        }));
    }

    /// <summary>"Hoje, 14:32", "Ontem, 09:10" ou "18/10, 11:00".</summary>
    static string Quando(string iso)
    {
        if (!DateTime.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) return Util.FmtData(iso);
        var dia = d.Date == DateTime.Today ? "Hoje" : d.Date == DateTime.Today.AddDays(-1) ? "Ontem" : d.ToString("dd/MM");
        return $"{dia}, {d:HH:mm}";
    }
}
