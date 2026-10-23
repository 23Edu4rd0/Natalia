using Avalonia.Controls;
using GestaoComercial.Core;

namespace GestaoComercial.App.Ui;

/// <summary>Ações usadas por mais de uma tela (receber fiado, cliente rápido, WhatsApp, imprimir).</summary>
public static class Fluxos
{
    /// <summary>Abre o formulário de pagamento de fiado do cliente. Retorna true se registrou.</summary>
    public static async Task<bool> ReceberPagamentoAsync(Window? dono, Servico svc, long clienteId)
    {
        var cli = svc.ObterPessoa(TipoPessoa.Cliente, clienteId);
        var saldo = svc.SaldoCliente(clienteId);
        if (saldo <= 0)
        {
            await Plat.Atual.Mensagem(dono, "Receber pagamento", $"{cli.Nome} não tem nada a pagar.", Nivel.Info);
            return false;
        }
        var formas = Servico.FormasRecebimento.ToList();
        var campos = new List<Campo>
        {
            new() { Chave = "valor", Rotulo = "Valor recebido (R$)", Tipo = TipoCampo.Dinheiro, Obrigatorio = true, Dica = $"Dívida atual: {Util.FmtDinheiro(saldo)}. Pode receber só uma parte." },
            new() { Chave = "forma", Rotulo = "Forma de pagamento", Tipo = TipoCampo.Escolha, Opcoes = formas.Select((f, i) => ((long?)i, f)).ToList() },
            new() { Chave = "obs", Rotulo = "Observação" },
        };
        return await FormDialog.Abrir(dono, $"Receber de {cli.Nome}", campos, new() { ["valor"] = saldo, ["forma"] = 0L },
            v => svc.RegistrarRecebimento(clienteId, v.Num("valor"), formas[(int)(v.Escolha("forma") ?? 0)], v.Texto("obs")), "Registrar pagamento");
    }

    /// <summary>Cadastro rápido de cliente (nome e telefone). Retorna o id criado, ou null se cancelou.</summary>
    public static async Task<long?> NovoClienteRapidoAsync(Window? dono, Servico svc)
    {
        long? criado = null;
        var campos = new List<Campo>
        {
            new() { Chave = "nome", Rotulo = "Nome", Obrigatorio = true },
            new() { Chave = "telefone", Rotulo = "Telefone / WhatsApp", Dica = "Com DDD. Usado para enviar nota e cobrança." },
            new() { Chave = "documento", Rotulo = "CPF/CNPJ", Dica = "Opcional." },
        };
        var ok = await FormDialog.Abrir(dono, "Novo cliente", campos, null,
            v => criado = svc.SalvarPessoa(TipoPessoa.Cliente, new PessoaDados(v.Texto("nome"), v.Texto("documento"), v.Texto("telefone"))), "Cadastrar");
        return ok ? criado : null;
    }

    /// <summary>
    /// Cadastro de produto a partir do que foi digitado na busca (nome ou código de barras): evita ter que sair da venda/entrada.
    /// Retorna o produto criado, ou null se cancelou.
    /// </summary>
    public static async Task<Produto?> NovoProdutoAsync(Window? dono, Servico svc, string? busca = null, bool avancado = true)
    {
        var t = (busca ?? "").Trim();
        var pareceCodigo = t.Length > 0 && !t.Contains(' ') && t.Any(char.IsDigit);
        long? criado = null;
        var ok = await FormDialog.Abrir(dono, "Novo produto", FormProduto.Campos(svc, true, avancado),
            FormProduto.Iniciais(null, pareceCodigo ? t : null, pareceCodigo ? null : t),
            v => criado = FormProduto.Salvar(svc, v, null), "Cadastrar", avancado ? FormProduto.AoMudar : null);
        return ok && criado != null ? svc.ObterProduto(criado.Value) : null;
    }

    /// <summary>Abre o WhatsApp (app ou navegador) com a mensagem pronta.</summary>
    public static async Task WhatsAppAsync(Window? dono, string? telefone, string mensagem)
    {
        var link = Nota.LinkWhatsApp(telefone, mensagem);
        if (!Plat.Atual.Abrir(link))
            await Plat.Atual.Mensagem(dono, "WhatsApp", $"Não foi possível abrir o WhatsApp automaticamente.\n\nEndereço:\n{link}", Nivel.Info);
    }

    /// <summary>Grava um HTML na pasta de notas e abre no navegador (para imprimir ou salvar em PDF).</summary>
    public static async Task ImprimirHtmlAsync(Window? dono, string html, string nomeArquivo)
    {
        var caminho = Path.Combine(Config.Subpasta("notas"), nomeArquivo);
        File.WriteAllText(caminho, html, new System.Text.UTF8Encoding(false));
        if (!Plat.Atual.Abrir(caminho))
            await Plat.Atual.Mensagem(dono, "Arquivo salvo",
                $"Não foi possível abrir o navegador automaticamente.\n\nO arquivo foi salvo em:\n{caminho}\n\nAbra-o no navegador para imprimir ou salvar como PDF.", Nivel.Info);
    }

    // ------------------------------------------------------------------ nota fiscal eletrônica
    /// <summary>Abre o DANFE (cupom da NFC-e ou folha da NF-e) no navegador para imprimir/salvar em PDF.</summary>
    public static Task AbrirDanfeAsync(Window? dono, Core.NotaFiscal n) =>
        ImprimirHtmlAsync(dono, Core.Fiscal.Danfe.Html(n), $"danfe-{(n.Chave.Length > 0 ? n.Chave : n.Id.ToString())}.html");

    /// <summary>Modelo sugerido: NF-e quando o cliente da venda tem CNPJ; senão NFC-e.</summary>
    public static int ModeloSugerido(Servico svc, long vendaId) => Util.SomenteDigitos(svc.ObterVenda(vendaId).ClienteDoc).Length == 14 ? 55 : 65;

    /// <summary>
    /// Nota fiscal da venda: se já existe autorizada, abre o DANFE; senão abre a tela de emissão
    /// (com o que faltar para a pessoa completar) e, autorizada, mostra o DANFE.
    /// </summary>
    public static async Task NotaFiscalAsync(Window? dono, MainWindow app, long vendaId, int? modelo = null)
    {
        var svc = app.Svc;
        if (svc.NotasDaVenda(vendaId).FirstOrDefault(n => n.Autorizada) is { } aut) { await AbrirDanfeAsync(dono, aut); return; }
        var r = await EmitirNotaDialog.Abrir(dono, svc, vendaId, modelo ?? ModeloSugerido(svc, vendaId));
        if (r != null) app.AtualizarTudo();
        if (r is { Autorizada: true }) await AvisarAutorizadaAsync(dono, svc, r);
    }

    /// <summary>
    /// Depois da venda: se não falta nada, emite a NFC-e direto (sem abrir janela) e abre o cupom;
    /// se falta algo (NCM de um produto, certificado...), abre a tela de emissão para completar.
    /// </summary>
    public static async Task NfceAposVendaAsync(Window? dono, MainWindow app, long vendaId)
    {
        var svc = app.Svc;
        var d = svc.PrepararNota(vendaId, 65);
        if (Core.Fiscal.RegrasNota.Verificar(d, svc.ObterConfigFiscal()).Count > 0) { await NotaFiscalAsync(dono, app, vendaId, 65); return; }
        Plat.Atual.Toast(dono, "Emitindo a NFC-e…", Nivel.Info, null, 3);
        Core.Fiscal.ResultadoNota r;
        try { r = EmitirNotaDialog.EmSegundoPlano ? await Task.Run(() => svc.EmitirNota(d)) : svc.EmitirNota(d); }
        catch (ValidacaoException) { await NotaFiscalAsync(dono, app, vendaId, 65); return; }
        app.AtualizarTudo();
        if (r.Autorizada) { await AvisarAutorizadaAsync(dono, svc, r); return; }
        Plat.Atual.Toast(dono, r.Mensagem, Nivel.Aviso, new (string, Action)[] { ("Abrir a nota", () => _ = NotaFiscalAsync(dono, app, vendaId, 65)) }, 15);
    }

    static async Task AvisarAutorizadaAsync(Window? dono, Servico svc, Core.Fiscal.ResultadoNota r)
    {
        var n = svc.ObterNotaFiscal(r.NotaId);
        Plat.Atual.Toast(dono, r.Mensagem, Nivel.Info, new (string, Action)[]
        {
            ("Imprimir de novo", () => _ = AbrirDanfeAsync(dono, svc.ObterNotaFiscal(r.NotaId))),
        }, 8);
        await AbrirDanfeAsync(dono, n);
    }
}
