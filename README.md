# Gestão Comercial (versão C#)

Programa de desktop para **Windows e Linux** com o controle básico de **produtos, estoque, entrada de mercadorias
de fornecedores, vendas e notas**. É a versão em **C# (.NET 8) + Avalonia UI** do programa que antes era em Python:
mesmas telas, mesmas regras e **o mesmo banco de dados** (`gestao.db`), então os dados antigos continuam valendo.

| Anotação                           | No programa                                                              |
|------------------------------------|--------------------------------------------------------------------------|
| Fornecedor = mercadorias (entrada) | Aba **Entrada de mercadorias** (soma no estoque, atualiza o custo)       |
| Venda → cliente                    | Aba **Nova venda** (baixa o estoque e gera a **nota**)                   |
| Estoque → (tipo de produto)        | Aba **Estoque**, agrupado por **tipo de produto**                        |

## Como usar (sem instalar nada)

Os executáveis prontos ficam em `dist/` — um arquivo só, não precisa instalar .NET nem Python:

- **Windows:** `dist/windows-x64/GestaoComercial.exe` (dois cliques)
- **Linux:** `dist/linux-x64/GestaoComercial` (`chmod +x` se necessário e execute)

> Na primeira vez o Windows pode mostrar o aviso "SmartScreen" (programa novo, sem assinatura digital):
> clique em *Mais informações → Executar assim mesmo*.

## Como gerar os executáveis de novo
Precisa do [.NET SDK 8](https://dotnet.microsoft.com/download).
- `./publicar.sh` (Linux/macOS/Git Bash) gera **Windows e Linux** de uma vez em `dist/`
- `publicar.bat` (Windows) gera o `.exe`
- `./executar.sh` / `executar.bat`: roda direto do código, para desenvolvimento
- `COMPRIMIR=1 ./publicar.sh`: arquivo ~55 MB em vez de ~120 MB, porém abre ~2x mais devagar

## O que ele faz

**Vender**
- **Nova venda**: busque por código, código de barras (leitor funciona) ou nome, ou clique nos botões dos **produtos mais vendidos**. Desconto em **R$ ou %** (ex.: `10%`), forma de pagamento, **troco** automático para pagamentos em dinheiro e **cadastro rápido de cliente** na hora. Atalhos: **F2** nova venda, **F9** finalizar.
- **Fiado**: escolha o cliente e a forma "Fiado". Em **A receber** você vê quem deve, registra pagamentos (total ou parcial), consulta o **extrato**, **cobra por WhatsApp** com mensagem pronta e imprime o extrato.
- **Vendas**: lista com filtros; ver nota, **imprimir / PDF**, **cupom 80 mm** (impressora térmica), **enviar a nota por WhatsApp**, cancelar (devolve ao estoque) e exportar para Excel.
- **Caixa**: troco inicial, retiradas (sangria), total por forma de pagamento, **dinheiro que deve ter na gaveta** e fechamento do dia para imprimir.

**Cadastros e estoque**
- **Clientes** (com histórico de compras e saldo no fiado), **Fornecedores** e **Produtos** (código, tipo, custo, preço, estoque mínimo).
- **Reajuste de preços em %** para todos os produtos ou só de um tipo.
- **Estoque** por tipo de produto, ajuste de inventário, histórico de movimentos e **lista de reposição** (com sugestão de compra) para imprimir ou exportar.
- **Entrada de mercadorias** de fornecedores: soma no estoque e atualiza o custo; pode ser cancelada.

**Acompanhar**: **Início** com os números do dia e atalhos, **Relatórios** (faturamento, ticket médio, lucro, mais vendidos, formas de pagamento) e **backup** automático diário.

> ⚠️ **As notas são comprovantes internos e NÃO têm valor fiscal.** NF-e / NFC-e exigem certificado digital e emissor autorizado pela SEFAZ.

## Onde ficam os dados
Windows: `%APPDATA%\GestaoComercial` — Linux: `~/.local/share/gestao-comercial` (troque com a variável `GESTAO_DATA_DIR`).
Dentro: `gestao.db`, `backups/`, `notas/`, `exportacoes/` e `logs/gestao.log`.
**Atualizando:** bancos de versões anteriores (inclusive os da versão Python) são atualizados sozinhos na primeira abertura, sem perder nada. Depois disso, não abra o mesmo banco na versão Python. Para levar para outro PC, copie o `gestao.db`.

## Como lida com erros
- Venda/entrada/cancelamento rodam em **transação**: se algo falhar (ex.: estoque insuficiente), **nada** é gravado. O próprio banco recusa estoque negativo.
- Dinheiro em **centavos inteiros** (sem erro de arredondamento). Campos inválidos mostram mensagem clara e o formulário continua aberto.
- Produto/cliente/fornecedor com histórico é **inativado**, não apagado; a nota guarda cópia do nome, preços e dados da empresa da época.
- Banco ocupado, disco cheio, arquivo corrompido, falta de permissão: mensagens em português; detalhes em `logs/gestao.log`. Erro inesperado em um botão não fecha o programa.

## Para desenvolvedores
```
src/GestaoComercial.Core   regras de negócio e SQLite (sem interface)
src/GestaoComercial.App    telas (Avalonia)
tests/GestaoComercial.Tests  125 testes: regras + interface renderizada em memória   →   dotnet test
```
