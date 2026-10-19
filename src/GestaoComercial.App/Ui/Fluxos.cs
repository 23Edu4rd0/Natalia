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
}
