using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using GestaoComercial.Core;
using GestaoComercial.Core.Fiscal;

namespace GestaoComercial.App.Ui;

/// <summary>Notas fiscais eletrônicas emitidas: DANFE, XML para o contador, consultar/reenviar, cancelar e inutilizar.</summary>
public sealed class NotasFiscaisTela : Tela
{
    public TextBox De { get; }
    public TextBox Ate { get; }
    public Tabela Tabela { get; }
    readonly TextBlock rodape = K.Discreto("");
    readonly Border faixaConfig;
    readonly Button botaoConfig;
    readonly TextBlock lblConfig = new() { FontSize = 13.5, TextWrapping = Avalonia.Media.TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    List<NotaFiscal> notas = new();
    readonly Control acoes;
    public override Control? AcoesCabecalho => acoes;
    public string TextoConfig => faixaConfig.IsVisible ? lblConfig.Text ?? "" : "";

    public override string Titulo => "Notas fiscais";
    public override string Subtitulo => "NFC-e e NF-e emitidas para a SEFAZ. Os arquivos XML ficam guardados para o seu contador.";

    public NotasFiscaisTela(MainWindow app) : base(app)
    {
        acoes = K.Botao("Configurar nota fiscal", SeguroAsync(ConfigurarAsync), icone: "ajustes");

        De = K.Data(Util.InicioMesBr()); Ate = K.Data(Util.HojeBr());
        var filtros = K.Fila(10, K.Linha(8, De, K.Rotulo("até", false, 13, Cores.Texto2), Ate), K.Botao("Filtrar", Seguro(Atualizar), icone: "busca"));
        K.AoApertarEnter(De, Seguro(Atualizar)); K.AoApertarEnter(Ate, Seguro(Atualizar));
        var botoes = K.Fila(4,
            K.Botao("Ver / imprimir", SeguroAsync(VerAsync), icone: "imprimir", classe: "fantasma"),
            K.Botao("Abrir XML", Seguro(AbrirXml), icone: "pasta", classe: "fantasma"),
            K.Botao("Tentar de novo / consultar", SeguroAsync(ReenviarAsync), icone: "raio", classe: "fantasma"),
            K.Botao("Cancelar nota", SeguroAsync(CancelarAsync), icone: "lixeira", classe: "fantasma perigo"),
            K.Botao("Inutilizar número", SeguroAsync(InutilizarAsync), icone: "fechar", classe: "fantasma"));

        Tabela = new Tabela(new Col("Número", 100), new Col("Tipo", 80), new Col("Data", 160), new Col("Cliente", 150, Expande: true), new Col("Total", 120, true),
                            new Col("Situação", 130, Chip: true), new Col("Detalhe", 170, Expande: true));
        var pasta = K.Botao("Abrir pasta dos XML", Seguro(AbrirPasta), icone: "pasta", classe: "fantasma");
        var baixo = K.Ferramentas(rodape, pasta);

        botaoConfig = K.Botao("Configurar agora", SeguroAsync(ConfigurarAsync), classe: "primario");
        var linhaFaixa = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,12,*,16,Auto") };
        var iconeFaixa = Icones.Criar("info", 20, "#D97706"); iconeFaixa.VerticalAlignment = VerticalAlignment.Center;
        botaoConfig.VerticalAlignment = VerticalAlignment.Center;
        K.ColocarNaGrade(linhaFaixa, iconeFaixa, 0, 0); K.ColocarNaGrade(linhaFaixa, lblConfig, 0, 2); K.ColocarNaGrade(linhaFaixa, botaoConfig, 0, 4);
        faixaConfig = new Border
        {
            Background = Cores.P("#FFFBEB"), BorderBrush = Cores.P("#FDE68A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 12),
            Margin = new Thickness(0, 0, 0, 18), Child = linhaFaixa,
        };
        Content = K.Doca(faixaConfig, K.Lista(Tabela, K.Ferramentas(filtros, botoes), baixo,
            "Nenhuma nota fiscal neste período.\nPara emitir: depois da venda, clique em “Nota fiscal”, ou na lista de Vendas.", "notas"));
        Tabela.Duplo += SeguroAsync(VerAsync);
    }

    public override void Atualizar()
    {
        var cfg = Svc.ObterConfigFiscal();
        var p = cfg.Pendencias(65, verificarCertificado: false);
        faixaConfig.IsVisible = p.Count > 0 || !cfg.Producao;
        lblConfig.Text = p.Count > 0 ? $"A nota fiscal ainda não está configurada ({p.Count} item(ns) faltando). Leva uns minutos: dados da empresa, certificado digital e CSC."
                                     : "Você está no ambiente de HOMOLOGAÇÃO: as notas são de teste, sem valor fiscal. Quando estiver tudo certo, mude para produção na configuração.";
        botaoConfig.Content = p.Count > 0 ? "Configurar agora" : "Abrir configuração";
        notas = Svc.ListarNotasFiscais(Util.ParseDataBr(De.Text, "Data inicial"), Util.ParseDataBr(Ate.Text, "Data final"));
        Tabela.Definir(notas.Select(n => new Linha
        {
            Id = n.Id.ToString(), Estilo = n.Status is "CANCELADA" or "INUTILIZADA" ? "cancelado" : n.Status is "REJEITADA" or "DENEGADA" ? "alerta" : "",
            Fabrica = () => new[]
            {
                n.Numero.ToString(), n.Tipo, Util.FmtData(n.CriadaEm), n.DestNome.Length > 0 ? n.DestNome : n.DestDoc.Length > 0 ? Util.FmtDocumento(n.DestDoc) : "Consumidor",
                Util.FmtDinheiro(n.Total), Situacao(n.Status), (n.Homologacao ? "Teste · " : "") + (n.Autorizada ? $"Protocolo {n.Protocolo}" : n.Motivo),
            },
        }));
        var aut = notas.Where(n => n.Autorizada && !n.Homologacao).ToList();
        rodape.Text = $"{notas.Count} nota(s) no período  ·  {aut.Count} autorizada(s) em produção — {Util.FmtDinheiro(aut.Sum(n => n.Total))}";
    }

    public static string Situacao(string status) => status switch
    {
        "AUTORIZADA" => "Autorizada", "PENDENTE" => "Pendente", "REJEITADA" => "Rejeitada", "DENEGADA" => "Denegada",
        "CANCELADA" => "Cancelada", "INUTILIZADA" => "Inutilizada", _ => status,
    };

    NotaFiscal Selecionada() => notas.FirstOrDefault(n => n.Id == Exigir(Tabela.IdLong, "uma nota")) ?? throw new ValidacaoException("Selecione uma nota na lista.");

    public async Task ConfigurarAsync()
    {
        if (await ConfigFiscalDialog.Abrir(Janela, Svc)) AtualizarTudo();
    }

    public async Task VerAsync()
    {
        var n = Selecionada();
        if (n.Xml.Length == 0) { Dica("Esta nota ainda não foi montada. Use “Tentar de novo”."); return; }
        await Fluxos.AbrirDanfeAsync(Janela, n);
    }

    public void AbrirXml()
    {
        var n = Selecionada();
        if (n.Xml.Length == 0) { Dica("Esta nota ainda não tem XML."); return; }
        var arq = Svc.SalvarArquivoXml(n, n.Xml, n.Autorizada || n.Status == "CANCELADA" ? "procNFe" : "nao-autorizada");
        if (!Plat.Atual.Abrir(Path.GetDirectoryName(arq)!)) _ = Info($"O XML está em:\n{arq}", "XML da nota");
    }

    void AbrirPasta()
    {
        var pasta = Config.Subpasta("notas-fiscais");
        if (!Plat.Atual.Abrir(pasta)) _ = Info($"Os XML ficam em:\n{pasta}", "Pasta das notas");
    }

    /// <summary>Pendente: pergunta à SEFAZ / reenvia. Rejeitada: abre a tela de emissão para corrigir.</summary>
    public async Task ReenviarAsync()
    {
        var n = Selecionada();
        if (n.Status == "REJEITADA" || (n.Status == "PENDENTE" && n.Chave.Length == 0)) { await Fluxos.NotaFiscalAsync(Janela, App, n.VendaId, n.Modelo); return; }
        if (n.Status != "PENDENTE") { Dica($"Esta nota está {Situacao(n.Status).ToLowerInvariant()}: não há o que reenviar."); return; }
        var r = await Task.Run(() => Svc.ConsultarNota(n.Id));
        AtualizarTudo();
        if (r.Autorizada) { Sucesso(r.Mensagem); await Fluxos.AbrirDanfeAsync(Janela, Svc.ObterNotaFiscal(n.Id)); }
        else if (r.Mensagem.StartsWith("217")) await Fluxos.NotaFiscalAsync(Janela, App, n.VendaId, n.Modelo);   // a SEFAZ não recebeu: envia de novo
        else await Info($"Situação na SEFAZ: {r.Mensagem}", "Consulta");
    }

    public async Task CancelarAsync()
    {
        var n = Selecionada();
        if (!n.Autorizada) { Dica("Só dá para cancelar nota autorizada."); return; }
        var just = await Plat.Atual.Perguntar(Janela, "Cancelar nota fiscal",
            $"{n.Tipo} nº {n.Numero} — {Util.FmtDinheiro(n.Total)}\n\nO prazo é curto (NFC-e: cerca de 30 minutos; NF-e: 24 horas, na maioria dos estados).\n\nMotivo do cancelamento (mínimo 15 letras):");
        if (just == null) return;
        await Task.Run(() => Svc.CancelarNota(n.Id, just));
        AtualizarTudo();
        if (await Confirmar("Nota cancelada na SEFAZ.\n\nCancelar também a venda (os produtos voltam ao estoque)?", "Nota cancelada"))
        {
            Svc.CancelarVenda(n.VendaId, "Nota fiscal cancelada: " + just.Trim());
            AtualizarTudo();
            Sucesso("Venda cancelada e produtos devolvidos ao estoque.");
        }
    }

    public async Task InutilizarAsync()
    {
        var n = Selecionada();
        if (!n.PodeReenviar) { Dica("Só dá para inutilizar o número de uma nota rejeitada ou pendente."); return; }
        var just = await Plat.Atual.Perguntar(Janela, "Inutilizar número",
            $"Avisa a SEFAZ que o número {n.Numero} da {n.Tipo} não será usado (quando a nota foi rejeitada e você desistiu dela).\n\nMotivo (mínimo 15 letras):");
        if (just == null) return;
        await Task.Run(() => Svc.InutilizarNota(n.Id, just));
        AtualizarTudo();
        Sucesso($"Número {n.Numero} inutilizado.");
    }
}
