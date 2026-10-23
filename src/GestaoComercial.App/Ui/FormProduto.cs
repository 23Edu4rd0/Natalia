using System.Globalization;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Formulário de produto (usado na lista de produtos e no cadastro rápido a partir da venda/entrada).</summary>
public static class FormProduto
{
    static bool calculando;

    /// <summary>Modo simples: só nome, tipo, preço e quantidade. O avançado traz também código, unidade, custo, margem e estoque mínimo.</summary>
    public static List<Campo> Campos(Servico svc, bool novo, bool avancado)
    {
        var cats = new List<(long? Valor, string Rotulo)> { (null, "(sem tipo)") };
        cats.AddRange(svc.ListarCategorias().Select(c => ((long?)c.Id, c.Nome)));
        var c = new List<Campo> { new() { Chave = "nome", Rotulo = "Nome do produto", Obrigatorio = true } };
        if (avancado) c.Add(new Campo { Chave = "codigo", Rotulo = "Código / cód. barras", Dica = "Em branco = gerado automaticamente. Pode passar o leitor de código de barras aqui." });
        c.Add(new Campo { Chave = "categoria", Rotulo = "Tipo de produto", Tipo = TipoCampo.Escolha, Opcoes = cats });
        if (avancado) c.Add(new Campo { Chave = "ncm", Rotulo = "NCM (para nota fiscal)", Dica = "8 números (ex.: 6109.10.00). Pode deixar em branco e informar na hora de emitir a nota." });
        if (avancado)
        {
            c.Add(new Campo { Chave = "unidade", Rotulo = "Unidade", Dica = "un, kg, cx, pct, l..." });
            c.Add(new Campo { Chave = "custo", Rotulo = "Preço de custo (R$)", Tipo = TipoCampo.Dinheiro });
            c.Add(new Campo { Chave = "margem", Rotulo = "Margem de lucro (%)", Dica = "Opcional: digite o custo e a margem e o preço de venda é calculado." });
        }
        c.Add(new Campo { Chave = "venda", Rotulo = "Preço de venda (R$)", Tipo = TipoCampo.Dinheiro, Obrigatorio = true });
        if (avancado) c.Add(new Campo { Chave = "minimo", Rotulo = "Estoque mínimo", Tipo = TipoCampo.Inteiro, Dica = "Avisa quando o estoque chegar a este valor." });
        if (novo) c.Add(new Campo { Chave = "inicial", Rotulo = avancado ? "Estoque inicial" : "Quantidade em estoque", Tipo = TipoCampo.Inteiro });
        else
        {
            c.Add(new Campo
            {
                Chave = "estoque", Rotulo = avancado ? "Estoque atual" : "Quantidade em estoque", Tipo = TipoCampo.Inteiro, SomenteLeitura = true,
                Dica = avancado ? "Para corrigir o estoque use 'Ajustar estoque' na página Estoque." : "Para mudar, use “Chegou mercadoria” ou “Corrigir estoque” na lista de produtos.",
            });
            c.Add(new Campo { Chave = "ativo", Rotulo = "Ativo", Tipo = TipoCampo.Bool });
        }
        return c;
    }

    public static Dictionary<string, object?> Iniciais(Produto? p, string? codigo = null, string? nome = null, string? ncm = null)
    {
        if (p == null)
            return new() { ["unidade"] = "un", ["minimo"] = 0L, ["inicial"] = 0L, ["codigo"] = codigo ?? "", ["nome"] = nome ?? "" };
        return new()
        {
            ["nome"] = p.Nome, ["codigo"] = p.Codigo, ["categoria"] = p.CategoriaId, ["unidade"] = p.Unidade, ["custo"] = p.PrecoCusto,
            ["margem"] = p.PrecoCusto > 0 ? FmtMargem(p.PrecoVenda, p.PrecoCusto) : "", ["venda"] = p.PrecoVenda, ["minimo"] = p.EstoqueMin,
            ["estoque"] = p.Estoque, ["ativo"] = p.Ativo, ["ncm"] = ncm ?? "",
        };
    }

    static string FmtMargem(long venda, long custo) =>
        (((decimal)venda - custo) / custo * 100m).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',');

    static bool LerMargem(string t, out decimal m) =>
        decimal.TryParse(t.Replace("%", "").Replace(',', '.').Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out m);

    /// <summary>Calcula preço de venda a partir de custo e margem (e a margem a partir do preço), enquanto a pessoa digita.</summary>
    public static void AoMudar(FormDialog f, string chave)
    {
        if (calculando || !f.Chaves.Contains("custo")) return;    // modo simples não tem custo/margem
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

    /// <summary>Campos que o formulário não mostra (modo simples) mantêm o valor que o produto já tinha.</summary>
    public static long Salvar(Servico svc, Valores v, long? id)
    {
        var atual = id is long i ? svc.ObterProduto(i) : null;
        string Txt(string k, string padrao) => v.Tem(k) ? v.Texto(k) : padrao;
        long Num(string k, long padrao) => v.Tem(k) ? v.Num(k) : padrao;
        var ncm = Util.SomenteDigitos(v.Texto("ncm"));
        if (v.Tem("ncm") && ncm.Length is not (0 or 8)) throw new ValidacaoException("NCM deve ter 8 números (ou ficar em branco).");
        var pid = svc.SalvarProduto(new ProdutoDados(v.Texto("nome"), Txt("codigo", atual?.Codigo ?? ""), v.Escolha("categoria"), Txt("unidade", atual?.Unidade ?? "un"),
                                                  Num("custo", atual?.PrecoCusto ?? 0), v.Num("venda"), Num("minimo", atual?.EstoqueMin ?? 0), v.Num("inicial"),
                                                  id == null || v.Bool("ativo")), id);
        if (v.Tem("ncm"))
        {
            var f = svc.ObterFiscalProduto(pid);
            if (f.Ncm != ncm) svc.SalvarFiscalProduto(f with { Ncm = ncm });
        }
        return pid;
    }
}
