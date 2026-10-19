using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Paleta do programa.</summary>
public static class Cores
{
    static readonly Dictionary<string, IBrush> Cache = new();
    public static IBrush P(string hex) { if (!Cache.TryGetValue(hex, out var b)) Cache[hex] = b = new SolidColorBrush(Color.Parse(hex)); return b; }

    public const string Primaria = "#4F46E5", Texto = "#111827", Texto2 = "#6B7280", Borda = "#E6E9F0", Fundo = "#F5F6FA";
    public const string Verde = "#16A34A", VerdeFundo = "#DCFCE7", Vermelho = "#DC2626", VermelhoFundo = "#FEE2E2";
    public const string Ambar = "#B45309", AmbarFundo = "#FEF3C7", Cinza = "#4B5563", CinzaFundo = "#F3F4F6", Azul = "#3730A3", AzulFundo = "#E0E7FF";
}

/// <summary>Ícones vetoriais simples (Material Design), desenhados como geometria: iguais em qualquer sistema.</summary>
public static class Icones
{
    static readonly Dictionary<string, string> Dados = new()
    {
        ["inicio"] = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z",
        ["venda"] = "M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49A1.003 1.003 0 0 0 20 4H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z",
        ["notas"] = "M18 17H6v-2h12v2zm0-4H6v-2h12v2zm0-4H6V7h12v2zM3 22l1.5-1.5L6 22l1.5-1.5L9 22l1.5-1.5L12 22l1.5-1.5L15 22l1.5-1.5L18 22l1.5-1.5L21 22V2l-1.5 1.5L18 2l-1.5 1.5L15 2l-1.5 1.5L12 2l-1.5 1.5L9 2 7.5 3.5 6 2 4.5 3.5 3 2v20z",
        ["caixa"] = "M19 14V6c0-1.1-.9-2-2-2H3c-1.1 0-2 .9-2 2v8c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2zm-9 1c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm13-6v11c0 1.1-.9 2-2 2H4v-2h17V9h2z",
        ["receber"] = "M21 18v1c0 1.1-.9 2-2 2H5c-1.11 0-2-.9-2-2V5c0-1.1.89-2 2-2h14c1.1 0 2 .9 2 2v1h-9c-1.11 0-2 .9-2 2v8c0 1.1.89 2 2 2h9zm-9-2h10V8H12v8zm4-2.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z",
        ["clientes"] = "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z",
        ["produtos"] = "M20 2H4c-1 0-2 .9-2 2v3.01c0 .72.43 1.34 1 1.69V20c0 1.1 1.1 2 2 2h14c.9 0 2-.9 2-2V8.7c.57-.35 1-.97 1-1.69V4c0-1.1-1-2-2-2zm-5 12H9v-2h6v2zm5-7H4V4l16-.02V7z",
        ["estoque"] = "M12 2 2 7v2h20V7L12 2zM4 11v8H2v2h20v-2h-2v-8h-2v8h-3v-8h-2v8h-2v-8H9v8H6v-8H4z",
        ["entrada"] = "M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zM6 18.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm13.5-9l1.96 2.5H17V9.5h2.5zm-1.5 9c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z",
        ["fornecedores"] = "M12 7V3H2v18h20V7H12zM6 19H4v-2h2v2zm0-4H4v-2h2v2zm0-4H4V9h2v2zm0-4H4V5h2v2zm4 12H8v-2h2v2zm0-4H8v-2h2v2zm0-4H8V9h2v2zm0-4H8V5h2v2zm10 12h-8v-2h2v-2h-2v-2h2v-2h-2V9h8v10zm-2-8h-2v2h2v-2zm0 4h-2v2h2v-2z",
        ["relatorios"] = "M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z",
        ["config"] = "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58a.49.49 0 0 0 .12-.61l-1.92-3.32a.488.488 0 0 0-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54a.484.484 0 0 0-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58a.49.49 0 0 0-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z",
        ["mais"] = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z",
        ["menos"] = "M19 13H5v-2h14v2z",
        ["busca"] = "M15.5 14h-.79l-.28-.27C15.41 12.59 16 11.11 16 9.5 16 5.91 13.09 3 9.5 3S3 5.91 3 9.5 5.91 16 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z",
        ["imprimir"] = "M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z",
        ["whatsapp"] = "M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 9h12v2H6V9zm8 5H6v-2h8v2zm4-6H6V6h12v2z",
        ["ver"] = "M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z",
        ["lixeira"] = "M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z",
        ["editar"] = "M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z",
        ["ok"] = "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z",
        ["alerta"] = "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z",
        ["exportar"] = "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z",
        ["pasta"] = "M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z",
        ["sacola"] = "M18 6h-2c0-2.21-1.79-4-4-4S8 3.79 8 6H6c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm-8 4c0 .55-.45 1-1 1s-1-.45-1-1V8h2v2zm2-6c1.1 0 2 .9 2 2h-4c0-1.1.9-2 2-2zm4 6c0 .55-.45 1-1 1s-1-.45-1-1V8h2v2z",
    };

    public static Control Criar(string nome, double tamanho = 18, string cor = "#6B7280") => new Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse(Dados[nome]), Fill = Cores.P(cor), Width = tamanho, Height = tamanho, Stretch = Stretch.Uniform,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static void Pintar(Control icone, string cor) { if (icone is Avalonia.Controls.Shapes.Path p) p.Fill = Cores.P(cor); }
}

/// <summary>Linha de uma tabela: textos já formatados + estilo visual.</summary>
public sealed class Linha
{
    string[]? c;
    /// <summary>Alternativa a <see cref="C"/>: os textos só são formatados quando a linha aparece na tela (listas grandes).</summary>
    public Func<string[]>? Fabrica { get; init; }
    public string[] C { get => c ??= Fabrica?.Invoke() ?? Array.Empty<string>(); init => c = value; }
    public string? Id { get; init; }
    /// <summary>"", "alerta" (vermelho claro), "grupo" (cabeçalho de grupo) ou "cancelado" (cinza).</summary>
    public string Estilo { get; init; } = "";
    public object? Tag { get; init; }
}

/// <param name="Chip">Mostra o texto como uma etiqueta colorida (ex.: Concluída, Cancelada, Fiado).</param>
public sealed record Col(string Titulo, double Largura, bool Direita = false, bool Expande = false, bool Chip = false);

/// <summary>Tabela somente-leitura com seleção única, cores por linha, etiquetas de status e duplo clique.</summary>
public sealed class Tabela : DataGrid
{
    protected override Type StyleKeyOverride => typeof(DataGrid);

    public event Action? Duplo;
    public IReadOnlyList<Linha> Linhas { get; private set; } = Array.Empty<Linha>();

    static (string Fundo, string Texto) CorChip(string t) => t switch
    {
        "Concluída" or "Ativo" or "Ativa" or "Entrada (troco)" or "Em dia" => (Cores.VerdeFundo, "#166534"),
        "Cancelada" or "Inativo" or "Estornado" or "Retirada" => (Cores.VermelhoFundo, "#991B1B"),
        "Fiado" or "Pendente" => (Cores.AmbarFundo, Cores.Ambar),
        _ => (Cores.CinzaFundo, Cores.Cinza),
    };

    public Tabela(params Col[] colunas)
    {
        AutoGenerateColumns = false;
        IsReadOnly = true;
        SelectionMode = DataGridSelectionMode.Single;
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        HorizontalGridLinesBrush = Cores.P("#EEF0F5");
        CanUserSortColumns = false;
        CanUserReorderColumns = false;
        CanUserResizeColumns = true;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        RowHeight = 42;
        ColumnHeaderHeight = 38;
        BorderThickness = new Thickness(1);
        BorderBrush = Cores.P(Cores.Borda);
        CornerRadius = new CornerRadius(10);
        ClipToBounds = true;
        Background = Brushes.White;
        MinHeight = 120;
        FontSize = 13.5;

        for (int i = 0; i < colunas.Length; i++)
        {
            int idx = i; var c = colunas[i];
            Columns.Add(new DataGridTemplateColumn
            {
                Header = c.Titulo,
                Width = c.Expande ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(c.Largura),
                MinWidth = Math.Min(c.Largura, 60),
                CanUserSort = false,
                CellTemplate = new FuncDataTemplate<Linha>((l, _) =>
                {
                    var estilo = l?.Estilo ?? "";
                    var texto = l != null && idx < l.C.Length ? l.C[idx] : "";
                    if (c.Chip && texto.Length > 0)
                    {
                        var (fundo, fg) = CorChip(texto);
                        return new Border
                        {
                            Background = Cores.P(fundo), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3), Margin = new Thickness(8, 0),
                            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock { Text = texto, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(fg) },
                        };
                    }
                    return new TextBlock
                    {
                        Text = texto,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = c.Direita ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        Margin = new Thickness(12, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = estilo == "grupo" ? FontWeight.Bold : FontWeight.Normal,
                        Foreground = estilo == "cancelado" ? Cores.P("#9CA3AF") : Cores.P(Cores.Texto),
                    };
                }),
            });
        }

        LoadingRow += (_, e) =>
        {
            var l = e.Row.DataContext as Linha;
            e.Row.Background = l?.Estilo switch
            {
                "alerta" => Cores.P("#FEF2F2"),
                "grupo" => Cores.P("#EEF2FF"),
                _ => Brushes.White,
            };
        };
        DoubleTapped += (_, _) => { if (SelectedItem != null) Duplo?.Invoke(); };
        KeyDown += (_, e) => { if (e.Key == Key.Enter && SelectedItem != null) { Duplo?.Invoke(); e.Handled = true; } };
    }

    public string? IdSelecionado => (SelectedItem as Linha)?.Id;
    public long? IdLong => long.TryParse(IdSelecionado, out var n) ? n : null;
    public Linha? Selecionada => SelectedItem as Linha;

    /// <summary>Substitui o conteúdo mantendo (se possível) a linha selecionada.</summary>
    public void Definir(IEnumerable<Linha> linhas)
    {
        var sel = IdSelecionado;
        var lista = linhas.ToList();
        Linhas = lista;
        ItemsSource = lista;
        if (sel != null) SelectedItem = lista.FirstOrDefault(x => x.Id == sel);
    }

    public void Selecionar(string id) => SelectedItem = Linhas.FirstOrDefault(x => x.Id == id);
}

/// <summary>Atalhos para montar controles.</summary>
public static class K
{
    public static TextBlock Rotulo(string texto, bool negrito = false, double? tamanho = null, string? cor = null)
    {
        var t = new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center };
        if (negrito) t.FontWeight = FontWeight.SemiBold;
        if (tamanho != null) t.FontSize = tamanho.Value;
        if (cor != null) t.Foreground = Cores.P(cor);
        return t;
    }

    public static TextBlock Discreto(string texto) { var t = Rotulo(texto); t.Classes.Add("sub"); return t; }
    public static TextBlock H1(string texto) { var t = Rotulo(texto); t.Classes.Add("h1"); return t; }
    public static TextBlock H2(string texto) { var t = Rotulo(texto); t.Classes.Add("h2"); return t; }
    /// <summary>Pequeno título cinza de um campo ("CLIENTE", "DESCONTO"...).</summary>
    public static TextBlock Etiqueta(string texto) { var t = Rotulo(texto.ToUpperInvariant()); t.Classes.Add("rotulo"); return t; }

    /// <summary>Etiqueta pequena em cima do campo.</summary>
    public static StackPanel Campo(string rotulo, Control campo) => new() { Spacing = 5, Children = { Etiqueta(rotulo), campo } };

    /// <param name="icone">nome em <see cref="Icones"/>; "classe" = primario, perigo, verde, chip.</param>
    public static Button Botao(string texto, Action clique, bool primario = false, string? icone = null, string? classe = null)
    {
        var b = new Button { VerticalAlignment = VerticalAlignment.Center, Tag = texto };
        var cls = classe ?? (primario ? "primario" : null);
        if (cls != null) b.Classes.Add(cls);
        if (icone == null) b.Content = texto;
        else
        {
            var cor = cls is "primario" or "verde" ? "#FFFFFF" : cls == "perigo" ? "#B91C1C" : "#4B5563";
            b.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Icones.Criar(icone, 16, cor), new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center } } };
        }
        b.Click += (_, _) => clique();
        return b;
    }

    public static TextBox Caixa(string? texto = "", double largura = double.NaN, string? dica = null)
    {
        var t = new TextBox { Text = texto, VerticalContentAlignment = VerticalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (!double.IsNaN(largura)) t.Width = largura;
        if (dica != null) t.Watermark = dica;
        return t;
    }

    public static ComboBox Combo(IEnumerable<string> itens, double largura = 200, string? selecionado = null)
    {
        var c = new ComboBox { ItemsSource = itens.ToList(), VerticalAlignment = VerticalAlignment.Center };
        if (!double.IsNaN(largura)) c.Width = largura;
        if (selecionado != null) c.SelectedItem = selecionado;
        return c;
    }

    public static StackPanel Linha(params Control[] filhos) => Linha(8, filhos);

    public static StackPanel Linha(double espaco, params Control[] filhos)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = espaco, VerticalAlignment = VerticalAlignment.Center };
        foreach (var f in filhos) p.Children.Add(f);
        return p;
    }

    public static Border Cartao(Control conteudo)
    {
        var b = new Border { Child = conteudo };
        b.Classes.Add("cartao");
        return b;
    }

    public static Control Margem(Control c, double esq, double topo, double dir, double baixo)
    {
        c.Margin = new Thickness(esq, topo, dir, baixo);
        return c;
    }

    /// <summary>Painel com o conteúdo principal ocupando o espaço e barras opcionais em cima/baixo.</summary>
    public static DockPanel Doca(Control? topo, Control centro, Control? baixo = null)
    {
        var d = new DockPanel { LastChildFill = true };
        if (topo != null) { DockPanel.SetDock(topo, Dock.Top); d.Children.Add(topo); }
        if (baixo != null) { DockPanel.SetDock(baixo, Dock.Bottom); d.Children.Add(baixo); }
        d.Children.Add(centro);
        return d;
    }

    /// <summary>Barra com botões à esquerda e (opcionalmente) à direita.</summary>
    public static DockPanel Barra(Control esquerda, Control? direita = null, double margemBaixo = 14)
    {
        var d = new DockPanel { Margin = new Thickness(0, 0, 0, margemBaixo) };
        if (direita != null) { DockPanel.SetDock(direita, Dock.Right); d.Children.Add(direita); }
        d.Children.Add(esquerda);
        return d;
    }

    public static void ColocarNaGrade(Grid g, Control c, int linha, int coluna, int colSpan = 1)
    {
        Grid.SetRow(c, linha); Grid.SetColumn(c, coluna); if (colSpan > 1) Grid.SetColumnSpan(c, colSpan);
        g.Children.Add(c);
    }

    /// <summary>Observa mudanças do texto de uma caixa.</summary>
    public static void AoMudarTexto(TextBox t, Action acao) =>
        t.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) acao(); };

    public static void AoApertarEnter(Control c, Action acao) =>
        c.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; acao(); } };

    /// <summary>Número grande com título e detalhe (indicadores do painel, do caixa etc.).</summary>
    public sealed class Indicador : Border
    {
        readonly TextBlock valor = new() { FontSize = 24, FontWeight = FontWeight.Bold };
        readonly TextBlock detalhe = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        public Indicador(string titulo, string icone, string corIcone, string fundoIcone)
        {
            Classes.Add("cartao");
            Padding = new Thickness(16, 14);
            valor.Foreground = Cores.P(Cores.Texto); detalhe.Foreground = Cores.P(Cores.Texto2);
            var bolha = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(8), Background = Cores.P(fundoIcone), Child = Icones.Criar(icone, 16, corIcone) };
            bolha.Child!.HorizontalAlignment = HorizontalAlignment.Center;
            var cab = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { bolha, Etiqueta(titulo) } };
            Child = new StackPanel { Spacing = 6, Children = { cab, valor, detalhe } };
        }
        public string Valor => valor.Text ?? "";
        public string Detalhe => detalhe.Text ?? "";
        public void Definir(string v, string d = "") { valor.Text = v; detalhe.Text = d; }
    }
}

/// <summary>Base das telas: acesso ao serviço, título da página e execução segura de ações.</summary>
public abstract class Tela : UserControl
{
    protected Servico Svc { get; }
    protected MainWindow App { get; }

    protected Tela(MainWindow app) { App = app; Svc = app.Svc; }

    protected Window? Janela => TopLevel.GetTopLevel(this) as Window;

    public virtual string Titulo => "";
    public virtual string Subtitulo => "";
    /// <summary>Botões que aparecem no canto direito do cabeçalho da página.</summary>
    public virtual Control? AcoesCabecalho => null;

    /// <summary>Chamado sempre que a página é exibida.</summary>
    public virtual void Atualizar() { }

    /// <summary>Transforma uma ação em handler que nunca derruba o programa: erros viram mensagens.</summary>
    protected Action Seguro(Action acao) => () =>
    {
        try { acao(); }
        catch (Exception ex) { _ = Erros.Tratar(ex, Janela); }
    };

    protected Action SeguroAsync(Func<Task> acao) => async () =>
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, Janela); }
    };

    protected Task Info(string texto, string titulo = "Pronto") => Plat.Atual.Mensagem(Janela, titulo, texto, Nivel.Info);
    protected Task Aviso(string texto, string titulo = "Atenção") => Plat.Atual.Mensagem(Janela, titulo, texto, Nivel.Aviso);
    protected Task<bool> Confirmar(string texto, string titulo = "Confirmar") => Plat.Atual.Confirmar(Janela, titulo, texto);

    /// <summary>Atualiza a página atual (usado depois de operações que mudam estoque/vendas).</summary>
    protected void AtualizarTudo() => App.AtualizarTudo();

    protected static long Exigir(long? id, string oQue = "um item") =>
        id ?? throw new ValidacaoException($"Selecione {oQue} na lista primeiro.");
}
