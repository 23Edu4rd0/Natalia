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
                                         Action<Valores> aoSalvar, string textoBotao = "Salvar")
    {
        var f = new FormDialog(titulo, campos, iniciais ?? new(), aoSalvar, textoBotao);
        if (dono != null) await f.ShowDialog(dono); else { f.Show(); await Task.Delay(Timeout.Infinite); }
        return f.salvo;
    }

    /// <summary>Para testes: cria o formulário sem exibi-lo de forma bloqueante.</summary>
    public static FormDialog Criar(string titulo, List<Campo> campos, Dictionary<string, object?>? iniciais, Action<Valores> aoSalvar, string textoBotao = "Salvar")
        => new(titulo, campos, iniciais ?? new(), aoSalvar, textoBotao);

    public bool Salvo => salvo;

    FormDialog(string titulo, List<Campo> campos, Dictionary<string, object?> iniciais, Action<Valores> aoSalvar, string textoBotao)
    {
        this.campos = campos; this.aoSalvar = aoSalvar;
        Title = titulo;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grade = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,360"), Margin = new Thickness(22, 18, 22, 8) };
        int linha = 0; Control? primeiro = null;
        foreach (var c in campos)
        {
            grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var rot = K.Rotulo(c.Rotulo + (c.Obrigatorio ? " *" : ""));
            rot.VerticalAlignment = c.Tipo == TipoCampo.Multilinha ? VerticalAlignment.Top : VerticalAlignment.Center;
            rot.Margin = new Thickness(0, c.Tipo == TipoCampo.Multilinha ? 8 : 0, 14, 0);
            K.ColocarNaGrade(grade, rot, linha, 0);

            iniciais.TryGetValue(c.Chave, out var v);
            Control w;
            switch (c.Tipo)
            {
                case TipoCampo.Bool:
                    w = new CheckBox { IsChecked = v is null || v is true, IsEnabled = !c.SomenteLeitura };
                    break;
                case TipoCampo.Escolha:
                    var combo = new ComboBox { ItemsSource = c.Opcoes.Select(o => o.Rotulo).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch };
                    var atual = c.Opcoes.FindIndex(o => o.Valor == (v as long?));
                    combo.SelectedIndex = atual >= 0 ? atual : (c.Opcoes.Count > 0 ? 0 : -1);
                    w = combo;
                    break;
                case TipoCampo.Multilinha:
                    w = new TextBox { Text = v as string ?? "", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Height = 72 };
                    break;
                default:
                    var txt = c.Tipo == TipoCampo.Dinheiro
                        ? (v is long cent ? Util.FmtDinheiro(cent, false) : "")
                        : v switch { null => "", long n => n.ToString(), _ => v.ToString() ?? "" };
                    w = new TextBox { Text = txt, IsReadOnly = c.SomenteLeitura };
                    if (c.SomenteLeitura) { ((TextBox)w).IsReadOnly = true; w.Focusable = false; w.Opacity = 0.7; }
                    break;
            }
            w.Margin = new Thickness(0, 4);
            controles[c.Chave] = w;
            if (primeiro == null && !c.SomenteLeitura && c.Tipo != TipoCampo.Bool) primeiro = w;
            K.ColocarNaGrade(grade, w, linha, 1);
            linha++;
            if (c.Dica.Length > 0)
            {
                grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var dica = K.Discreto(c.Dica); dica.FontSize = 11; dica.Margin = new Thickness(0, 0, 0, 4);
                K.ColocarNaGrade(grade, dica, linha, 1);
                linha++;
            }
        }

        var salvar = new Button { Content = textoBotao, MinWidth = 96 }; salvar.Classes.Add("primario");
        salvar.Click += (_, _) => Salvar();
        var cancelar = new Button { Content = "Cancelar", MinWidth = 96 };
        cancelar.Click += (_, _) => Close();
        var barra = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        barra.Children.Add(salvar); barra.Children.Add(cancelar);
        grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        K.ColocarNaGrade(grade, barra, linha, 0, 2);
        Content = grade;

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
