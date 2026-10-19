using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Início: números do dia e atalhos para o que a vendedora mais faz.</summary>
public sealed class PainelTela : Tela
{
    readonly K.Indicador hoje = new("Vendas hoje", "venda", "#4F46E5", "#E0E7FF");
    readonly K.Indicador mes = new("Vendas no mês", "relatorios", "#0E7490", "#CFFAFE");
    readonly K.Indicador lucro = new("Lucro no mês", "caixa", "#16A34A", "#DCFCE7");
    readonly K.Indicador receber = new("A receber", "receber", "#B45309", "#FEF3C7");
    readonly K.Indicador baixo = new("Estoque baixo", "alerta", "#DC2626", "#FEE2E2");
    readonly Tabela tBaixo, tVendas;
    readonly Control primeirosPassos;
    public bool PrimeirosPassosVisivel => primeirosPassos.IsVisible;
    public IReadOnlyList<K.Indicador> Cartoes => new[] { hoje, mes, lucro, receber, baixo };

    public override string Titulo => $"{Saudacao()}!";
    static readonly string[] Dias = { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
    static readonly string[] Meses = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    public override string Subtitulo { get { var h = DateTime.Today; return $"{char.ToUpperInvariant(Dias[(int)h.DayOfWeek][0])}{Dias[(int)h.DayOfWeek][1..]}, {h.Day} de {Meses[h.Month - 1]}"; } }

    static string Saudacao() => DateTime.Now.Hour switch { < 12 => "Bom dia", < 18 => "Boa tarde", _ => "Boa noite" };

    public PainelTela(MainWindow app) : base(app)
    {
        // sem custo no cadastro (modo simples) o "lucro" não seria confiável, então o cartão some
        var cartoes = new UniformGrid { Rows = 1, Columns = App.Avancado ? 5 : 4 };
        foreach (var c in Cartoes.Where(c => App.Avancado || c != lucro)) { c.Margin = new Thickness(0, 0, 14, 0); cartoes.Children.Add(c); }

        var atalhos = K.Linha(10,
            K.Botao("Nova venda", () => App.IrPara("Nova venda"), icone: "venda", classe: "primario"),
            K.Botao("Receber fiado", () => App.IrPara("A receber"), icone: "receber"),
            App.Avancado ? K.Botao("Entrada de mercadorias", () => App.IrPara("Entrada de mercadorias"), icone: "entrada")
                         : K.Botao("Chegou mercadoria", () => { App.IrPara("Produtos"); _ = ((ProdutosTela)App.Telas["Produtos"]).ChegouMercadoriaAsync(); }, icone: "entrada"));
        if (App.Avancado) atalhos.Children.Add(K.Botao("Fechar caixa", () => App.IrPara("Caixa"), icone: "caixa"));
        atalhos.Margin = new Thickness(0, 18, 0, 18);

        tBaixo = new Tabela(new Col("Produto", 200, Expande: true), new Col("Tipo", 120), new Col("Estoque", 105, true), new Col("Mín.", 80, true));
        tVendas = new Tabela(new Col("Nota", 95), new Col("Data", 150), new Col("Cliente", 150, Expande: true), new Col("Total", 110, true));
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        K.ColocarNaGrade(g, K.H2("Para repor no estoque"), 0, 0);
        K.ColocarNaGrade(g, K.Margem(K.H2("Últimas vendas"), 14, 0, 0, 0), 0, 1);
        tBaixo.Margin = new Thickness(0, 10, 14, 0); tVendas.Margin = new Thickness(14, 10, 0, 0);
        K.ColocarNaGrade(g, tBaixo, 1, 0); K.ColocarNaGrade(g, tVendas, 1, 1);
        primeirosPassos = MontarPrimeirosPassos();
        Content = K.Doca(new StackPanel { Children = { primeirosPassos, cartoes, atalhos } }, g);
    }

    Control MontarPrimeirosPassos()
    {
        Control Passo(string n, string texto, params Control[] botoes)
        {
            var num = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Cores.P(Cores.Primaria), VerticalAlignment = VerticalAlignment.Center,
                                   Child = new TextBlock { Text = n, FontWeight = Avalonia.Media.FontWeight.Bold, Foreground = Avalonia.Media.Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
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
                K.H2("Bem-vinda! Vamos começar em 3 passos"),
                K.Discreto("Leva poucos minutos e depois é só vender."),
                Passo("1", "Coloque o nome e os dados da sua loja (aparecem nas notas).", K.Botao("Dados da loja", () => App.IrPara("Configurações"))),
                Passo("2", "Cadastre seus produtos — ou traga a lista do Excel de uma vez.", K.Botao("Cadastrar produtos", () => App.IrPara("Produtos")), K.Botao("Importar planilha", () => _ = ((ProdutosTela)App.Telas["Produtos"]).ImportarAsync())),
                Passo("3", "Faça sua primeira venda.", K.Botao("Nova venda", () => App.IrPara("Nova venda"), classe: "primario")),
            },
        };
        var cartao = K.Cartao(corpo); cartao.Margin = new Thickness(0, 0, 0, 16); cartao.IsVisible = false;
        return cartao;
    }

    public override void Atualizar()
    {
        primeirosPassos.IsVisible = !Svc.TemProdutos();
        var d = Svc.ObterPainel();
        hoje.Definir(Util.FmtDinheiro(d.Hoje.Total), $"{d.Hoje.Qtd} venda(s)");
        mes.Definir(Util.FmtDinheiro(d.Mes.Total), $"{d.Mes.Qtd} venda(s)");
        lucro.Definir(Util.FmtDinheiro(d.Mes.Lucro), "vendas − custo");
        var devedores = Svc.ListarDevedores().Where(x => x.Saldo > 0).ToList();
        receber.Definir(Util.FmtDinheiro(devedores.Sum(x => x.Saldo)), $"{devedores.Count} cliente(s) devendo");
        baixo.Definir(d.Estoque.Baixos.ToString(), "abaixo do mínimo");
        tBaixo.Definir(d.BaixoEstoque.Select(p => new Linha { Id = p.Id.ToString(), C = new[] { p.Nome, p.Categoria, p.Estoque.ToString(), p.EstoqueMin.ToString() } }));
        tVendas.Definir(d.Recentes.Select(v => new Linha
        {
            Id = v.Id.ToString(), Estilo = v.Cancelada ? "cancelado" : "",
            C = new[] { $"{v.Id:000000}", Util.FmtData(v.Data), v.ClienteNome, Util.FmtDinheiro(v.Total) },
        }));
    }
}
