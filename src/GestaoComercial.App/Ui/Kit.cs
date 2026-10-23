using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Paleta do programa (tons neutros "slate" + índigo como cor da marca).</summary>
public static class Cores
{
    static readonly Dictionary<string, IBrush> Cache = new();
    public static IBrush P(string hex) { if (!Cache.TryGetValue(hex, out var b)) Cache[hex] = b = new SolidColorBrush(Color.Parse(hex)); return b; }

    public const string Primaria = "#4F46E5", PrimariaEscura = "#4338CA", PrimariaSuave = "#EEF2FF", PrimariaBorda = "#C7D2FE";
    public const string Texto = "#0F172A", Texto2 = "#64748B", Texto3 = "#94A3B8", Corpo = "#1E293B", Borda = "#E7E9EF", Linha = "#EEF0F4", Fundo = "#F5F6F8";
    public const string Verde = "#059669", VerdeFundo = "#D1FAE5", Vermelho = "#DC2626", VermelhoFundo = "#FEE2E2";
    public const string Ambar = "#B45309", AmbarFundo = "#FEF3C7", Cinza = "#475569", CinzaFundo = "#F1F5F9", Azul = "#3730A3", AzulFundo = "#E0E7FF";
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
        ["menos"] = "M7 11v2h10v-2H7zm5-9C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z",
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
        ["cartao"] = "M20 4H4c-1.11 0-1.99.89-1.99 2L2 18c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V6c0-1.11-.89-2-2-2zm0 14H4v-6h16v6zm0-10H4V6h16v2z",
        ["dinheiro"] = "M11.8 10.9c-2.27-.59-3-1.2-3-2.15 0-1.09 1.01-1.85 2.7-1.85 1.78 0 2.44.85 2.5 2.1h2.21c-.07-1.72-1.12-3.3-3.21-3.81V3h-3v2.16c-1.94.42-3.5 1.68-3.5 3.61 0 2.31 1.91 3.46 4.7 4.13 2.5.6 3 1.48 3 2.41 0 .69-.49 1.79-2.7 1.79-2.06 0-2.87-.92-2.98-2.1h-2.2c.12 2.19 1.76 3.42 3.68 3.83V21h3v-2.15c1.95-.37 3.5-1.5 3.5-3.55 0-2.84-2.43-3.81-4.7-4.4z",
        ["pix"] = "M3 11h8V3H3v8zm2-6h4v4H5V5zM3 21h8v-8H3v8zm2-6h4v4H5v-4zM13 3v8h8V3h-8zm6 6h-4V5h4v4zM19 19h2v2h-2zM13 13h2v2h-2zM15 15h2v2h-2zM13 17h2v2h-2zM15 19h2v2h-2zM17 17h2v2h-2zM17 13h2v2h-2zM19 15h2v2h-2z",
        ["pessoa"] = "M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z",
        ["info"] = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z",
        ["fechar"] = "M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
        ["subindo"] = "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z",
        ["vazio"] = "M19 3H4.99c-1.11 0-1.98.89-1.98 2L3 19c0 1.1.88 2 1.99 2H19c1.1 0 2-.9 2-2V5c0-1.11-.9-2-2-2zm0 12h-4c0 1.66-1.35 3-3 3s-3-1.34-3-3H4.99V5H19v10z",
        ["nuvem"] = "M19.35 10.04C18.67 6.59 15.64 4 12 4 9.11 4 6.6 5.64 5.35 8.04 2.34 8.36 0 10.91 0 14c0 3.31 2.69 6 6 6h13c2.76 0 5-2.24 5-5 0-2.64-2.05-4.78-4.65-4.96zM14 13v4h-4v-4H7l5-5 5 5h-3z",
        ["ajustes"] = "M3 17v2h6v-2H3zM3 5v2h10V5H3zm10 16v-2h8v-2h-8v-2h-2v6h2zM7 9v2H3v2h4v2h2V9H7zm14 4v-2H11v2h10zm-6-4h2V7h4V5h-4V3h-2v6z",
        ["loja"] = "M20 4H4v2h16V4zm1 10v-2l-1-5H4l-1 5v2h1v6h10v-6h4v6h2v-6h1zm-9 4H6v-4h6v4z",
        ["raio"] = "M7 2v11h3v9l7-12h-4l4-8z",
        ["calendario"] = "M19 4h-1V2h-2v2H8V2H6v2H5c-1.11 0-1.99.9-1.99 2L3 20a2 2 0 0 0 2 2h14c1.1 0 2-.9 2-2V6c0-1.1-.9-2-2-2zm0 16H5V10h14v10zm0-12H5V6h14v2z",
        ["etiqueta"] = "M21.41 11.58l-9-9C12.05 2.22 11.55 2 11 2H4c-1.1 0-2 .9-2 2v7c0 .55.22 1.05.59 1.42l9 9c.36.36.86.58 1.41.58.55 0 1.05-.22 1.41-.59l7-7c.37-.36.59-.86.59-1.41 0-.55-.23-1.06-.59-1.42zM5.5 7C4.67 7 4 6.33 4 5.5S4.67 4 5.5 4 7 4.67 7 5.5 6.33 7 5.5 7z",
        ["mais-horiz"] = "M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z",
        ["nf"] = "M19.5 3.5 18 2l-1.5 1.5L15 2l-1.5 1.5L12 2l-1.5 1.5L9 2 7.5 3.5 6 2v14H3v3c0 1.66 1.34 3 3 3h12c1.66 0 3-1.34 3-3V2l-1.5 1.5zM19 19c0 .55-.45 1-1 1s-1-.45-1-1v-3H8V5h11v14zM9 7h6v2H9zm7 0h2v2h-2zm-7 3h6v2H9zm7 0h2v2h-2z",
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
    TextBlock? vazio;
    Control? painelVazio;

    /// <summary>Mensagem amigável mostrada no meio da tabela quando não há linhas.</summary>
    public string TextoVazio
    {
        get => vazio?.Text ?? "";
        set { if (vazio != null) vazio.Text = value; }
    }

    /// <summary>Devolve a tabela envolvida num painel que mostra <paramref name="texto"/> (com um ícone) quando a lista está vazia.</summary>
    public Control ComVazio(string texto, string icone = "vazio")
    {
        vazio = new TextBlock
        {
            Text = texto, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 420, Foreground = Cores.P(Cores.Texto2), FontSize = 14,
            LineHeight = 21, HorizontalAlignment = HorizontalAlignment.Center,
        };
        var bolha = new Border
        {
            Width = 56, Height = 56, CornerRadius = new CornerRadius(28), Background = Cores.P(Cores.CinzaFundo), HorizontalAlignment = HorizontalAlignment.Center,
            Child = Icones.Criar(icone, 26, Cores.Texto3),
        };
        bolha.Child!.HorizontalAlignment = HorizontalAlignment.Center;
        painelVazio = new StackPanel
        {
            Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 60, 24, 24),
            IsHitTestVisible = false, IsVisible = Linhas.Count == 0, Children = { bolha, vazio },
        };
        return new Grid { Children = { this, painelVazio } };
    }
    public bool VazioVisivel => painelVazio?.IsVisible == true;

    static (string Fundo, string Texto) CorChip(string t) => t switch
    {
        "Concluída" or "Ativo" or "Ativa" or "Entrada (troco)" or "Em dia" or "Autorizada" => (Cores.VerdeFundo, "#065F46"),
        "Cancelada" or "Inativo" or "Estornado" or "Retirada" or "Rejeitada" or "Denegada" => (Cores.VermelhoFundo, "#991B1B"),
        "Fiado" or "Pendente" or "Contingência" or "Substituída" => (Cores.AmbarFundo, "#92400E"),
        _ => (Cores.CinzaFundo, Cores.Cinza),
    };

    public Tabela(params Col[] colunas)
    {
        AutoGenerateColumns = false;
        IsReadOnly = true;
        SelectionMode = DataGridSelectionMode.Single;
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        HorizontalGridLinesBrush = Cores.P(Cores.Linha);
        CanUserSortColumns = false;
        CanUserReorderColumns = false;
        CanUserResizeColumns = true;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        RowHeight = 46;
        ColumnHeaderHeight = 40;
        BorderThickness = new Thickness(1);
        BorderBrush = Cores.P(Cores.Borda);
        CornerRadius = new CornerRadius(12);
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
                            Background = Cores.P(fundo), CornerRadius = new CornerRadius(20), Padding = new Thickness(10, 3), Margin = new Thickness(12, 0),
                            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock { Text = texto, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(fg) },
                        };
                    }
                    return new TextBlock
                    {
                        Text = texto,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = c.Direita ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                        Margin = new Thickness(16, 0),
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontWeight = estilo == "grupo" ? FontWeight.SemiBold : c.Direita ? FontWeight.Medium : FontWeight.Normal,
                        Foreground = estilo == "cancelado" ? Cores.P(Cores.Texto3) : estilo == "alerta" && idx == 0 ? Cores.P("#B91C1C") : Cores.P(Cores.Corpo),
                    };
                }),
            });
        }

        LoadingRow += (_, e) =>
        {
            var l = e.Row.DataContext as Linha;
            e.Row.Background = l?.Estilo switch
            {
                "alerta" => Cores.P("#FFF5F5"),
                "grupo" => Cores.P("#F8FAFC"),
                _ => Brushes.Transparent,
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
        if (painelVazio != null) painelVazio.IsVisible = lista.Count == 0;
        if (sel != null) SelectedItem = lista.FirstOrDefault(x => x.Id == sel);
    }

    public void Selecionar(string id) => SelectedItem = Linhas.FirstOrDefault(x => x.Id == id);
}

/// <summary>Gráfico de colunas simples (vendas por dia), desenhado com controles comuns.</summary>
public sealed class GraficoBarras : UserControl
{
    public sealed record Ponto(string Rotulo, string Rotulo2, long Valor, string Dica, bool Destaque = false);

    readonly Grid area = new();
    public IReadOnlyList<Ponto> Pontos { get; private set; } = Array.Empty<Ponto>();

    readonly List<TextBlock> valores = new();

    public GraficoBarras()
    {
        Content = area; MinHeight = 160;
        area.Margin = new Thickness(0, 10, 0, 0);
        SizeChanged += (_, _) => AjustarEtiquetas();
    }

    /// <summary>Com colunas estreitas, só a coluna em destaque mostra o valor (o resto fica no balão ao passar o mouse).</summary>
    void AjustarEtiquetas()
    {
        if (Pontos.Count == 0) return;
        var larguraColuna = (Bounds.Width - 60) / Pontos.Count;
        foreach (var v in valores) v.IsVisible = larguraColuna >= 52 || v.Tag is true;
    }

    /// <summary>Valor curto para o eixo e as etiquetas: "R$ 850", "R$ 1,2 mil".</summary>
    public static string Curto(long centavos)
    {
        var reais = centavos / 100m;
        if (reais >= 1000) return $"R$ {(reais / 1000m).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} mil";
        return $"R$ {Math.Round(reais):0}";
    }

    public void Definir(IReadOnlyList<Ponto> pontos)
    {
        Pontos = pontos;
        valores.Clear();
        area.Children.Clear();
        area.ColumnDefinitions.Clear(); area.RowDefinitions.Clear();
        area.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        area.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        area.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        area.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (pontos.Count == 0) return;

        var max = Math.Max(1, pontos.Max(p => p.Valor));
        // escala "redonda" para o eixo
        var passo = new[] { 1000L, 2000, 5000, 10000, 20000, 25000, 50000, 100000, 200000, 250000, 500000, 1000000, 2000000, 5000000, 10000000 }.FirstOrDefault(x => x * 4 >= max, max / 4 + 1);
        var topo = passo * 4;

        // eixo com 5 marcas (0, 1/4, 2/4, 3/4, topo) e linhas de grade
        var eixo = new Grid { Margin = new Thickness(0, 0, 10, 0), RowDefinitions = new RowDefinitions("*,*,*,*") };
        var grade = new Grid { RowDefinitions = new RowDefinitions("*,*,*,*") };
        for (int i = 0; i < 4; i++)
        {
            var t = new TextBlock { Text = Curto(topo - passo * i), FontSize = 11, Foreground = Cores.P(Cores.Texto3), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, -7, 0, 0) };
            Grid.SetRow(t, i); eixo.Children.Add(t);
            var linha = new Border { BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0) };
            Grid.SetRow(linha, i); grade.Children.Add(linha);
        }
        var zero = new TextBlock { Text = "R$ 0", FontSize = 11, Foreground = Cores.P(Cores.Texto3), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, -7) };
        Grid.SetRow(zero, 3); eixo.Children.Add(zero);
        var baseLinha = new Border { BorderBrush = Cores.P(Cores.Borda), BorderThickness = new Thickness(0, 0, 0, 1) };
        Grid.SetRow(baseLinha, 3); grade.Children.Add(baseLinha);
        Grid.SetColumn(eixo, 0); Grid.SetRow(eixo, 0); area.Children.Add(eixo);

        var barras = new Grid();
        var rotulos = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        for (int i = 0; i < pontos.Count; i++)
        {
            barras.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rotulos.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var p = pontos[i];
            var f = Math.Clamp((double)p.Valor / topo, 0, 1);
            var coluna = new Grid { RowDefinitions = new RowDefinitions($"{1 - f}*,Auto,{f}*"), Margin = new Thickness(4, 0) };
            var barra = new Border
            {
                CornerRadius = new CornerRadius(6, 6, 2, 2), MaxWidth = 44, MinHeight = p.Valor > 0 ? 3 : 0,
                Background = Cores.P(p.Destaque ? Cores.Primaria : "#C7D2FE"),
            };
            ToolTip.SetTip(barra, p.Dica);
            Grid.SetRow(barra, 2); coluna.Children.Add(barra);
            if (p.Valor > 0)
            {
                var v = new TextBlock { Text = Curto(p.Valor), FontSize = 10.5, FontWeight = p.Destaque ? FontWeight.SemiBold : FontWeight.Normal, Foreground = Cores.P(p.Destaque ? Cores.Primaria : Cores.Texto2), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
                v.Tag = p.Destaque; valores.Add(v);
                Grid.SetRow(v, 1); coluna.Children.Add(v);
            }
            Grid.SetColumn(coluna, i); barras.Children.Add(coluna);
            var r = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = p.Rotulo, FontSize = 11, FontWeight = p.Destaque ? FontWeight.SemiBold : FontWeight.Normal, Foreground = Cores.P(p.Destaque ? Cores.Texto : Cores.Texto2), HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = p.Rotulo2, FontSize = 10.5, Foreground = Cores.P(Cores.Texto3), HorizontalAlignment = HorizontalAlignment.Center },
                },
            };
            Grid.SetColumn(r, i); rotulos.Children.Add(r);
        }
        var plot = new Grid { Children = { grade, barras } };
        Grid.SetColumn(plot, 1); Grid.SetRow(plot, 0); area.Children.Add(plot);
        Grid.SetColumn(rotulos, 1); Grid.SetRow(rotulos, 1); area.Children.Add(rotulos);
        AjustarEtiquetas();
    }
}

/// <summary>
/// Duas partes lado a lado (esquerda e direita). Quando a janela é estreita, a parte da direita desce para
/// uma segunda linha (e quebra, se for um WrapPanel) em vez de ficar por cima da outra.
/// </summary>
public sealed class BarraFerramentas : Panel
{
    readonly Control? esquerda, direita;
    const double Espaco = 16, EspacoLinhas = 12;
    bool empilhado;
    public bool Empilhado => empilhado;

    public BarraFerramentas(Control? esquerda, Control? direita)
    {
        this.esquerda = esquerda; this.direita = direita;
        if (esquerda != null) { esquerda.VerticalAlignment = VerticalAlignment.Center; Children.Add(esquerda); }
        if (direita != null) { direita.VerticalAlignment = VerticalAlignment.Center; Children.Add(direita); }
    }

    protected override Size MeasureOverride(Size disponivel)
    {
        var inf = new Size(double.PositiveInfinity, disponivel.Height);
        esquerda?.Measure(inf); direita?.Measure(inf);
        var we = esquerda?.DesiredSize.Width ?? 0; var wd = direita?.DesiredSize.Width ?? 0;
        var he = esquerda?.DesiredSize.Height ?? 0; var hd = direita?.DesiredSize.Height ?? 0;
        empilhado = esquerda != null && direita != null && we + Espaco + wd > disponivel.Width;
        if (!empilhado) return new Size(Math.Min(disponivel.Width, we + wd + (esquerda != null && direita != null ? Espaco : 0)), Math.Max(he, hd));
        esquerda!.Measure(new Size(disponivel.Width, double.PositiveInfinity));
        direita!.Measure(new Size(disponivel.Width, double.PositiveInfinity));
        return new Size(disponivel.Width, esquerda.DesiredSize.Height + EspacoLinhas + direita.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (!empilhado)
        {
            var h = final.Height;
            var wd = direita?.DesiredSize.Width ?? 0;
            esquerda?.Arrange(new Rect(0, 0, Math.Max(0, final.Width - wd - (direita != null ? Espaco : 0)), h));
            direita?.Arrange(new Rect(Math.Max(0, final.Width - wd), 0, wd, h));
        }
        else
        {
            var he = esquerda!.DesiredSize.Height;
            esquerda.Arrange(new Rect(0, 0, final.Width, he));
            direita!.Arrange(new Rect(0, he + EspacoLinhas, final.Width, direita.DesiredSize.Height));
        }
        return final;
    }
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

    /// <summary>Atraso (ms) antes de buscar enquanto a pessoa digita. 0 = imediato (usado nos testes).</summary>
    public static int DebounceMs = 180;

    /// <summary>Evita refazer a consulta a cada tecla: só executa quando a pessoa para de digitar.</summary>
    public static Action Debounce(Action acao, int? ms = null)
    {
        CancellationTokenSource? cts = null;
        return () =>
        {
            var espera = DebounceMs <= 0 ? 0 : ms ?? DebounceMs;
            if (espera <= 0) { acao(); return; }
            cts?.Cancel();
            var meu = cts = new CancellationTokenSource();
            Task.Delay(espera, meu.Token).ContinueWith(t => { if (!t.IsCanceled) Avalonia.Threading.Dispatcher.UIThread.Post(acao); }, TaskScheduler.Default);
        };
    }

    public static TextBlock Discreto(string texto) { var t = Rotulo(texto); t.Classes.Add("sub"); return t; }
    public static TextBlock H1(string texto) { var t = Rotulo(texto); t.Classes.Add("h1"); return t; }
    public static TextBlock H2(string texto) { var t = Rotulo(texto); t.Classes.Add("h2"); return t; }
    /// <summary>Pequeno título de um campo ("Cliente", "Desconto"...).</summary>
    public static TextBlock Etiqueta(string texto) { var t = Rotulo(texto); t.Classes.Add("rotulo"); return t; }

    /// <summary>Etiqueta pequena em cima do campo.</summary>
    public static StackPanel Campo(string rotulo, Control campo) => new() { Spacing = 6, Children = { Etiqueta(rotulo), campo } };

    /// <param name="icone">nome em <see cref="Icones"/>; "classe" = primario, perigo, verde, chip, fantasma.</param>
    public static Button Botao(string texto, Action clique, bool primario = false, string? icone = null, string? classe = null)
    {
        var b = new Button { VerticalAlignment = VerticalAlignment.Center, Tag = texto };
        var cls = classe ?? (primario ? "primario" : null);
        var classes = (cls ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var c in classes) b.Classes.Add(c);
        if (icone == null) b.Content = texto;
        else
        {
            var cor = classes.Contains("primario") || classes.Contains("verde") ? "#FFFFFF" : classes.Contains("perigo") ? Cores.Vermelho : classes.Contains("fantasma") ? Cores.Texto2 : Cores.Cinza;
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

    /// <summary>Caixa de busca com lupa à esquerda.</summary>
    public static TextBox CaixaBusca(string dica, double largura = double.NaN)
    {
        var t = Caixa("", largura, dica);
        t.InnerLeftContent = Margem(Icones.Criar("busca", 18, Cores.Texto3), 12, 0, 0, 0);
        return t;
    }

    /// <summary>Campo de data (dd/mm/aaaa) com ícone de calendário.</summary>
    public static TextBox Data(string? texto, double largura = 136)
    {
        var t = Caixa(texto, largura, "dd/mm/aaaa");
        t.InnerLeftContent = Margem(Icones.Criar("calendario", 16, Cores.Texto3), 11, 0, 0, 0);
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

    /// <summary>Linha de controles que quebra para baixo quando falta espaço.</summary>
    public static WrapPanel Fila(double espaco, params Control[] filhos)
    {
        var w = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var f in filhos) { f.Margin = new Thickness(0, 0, espaco, espaco); w.Children.Add(f); }
        w.Margin = new Thickness(0, 0, 0, -espaco);
        return w;
    }

    public static Border Cartao(Control conteudo)
    {
        var b = new Border { Child = conteudo };
        b.Classes.Add("cartao");
        return b;
    }

    /// <summary>Cartão com título (e, opcionalmente, subtítulo, ícone e botões à direita).</summary>
    /// <param name="textoVivo">subtítulo que a tela atualiza depois (no lugar de <paramref name="subtitulo"/>).</param>
    public static Border Secao(string titulo, Control conteudo, Control? acoes = null, string? subtitulo = null, string? icone = null, bool preencher = false, TextBlock? textoVivo = null)
    {
        var textos = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { H2(titulo) } };
        if (textoVivo != null) textos.Children.Add(textoVivo);
        else if (subtitulo != null) { var sub = Discreto(subtitulo); sub.TextWrapping = TextWrapping.Wrap; textos.Children.Add(sub); }
        var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        if (acoes != null) { DockPanel.SetDock(acoes, Dock.Right); acoes.VerticalAlignment = VerticalAlignment.Center; cab.Children.Add(acoes); }
        if (icone != null)
        {
            var bolha = Bolha(icone, Cores.Primaria, Cores.PrimariaSuave, 36);
            bolha.Margin = new Thickness(0, 0, 12, 0);
            DockPanel.SetDock(bolha, Dock.Left); cab.Children.Add(bolha);
        }
        cab.Children.Add(textos);
        var corpo = preencher ? (Control)Doca(cab, conteudo) : new StackPanel { Children = { cab, conteudo } };
        var c = Cartao(corpo);
        c.Padding = new Thickness(20, 18);
        return c;
    }

    /// <summary>
    /// Lista "profissional": um cartão com a barra de ferramentas em cima, a tabela no meio (sem borda própria)
    /// e um rodapé discreto embaixo.
    /// </summary>
    public static Border Lista(Tabela tabela, Control? barra = null, Control? rodape = null, string? vazio = null, string iconeVazio = "vazio")
    {
        tabela.BorderThickness = new Thickness(0);
        tabela.CornerRadius = new CornerRadius(0);
        var d = new DockPanel();
        if (barra != null)
        {
            var b = new Border { Child = barra, Padding = new Thickness(16, 14), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 0, 0, 1) };
            DockPanel.SetDock(b, Dock.Top); d.Children.Add(b);
        }
        if (rodape != null)
        {
            var r = new Border { Child = rodape, Padding = new Thickness(16, 11), Background = Cores.P("#FAFBFC"), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0) };
            DockPanel.SetDock(r, Dock.Bottom); d.Children.Add(r);
        }
        d.Children.Add(vazio != null ? tabela.ComVazio(vazio, iconeVazio) : tabela);
        var c = Cartao(d);
        c.Padding = new Thickness(0);
        c.ClipToBounds = true;
        return c;
    }

    /// <summary>Barra de ferramentas: itens à esquerda (busca, filtros) e à direita (ações); se não couber numa linha, as ações descem.</summary>
    public static Control Ferramentas(Control? esquerda, Control? direita) => new BarraFerramentas(esquerda, direita);

    /// <summary>Ícone dentro de um quadradinho colorido.</summary>
    public static Border Bolha(string icone, string cor, string fundo, double tamanho = 36)
    {
        var b = new Border { Width = tamanho, Height = tamanho, CornerRadius = new CornerRadius(tamanho * 0.3), Background = Cores.P(fundo), Child = Icones.Criar(icone, tamanho * 0.5, cor) };
        b.Child!.HorizontalAlignment = HorizontalAlignment.Center;
        return b;
    }

    /// <summary>Tecla de atalho desenhada ("F9").</summary>
    public static Border Tecla(string texto, bool clara = false) => new()
    {
        Padding = new Thickness(6, 1), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center,
        Background = Cores.P(clara ? "#33FFFFFF" : "#FFFFFF"), BorderBrush = Cores.P(clara ? "#55FFFFFF" : Cores.Borda), BorderThickness = new Thickness(1, 1, 1, 2),
        Child = new TextBlock { Text = texto, FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(clara ? "#FFFFFF" : Cores.Texto2) },
    };

    /// <summary>Etiqueta arredondada (contadores, situações).</summary>
    public static Border Selo(string texto, string fundo, string cor) => new()
    {
        Padding = new Thickness(9, 2), CornerRadius = new CornerRadius(20), Background = Cores.P(fundo), VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = texto, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(cor) },
    };

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

    /// <summary>
    /// Ocupa a tela inteira quando há espaço; em telas baixas (notebook), mantém uma altura mínima e deixa rolar,
    /// em vez de espremer as listas.
    /// </summary>
    public static ScrollViewer Rolagem(Control conteudo, double alturaMinima)
    {
        var sv = new ScrollViewer { Content = conteudo, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        sv.SizeChanged += (_, e) => conteudo.Height = Math.Max(e.NewSize.Height, alturaMinima);
        return sv;
    }

    /// <summary>Grade de colunas iguais (cartões de indicadores etc.) com espaço entre elas.</summary>
    public static Grid Colunas(double espaco, params Control[] itens)
    {
        var g = new Grid();
        for (int i = 0; i < itens.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            itens[i].Margin = new Thickness(i == 0 ? 0 : espaco / 2, 0, i == itens.Length - 1 ? 0 : espaco / 2, 0);
            Grid.SetColumn(itens[i], i); g.Children.Add(itens[i]);
        }
        return g;
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

    /// <summary>Número grande com título, ícone e detalhe (indicadores do painel, do caixa etc.).</summary>
    public sealed class Indicador : Border
    {
        readonly TextBlock valor = new() { FontSize = 26, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        readonly TextBlock detalhe = new() { FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis };
        public Indicador(string titulo, string icone, string corIcone, string fundoIcone)
        {
            Classes.Add("cartao");
            Padding = new Thickness(20, 18);
            valor.Foreground = Cores.P(Cores.Texto); detalhe.Foreground = Cores.P(Cores.Texto2);
            var bolha = Bolha(icone, corIcone, fundoIcone, 40);
            // cartão estreito (muitos cartões numa tela pequena): número menor e sem o ícone, para nada ficar cortado
            SizeChanged += (_, e) =>
            {
                var estreito = e.NewSize.Width < 200;
                bolha.IsVisible = !estreito;
                valor.FontSize = e.NewSize.Width < 170 ? 19 : e.NewSize.Width < 230 ? 22 : 26;
                Padding = estreito ? new Thickness(16, 16) : new Thickness(20, 18);
            };
            var rot = new TextBlock { Text = titulo, FontSize = 13.5, FontWeight = FontWeight.Medium, Foreground = Cores.P(Cores.Texto2), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var cab = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(bolha, Dock.Right); cab.Children.Add(bolha); cab.Children.Add(rot);
            Child = new StackPanel { Spacing = 4, Children = { cab, valor, detalhe } };
        }
        public string Valor => valor.Text ?? "";
        public string Detalhe => detalhe.Text ?? "";
        public void Definir(string v, string d = "", string? corDetalhe = null)
        {
            valor.Text = v; detalhe.Text = d;
            detalhe.Foreground = Cores.P(corDetalhe ?? Cores.Texto2);
        }
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

    /// <summary>Aviso de sucesso que some sozinho (com botões opcionais).</summary>
    protected void Sucesso(string texto, params (string Rotulo, Action Acao)[] acoes) =>
        Plat.Atual.Toast(Janela, texto, Nivel.Info, acoes.Length > 0 ? acoes : null, 6);

    /// <summary>Aviso leve (sem janela) para situações simples, como "adicione um produto".</summary>
    protected void Dica(string texto) => Plat.Atual.Toast(Janela, texto, Nivel.Aviso);

    /// <summary>Atualiza a página atual (usado depois de operações que mudam estoque/vendas).</summary>
    protected void AtualizarTudo() => App.AtualizarTudo();

    protected static long Exigir(long? id, string oQue = "um item") =>
        id ?? throw new ValidacaoException($"Selecione {oQue} na lista primeiro.");
}
