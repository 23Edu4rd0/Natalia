using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;

namespace GestaoComercial.App.Ui;

/// <summary>
/// Emissão da nota de uma venda. Vem tudo preenchido com o que o programa já sabe; o que faltar
/// (NCM do produto, CPF/CNPJ, endereço do cliente na NF-e...) fica em vermelho para a pessoa completar ali mesmo.
/// </summary>
public sealed class EmitirNotaDialog : JanelaBase
{
    /// <summary>Envia em segundo plano (a tela não trava esperando a SEFAZ). Os testes desligam.</summary>
    public static bool EmSegundoPlano { get; set; } = true;

    readonly Servico svc;
    readonly long vendaId;
    readonly Venda venda;
    DadosNota dados;
    public ResultadoNota? Resultado { get; private set; }

    readonly Button blocoNfce, blocoNfe, emitir, cancelar;
    public TextBox Doc { get; } = K.Caixa("", double.NaN, "CPF ou CNPJ");
    public TextBox Nome { get; } = K.Caixa("");
    public TextBox Email { get; } = K.Caixa("", double.NaN, "Opcional (a nota pode ser enviada por e-mail)");
    public ComboBox IndIE { get; } = K.Combo(new[] { "Não contribuinte (pessoa física / sem IE)", "Contribuinte do ICMS (tem IE)", "Isento de inscrição estadual" }, double.NaN);
    public TextBox IE { get; } = K.Caixa("", double.NaN, "Inscrição estadual");
    public TextBox Cep { get; } = K.Caixa("", double.NaN, "00000-000");
    public TextBox Logradouro { get; } = K.Caixa("");
    public TextBox Numero { get; } = K.Caixa("", double.NaN, "Nº ou S/N");
    public TextBox Complemento { get; } = K.Caixa("", double.NaN, "Opcional");
    public TextBox Bairro { get; } = K.Caixa("");
    public TextBox Municipio { get; } = K.Caixa("");
    public ComboBox Uf { get; } = K.Combo(Ufs.Siglas, double.NaN);
    public TextBox CodMunicipio { get; } = K.Caixa("", double.NaN, "7 números");
    public TextBox InfoCompl { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 56, Watermark = "Opcional: observação que sai nesta nota" };
    public CheckBox SalvarCadastros { get; } = new() { Content = "Guardar NCM e endereço nos cadastros (da próxima vez já vem preenchido)", IsChecked = true };
    readonly List<(TextBox Ncm, TextBox Cfop, ComboBox Trib)> camposItens = new();
    public IReadOnlyList<TextBox> CamposNcm => camposItens.Select(c => c.Ncm).ToList();
    readonly StackPanel listaItens = new() { Spacing = 8 };
    readonly Control secaoEndereco, campoIe, rotuloDoc;
    readonly TextBlock lblAviso = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    readonly Border caixaAviso;
    readonly Button configurar;
    readonly ProgressBar progresso = new() { IsIndeterminate = true, Width = 140, IsVisible = false, VerticalAlignment = VerticalAlignment.Center };
    readonly Dictionary<string, Control> camposPorChave = new();
    public string TextoAviso => caixaAviso.IsVisible ? lblAviso.Text ?? "" : "";
    public int Modelo => dados.Modelo;

    public static async Task<ResultadoNota?> Abrir(Window? dono, Servico svc, long vendaId, int modelo)
    {
        var d = new EmitirNotaDialog(svc, vendaId, modelo);
        if (dono != null) await d.ShowDialog(dono); else d.Show();
        return d.Resultado;
    }

    public EmitirNotaDialog(Servico svc, long vendaId, int modelo)
    {
        this.svc = svc; this.vendaId = vendaId;
        venda = svc.ObterVenda(vendaId);
        dados = svc.PrepararNota(vendaId, modelo);
        var cfg = svc.ObterConfigFiscal();
        Title = "Emitir nota fiscal";
        Width = 1000; Height = 800; MinWidth = 820; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Cores.P(Cores.Fundo);
        foreach (var c in new Control[] { IndIE, Uf }) c.HorizontalAlignment = HorizontalAlignment.Stretch;

        // ---- cabeçalho
        var ambiente = cfg.Producao ? K.Selo("PRODUÇÃO — vale de verdade", "#ECFDF5", "#065F46") : K.Selo("HOMOLOGAÇÃO — teste, sem valor fiscal", "#FFFBEB", "#92400E");
        var titulo = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { new TextBlock { Text = $"Nota fiscal da venda nº {venda.Id:000000}", FontSize = 21, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto) }, ambiente } },
                K.Discreto($"{Util.FmtData(venda.Data)}  ·  {venda.ClienteNome}  ·  {venda.FormaPagamento}  ·  Total {Util.FmtDinheiro(venda.Total)}"),
            },
        };

        blocoNfce = Opcao("NFC-e — cupom fiscal", "Venda ao consumidor no balcão. CPF é opcional.", "venda", () => TrocarModelo(65));
        blocoNfe = Opcao("NF-e — nota fiscal", "Para empresa (CNPJ), entrega ou cliente de outro estado. Pede endereço.", "notas", () => TrocarModelo(55));
        var modelos = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
        K.ColocarNaGrade(modelos, blocoNfce, 0, 0); K.ColocarNaGrade(modelos, blocoNfe, 0, 2);

        // ---- aviso (pendências da configuração, rejeição, sem internet)
        configurar = K.Botao("Configurar nota fiscal…", () => Fazer(ConfigurarAsync), icone: "ajustes");
        var linhaAviso = new DockPanel();
        DockPanel.SetDock(configurar, Dock.Right); configurar.VerticalAlignment = VerticalAlignment.Center; configurar.Margin = new Thickness(12, 0, 0, 0);
        linhaAviso.Children.Add(configurar); linhaAviso.Children.Add(lblAviso);
        caixaAviso = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12), Child = linhaAviso, IsVisible = false };

        // ---- cliente
        rotuloDoc = K.Campo("CPF ou CNPJ do cliente", Doc);
        campoIe = Grade(K.Campo("Situação do cliente no ICMS", IndIE), K.Campo("Inscrição estadual", IE));
        var buscarCep = K.Botao("Buscar CEP", () => Fazer(BuscarCepAsync), icone: "busca");
        buscarCep.VerticalAlignment = VerticalAlignment.Bottom;
        var linhaCep = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,Auto") };
        K.ColocarNaGrade(linhaCep, K.Campo("CEP", Cep), 0, 0); K.ColocarNaGrade(linhaCep, buscarCep, 0, 2);
        secaoEndereco = Pilha(campoIe, linhaCep, Grade3(K.Campo("Rua *", Logradouro), K.Campo("Número *", Numero)),
                              Grade(K.Campo("Complemento", Complemento), K.Campo("Bairro *", Bairro)),
                              Grade3(K.Campo("Cidade *", Municipio), K.Campo("UF *", Uf)), K.Campo("Código IBGE da cidade *", CodMunicipio));
        var cliente = K.Secao("Cliente na nota", Pilha(Grade(rotuloDoc, K.Campo("Nome", Nome)), K.Campo("E-mail", Email), secaoEndereco),
                              subtitulo: "Na NFC-e pode ficar em branco (consumidor não identificado). Na NF-e, documento e endereço são obrigatórios.", icone: "pessoa");
        IndIE.SelectionChanged += (_, _) => IE.IsEnabled = IndIE.SelectedIndex == 1;
        foreach (var (chave, ctl) in new (string, Control)[] { ("dest.doc", Doc), ("dest.nome", Nome), ("dest.logradouro", Logradouro), ("dest.numero", Numero), ("dest.bairro", Bairro),
                                                              ("dest.uf", Uf), ("dest.cmun", CodMunicipio), ("dest.municipio", Municipio), ("dest.cep", Cep), ("dest.ie", IE) })
            camposPorChave[chave] = ctl;

        // ---- produtos
        var produtos = K.Secao("Produtos", Pilha(listaItens, K.Discreto("NCM: código de 8 números da classificação do produto. Está na nota do fornecedor ou o contador informa.")),
                               subtitulo: "Complete o NCM dos produtos marcados em vermelho.", icone: "produtos");
        MontarItens(cfg);

        var extras = K.Secao("Mais", Pilha(K.Campo("Informações complementares", InfoCompl), SalvarCadastros), icone: "info");

        var corpo = Pilha(modelos, caixaAviso, cliente, produtos, extras);
        corpo.Spacing = 18; corpo.Margin = new Thickness(24, 4, 24, 24);

        emitir = K.Botao("Emitir NFC-e", () => Fazer(async () => await EmitirAsync()), icone: "ok", classe: "verde");
        emitir.MinHeight = 44; emitir.Padding = new Thickness(20, 0);
        cancelar = K.Botao("Fechar", Close);
        var rodape = new Border
        {
            Padding = new Thickness(24, 14), Background = Brushes.White, BorderBrush = Cores.P(Cores.Borda), BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right, Children = { progresso, cancelar, emitir } },
        };
        var cab = new StackPanel { Margin = new Thickness(24, 20, 24, 14), Children = { titulo } };
        var doca = new DockPanel();
        DockPanel.SetDock(cab, Dock.Top); DockPanel.SetDock(rodape, Dock.Bottom);
        doca.Children.Add(cab); doca.Children.Add(rodape);
        doca.Children.Add(new ScrollViewer { Content = corpo, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape && emitir.IsEnabled) Close(); };

        Preencher();
        TrocarModelo(modelo);
        MostrarPendenciasConfig(cfg);
        // tentativa anterior rejeitada ou sem resposta: mostra o porquê
        if (svc.NotasDaVenda(vendaId).FirstOrDefault(n => n.PodeReenviar) is { } ant)
            Avisar(ant.Status == "PENDENTE" ? $"Tentativa anterior ficou pendente: {ant.Motivo}" : $"Tentativa anterior: {ant.Motivo}", ant.Status == "PENDENTE" ? "aviso" : "erro");
    }

    // ------------------------------------------------------------------ montagem
    static StackPanel Pilha(params Control[] filhos) { var p = new StackPanel { Spacing = 14 }; foreach (var f in filhos) p.Children.Add(f); return p; }
    static Grid Grade(Control a, Control b) { var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*") }; K.ColocarNaGrade(g, a, 0, 0); K.ColocarNaGrade(g, b, 0, 2); return g; }
    static Grid Grade3(Control a, Control b) { var g = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,14,*") }; K.ColocarNaGrade(g, a, 0, 0); K.ColocarNaGrade(g, b, 0, 2); return g; }

    static Button Opcao(string titulo, string texto, string icone, Action acao)
    {
        var b = new Button { Tag = titulo, Padding = new Thickness(14, 12), VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center };
        b.Classes.Add("bloco");
        var bolha = K.Bolha(icone, Cores.Primaria, Cores.PrimariaSuave, 36);
        var textos = new StackPanel
        {
            Spacing = 2, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = titulo, FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = Cores.P(Cores.Texto) },
                new TextBlock { Text = texto, FontSize = 12.5, Foreground = Cores.P(Cores.Texto2), TextWrapping = TextWrapping.Wrap },
            },
        };
        var d = new DockPanel(); DockPanel.SetDock(bolha, Dock.Left); d.Children.Add(bolha); d.Children.Add(textos);
        b.Content = d;
        b.Click += (_, _) => acao();
        return b;
    }

    void MontarItens(ConfigFiscal cfg)
    {
        listaItens.Children.Clear(); camposItens.Clear();
        var opcoes = Tributacao.Opcoes(cfg.Crt);
        for (int i = 0; i < dados.Itens.Count; i++)
        {
            var it = dados.Itens[i];
            var ncm = K.Caixa(it.Ncm, double.NaN, "8 números"); ncm.Width = 130;
            var cfop = K.Caixa(it.Cfop, double.NaN); cfop.Width = 80;
            var trib = K.Combo(opcoes.Select(o => o.Codigo).ToList(), 90, opcoes.Any(o => o.Codigo == it.Tributacao) ? it.Tributacao : opcoes[0].Codigo);
            ToolTip.SetTip(trib, string.Join("\n", opcoes.Select(o => o.Descricao)));
            ToolTip.SetTip(ncm, "Código NCM de 8 números (ex.: 6109.10.00 para camisetas de algodão).");
            var nome = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center, Spacing = 1,
                Children =
                {
                    new TextBlock { Text = it.Descricao, FontWeight = FontWeight.SemiBold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Cores.P(Cores.Texto) },
                    new TextBlock { Text = $"{it.Quantidade} × {Util.FmtDinheiro(it.PrecoUnit)} = {Util.FmtDinheiro(it.Total)}", FontSize = 12, Foreground = Cores.P(Cores.Texto2) },
                },
            };
            var linha = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,Auto,10,Auto,10,Auto") };
            K.ColocarNaGrade(linha, nome, 0, 0);
            K.ColocarNaGrade(linha, K.Campo("NCM *", ncm), 0, 2); K.ColocarNaGrade(linha, K.Campo("CFOP", cfop), 0, 4); K.ColocarNaGrade(linha, K.Campo("ICMS", trib), 0, 6);
            var cartao = new Border { Child = linha, Padding = new Thickness(12, 10), CornerRadius = new CornerRadius(10), BorderBrush = Cores.P(Cores.Linha), BorderThickness = new Thickness(1), Background = Cores.P("#FCFCFD") };
            listaItens.Children.Add(cartao);
            camposItens.Add((ncm, cfop, trib));
            camposPorChave[$"item:{i}:ncm"] = ncm; camposPorChave[$"item:{i}:cfop"] = cfop; camposPorChave[$"item:{i}:trib"] = trib;
        }
    }

    void Preencher()
    {
        Doc.Text = dados.DestDocumento; Nome.Text = dados.DestNome; Email.Text = dados.DestEmail;
        IndIE.SelectedIndex = dados.DestIndIE switch { 1 => 1, 2 => 2, _ => 0 }; IE.Text = dados.DestIE; IE.IsEnabled = IndIE.SelectedIndex == 1;
        Cep.Text = dados.DestCep; Logradouro.Text = dados.DestLogradouro; Numero.Text = dados.DestNumero; Complemento.Text = dados.DestComplemento;
        Bairro.Text = dados.DestBairro; Municipio.Text = dados.DestMunicipio; Uf.SelectedItem = Ufs.Codigos.ContainsKey(dados.DestUf) ? dados.DestUf : null;
        CodMunicipio.Text = dados.DestCodMunicipio; InfoCompl.Text = dados.InfoComplementar;
    }

    public void TrocarModelo(int modelo)
    {
        dados.Modelo = modelo == 55 ? 55 : 65;
        var nfe = dados.Modelo == 55;
        foreach (var (b, ativo) in new[] { (blocoNfce, !nfe), (blocoNfe, nfe) })
            if (ativo) b.Classes.Add("ativo"); else b.Classes.Remove("ativo");
        secaoEndereco.IsVisible = nfe;
        ((TextBlock)((StackPanel)rotuloDoc).Children[0]).Text = nfe ? "CPF ou CNPJ do cliente *" : "CPF ou CNPJ do cliente (opcional)";
        emitir.Content = MontarConteudoBotao(nfe ? "Emitir NF-e" : "Emitir NFC-e");
        emitir.Tag = nfe ? "Emitir NF-e" : "Emitir NFC-e";
        MostrarPendenciasConfig(svc.ObterConfigFiscal());
    }

    static Control MontarConteudoBotao(string texto) =>
        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Icones.Criar("ok", 16, "#FFFFFF"), new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center } } };

    void MostrarPendenciasConfig(ConfigFiscal cfg)
    {
        var p = cfg.Pendencias(dados.Modelo);
        configurar.IsVisible = p.Count > 0;
        if (p.Count > 0) Avisar("Antes de emitir, configure a nota fiscal: " + Limitar(p), "aviso");
        else if (caixaAviso.Tag as string == "config") caixaAviso.IsVisible = false;
        if (p.Count > 0) caixaAviso.Tag = "config";
    }

    void Avisar(string texto, string tom)
    {
        caixaAviso.IsVisible = true; caixaAviso.Tag = tom;
        lblAviso.Text = texto;
        var (fundo, borda, cor) = tom switch
        {
            "erro" => ("#FEF2F2", "#FECACA", "#B91C1C"),
            "ok" => ("#ECFDF5", "#A7F3D0", "#065F46"),
            _ => ("#FFFBEB", "#FDE68A", "#92400E"),
        };
        caixaAviso.Background = Cores.P(fundo); caixaAviso.BorderBrush = Cores.P(borda); caixaAviso.BorderThickness = new Thickness(1);
        lblAviso.Foreground = Cores.P(cor);
    }

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    async Task ConfigurarAsync()
    {
        var antes = Coletar();     // o que a pessoa já digitou nesta tela não se perde
        await ConfigFiscalDialog.Abrir(this, svc);
        var cfg = svc.ObterConfigFiscal();
        dados = svc.PrepararNota(vendaId, antes.Modelo);
        MontarItens(cfg);
        for (int i = 0; i < antes.Itens.Count && i < camposItens.Count; i++)
        {
            if (antes.Itens[i].Ncm.Length > 0) camposItens[i].Ncm.Text = antes.Itens[i].Ncm;
            if (antes.Itens[i].Cfop.Length > 0) camposItens[i].Cfop.Text = antes.Itens[i].Cfop;
        }
        caixaAviso.IsVisible = false;
        MostrarPendenciasConfig(cfg);
    }

    async Task BuscarCepAsync()
    {
        var e = await ConsultaPublica.CepAsync(Cep.Text ?? "");
        Cep.Text = e.Cep; if (e.Logradouro.Length > 0) Logradouro.Text = e.Logradouro; if (e.Bairro.Length > 0) Bairro.Text = e.Bairro;
        Municipio.Text = e.Municipio; Uf.SelectedItem = e.Uf; CodMunicipio.Text = e.CodMunicipio;
        Numero.Focus();
    }

    /// <summary>Lê a tela para o rascunho (em <paramref name="destino"/>, ou no rascunho atual).</summary>
    public DadosNota Coletar(DadosNota? destino = null)
    {
        var d = destino ?? dados;
        d.DestDocumento = (Doc.Text ?? "").Trim(); d.DestNome = (Nome.Text ?? "").Trim(); d.DestEmail = (Email.Text ?? "").Trim();
        d.DestIndIE = IndIE.SelectedIndex switch { 1 => 1, 2 => 2, _ => 9 }; d.DestIE = (IE.Text ?? "").Trim();
        d.DestCep = (Cep.Text ?? "").Trim(); d.DestLogradouro = (Logradouro.Text ?? "").Trim(); d.DestNumero = (Numero.Text ?? "").Trim();
        d.DestComplemento = (Complemento.Text ?? "").Trim(); d.DestBairro = (Bairro.Text ?? "").Trim(); d.DestMunicipio = (Municipio.Text ?? "").Trim();
        d.DestUf = Uf.SelectedItem as string ?? ""; d.DestCodMunicipio = (CodMunicipio.Text ?? "").Trim();
        d.InfoComplementar = (InfoCompl.Text ?? "").Trim(); d.SalvarNosCadastros = SalvarCadastros.IsChecked == true;
        if (destino == null)
            for (int i = 0; i < d.Itens.Count && i < camposItens.Count; i++)
            {
                d.Itens[i].Ncm = (camposItens[i].Ncm.Text ?? "").Trim();
                d.Itens[i].Cfop = (camposItens[i].Cfop.Text ?? "").Trim();
                d.Itens[i].Tributacao = camposItens[i].Trib.SelectedItem as string ?? "";
            }
        return d;
    }

    /// <summary>Até 3 mensagens; o resto vira "e mais N".</summary>
    static string Limitar(List<string> msgs) =>
        string.Join("  •  ", msgs.Take(3)) + (msgs.Count > 3 ? $"  •  e mais {msgs.Count - 3} item(ns) na configuração." : "");

    /// <summary>NCM faltando em vários produtos vira uma frase só (os campos já ficam em vermelho).</summary>
    static string Resumo(List<Pendencia> pend)
    {
        var semNcm = pend.Where(p => p.Campo.EndsWith(":ncm")).ToList();
        var msgs = pend.Where(p => !p.Campo.EndsWith(":ncm")).Select(p => p.Mensagem).ToList();
        if (semNcm.Count > 0) msgs.Insert(0, semNcm.Count == 1 ? semNcm[0].Mensagem : $"falta o NCM de {semNcm.Count} produtos (marcados em vermelho em “Produtos”). É um código de 8 números que está na nota do fornecedor.");
        return string.Join("  •  ", msgs);
    }

    /// <summary>Marca em vermelho os campos com problema (e limpa os que foram corrigidos).</summary>
    void Destacar(IEnumerable<Pendencia> pendencias)
    {
        var ruins = pendencias.Select(p => p.Campo).ToHashSet();
        foreach (var (chave, ctl) in camposPorChave)
        {
            var erro = ruins.Contains(chave);
            if (ctl is TextBox t) t.BorderBrush = Cores.P(erro ? "#F87171" : "#E2E8F0");
            else if (ctl is ComboBox c) c.BorderBrush = Cores.P(erro ? "#F87171" : "#E2E8F0");
        }
        if (pendencias.FirstOrDefault(p => camposPorChave.ContainsKey(p.Campo)) is { } primeira) camposPorChave[primeira.Campo].Focus();
    }

    public bool CampoComErro(string chave) => camposPorChave.TryGetValue(chave, out var c) && (c is TextBox t ? t.BorderBrush : ((ComboBox)c).BorderBrush) == Cores.P("#F87171");

    /// <summary>Confere, envia à SEFAZ e mostra o resultado. Autorizada: fecha a janela.</summary>
    public async Task<ResultadoNota?> EmitirAsync()
    {
        var d = Coletar();
        var pend = RegrasNota.Verificar(d, svc.ObterConfigFiscal());
        Destacar(pend);
        if (pend.Count > 0)
        {
            Avisar(pend.Any(p => p.Campo == "config") ? "Antes de emitir, configure a nota fiscal: " + Limitar(pend.Where(p => p.Campo == "config").Select(p => p.Mensagem).ToList())
                                                      : "Falta completar: " + Resumo(pend), "erro");
            configurar.IsVisible = pend.Any(p => p.Campo == "config");
            return null;
        }
        emitir.IsEnabled = cancelar.IsEnabled = false; progresso.IsVisible = true;
        Avisar("Enviando para a SEFAZ…", "aviso");
        try
        {
            var r = EmSegundoPlano ? await Task.Run(() => svc.EmitirNota(d)) : svc.EmitirNota(d);
            Resultado = r;
            if (r.Autorizada) { Close(); return r; }
            Avisar(r.Status == "PENDENTE" ? r.Mensagem + "\nA nota ficou pendente com o mesmo número. Clique em “Tentar de novo”." : r.Mensagem, r.Status == "PENDENTE" ? "aviso" : "erro");
            if (r.Status is "PENDENTE" or "REJEITADA") emitir.Content = MontarConteudoBotao("Tentar de novo");
            return r;
        }
        catch (PendenciasException ex) { Destacar(ex.Pendencias); Avisar(ex.Message, "erro"); return null; }
        catch (ValidacaoException ex) { Avisar(ex.Message, "erro"); return null; }
        finally { emitir.IsEnabled = cancelar.IsEnabled = true; progresso.IsVisible = false; }
    }
}
