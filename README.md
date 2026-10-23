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

Os executáveis prontos estão na pasta [`download/`](download/) — um arquivo só, não precisa instalar .NET nem Python:

- **Windows:** [`download/GestaoComercial-Windows.zip`](download/GestaoComercial-Windows.zip) → extraia e dê dois cliques em `GestaoComercial.exe`
- **Linux:** [`download/GestaoComercial-Linux.tar.gz`](download/GestaoComercial-Linux.tar.gz) → `tar -xzf` e execute `./GestaoComercial`

(Ao gerar de novo com `./publicar.sh`, os arquivos saem em `dist/`.)

> Na primeira vez o Windows pode mostrar o aviso "SmartScreen" (programa novo, sem assinatura digital):
> clique em *Mais informações → Executar assim mesmo*.

## Como gerar os executáveis de novo
Precisa do [.NET SDK 8](https://dotnet.microsoft.com/download).
- `./publicar.sh` (Linux/macOS/Git Bash) gera **Windows e Linux** de uma vez em `dist/`
- `publicar.bat` (Windows) gera o `.exe`
- `./executar.sh` / `executar.bat`: roda direto do código, para desenvolvimento
- `COMPRIMIR=1 ./publicar.sh`: arquivo ~55 MB em vez de ~120 MB, porém abre ~2x mais devagar

## O que ele faz

**Novidades da 2.4 (visual novo)**
- **Visual redesenhado**: menu lateral claro com o nome da loja, cartões com números grandes, listas com busca e ações no mesmo cartão, campos com destaque ao digitar, formulários e mensagens mais limpos. Funciona bem em tela grande (1920) e em notebook.
- **Sugestões enquanto digita o produto** (na venda e na entrada): aparece a lista com nome, código, preço e estoque; setas ↑ ↓ e Enter adicionam, sem precisar saber o código. Não liga para acentos ("calca" acha "Calça").
- **Venda mais rápida**: formas de pagamento em blocos de um clique, total e botão **Finalizar (F9)** sempre visíveis, mais vendidos em blocos.
- **Painel** com gráfico das vendas dos últimos 14 dias e atalhos; **Relatórios** com cartões (faturamento, lucro, ticket médio) e barras por forma de pagamento; **Configurações** em duas colunas.

**Modo simples e modo avançado** (Configurações → *Modo de uso*; o programa abre no **simples**)
- **Simples** (padrão): menu com só *Painel, Nova venda, Vendas, A receber, Clientes, Produtos e Configurações*. Cadastro de produto com nome, tipo, preço e quantidade. Na venda, desconto/troco/observação ficam atrás de um botão. O estoque se mexe na lista de produtos (*Chegou mercadoria* e *Corrigir estoque*), sem fornecedor.
- **Avançado**: tudo — Caixa, Fornecedores, Estoque por tipo, Entrada de mercadorias, Relatórios, custo/margem/código/estoque mínimo, reajuste de preços e lucro no painel.
- A troca é na hora e os dados não mudam; o programa lembra a escolha.

**Novidades da 2.2 (menos cliques, mais rápido)**
- **Venda em poucos cliques**: sem janela de confirmação; ao finalizar aparece um aviso com **Desfazer**, **Imprimir nota**, **Cupom 80 mm** e **WhatsApp**. O que acontece após a venda é configurável (Configurações).
- **Digitou o código, já entrou**: leitor de código de barras adiciona na hora; `3x blusa` vende 3 de uma vez; produto não cadastrado pode ser cadastrado ali mesmo.
- **Cliente por digitação** (com aviso se já deve no fiado), atalhos de **5% / 10%** e **valor recebido** (valor exato, R$ 20/50/100).
- **Importar produtos do Excel/CSV** (com planilha modelo; atualiza por código e aponta a linha de cada erro), **restaurar backup** pelo programa e **margem de lucro ↔ preço** calculada enquanto digita.
- **Primeiros passos** na tela inicial para quem acabou de instalar, mensagens nas listas vazias e erros no próprio formulário.
- **Mais rápido**: telas carregadas só quando abertas e atualizadas só quando os dados mudam, busca com atraso inteligente (sem travar ao digitar), banco em modo WAL e backup diário em segundo plano.

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
tests/GestaoComercial.Tests  177 testes: regras + interface renderizada em memória   →   dotnet test
```
