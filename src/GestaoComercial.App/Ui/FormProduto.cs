using System.Globalization;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Formulário de produto (usado na lista de produtos e no cadastro rápido a partir da venda/entrada).</summary>
public static class FormProduto
{
    static bool calculando;

    public static List<Campo> Campos(Servico svc, bool novo)
    {
        var cats = new List<(long? Valor, string Rotulo)> { (null, "(sem tipo)") };
        cats.AddRange(svc.ListarCategorias().Select(c => ((long?)c.Id, c.Nome)));
        var c = new List<Campo>
        {
            new() { Chave = "nome", Rotulo = "Nome do produto", Obrigatorio = true },
            new() { Chave = "codigo", Rotulo = "Código / cód. barras", Dica = "Em branco = gerado automaticamente. Pode passar o leitor de código de barras aqui." },
            new() { Chave = "categoria", Rotulo = "Tipo de produto", Tipo = TipoCampo.Escolha, Opcoes = cats },
            new() { Chave = "unidade", Rotulo = "Unidade", Dica = "un, kg, cx, pct, l..." },
            new() { Chave = "custo", Rotulo = "Preço de custo (R$)", Tipo = TipoCampo.Dinheiro },
            new() { Chave = "margem", Rotulo = "Margem de lucro (%)", Dica = "Opcional: digite o custo e a margem e o preço de venda é calculado." },
            new() { Chave = "venda", Rotulo = "Preço de venda (R$)", Tipo = TipoCampo.Dinheiro, Obrigatorio = true },
            new() { Chave = "minimo", Rotulo = "Estoque mínimo", Tipo = TipoCampo.Inteiro, Dica = "Avisa quando o estoque chegar a este valor." },
        };
        if (novo) c.Add(new Campo { Chave = "inicial", Rotulo = "Estoque inicial", Tipo = TipoCampo.Inteiro });
        else
        {
            c.Add(new Campo { Chave = "estoque", Rotulo = "Estoque atual", Tipo = TipoCampo.Inteiro, SomenteLeitura = true, Dica = "Para corrigir o estoque use 'Ajustar estoque' na página Estoque." });
            c.Add(new Campo { Chave = "ativo", Rotulo = "Ativo", Tipo = TipoCampo.Bool });
        }
        return c;
    }

    public static Dictionary<string, object?> Iniciais(Produto? p, string? codigo = null, string? nome = null)
    {
        if (p == null)
            return new() { ["unidade"] = "un", ["minimo"] = 0L, ["inicial"] = 0L, ["codigo"] = codigo ?? "", ["nome"] = nome ?? "" };
        return new()
        {
            ["nome"] = p.Nome, ["codigo"] = p.Codigo, ["categoria"] = p.CategoriaId, ["unidade"] = p.Unidade, ["custo"] = p.PrecoCusto,
            ["margem"] = p.PrecoCusto > 0 ? FmtMargem(p.PrecoVenda, p.PrecoCusto) : "", ["venda"] = p.PrecoVenda, ["minimo"] = p.EstoqueMin,
            ["estoque"] = p.Estoque, ["ativo"] = p.Ativo,
        };
    }

    static string FmtMargem(long venda, long custo) =>
        (((decimal)venda - custo) / custo * 100m).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    static bool LerMargem(string t, out decimal m) =>
        decimal.TryParse(t.Replace("%", "").Replace(',', '.').Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out m);

    /// <summary>Calcula preço de venda a partir de custo e margem (e a margem a partir do preço), enquanto a pessoa digita.</summary>
    public static void AoMudar(FormDialog f, string chave)
    {
        if (calculando) return;
        try
        {
            calculando = true;
            if (chave is "custo" or "margem")
            {
                var custoTxt = f.Texto("custo"); var margemTxt = f.Texto("margem");
                if (custoTxt.Trim().Length == 0 || margemTxt.Trim().Length == 0) return;
                var custo = Util.ParseDinheiro(custoTxt, "Custo");
                if (custo <= 0 || !LerMargem(margemTxt, out var m) || m <= -100 || m > 10000) return;
                f.Definir("venda", Util.FmtDinheiro((long)Math.Round(custo * (1 + m / 100m), MidpointRounding.AwayFromZero), false));
            }
            else if (chave == "venda")
            {
                if (f.Texto("custo").Trim().Length == 0 || f.Texto("venda").Trim().Length == 0) return;
                var custo = Util.ParseDinheiro(f.Texto("custo"), "Custo"); var venda = Util.ParseDinheiro(f.Texto("venda"), "Preço");
                if (custo > 0) f.Definir("margem", FmtMargem(venda, custo));
            }
        }
        catch (ValidacaoException) { /* ainda digitando */ }
        finally { calculando = false; }
    }

    public static long Salvar(Servico svc, Valores v, long? id) =>
        svc.SalvarProduto(new ProdutoDados(v.Texto("nome"), v.Texto("codigo"), v.Escolha("categoria"), v.Texto("unidade"), v.Num("custo"), v.Num("venda"),
                                           v.Num("minimo"), v.Num("inicial"), id == null || v.Bool("ativo")), id);
}
