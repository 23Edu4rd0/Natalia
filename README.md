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

**Novidades da 2.6: contingência offline da NFC-e (vender sem internet)**
- **Caiu a internet ou a SEFAZ saiu do ar?** A NFC-e sai mesmo assim, **em contingência offline** (tipo de emissão 9): o cupom é impresso na hora com “EMITIDA EM CONTINGÊNCIA – Pendente de autorização” e o QR Code de contingência, e vale para o cliente.
- **Envio sozinho**: o programa tenta mandar as notas guardadas a cada 3 minutos (e ao abrir). Quando a SEFAZ responde, elas viram *Autorizada* e o programa volta ao normal. Também dá para clicar em **Enviar agora** na aba *Notas fiscais*.
- **Sem fila parada**: depois da primeira falha, as próximas NFC-e saem direto em contingência, sem esperar a SEFAZ.
- **Sem nota em duplicidade**: se uma nota tinha sido enviada e a resposta não voltou, a contingência usa outro número. Quando a internet volta, o programa confere aquela: se a SEFAZ tinha autorizado, ele faz o **cancelamento por substituição**; se nunca recebeu, **inutiliza** o número. Tudo automático.
- **Recusada depois?** A nota fica marcada na lista; corrija (ex.: NCM) e envie de novo: continua com o **mesmo número, chave e data** do cupom que o cliente levou.
- Aviso na aba *Notas fiscais* e selo no menu com quantas faltam enviar, e alerta quando a mais antiga se aproxima do **prazo de 24 h**.
- Pode ser desligada em *Configurar nota fiscal → NFC-e*. A **NF-e** (modelo 55) não tem contingência offline (a lei só permite com formulário de segurança): sem internet ela fica *pendente* para reenviar.

**Novidades da 2.5: nota fiscal eletrônica de verdade (NFC-e e NF-e)**
- **Emite NFC-e** (cupom fiscal, modelo 65) e **NF-e** (modelo 55) direto para a SEFAZ, com o certificado digital A1 da loja. Layout 4.00, XML assinado e conferido nos esquemas oficiais antes de enviar.
- **O que falta, a pessoa completa na hora**: na tela *Emitir nota fiscal* vem tudo preenchido com o que o programa já sabe; o que faltar (NCM do produto, CPF/CNPJ, endereço do cliente na NF-e, inscrição estadual…) fica **em vermelho** para preencher ali mesmo. Com *“Guardar nos cadastros”* marcado, da próxima vez já vem preenchido.
- **Onde emitir**: botão **Nota fiscal** no aviso depois da venda e na lista de **Vendas**. Ou, em *Configurações → Ao finalizar uma venda*, escolha *“Emitir a NFC-e na hora”*: se não faltar nada, sai sozinha e abre o cupom; se faltar, abre a tela para completar.
- **DANFE** para imprimir ou salvar em PDF: cupom 80 mm com QR Code (NFC-e) e folha A4 com código de barras da chave (NF-e).
- **Aba Notas fiscais**: reimprimir, abrir o XML (fica guardado para o contador em `notas-fiscais/AAAA-MM/`), **tentar de novo / consultar** nota sem resposta, **cancelar** nota (e depois a venda, devolvendo ao estoque) e **inutilizar** número de nota rejeitada.
- **Sem internet ou SEFAZ fora do ar**: a nota fica *pendente* com o mesmo número; ao tentar de novo, o programa primeiro pergunta à SEFAZ se ela já foi autorizada (nunca gera nota em duplicidade).
- **Configuração em uma tela** (*Configurações → Nota fiscal eletrônica*): “Buscar dados” preenche razão social, endereço e regime pelo CNPJ; “Buscar CEP” preenche o código IBGE; teste de comunicação com a SEFAZ; numeração que pode continuar de outro programa. Começa em **homologação** (notas de teste, sem valor fiscal).
- Simples Nacional / MEI (CSOSN) e regime normal (CST com ICMS, PIS e COFINS); tributos aproximados (Lei 12.741); Pix, cartão, dinheiro e fiado; CNPJ alfanumérico (2026).

**O que a loja precisa para emitir de verdade**
1. **Certificado digital A1** (arquivo `.pfx` + senha), comprado numa certificadora. *Certificado A3 (cartão/token) não funciona.*
2. **Inscrição estadual** ativa e a empresa **credenciada para emitir NFC-e/NF-e** na SEFAZ do estado (o contador faz/confirma).
3. Para NFC-e: o **CSC** e o **ID do CSC**, gerados no site da SEFAZ do estado.
4. O **NCM** de cada produto (está na nota do fornecedor ou o contador informa) e a tributação certa (CSOSN/CST, CFOP) — **confira com o contador**.
5. Emitir algumas notas em **homologação**, conferir, e só então mudar para **produção** na configuração.

**Limites conhecidos (desta versão)**
- Não foi testado contra a SEFAZ real neste desenvolvimento (precisa do certificado e do CSC da loja): os testes usam uma SEFAZ simulada, mas o XML é validado nos esquemas oficiais.
- **NF-e** sem internet fica pendente (não há contingência offline para ela; as contingências SVC e EPEC, que dependem de internet, não estão no programa). A **NFC-e** tem contingência offline desde a 2.6.
- Em contingência, entregue o **cupom impresso** ao cliente e mantenha a internet funcionando para o envio dentro do prazo (24 h na maioria dos estados).
- Não calcula **DIFAL** (venda a consumidor de outro estado no regime normal: o programa avisa e não emite), nem substituição tributária com valores, nem os campos novos da reforma tributária (IBS/CBS).
- Não envia a nota por e-mail sozinho (o XML e o DANFE ficam na pasta para enviar).

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

> ⚠️ O **comprovante de venda** (botões *Nota* / *Cupom*) é interno e **não tem valor fiscal**. A nota fiscal de verdade é a **NFC-e / NF-e**, emitida pelo botão **Nota fiscal** (veja acima o que a loja precisa).

## Onde ficam os dados
Windows: `%APPDATA%\GestaoComercial` — Linux: `~/.local/share/gestao-comercial` (troque com a variável `GESTAO_DATA_DIR`).
Dentro: `gestao.db`, `backups/`, `notas/`, `notas-fiscais/` (XML das NFC-e/NF-e, para o contador), `certificado/`, `exportacoes/` e `logs/gestao.log`.
A senha do certificado digital não fica em texto puro (no Windows é protegida pelo próprio sistema, para o seu usuário).
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
tests/GestaoComercial.Tests  225 testes: regras + interface renderizada em memória   →   dotnet test
```

## Bibliotecas de terceiros
- [Hercules.NET / ZeusFiscal](https://github.com/Hercules-NET/ZeusFiscal) (LGPL-2.1): montagem, assinatura e comunicação da NF-e/NFC-e com a SEFAZ. O código-fonte dela está no link; ela é usada como biblioteca, sem alterações.
- [QRCoder](https://github.com/codebude/QRCoder) (MIT): QR Code da NFC-e.
- [Avalonia UI](https://avaloniaui.net/) (MIT), Microsoft.Data.Sqlite (MIT).
- Esquemas XML oficiais da NF-e (Portal Nacional da NF-e).
