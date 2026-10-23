using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

public enum TipoCampo { Texto, Dinheiro, Inteiro, Escolha, Multilinha, Bool }

public sealed class Campo
{
    public required string Chave { get; init; }
    public required string Rotulo { get; init; }
    public TipoCampo Tipo { get; init; } = TipoCampo.Texto;
    public bool Obrigatorio { get; init; }
    public bool SomenteLeitura { get; init; }
    public string Dica { get; init; } = "";
    public List<(long? Valor, string Rotulo)> Opcoes { get; init; } = new();
}

/// <summary>Valores digitados já convertidos (texto, centavos, inteiros, escolha, bool).</summary>
public sealed class Valores
{
    readonly Dictionary<string, object?> d = new();
    public object? this[string k] { get => d.GetValueOrDefault(k); set => d[k] = value; }
    public bool Tem(string k) => d.ContainsKey(k);
    public string Texto(string k) => d.GetValueOrDefault(k) as string ?? "";
    public long Num(string k) => d.GetValueOrDefault(k) is long l ? l : 0;
    public long? Escolha(string k) => d.GetValueOrDefault(k) as long?;
    public bool Bool(string k) => d.GetValueOrDefault(k) is true;
}

/// <summary>Formulário modal genérico. O callback pode lançar erros: a janela continua aberta mostrando a mensagem.</summary>
public sealed class FormDialog : JanelaBase
{
    readonly List<Campo> campos;
    readonly Action<Valores> aoSalvar;
    readonly Dictionary<string, Control> controles = new();
    bool salvo;

    public static async Task<bool> Abrir(Window? dono, string titulo, List<Campo> campos, Dictionary<string, object?>? iniciais,
                                         Action<Valores> aoSalvar, string textoBotao = "Salvar", Action<FormDialog, string>? aoMudarCampo = null)
    {
        var f = new FormDialog(titulo, campos, iniciais ?? new(), aoSalvar, textoBotao) { aoMudarCampo = aoMudarCampo };
        if (dono != null) await f.ShowDialog(dono); else { f.Show(); await Task.Delay(Timeout.Infinite); }
        return f.salvo;
    }

    /// <summary>Para testes: cria o formulário sem exibi-lo de forma bloqueante.</summary>
    public static FormDialog Criar(string titulo, List<Campo> campos, Dictionary<string, object?>? iniciais, Action<Valores> aoSalvar, string textoBotao = "Salvar")
        => new(titulo, campos, iniciais ?? new(), aoSalvar, textoBotao);

    public bool Salvo => salvo;
    Action<FormDialog, string>? aoMudarCampo;
    readonly TextBlock lblErro = new() { Foreground = Cores.P("#B91C1C"), TextWrapping = Avalonia.Media.TextWrapping.Wrap, FontSize = 13, IsVisible = false };
    /// <summary>Último erro de preenchimento mostrado dentro da janela (vazio se não há).</summary>
    public string Erro => lblErro.IsVisible ? lblErro.Text ?? "" : "";

    /// <summary>Chaves dos campos mostrados, na ordem.</summary>
    public IReadOnlyList<string> Chaves => campos.Select(c => c.Chave).ToList();

    /// <summary>Valor escolhido em um campo de lista (null se nada escolhido ou se o campo não é uma lista).</summary>
    public object? Valor(string chave) =>
        controles[chave] is ComboBox co && co.SelectedIndex >= 0 ? campos.First(x => x.Chave == chave).Opcoes[co.SelectedIndex].Valor : null;

    /// <summary>Texto atual de um campo digitável.</summary>
    public string Texto(string chave) => controles[chave] is TextBox t ? t.Text ?? "" : "";

    void MostrarErro(string texto) { lblErro.Text = texto; lblErro.IsVisible = true; }

    FormDialog(string titulo, List<Campo> campos, Dictionary<string, object?> iniciais, Action<Valores> aoSalvar, string textoBotao)
    {
        this.campos = campos; this.aoSalvar = aoSalvar;
        Title = titulo;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Background = Avalonia.Media.Brushes.White;
        // muitos campos: duas colunas (o formulário não fica comprido demais e nada some lá embaixo)
        var duasColunas = campos.Count(c => c.Tipo is not (TipoCampo.Bool or TipoCampo.Multilinha)) > 5;
        var grade = new Grid { Width = duasColunas ? 620 : 420, ColumnDefinitions = duasColunas ? new ColumnDefinitions("*,20,*") : new ColumnDefinitions("*") };
        var corpo = new StackPanel { Spacing = 14, Margin = new Thickness(28, 6, 28, 8) };
        int linhaGrade = 0, colunaGrade = 0;
        void Colocar(Control bloco, bool inteiro)
        {
            if (inteiro && colunaGrade != 0) { linhaGrade++; colunaGrade = 0; }
            while (grade.RowDefinitions.Count <= linhaGrade) grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            bloco.Margin = new Thickness(0, 0, 0, 16);
            K.ColocarNaGrade(grade, bloco, linhaGrade, colunaGrade, inteiro && duasColunas ? 3 : 1);
            if (!duasColunas || inteiro || colunaGrade == 2) { linhaGrade++; colunaGrade = 0; } else colunaGrade = 2;
        }
        Control? primeiro = null;
        // rótulo em cima do campo, dica embaixo (quebra linha em vez de ser cortada)
        foreach (var c in campos)
        {
            iniciais.TryGetValue(c.Chave, out var v);
            Control w;
            switch (c.Tipo)
            {
                case TipoCampo.Bool:
                    w = new CheckBox { IsChecked = v is null || v is true, IsEnabled = !c.SomenteLeitura, Content = c.Rotulo };
                    break;
                case TipoCampo.Escolha:
                    var combo = new ComboBox { ItemsSource = c.Opcoes.Select(o => o.Rotulo).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch };
                    var atual = c.Opcoes.FindIndex(o => o.Valor == (v as long?));
                    combo.SelectedIndex = atual >= 0 ? atual : (c.Opcoes.Count > 0 ? 0 : -1);
                    w = combo;
                    break;
                case TipoCampo.Multilinha:
                    w = new TextBox { Text = v as string ?? "", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 80, VerticalContentAlignment = VerticalAlignment.Top };
                    break;
                default:
                    var txt = c.Tipo == TipoCampo.Dinheiro
                        ? (v is long cent ? Util.FmtDinheiro(cent, false) : "")
                        : v switch { null => "", long n => n.ToString(), _ => v.ToString() ?? "" };
                    var tb = new TextBox { Text = txt, IsReadOnly = c.SomenteLeitura, VerticalContentAlignment = VerticalAlignment.Center };
                    if (c.Tipo == TipoCampo.Dinheiro) tb.InnerLeftContent = new TextBlock { Text = "R$", Foreground = Cores.P(Cores.Texto3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                    if (c.SomenteLeitura) { tb.Focusable = false; tb.Background = Cores.P("#F8FAFC"); tb.Foreground = Cores.P(Cores.Texto2); }
                    w = tb;
                    break;
            }
            controles[c.Chave] = w;
            if (primeiro == null && !c.SomenteLeitura && c.Tipo != TipoCampo.Bool) primeiro = w;
            var bloco = new StackPanel { Spacing = 6 };
            if (c.Tipo != TipoCampo.Bool) bloco.Children.Add(K.Etiqueta(c.Rotulo + (c.Obrigatorio ? " *" : "")));
            bloco.Children.Add(w);
            if (c.Dica.Length > 0)
            {
                var dica = K.Discreto(c.Dica); dica.FontSize = 12; dica.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
                bloco.Children.Add(dica);
            }
            Colocar(bloco, c.Tipo is TipoCampo.Bool or TipoCampo.Multilinha);
        }
        var caixaErro = new Border
        {
            Background = Cores.P("#FEF2F2"), BorderBrush = Cores.P("#FECACA"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 9),
            Child = lblErro,
        };
        lblErro.Margin = new Thickness(0);
        caixaErro.Bind(IsVisibleProperty, lblErro.GetObservable(IsVisibleProperty));
        corpo.Children.Add(caixaErro);   // em cima, logo abaixo do título: o erro sempre fica à vista
        corpo.Children.Add(grade);

        var salvar = new Button { Content = textoBotao, MinWidth = 110 }; salvar.Classes.Add("primario");
        salvar.Click += (_, _) => Salvar();
        var cancelar = new Button { Content = "Cancelar", MinWidth = 96 };
        cancelar.Click += (_, _) => Close();
        var barra = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        barra.Children.Add(cancelar); barra.Children.Add(salvar);
        var rodape = new Border { Child = barra, Padding = new Thickness(28, 14), Background = Cores.P("#F8FAFC"), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(0, 1, 0, 0) };
        var cabecalho = new TextBlock { Text = titulo, FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto), Margin = new Thickness(28, 22, 28, 10) };
        var doca = new DockPanel();
        DockPanel.SetDock(cabecalho, Dock.Top); DockPanel.SetDock(rodape, Dock.Bottom);
        doca.Children.Add(cabecalho); doca.Children.Add(rodape);
        doca.Children.Add(new ScrollViewer { Content = corpo, MaxHeight = 620, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        // ao digitar: some o erro anterior e avisa quem quiser reagir (ex.: calcular preço pela margem)
        foreach (var (chave, ctl) in controles)
            if (ctl is TextBox tb)
            {
                var k = chave;
                tb.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { lblErro.IsVisible = false; aoMudarCampo?.Invoke(this, k); } };
            }
        Content = doca;

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.Enter && (e.Source as TextBox)?.AcceptsReturn != true) { e.Handled = true; Salvar(); }
        };
        Opened += (_, _) => primeiro?.Focus();
    }

    // --- acesso programático (usado também pelos testes automáticos)
    public void Definir(string chave, object? valor)
    {
        var c = campos.First(x => x.Chave == chave);
        switch (controles[chave])
        {
            case CheckBox cb: cb.IsChecked = valor is true; break;
            case ComboBox co: co.SelectedIndex = c.Opcoes.FindIndex(o => valor is long l ? o.Valor == l : o.Rotulo == valor as string); break;
            case TextBox tb: tb.Text = valor?.ToString() ?? ""; break;
        }
    }

    public Valores Coletar()
    {
        var saida = new Valores();
        foreach (var c in campos)
        {
            var w = controles[c.Chave];
            switch (w)
            {
                case CheckBox cb: saida[c.Chave] = cb.IsChecked == true; continue;
                case ComboBox co: saida[c.Chave] = co.SelectedIndex >= 0 ? c.Opcoes[co.SelectedIndex].Valor : null; continue;
            }
            var texto = ((TextBox)w).Text?.Trim() ?? "";
            if (texto.Length == 0)
            {
                if (c.Obrigatorio) throw new ValidacaoException($"Preencha o campo '{c.Rotulo}'.");
                saida[c.Chave] = c.Tipo is TipoCampo.Dinheiro or TipoCampo.Inteiro ? 0L : "";
                continue;
            }
            saida[c.Chave] = c.Tipo switch
            {
                TipoCampo.Dinheiro => Util.ParseDinheiro(texto, c.Rotulo),
                TipoCampo.Inteiro => Util.ParseInteiro(texto, c.Rotulo),
                _ => texto,
            };
        }
        return saida;
    }

    public bool Salvar()
    {
        try
        {
            aoSalvar(Coletar());
        }
        catch (AppException ex)
        {
            MostrarErro(ex.Message);   // erro de preenchimento: aparece aqui mesmo, sem abrir outra caixa
            return false;
        }
        catch (Exception ex)
        {
            _ = Erros.Tratar(ex, this);
            return false;
        }
        salvo = true;
        Close();
        return true;
    }
}
