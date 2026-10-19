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
    public IReadOnlyList<K.Indicador> Cartoes => new[] { hoje, mes, lucro, receber, baixo };

    public override string Titulo => $"{Saudacao()}!";
    static readonly string[] Dias = { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" };
    static readonly string[] Meses = { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
    public override string Subtitulo { get { var h = DateTime.Today; return $"{char.ToUpperInvariant(Dias[(int)h.DayOfWeek][0])}{Dias[(int)h.DayOfWeek][1..]}, {h.Day} de {Meses[h.Month - 1]}"; } }

    static string Saudacao() => DateTime.Now.Hour switch { < 12 => "Bom dia", < 18 => "Boa tarde", _ => "Boa noite" };

    public PainelTela(MainWindow app) : base(app)
    {
        var cartoes = new UniformGrid { Rows = 1, Columns = 5 };
        foreach (var c in Cartoes) { c.Margin = new Thickness(0, 0, 14, 0); cartoes.Children.Add(c); }

        var atalhos = K.Linha(10,
            K.Botao("Nova venda", () => App.IrPara("Nova venda"), icone: "venda", classe: "primario"),
            K.Botao("Receber fiado", () => App.IrPara("A receber"), icone: "receber"),
            K.Botao("Entrada de mercadorias", () => App.IrPara("Entrada de mercadorias"), icone: "entrada"),
            K.Botao("Fechar caixa", () => App.IrPara("Caixa"), icone: "caixa"));
        atalhos.Margin = new Thickness(0, 18, 0, 18);

        tBaixo = new Tabela(new Col("Produto", 200, Expande: true), new Col("Tipo", 120), new Col("Estoque", 105, true), new Col("Mín.", 80, true));
        tVendas = new Tabela(new Col("Nota", 95), new Col("Data", 150), new Col("Cliente", 150, Expande: true), new Col("Total", 110, true));
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,*") };
        K.ColocarNaGrade(g, K.H2("Para repor no estoque"), 0, 0);
        K.ColocarNaGrade(g, K.Margem(K.H2("Últimas vendas"), 14, 0, 0, 0), 0, 1);
        tBaixo.Margin = new Thickness(0, 10, 14, 0); tVendas.Margin = new Thickness(14, 10, 0, 0);
        K.ColocarNaGrade(g, tBaixo, 1, 0); K.ColocarNaGrade(g, tVendas, 1, 1);
        Content = K.Doca(new StackPanel { Children = { cartoes, atalhos } }, g);
    }

    public override void Atualizar()
    {
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
