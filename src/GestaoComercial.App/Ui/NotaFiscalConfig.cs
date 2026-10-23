using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;

namespace GestaoComercial.App.Ui;

/// <summary>Configuração da nota fiscal eletrônica: preenchida uma vez (dados da empresa, certificado, CSC, numeração, impostos).</summary>
public sealed class ConfigFiscalDialog : JanelaBase
{
    readonly Servico svc;
    readonly ConfigFiscal cfg;
    bool salvo;

    // empresa
    public TextBox Cnpj { get; } = K.Caixa("", double.NaN, "00.000.000/0000-00");
    public TextBox Razao { get; } = K.Caixa("", double.NaN, "Como está no cartão do CNPJ");
    public TextBox Fantasia { get; } = K.Caixa("", double.NaN, "Opcional");
    public TextBox IE { get; } = K.Caixa("", double.NaN, "Só números (ou ISENTO)");
    public ComboBox Regime { get; }
    // endereço
    public TextBox Cep { get; } = K.Caixa("", double.NaN, "00000-000");
    public TextBox Logradouro { get; } = K.Caixa("");
    public TextBox Numero { get; } = K.Caixa("");
    public TextBox Complemento { get; } = K.Caixa("", double.NaN, "Opcional");
    public TextBox Bairro { get; } = K.Caixa("");
    public TextBox Municipio { get; } = K.Caixa("");
    public ComboBox Uf { get; } = K.Combo(Ufs.Siglas, double.NaN);
    public TextBox CodMunicipio { get; } = K.Caixa("", double.NaN, "7 números");
    public TextBox Telefone { get; } = K.Caixa("", double.NaN, "Opcional");
    // certificado e ambiente
    public TextBox Senha { get; } = new() { PasswordChar = '•', Watermark = "Senha do certificado" };
    readonly TextBlock lblCert = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    readonly Border caixaCert = new() { CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10) };
    string arquivoCert = "";
    public int Ambiente { get; private set; }
    readonly Button blocoHomolog, blocoProducao;
    // NFC-e / NF-e
    public TextBox CscId { get; } = K.Caixa("", double.NaN, "Ex.: 000001");
    public TextBox Csc { get; } = K.Caixa("", double.NaN, "Código gerado no site da SEFAZ");
    public ComboBox VersaoQr { get; } = K.Combo(new[] { "Versão 2 (com CSC)", "Versão 3 (sem CSC)" }, double.NaN, "Versão 2 (com CSC)");
    public TextBox SerieNfce { get; } = K.Caixa("1");
    public TextBox ProximoNfce { get; } = K.Caixa("1");
    public TextBox SerieNfe { get; } = K.Caixa("1");
    public TextBox ProximoNfe { get; } = K.Caixa("1");
    // impostos
    public TextBox Cfop { get; } = K.Caixa("5102");
    public ComboBox Trib { get; } = K.Combo(Array.Empty<string>(), double.NaN);
    public TextBox AliqIcms { get; } = K.Caixa("0", double.NaN, "Ex.: 18");
    public ComboBox PisCofins { get; } = K.Combo(new[] { "99 – Outras operações (sem valor)", "01 – Tributável (alíquota básica)", "49 – Outras operações de saída", "07 – Isenta", "08 – Sem incidência" }, double.NaN);
    public TextBox AliqPis { get; } = K.Caixa("0", double.NaN, "Ex.: 0,65");
    public TextBox AliqCofins { get; } = K.Caixa("0", double.NaN, "Ex.: 3");
    public TextBox PctTributos { get; } = K.Caixa("", double.NaN, "Ex.: 8,5 (deixe vazio se não souber)");
    public ComboBox Pix { get; } = K.Combo(new[] { "Chave ou QR Code fixo (estático)", "QR Code da maquininha/banco a cada venda (dinâmico)" }, double.NaN);
    public TextBox Natureza { get; } = K.Caixa("VENDA DE MERCADORIA");
    public TextBox InfoCompl { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 64, Watermark = "Texto que sai em todas as notas (opcional)" };
    public TextBox RtCnpj { get; } = K.Caixa("", double.NaN, "CNPJ de quem dá suporte ao programa");
    public TextBox RtContato { get; } = K.Caixa("");
    public TextBox RtEmail { get; } = K.Caixa("");
    public TextBox RtFone { get; } = K.Caixa("");
    readonly StackPanel camposNormal;
    readonly TextBlock lblPendencias = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Cores.P("#92400E") };
    readonly Border caixaPendencias;
    public string TextoPendencias => caixaPendencias.IsVisible ? lblPendencias.Text ?? "" : "";
    public string TextoCertificado => lblCert.Text ?? "";

    public static async Task<bool> Abrir(Window? dono, Servico svc)
    {
        var d = new ConfigFiscalDialog(svc);
        if (dono != null) await d.ShowDialog(dono); else d.Show();
        return d.salvo;
    }

    static readonly int[] Regimes = { 1, 4, 2, 3 };

    public ConfigFiscalDialog(Servico svc)
    {
        this.svc = svc;
        cfg = svc.ObterConfigFiscal();
        Title = "Configurar nota fiscal eletrônica";
        Width = 1100; Height = 820; MinWidth = 860; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = Cores.P(Cores.Fundo);

        Regime = K.Combo(new[] { "Simples Nacional", "MEI (Microempreendedor Individual)", "Simples Nacional – excesso de sublimite", "Regime normal (Lucro Presumido ou Real)" }, double.NaN);
        foreach (var c in new Control[] { Regime, Uf, VersaoQr, Trib, PisCofins, Pix }) c.HorizontalAlignment = HorizontalAlignment.Stretch;

        // ---- empresa
        var buscarCnpj = K.Botao("Buscar dados", () => Fazer(BuscarCnpjAsync), icone: "busca");
        ToolTip.SetTip(buscarCnpj, "Preenche razão social, endereço e regime pela Receita (precisa de internet)");
        var empresa = K.Secao("Sua empresa", Pilha(
            Linha2(K.Campo("CNPJ *", Cnpj), buscarCnpj),
            K.Campo("Razão social *", Razao), K.Campo("Nome fantasia", Fantasia),
            Grade(K.Campo("Inscrição estadual (IE) *", IE), K.Campo("Regime tributário *", Regime))),
            subtitulo: "Dados que aparecem na nota. “Buscar dados” preenche quase tudo pelo CNPJ.", icone: "loja");

        var buscarCep = K.Botao("Buscar CEP", () => Fazer(BuscarCepAsync), icone: "busca");
        var endereco = K.Secao("Endereço da empresa", Pilha(
            Linha2(K.Campo("CEP *", Cep), buscarCep),
            Grade3(K.Campo("Rua *", Logradouro), K.Campo("Número *", Numero), 2),
            Grade(K.Campo("Complemento", Complemento), K.Campo("Bairro *", Bairro)),
            Grade3(K.Campo("Cidade *", Municipio), K.Campo("UF *", Uf), 2),
            Grade(K.Campo("Código IBGE da cidade *", CodMunicipio), K.Campo("Telefone", Telefone))),
            subtitulo: "“Buscar CEP” também preenche o código IBGE da cidade.", icone: "inicio");

        // ---- certificado
        var escolher = K.Botao("Escolher arquivo .pfx…", () => Fazer(EscolherCertificadoAsync), icone: "pasta");
        var testar = K.Botao("Testar comunicação com a SEFAZ", () => Fazer(TestarAsync), icone: "raio");
        caixaCert.Child = lblCert;
        var certificado = K.Secao("Certificado digital A1", Pilha(caixaCert, Linha2(K.Campo("Senha do certificado", Senha), escolher), testar),
            subtitulo: "Arquivo .pfx comprado numa certificadora (e-CNPJ A1). Certificado A3 (cartão/token) não é aceito.", icone: "info");
        Senha.TextChanged += (_, _) => AtualizarCertificado();

        // ---- ambiente
        blocoHomolog = Opcao("Homologação (testes)", "Notas sem valor fiscal, para aprender e testar. Comece por aqui.", "info", () => DefinirAmbiente(2));
        blocoProducao = Opcao("Produção (valendo)", "Notas de verdade, enviadas à SEFAZ com valor fiscal.", "ok", () => Fazer(EscolherProducaoAsync));
        var ambiente = K.Secao("Ambiente", Pilha(blocoHomolog, blocoProducao), subtitulo: "A numeração de teste e a de produção são separadas.", icone: "ajustes");

        // ---- NFC-e e NF-e
        var nfce = K.Secao("NFC-e (cupom fiscal ao consumidor)", Pilha(
            Grade(K.Campo("Identificador do CSC (ID)", CscId), K.Campo("Versão do QR Code", VersaoQr)),
            K.Campo("CSC (código de segurança do contribuinte)", Csc),
            Grade(K.Campo("Série", SerieNfce), K.Campo("Próximo número", ProximoNfce))),
            subtitulo: "O CSC é gerado no site da SEFAZ do seu estado, na área do contribuinte.", icone: "venda");
        var nfe = K.Secao("NF-e (nota para empresa, entrega ou outro estado)", Grade(K.Campo("Série", SerieNfe), K.Campo("Próximo número", ProximoNfe)), icone: "notas");

        // ---- impostos
        camposNormal = Pilha(K.Campo("Alíquota de ICMS (%)", AliqIcms), K.Campo("PIS/COFINS", PisCofins), Grade(K.Campo("Alíquota PIS (%)", AliqPis), K.Campo("Alíquota COFINS (%)", AliqCofins)));
        var impostos = K.Secao("Impostos (confira com o seu contador)", Pilha(
            Grade(K.Campo("CFOP padrão", Cfop), K.Campo("Natureza da operação", Natureza)),
            K.Campo("Tributação padrão do ICMS", Trib), camposNormal,
            Grade(K.Campo("Tributos aproximados (%)", PctTributos), K.Campo("Como você recebe Pix", Pix)),
            K.Campo("Informações complementares", InfoCompl)),
            subtitulo: "Valem para todos os produtos; dá para mudar produto por produto na hora de emitir.", icone: "relatorios");
        Regime.SelectionChanged += (_, _) => AtualizarRegime();

        var rt = K.Secao("Responsável técnico (exigido por alguns estados)", Pilha(
            Grade(K.Campo("CNPJ", RtCnpj), K.Campo("Contato", RtContato)), Grade(K.Campo("E-mail", RtEmail), K.Campo("Telefone", RtFone))),
            subtitulo: "Quem instalou ou dá suporte ao programa. Se a SEFAZ recusar por falta disso, preencha aqui.", icone: "pessoa");

        var esquerda = Pilha(empresa, endereco, impostos);
        var direita = Pilha(certificado, ambiente, nfce, nfe, rt);
        esquerda.Spacing = direita.Spacing = 18;
        var colunas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,18,*"), Margin = new Thickness(24, 4, 24, 24) };
        K.ColocarNaGrade(colunas, esquerda, 0, 0); K.ColocarNaGrade(colunas, direita, 0, 2);

        var cab = new StackPanel
        {
            Margin = new Thickness(24, 20, 24, 12), Spacing = 4,
            Children =
            {
                new TextBlock { Text = "Nota fiscal eletrônica", FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = Cores.P(Cores.Texto) },
                K.Discreto("Preencha uma vez. Os dados vêm do cartão do CNPJ, da sua contabilidade e do certificado digital."),
            },
        };
        caixaPendencias = new Border { Background = Cores.P("#FFFBEB"), BorderBrush = Cores.P("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8), Child = lblPendencias, Margin = new Thickness(24, 0, 24, 10) };
        var salvar = K.Botao("Salvar", () => Fazer(SalvarAsync), icone: "ok", classe: "primario");
        var cancelar = K.Botao("Cancelar", Close);
        var rodape = new Border
        {
            Padding = new Thickness(24, 14), Background = Brushes.White, BorderBrush = Cores.P(Cores.Borda), BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancelar, salvar } },
        };
        var doca = new DockPanel();
        DockPanel.SetDock(cab, Dock.Top); DockPanel.SetDock(rodape, Dock.Bottom); DockPanel.SetDock(caixaPendencias, Dock.Bottom);
        doca.Children.Add(cab); doca.Children.Add(rodape); doca.Children.Add(caixaPendencias);
        doca.Children.Add(new ScrollViewer { Content = colunas, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        Content = doca;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        Carregar();
    }

    // ------------------------------------------------------------------ montagem
    static StackPanel Pilha(params Control[] filhos) { var p = new StackPanel { Spacing = 14 }; foreach (var f in filhos) p.Children.Add(f); return p; }

    static Grid Grade(Control a, Control b)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,14,*") };
        K.ColocarNaGrade(g, a, 0, 0); K.ColocarNaGrade(g, b, 0, 2);
        return g;
    }

    /// <summary>Campo largo + campo estreito (ex.: rua e número; cidade e UF).</summary>
    static Grid Grade3(Control largo, Control estreito, int proporcao)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions($"{proporcao}*,14,*") };
        K.ColocarNaGrade(g, largo, 0, 0); K.ColocarNaGrade(g, estreito, 0, 2);
        return g;
    }

    /// <summary>Campo com um botão ao lado, alinhado embaixo.</summary>
    static Grid Linha2(Control campo, Control botao)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,Auto") };
        botao.VerticalAlignment = VerticalAlignment.Bottom;
        K.ColocarNaGrade(g, campo, 0, 0); K.ColocarNaGrade(g, botao, 0, 2);
        return g;
    }

    static Button Opcao(string titulo, string texto, string icone, Action acao)
    {
        var b = new Button { Tag = titulo, Padding = new Thickness(14, 12) };
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

    async void Fazer(Func<Task> acao)
    {
        try { await acao(); }
        catch (Exception ex) { await Erros.Tratar(ex, this); }
    }

    // ------------------------------------------------------------------ dados
    static string D(decimal v) => v == 0 ? "0" : v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    void Carregar()
    {
        // primeira vez: aproveita o que já existe nos "Dados da loja"
        var emp = svc.ObterEmpresa();
        Cnpj.Text = cfg.Cnpj.Length > 0 ? Util.FmtDocumento(cfg.Cnpj) : Util.SomenteDigitos(emp.Documento).Length == 14 ? Util.FmtDocumento(emp.Documento) : "";
        Razao.Text = cfg.RazaoSocial.Length > 0 ? cfg.RazaoSocial : emp.Nome != Empresa.Padrao.Nome ? emp.Nome : "";
        Fantasia.Text = cfg.NomeFantasia; IE.Text = cfg.IE;
        Regime.SelectedIndex = Math.Max(0, Array.IndexOf(Regimes, cfg.Crt));
        Cep.Text = cfg.Cep; Logradouro.Text = cfg.Logradouro; Numero.Text = cfg.Numero; Complemento.Text = cfg.Complemento; Bairro.Text = cfg.Bairro;
        Municipio.Text = cfg.Municipio; Uf.SelectedItem = Ufs.Codigos.ContainsKey(cfg.Uf) ? cfg.Uf : null; CodMunicipio.Text = cfg.CodMunicipio;
        Telefone.Text = cfg.Telefone.Length > 0 ? cfg.Telefone : emp.Telefone;
        arquivoCert = cfg.CertificadoArquivo; Senha.Text = cfg.CertificadoSenha;
        DefinirAmbiente(cfg.Ambiente);
        CscId.Text = cfg.CscId; Csc.Text = cfg.Csc; VersaoQr.SelectedIndex = cfg.VersaoQrCode == 3 ? 1 : 0;
        SerieNfce.Text = cfg.SerieNfce.ToString(); SerieNfe.Text = cfg.SerieNfe.ToString();
        MostrarNumeracao();
        foreach (var t in new[] { SerieNfce, SerieNfe }) t.TextChanged += (_, _) => MostrarNumeracao();
        Cfop.Text = cfg.CfopPadrao; AtualizarRegime(); SelecionarTrib(cfg.TributacaoPadrao);
        AliqIcms.Text = D(cfg.AliquotaIcms); AliqPis.Text = D(cfg.AliquotaPis); AliqCofins.Text = D(cfg.AliquotaCofins);
        PisCofins.SelectedIndex = Math.Max(0, Array.FindIndex(PisCofinsCodigos, c => c == cfg.CstPisCofins));
        PctTributos.Text = cfg.PercentualTributos > 0 ? D(cfg.PercentualTributos) : "";
        Pix.SelectedIndex = cfg.TipoPix == "17" ? 1 : 0;
        Natureza.Text = cfg.NaturezaOperacao; InfoCompl.Text = cfg.InfoComplementar;
        RtCnpj.Text = cfg.RespTecCnpj; RtContato.Text = cfg.RespTecContato; RtEmail.Text = cfg.RespTecEmail; RtFone.Text = cfg.RespTecFone;
        AtualizarCertificado();
        MostrarPendencias(cfg);
        // a lista do que falta acompanha o que a pessoa digita
        foreach (var t in new[] { Cnpj, Razao, IE, Cep, Logradouro, Numero, Bairro, Municipio, CodMunicipio, CscId, Csc, Cfop, Senha })
            t.TextChanged += (_, _) => AtualizarPendencias();
        foreach (var c in new[] { Regime, Uf, VersaoQr, Trib }) c.SelectionChanged += (_, _) => AtualizarPendencias();
    }

    void AtualizarPendencias()
    {
        try { MostrarPendencias(Coletar(), verificarCertificado: false); }   // o certificado tem o aviso próprio, logo acima da senha
        catch (ValidacaoException) { }
    }

    static readonly string[] PisCofinsCodigos = { "99", "01", "49", "07", "08" };

    int CrtEscolhido => Regimes[Math.Max(0, Regime.SelectedIndex)];

    void AtualizarRegime()
    {
        var atual = TribEscolhida();
        Trib.ItemsSource = Tributacao.Opcoes(CrtEscolhido).Select(o => o.Descricao).ToList();
        SelecionarTrib(atual);
        if (Trib.SelectedIndex < 0) Trib.SelectedIndex = 0;
        camposNormal.IsVisible = CrtEscolhido is 2 or 3;
    }

    string TribEscolhida()
    {
        var ops = Tributacao.Opcoes(CrtEscolhido);
        return Trib.SelectedItem is string s ? ops.FirstOrDefault(o => o.Descricao == s).Codigo ?? "" : "";
    }

    void SelecionarTrib(string codigo)
    {
        var ops = Tributacao.Opcoes(CrtEscolhido);
        var i = Array.FindIndex(ops, o => o.Codigo == codigo);
        if (i >= 0) Trib.SelectedIndex = i;
    }

    void DefinirAmbiente(int amb)
    {
        Ambiente = amb == 1 ? 1 : 2;
        foreach (var (b, ativo) in new[] { (blocoHomolog, Ambiente == 2), (blocoProducao, Ambiente == 1) })
            if (ativo) b.Classes.Add("ativo"); else b.Classes.Remove("ativo");
        MostrarNumeracao();
    }

    // a numeração é separada por série e ambiente: mostra a do que está escolhido na tela
    // (o número só é gravado se a pessoa mudar o que foi mostrado)
    long proxMostradoNfce, proxMostradoNfe;

    void MostrarNumeracao()
    {
        if (!int.TryParse(SerieNfce.Text, out var sNfce) || !int.TryParse(SerieNfe.Text, out var sNfe)) return;
        proxMostradoNfce = svc.ProximoNumeroNota(65, sNfce, Ambiente); proxMostradoNfe = svc.ProximoNumeroNota(55, sNfe, Ambiente);
        ProximoNfce.Text = proxMostradoNfce.ToString(); ProximoNfe.Text = proxMostradoNfe.ToString();
    }

    async Task EscolherProducaoAsync()
    {
        if (Ambiente == 1) return;
        if (await Plat.Atual.Confirmar(this, "Passar para produção",
                "Em produção as notas têm VALOR FISCAL e vão para a SEFAZ de verdade.\n\nFaça antes algumas notas em homologação para conferir. Confirmar?"))
            DefinirAmbiente(1);
    }

    void AtualizarCertificado()
    {
        if (arquivoCert.Length == 0 || !File.Exists(arquivoCert))
        {
            lblCert.Text = "Nenhum certificado escolhido."; Tom(false, neutro: true);
            return;
        }
        try
        {
            using var c = ConfigFiscal.AbrirCertificado(arquivoCert, Senha.Text ?? "");
            var nome = c.GetNameInfo(System.Security.Cryptography.X509Certificates.X509NameType.SimpleName, false);
            var vencido = c.NotAfter < DateTime.Now;
            lblCert.Text = vencido ? $"Certificado VENCIDO em {c.NotAfter:dd/MM/yyyy} — {nome}" : $"Válido até {c.NotAfter:dd/MM/yyyy} — {nome}";
            Tom(!vencido);
        }
        catch (ValidacaoException ex) { lblCert.Text = (Senha.Text ?? "").Length == 0 ? "Digite a senha do certificado." : ex.Message; Tom(false); }
    }

    void Tom(bool ok, bool neutro = false)
    {
        caixaCert.Background = Cores.P(neutro ? Cores.CinzaFundo : ok ? "#ECFDF5" : "#FEF2F2");
        lblCert.Foreground = Cores.P(neutro ? Cores.Texto2 : ok ? "#065F46" : "#B91C1C");
    }

    public async Task EscolherCertificadoAsync()
    {
        var arq = await Plat.Atual.AbrirArquivo(this, "Escolha o certificado digital A1", "Certificado A1 (.pfx)", new[] { "pfx", "p12" });
        if (arq == null) return;
        // guarda uma cópia na pasta de dados: o programa continua funcionando se o arquivo original for movido
        var destino = Path.Combine(Config.Subpasta("certificado"), "certificado" + Path.GetExtension(arq).ToLowerInvariant());
        if (!string.Equals(Path.GetFullPath(arq), Path.GetFullPath(destino), StringComparison.Ordinal)) File.Copy(arq, destino, true);
        arquivoCert = destino;
        AtualizarCertificado(); AtualizarPendencias();
        Senha.Focus();
    }

    async Task BuscarCepAsync()
    {
        var e = await ConsultaPublica.CepAsync(Cep.Text ?? "");
        Cep.Text = e.Cep; if (e.Logradouro.Length > 0) Logradouro.Text = e.Logradouro; if (e.Bairro.Length > 0) Bairro.Text = e.Bairro;
        Municipio.Text = e.Municipio; Uf.SelectedItem = e.Uf; CodMunicipio.Text = e.CodMunicipio;
        Numero.Focus();
    }

    async Task BuscarCnpjAsync()
    {
        var d = await ConsultaPublica.CnpjAsync(Cnpj.Text ?? "");
        Razao.Text = d.RazaoSocial; if (d.NomeFantasia.Length > 0) Fantasia.Text = d.NomeFantasia;
        var e = d.Endereco;
        Cep.Text = e.Cep; Logradouro.Text = e.Logradouro; Numero.Text = d.Numero; Complemento.Text = e.Complemento; Bairro.Text = e.Bairro;
        Municipio.Text = e.Municipio; Uf.SelectedItem = e.Uf; CodMunicipio.Text = e.CodMunicipio;
        if (d.Telefone.Length > 0) Telefone.Text = d.Telefone;
        if (d.CrtSugerido is int crt) Regime.SelectedIndex = Array.IndexOf(Regimes, crt);
        Plat.Atual.Toast(this, "Dados preenchidos pelo CNPJ. Confira e complete a inscrição estadual.");
        IE.Focus();
    }

    async Task TestarAsync()
    {
        Coletar().Salvar(svc);   // usa o que está na tela agora
        var r = await Task.Run(() => svc.StatusSefaz());
        await Plat.Atual.Mensagem(this, "Comunicação com a SEFAZ", $"A SEFAZ respondeu: {r}", r.StartsWith("107") ? Nivel.Info : Nivel.Aviso);
    }

    static decimal Num(TextBox t, string campo)
    {
        var s = (t.Text ?? "").Trim().Replace("%", "");
        if (s.Length == 0) return 0;
        return decimal.TryParse(s.Replace(".", "").Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? v : throw new ValidacaoException($"{campo}: número inválido.");
    }

    static int Inteiro(TextBox t, string campo) =>
        int.TryParse(Util.SomenteDigitos(t.Text), out var n) && Util.SomenteDigitos(t.Text) == (t.Text ?? "").Trim() ? n : throw new ValidacaoException($"{campo}: use só números.");

    public ConfigFiscal Coletar()
    {
        var c = cfg.Copia();
        c.Cnpj = DocFiscal.Limpar(Cnpj.Text).ToUpperInvariant(); c.RazaoSocial = (Razao.Text ?? "").Trim(); c.NomeFantasia = (Fantasia.Text ?? "").Trim();
        c.IE = (IE.Text ?? "").Trim(); c.Crt = CrtEscolhido;
        c.Cep = Util.SomenteDigitos(Cep.Text); c.Logradouro = (Logradouro.Text ?? "").Trim(); c.Numero = (Numero.Text ?? "").Trim(); c.Complemento = (Complemento.Text ?? "").Trim();
        c.Bairro = (Bairro.Text ?? "").Trim(); c.Municipio = (Municipio.Text ?? "").Trim(); c.Uf = Uf.SelectedItem as string ?? ""; c.CodMunicipio = Util.SomenteDigitos(CodMunicipio.Text);
        c.Telefone = (Telefone.Text ?? "").Trim();
        c.CertificadoArquivo = arquivoCert; c.CertificadoSenha = Senha.Text ?? "";
        c.Ambiente = Ambiente;
        c.CscId = (CscId.Text ?? "").Trim(); c.Csc = (Csc.Text ?? "").Trim(); c.VersaoQrCode = VersaoQr.SelectedIndex == 1 ? 3 : 2;
        c.SerieNfce = Inteiro(SerieNfce, "Série da NFC-e"); c.SerieNfe = Inteiro(SerieNfe, "Série da NF-e");
        c.CfopPadrao = Util.SomenteDigitos(Cfop.Text); c.TributacaoPadrao = TribEscolhida();
        c.AliquotaIcms = Num(AliqIcms, "Alíquota de ICMS"); c.CstPisCofins = PisCofinsCodigos[Math.Max(0, PisCofins.SelectedIndex)];
        c.AliquotaPis = Num(AliqPis, "Alíquota de PIS"); c.AliquotaCofins = Num(AliqCofins, "Alíquota de COFINS");
        c.PercentualTributos = Num(PctTributos, "Tributos aproximados"); c.TipoPix = Pix.SelectedIndex == 1 ? "17" : "20";
        c.NaturezaOperacao = (Natureza.Text ?? "").Trim(); c.InfoComplementar = (InfoCompl.Text ?? "").Trim();
        c.RespTecCnpj = DocFiscal.Limpar(RtCnpj.Text); c.RespTecContato = (RtContato.Text ?? "").Trim(); c.RespTecEmail = (RtEmail.Text ?? "").Trim(); c.RespTecFone = (RtFone.Text ?? "").Trim();
        return c;
    }

    void MostrarPendencias(ConfigFiscal c, bool verificarCertificado = true)
    {
        var p = c.Pendencias(65, verificarCertificado);
        caixaPendencias.IsVisible = p.Count > 0;
        lblPendencias.Text = p.Count == 0 ? "" : "Ainda falta para emitir: " + string.Join("  •  ", p);
    }

    /// <summary>Salva (mesmo incompleto: dá para terminar depois). Mostra o que ainda falta.</summary>
    public async Task SalvarAsync()
    {
        var c = Coletar();
        var proxNfce = Inteiro(ProximoNfce, "Próximo número da NFC-e"); var proxNfe = Inteiro(ProximoNfe, "Próximo número da NF-e");
        svc.SalvarConfigFiscal(c);
        if (proxNfce != proxMostradoNfce) svc.DefinirProximoNumeroNota(65, proxNfce);
        if (proxNfe != proxMostradoNfe) svc.DefinirProximoNumeroNota(55, proxNfe);
        salvo = true;
        var p = c.Pendencias(65);
        if (p.Count > 0)
        {
            MostrarPendencias(c);
            await Plat.Atual.Mensagem(this, "Salvo, mas ainda falta", "A configuração foi salva. Para emitir, ainda falta:\n\n• " + string.Join("\n• ", p), Nivel.Aviso);
            return;
        }
        Close();
    }
}
