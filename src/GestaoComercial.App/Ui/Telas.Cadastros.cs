using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Base de uma tela de cadastro: lista + busca + novo/editar/excluir + exportar CSV.</summary>
public abstract class CadastroTela<T> : Tela where T : class
{
    protected abstract string Singular { get; }
    protected abstract string Plural { get; }
    protected abstract Col[] Colunas { get; }
    protected abstract List<T> Listar();
    protected abstract Linha ParaLinha(T item);
    protected abstract T Obter(long id);
    protected abstract string NomeDe(T item);
    protected abstract List<Campo> Campos(bool novo);
    protected abstract Dictionary<string, object?> Iniciais(T? item);
    protected abstract void Salvar(Valores v, long? id);
    protected abstract ResultadoExclusao Remover(long id);
    protected abstract string[] CabecalhoCsv { get; }
    protected abstract object?[] LinhaCsv(T item);
    protected virtual void MontarFiltros(StackPanel barra) { }
    protected virtual void MontarAcoes(StackPanel barra) { }

    public TextBox Busca { get; }
    public CheckBox Inativos { get; }
    public Tabela Tabela { get; }
    readonly TextBlock total = K.Discreto("");
    Button novo;
    protected virtual string Descricao => "";
    public override string Titulo => char.ToUpperInvariant(Plural[0]) + Plural[1..];
    public override string Subtitulo => Descricao;
    public override Control? AcoesCabecalho => novo;

    protected CadastroTela(MainWindow app) : base(app)
    {
        Busca = K.Caixa("", 300, "Buscar…");
        Busca.InnerLeftContent = K.Margem(Icones.Criar("busca", 18, "#9CA3AF"), 10, 0, 0, 0);
        Inativos = new CheckBox { Content = "Mostrar inativos", VerticalAlignment = VerticalAlignment.Center };
        var filtros = K.Linha(14, Busca, Inativos);
        MontarFiltros(filtros);

        novo = K.Botao($"Novo {Singular}", SeguroAsync(NovoAsync), icone: "mais", classe: "primario");
        var direita = K.Linha(K.Botao("Editar", SeguroAsync(EditarAsync), icone: "editar"), K.Botao("Excluir", SeguroAsync(ExcluirAsync), icone: "lixeira", classe: "perigo"));
        MontarAcoes(direita);
        direita.Children.Add(K.Botao("Exportar CSV", SeguroAsync(ExportarAsync), icone: "exportar"));
        var acoes = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 0, 14), Children = { filtros, direita } };

        Tabela = new Tabela(Colunas);
        total.Margin = new Thickness(0, 10, 0, 0);
        Content = K.Doca(acoes, Tabela, total);

        K.AoMudarTexto(Busca, Seguro(Atualizar));
        Inativos.IsCheckedChanged += (_, _) => Seguro(Atualizar)();
        Tabela.Duplo += SeguroAsync(EditarAsync);
    }

    public override void Atualizar()
    {
        var dados = Listar();
        Tabela.Definir(dados.Select(ParaLinha));
        total.Text = $"{dados.Count} {(dados.Count == 1 ? Singular : Plural)}";
    }

    public async Task NovoAsync()
    {
        if (await FormDialog.Abrir(Janela, $"Novo {Singular}", Campos(true), Iniciais(null), v => Salvar(v, null))) Atualizar();
    }

    public async Task EditarAsync()
    {
        var id = Exigir(Tabela.IdLong, $"um {Singular}");
        var item = Obter(id);
        if (await FormDialog.Abrir(Janela, $"Editar {Singular}", Campos(false), Iniciais(item), v => Salvar(v, id))) Atualizar();
    }

    public async Task ExcluirAsync()
    {
        var id = Exigir(Tabela.IdLong, $"um {Singular}");
        var item = Obter(id);
        if (!await Confirmar($"Excluir '{NomeDe(item)}'?\n\nSe já houver histórico (vendas, entradas ou movimentos), ele será apenas inativado para preservar os registros."))
            return;
        var r = Remover(id);
        Atualizar();
        if (r == ResultadoExclusao.Inativado)
            await Info("Este registro já tem histórico e não pode ser apagado, então foi INATIVADO.\nEle não aparece mais nas listas de seleção. Use 'Mostrar inativos' + 'Editar' para reativar.", "Inativado");
    }

    public async Task ExportarAsync()
    {
        var dados = Listar();
        if (dados.Count == 0) { await Info("Não há dados para exportar.", "Exportar"); return; }
        var caminho = await Plat.Atual.SalvarArquivo(Janela, "Exportar CSV", Config.Subpasta("exportacoes"), $"{Plural}.csv", "CSV (Excel)", "csv");
        if (caminho == null) return;
        var n = Util.ExportarCsv(caminho, CabecalhoCsv, dados.Select(LinhaCsv));
        await Info($"{n} linha(s) exportada(s) para:\n{caminho}", "Exportado");
    }
}

public abstract class PessoasTela : CadastroTela<Pessoa>
{
    protected abstract TipoPessoa Tipo { get; }
    protected PessoasTela(MainWindow app) : base(app) { }

    protected override Col[] Colunas => new[]
    {
        new Col("Nome", 240, Expande: true), new Col("CPF/CNPJ", 150), new Col("Telefone", 130), new Col("E-mail", 210),
        new Col("Endereço", 240), new Col("Situação", 110, Chip: true),
    };
    protected override List<Pessoa> Listar() => Svc.ListarPessoas(Tipo, Busca.Text ?? "", Inativos.IsChecked == true);
    protected override Linha ParaLinha(Pessoa p) => new()
    {
        Id = p.Id.ToString(), Estilo = p.Ativo ? "" : "cancelado",
        Fabrica = () => new[] { p.Nome, Util.FmtDocumento(p.Documento), p.Telefone, p.Email, p.Endereco, p.Ativo ? "Ativo" : "Inativo" },
    };
    protected override Pessoa Obter(long id) => Svc.ObterPessoa(Tipo, id);
    protected override string NomeDe(Pessoa p) => p.Nome;
    protected override List<Campo> Campos(bool novo)
    {
        var c = new List<Campo>
        {
            new() { Chave = "nome", Rotulo = "Nome", Obrigatorio = true },
            new() { Chave = "documento", Rotulo = "CPF/CNPJ", Dica = "Opcional. Só números ou com pontuação." },
            new() { Chave = "telefone", Rotulo = "Telefone" },
            new() { Chave = "email", Rotulo = "E-mail" },
            new() { Chave = "endereco", Rotulo = "Endereço", Tipo = TipoCampo.Multilinha },
        };
        if (!novo) c.Add(new Campo { Chave = "ativo", Rotulo = "Ativo", Tipo = TipoCampo.Bool });
        return c;
    }
    protected override Dictionary<string, object?> Iniciais(Pessoa? p) => p == null ? new() : new()
    {
        ["nome"] = p.Nome, ["documento"] = Util.FmtDocumento(p.Documento), ["telefone"] = p.Telefone, ["email"] = p.Email,
        ["endereco"] = p.Endereco, ["ativo"] = p.Ativo,
    };
    protected override void Salvar(Valores v, long? id) =>
        Svc.SalvarPessoa(Tipo, new PessoaDados(v.Texto("nome"), v.Texto("documento"), v.Texto("telefone"), v.Texto("email"), v.Texto("endereco"),
                                               id == null || v.Bool("ativo")), id);
    protected override ResultadoExclusao Remover(long id) => Svc.ExcluirPessoa(Tipo, id);
    protected override string[] CabecalhoCsv => new[] { "Nome", "CPF/CNPJ", "Telefone", "E-mail", "Endereço", "Situação" };
    protected override object?[] LinhaCsv(Pessoa p) => new object?[] { p.Nome, Util.FmtDocumento(p.Documento), p.Telefone, p.Email, p.Endereco, p.Ativo ? "Ativo" : "Inativo" };
}

public sealed class ClientesTela : PessoasTela
{
    public ClientesTela(MainWindow app) : base(app) { }
    protected override string Descricao => "Quem compra de você. Abra o histórico para ver as compras e o fiado de cada um.";
    protected override void MontarAcoes(StackPanel barra) =>
        barra.Children.Insert(0, K.Botao("Histórico / fiado", SeguroAsync(HistoricoAsync), icone: "ver"));

    public Task HistoricoAsync() => HistoricoClienteDialog.Abrir(Janela, Svc, Exigir(Tabela.IdLong, "um cliente"), AtualizarTudo);
    protected override TipoPessoa Tipo => TipoPessoa.Cliente;
    protected override string Singular => "cliente";
    protected override string Plural => "clientes";
}

public sealed class FornecedoresTela : PessoasTela
{
    public FornecedoresTela(MainWindow app) : base(app) { }
    protected override string Descricao => "De quem você compra as mercadorias.";
    protected override TipoPessoa Tipo => TipoPessoa.Fornecedor;
    protected override string Singular => "fornecedor";
    protected override string Plural => "fornecedores";
}

public sealed class ProdutosTela : CadastroTela<Produto>
{
    ComboBox filtroTipo = null!;
    Dictionary<string, long?> tipos = new();
    bool carregando;

    public ProdutosTela(MainWindow app) : base(app) { }
    protected override string Descricao => "O que você vende: preços, tipos e estoque mínimo. Linhas em vermelho precisam de reposição.";

    protected override string Singular => "produto";
    protected override string Plural => "produtos";
    protected override Col[] Colunas => new[]
    {
        new Col("Código", 100), new Col("Produto", 260, Expande: true), new Col("Tipo", 140), new Col("Un.", 60), new Col("Custo", 100, true),
        new Col("Venda", 100, true), new Col("Estoque", 105, true), new Col("Mín.", 80, true), new Col("Situação", 110, Chip: true),
    };

    protected override void MontarFiltros(StackPanel barra)
    {
        filtroTipo = K.Combo(new[] { "Todos" }, 190, "Todos");
        filtroTipo.SelectionChanged += (_, _) => { if (!carregando) Seguro(Atualizar)(); };
        barra.Children.Add(filtroTipo);
    }

    protected override void MontarAcoes(StackPanel barra)
    {
        barra.Children.Insert(0, K.Botao("Tipos…", SeguroAsync(GerenciarTiposAsync), icone: "produtos"));
        barra.Children.Insert(1, K.Botao("Reajustar preços…", SeguroAsync(ReajustarAsync), icone: "relatorios"));
    }

    /// <summary>Aumenta ou reduz os preços de todos os produtos (ou de um tipo) em uma porcentagem.</summary>
    public async Task ReajustarAsync()
    {
        var opcoes = new List<(long? Valor, string Rotulo)> { (-1, "Todos os produtos"), (0, "Produtos sem tipo") };
        opcoes.AddRange(Svc.ListarCategorias().Select(c => ((long?)c.Id, c.Nome)));
        var campos = new List<Campo>
        {
            new() { Chave = "tipo", Rotulo = "Quais produtos", Tipo = TipoCampo.Escolha, Opcoes = opcoes },
            new() { Chave = "pct", Rotulo = "Porcentagem", Obrigatorio = true, Dica = "Use 10 para aumentar 10%, ou -5 para reduzir 5%." },
            new() { Chave = "custo", Rotulo = "Reajustar também o custo", Tipo = TipoCampo.Bool },
        };
        long? tipo = null; decimal pct = 0; bool custo = false;
        var ok = await FormDialog.Abrir(Janela, "Reajustar preços", campos, new() { ["tipo"] = -1L, ["custo"] = false }, v =>
        {
            var t = v.Texto("pct").Replace("%", "").Replace(',', '.').Trim();
            if (!decimal.TryParse(t, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out pct))
                throw new ValidacaoException("Porcentagem inválida. Exemplos: 10 ou -5,5.");
            if (pct == 0 || pct < -90 || pct > 500) throw new ValidacaoException("A porcentagem deve ser diferente de zero e ficar entre -90 e 500.");
            tipo = v.Escolha("tipo") is -1 ? null : v.Escolha("tipo"); custo = v.Bool("custo");
        }, "Continuar");
        if (!ok) return;
        var n = Svc.ListarProdutos(categoriaId: tipo).Count;
        if (n == 0) { await Info("Não há produtos nesse grupo."); return; }
        if (!await Confirmar($"Reajustar {n} produto(s) em {(pct > 0 ? "+" : "")}{pct:0.##}%?\n\nOs preços de venda{(custo ? " e de custo" : "")} serão alterados agora.")) return;
        Svc.ReajustarPrecos(tipo, pct, custo);
        Atualizar();
        await Info($"{n} produto(s) reajustado(s) em {(pct > 0 ? "+" : "")}{pct:0.##}%.");
    }

    public async Task GerenciarTiposAsync() => await CategoriasDialog.Abrir(Janela, Svc, () => Seguro(Atualizar)());

    public void SelecionarTipo(string nome) => filtroTipo.SelectedItem = nome;

    protected override List<Produto> Listar()
    {
        carregando = true;
        try
        {
            var atual = filtroTipo.SelectedItem as string ?? "Todos";
            tipos = new() { ["Todos"] = null, ["Sem categoria"] = 0 };
            foreach (var c in Svc.ListarCategorias()) tipos[c.Nome] = c.Id;
            filtroTipo.ItemsSource = tipos.Keys.ToList();
            filtroTipo.SelectedItem = tipos.ContainsKey(atual) ? atual : "Todos";
        }
        finally { carregando = false; }
        return Svc.ListarProdutos(Busca.Text ?? "", tipos.GetValueOrDefault(filtroTipo.SelectedItem as string ?? "Todos"), Inativos.IsChecked == true);
    }

    protected override Linha ParaLinha(Produto p) => new()
    {
        Id = p.Id.ToString(), Estilo = !p.Ativo ? "cancelado" : p.Baixo ? "alerta" : "",
        Fabrica = () => new[] { p.Codigo, p.Nome, p.Categoria, p.Unidade, Util.FmtDinheiro(p.PrecoCusto), Util.FmtDinheiro(p.PrecoVenda),
                                p.Estoque.ToString(), p.EstoqueMin.ToString(), p.Ativo ? "Ativo" : "Inativo" },
    };
    protected override Produto Obter(long id) => Svc.ObterProduto(id);
    protected override string NomeDe(Produto p) => p.Nome;

    protected override List<Campo> Campos(bool novo)
    {
        var cats = new List<(long? Valor, string Rotulo)> { (null, "(sem tipo)") };
        cats.AddRange(Svc.ListarCategorias().Select(c => ((long?)c.Id, c.Nome)));
        var c = new List<Campo>
        {
            new() { Chave = "nome", Rotulo = "Nome do produto", Obrigatorio = true },
            new() { Chave = "codigo", Rotulo = "Código / cód. barras", Dica = "Deixe em branco para gerar automaticamente." },
            new() { Chave = "categoria", Rotulo = "Tipo de produto", Tipo = TipoCampo.Escolha, Opcoes = cats },
            new() { Chave = "unidade", Rotulo = "Unidade", Dica = "un, kg, cx, pct, l..." },
            new() { Chave = "custo", Rotulo = "Preço de custo (R$)", Tipo = TipoCampo.Dinheiro },
            new() { Chave = "venda", Rotulo = "Preço de venda (R$)", Tipo = TipoCampo.Dinheiro, Obrigatorio = true },
            new() { Chave = "minimo", Rotulo = "Estoque mínimo", Tipo = TipoCampo.Inteiro, Dica = "Avisa quando o estoque chegar a este valor." },
        };
        if (novo) c.Add(new Campo { Chave = "inicial", Rotulo = "Estoque inicial", Tipo = TipoCampo.Inteiro });
        else
        {
            c.Add(new Campo { Chave = "estoque", Rotulo = "Estoque atual", Tipo = TipoCampo.Inteiro, SomenteLeitura = true, Dica = "Para corrigir o estoque use 'Ajustar estoque' na aba Estoque." });
            c.Add(new Campo { Chave = "ativo", Rotulo = "Ativo", Tipo = TipoCampo.Bool });
        }
        return c;
    }

    protected override Dictionary<string, object?> Iniciais(Produto? p) => p == null
        ? new() { ["unidade"] = "un", ["minimo"] = 0L, ["inicial"] = 0L }
        : new()
        {
            ["nome"] = p.Nome, ["codigo"] = p.Codigo, ["categoria"] = p.CategoriaId, ["unidade"] = p.Unidade, ["custo"] = p.PrecoCusto,
            ["venda"] = p.PrecoVenda, ["minimo"] = p.EstoqueMin, ["estoque"] = p.Estoque, ["ativo"] = p.Ativo,
        };

    protected override void Salvar(Valores v, long? id) =>
        Svc.SalvarProduto(new ProdutoDados(v.Texto("nome"), v.Texto("codigo"), v.Escolha("categoria"), v.Texto("unidade"), v.Num("custo"), v.Num("venda"),
                                           v.Num("minimo"), v.Num("inicial"), id == null || v.Bool("ativo")), id);

    protected override ResultadoExclusao Remover(long id) => Svc.ExcluirProduto(id);

    protected override string[] CabecalhoCsv => new[] { "Código", "Produto", "Tipo", "Unidade", "Custo", "Venda", "Estoque", "Estoque mínimo", "Situação" };
    protected override object?[] LinhaCsv(Produto p) => new object?[]
    {
        p.Codigo, p.Nome, p.Categoria, p.Unidade, Util.FmtDinheiro(p.PrecoCusto, false), Util.FmtDinheiro(p.PrecoVenda, false),
        p.Estoque, p.EstoqueMin, p.Ativo ? "Ativo" : "Inativo",
    };
}
