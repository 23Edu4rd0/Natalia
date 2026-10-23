namespace GestaoComercial.Core;

public record Categoria(long Id, string Nome, long Produtos);

public record Pessoa(long Id, string Nome, string Documento, string Telefone, string Email, string Endereco, bool Ativo);

public record PessoaDados(string Nome, string? Documento = null, string? Telefone = null, string? Email = null,
                          string? Endereco = null, bool Ativo = true);

public record Produto(long Id, string Codigo, string Nome, long? CategoriaId, string Categoria, string Unidade,
                      long PrecoCusto, long PrecoVenda, long Estoque, long EstoqueMin, bool Ativo)
{
    /// <summary>Estoque no mínimo ou abaixo dele (inclui zerado).</summary>
    public bool Baixo => Estoque <= EstoqueMin;
}

public record ProdutoDados(string Nome, string? Codigo = null, long? CategoriaId = null, string? Unidade = null,
                           long PrecoCusto = 0, long PrecoVenda = 0, long EstoqueMin = 0, long EstoqueInicial = 0,
                           bool Ativo = true);

public record Empresa(string Nome, string Documento, string Endereco, string Telefone, string Rodape)
{
    public static Empresa Padrao => new("Minha Empresa", "", "", "", "Obrigado pela preferência!");
}

public record Movimento(long Id, string Data, long ProdutoId, string Codigo, string Nome, string Tipo,
                        long Quantidade, long SaldoApos, string Referencia, string Obs);

public record ItemEntradaInput(long ProdutoId, long Quantidade, long CustoUnit);
public record ItemVendaInput(long ProdutoId, long Quantidade, long PrecoUnit);

public record EntradaItem(long Id, long ProdutoId, string Codigo, string Nome, string Unidade, long Quantidade, long CustoUnit);

public record Entrada(long Id, string Data, long FornecedorId, string Fornecedor, string NumeroDoc, string Obs,
                      long Total, string Status, string MotivoCancelamento, List<EntradaItem> Itens)
{
    public bool Cancelada => Status == "CANCELADA";
}

public record VendaItem(long Id, long ProdutoId, string Codigo, string Descricao, string Unidade, long Quantidade,
                        long PrecoUnit, long CustoUnit)
{
    public long Total => Quantidade * PrecoUnit;
}

public record Venda(long Id, string Data, long? ClienteId, string ClienteNome, string ClienteDoc, long Subtotal,
                    long Desconto, long Total, string FormaPagamento, string Obs, string Status, string? CanceladaEm,
                    string MotivoCancelamento, Empresa Emitente, List<VendaItem> Itens)
{
    public bool Cancelada => Status == "CANCELADA";
}

public record ResumoVendas(long Qtd, long Total, long Descontos, long Custo, long CanceladasQtd, long CanceladasTotal)
{
    public long Lucro => Total - Custo;
    public long TicketMedio => Qtd > 0 ? Total / Qtd : 0;
}

/// <summary>Dados fiscais do produto (vazio = usa o padrão da configuração fiscal).</summary>
public record FiscalProduto(long ProdutoId, string Ncm, string Cest, int Origem, string Cfop, string Tributacao);

/// <summary>Endereço/inscrição do cliente usados na NF-e (guardados para a próxima nota).</summary>
public record FiscalCliente(long ClienteId, string IE, int IndIE, string Logradouro, string Numero, string Complemento, string Bairro,
                            string CodMunicipio, string Municipio, string Uf, string Cep);

/// <summary>Nota fiscal eletrônica emitida (ou tentada) para uma venda. Modelo 65 = NFC-e, 55 = NF-e.</summary>
public record NotaFiscal(long Id, long VendaId, int Modelo, int Serie, long Numero, int Ambiente, string Status, string Chave, string Protocolo,
                         int CStat, string Motivo, string CriadaEm, string EmitidaEm, string DestDoc, string DestNome, long Total, string Xml, string XmlEvento)
{
    public string Tipo => Modelo == 65 ? "NFC-e" : "NF-e";
    public bool Homologacao => Ambiente == 2;
    public bool Autorizada => Status == "AUTORIZADA";
    /// <summary>Pode tentar de novo com o mesmo número (nunca foi aceita pela SEFAZ).</summary>
    public bool PodeReenviar => Status is "PENDENTE" or "REJEITADA";
}

/// <summary>Vendas concluídas de um dia (Data em aaaa-mm-dd).</summary>
public record VendaDia(string Data, long Qtd, long Total);

public record MaisVendido(string Codigo, string Descricao, long Quantidade, long Receita);
public record PorPagamento(string Forma, long Qtd, long Total);
public record ResumoCategoria(string Categoria, long Produtos, long Unidades, long ValorCusto, long ValorVenda, long Baixos);
public record EstoquePainel(long Produtos, long ValorCusto, long ValorVenda, long Baixos);

public record Painel(ResumoVendas Hoje, ResumoVendas Mes, EstoquePainel Estoque, List<Produto> BaixoEstoque, List<Venda> Recentes);

public enum TipoPessoa { Cliente, Fornecedor }

public record Devedor(long ClienteId, string Nome, string Telefone, long Saldo, string? UltimaCompra);
public record Recebimento(long Id, string Data, long ClienteId, string Cliente, long Valor, string Forma, string Obs, string Status, string MotivoEstorno)
{
    public bool Estornado => Status == "ESTORNADO";
}
/// <summary>Linha do extrato de fiado: Valor positivo = compra (aumenta a dívida), negativo = pagamento.</summary>
public record LinhaExtrato(string Data, string Descricao, long Valor, long Saldo);
public record MovCaixa(long Id, string Data, string Tipo, long Valor, string Motivo);
public record ResumoCaixa(string Data, long Suprimentos, long Sangrias, long VendasQtd, long VendasTotal, long FiadoLancado,
                          List<PorPagamento> VendasPorForma, long RecebimentosTotal, List<PorPagamento> RecebimentosPorForma,
                          long DinheiroEsperado, List<MovCaixa> Movimentos);
public record ItemReposicao(Produto Produto, long Sugestao);
