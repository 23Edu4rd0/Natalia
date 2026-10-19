using Microsoft.Data.Sqlite;

namespace GestaoComercial.Core;

/// <summary>Conexão SQLite e criação/migração do esquema (idêntico ao da versão Python).</summary>
public static class Banco
{
    public const int VersaoEsquema = 2;

    public static SqliteConnection Conectar(string caminho)
    {
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = caminho,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,       // libera o arquivo de verdade ao fechar (importante no Windows)
                DefaultTimeout = 10,   // espera até 10 s se outra janela estiver gravando
                ForeignKeys = true,
            }.ToString();
            var conn = new SqliteConnection(cs);
            conn.Open();
            return conn;
        }
        catch (SqliteException ex)
        {
            throw new AppException($"Não foi possível abrir o banco de dados em:\n{caminho}\n\n{ex.Message}");
        }
    }

    public static void Migrar(SqliteConnection conn)
    {
        long versao;
        using (var c = conn.CreateCommand()) { c.CommandText = "PRAGMA user_version"; versao = (long)(c.ExecuteScalar() ?? 0L); }
        if (versao > VersaoEsquema)
            throw new AppException("Este banco de dados foi criado por uma versão mais nova do programa. Atualize o programa para abri-lo.");
        if (versao < 1) Rodar(conn, EsquemaV1);
        if (versao < 2) Rodar(conn, EsquemaV2);
    }

    static void Rodar(SqliteConnection conn, string sql)
    {
        using var c = conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    /// <summary>v2: fiado (recebimentos de clientes) e movimentos manuais do caixa (suprimento/sangria).</summary>
    const string EsquemaV2 = """
BEGIN;
CREATE TABLE recebimentos (
    id            INTEGER PRIMARY KEY,
    data          TEXT NOT NULL,
    cliente_id    INTEGER NOT NULL REFERENCES clientes(id),
    valor         INTEGER NOT NULL CHECK (valor > 0),
    forma         TEXT NOT NULL,
    obs           TEXT NOT NULL DEFAULT '',
    status        TEXT NOT NULL DEFAULT 'ATIVO' CHECK (status IN ('ATIVO', 'ESTORNADO')),
    motivo_estorno TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_recebimentos_cliente ON recebimentos(cliente_id);
CREATE INDEX ix_recebimentos_data ON recebimentos(data);

CREATE TABLE caixa_movs (
    id     INTEGER PRIMARY KEY,
    data   TEXT NOT NULL,
    tipo   TEXT NOT NULL CHECK (tipo IN ('SUPRIMENTO', 'SANGRIA')),
    valor  INTEGER NOT NULL CHECK (valor > 0),
    motivo TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_caixa_movs_data ON caixa_movs(data);

PRAGMA user_version = 2;
COMMIT;
""";

    const string EsquemaV1 = """
BEGIN;
CREATE TABLE config (chave TEXT PRIMARY KEY, valor TEXT NOT NULL);

CREATE TABLE categorias (
    id   INTEGER PRIMARY KEY,
    nome TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE produtos (
    id           INTEGER PRIMARY KEY,
    codigo       TEXT NOT NULL UNIQUE COLLATE NOCASE,
    nome         TEXT NOT NULL,
    categoria_id INTEGER REFERENCES categorias(id),
    unidade      TEXT NOT NULL DEFAULT 'un',
    preco_custo  INTEGER NOT NULL DEFAULT 0 CHECK (preco_custo >= 0),
    preco_venda  INTEGER NOT NULL DEFAULT 0 CHECK (preco_venda >= 0),
    estoque      INTEGER NOT NULL DEFAULT 0 CHECK (estoque >= 0),
    estoque_min  INTEGER NOT NULL DEFAULT 0 CHECK (estoque_min >= 0),
    ativo        INTEGER NOT NULL DEFAULT 1 CHECK (ativo IN (0, 1)),
    criado_em    TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
CREATE INDEX ix_produtos_categoria ON produtos(categoria_id);
CREATE INDEX ix_produtos_nome ON produtos(nome COLLATE NOCASE);

CREATE TABLE clientes (
    id        INTEGER PRIMARY KEY,
    nome      TEXT NOT NULL,
    documento TEXT NOT NULL DEFAULT '',
    telefone  TEXT NOT NULL DEFAULT '',
    email     TEXT NOT NULL DEFAULT '',
    endereco  TEXT NOT NULL DEFAULT '',
    ativo     INTEGER NOT NULL DEFAULT 1 CHECK (ativo IN (0, 1)),
    criado_em TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
CREATE UNIQUE INDEX ux_clientes_documento ON clientes(documento) WHERE documento <> '';

CREATE TABLE fornecedores (
    id        INTEGER PRIMARY KEY,
    nome      TEXT NOT NULL,
    documento TEXT NOT NULL DEFAULT '',
    telefone  TEXT NOT NULL DEFAULT '',
    email     TEXT NOT NULL DEFAULT '',
    endereco  TEXT NOT NULL DEFAULT '',
    ativo     INTEGER NOT NULL DEFAULT 1 CHECK (ativo IN (0, 1)),
    criado_em TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
CREATE UNIQUE INDEX ux_fornecedores_documento ON fornecedores(documento) WHERE documento <> '';

CREATE TABLE entradas (
    id            INTEGER PRIMARY KEY,
    data          TEXT NOT NULL,
    fornecedor_id INTEGER NOT NULL REFERENCES fornecedores(id),
    numero_doc    TEXT NOT NULL DEFAULT '',
    obs           TEXT NOT NULL DEFAULT '',
    total         INTEGER NOT NULL CHECK (total >= 0),
    status        TEXT NOT NULL DEFAULT 'ATIVA' CHECK (status IN ('ATIVA', 'CANCELADA')),
    motivo_cancelamento TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_entradas_data ON entradas(data);
CREATE INDEX ix_entradas_fornecedor ON entradas(fornecedor_id);

CREATE TABLE entrada_itens (
    id         INTEGER PRIMARY KEY,
    entrada_id INTEGER NOT NULL REFERENCES entradas(id) ON DELETE CASCADE,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    quantidade INTEGER NOT NULL CHECK (quantidade > 0),
    custo_unit INTEGER NOT NULL CHECK (custo_unit >= 0)
);
CREATE INDEX ix_entrada_itens_entrada ON entrada_itens(entrada_id);
CREATE INDEX ix_entrada_itens_produto ON entrada_itens(produto_id);

CREATE TABLE vendas (
    id              INTEGER PRIMARY KEY,
    data            TEXT NOT NULL,
    cliente_id      INTEGER REFERENCES clientes(id),
    cliente_nome    TEXT NOT NULL,
    cliente_doc     TEXT NOT NULL DEFAULT '',
    subtotal        INTEGER NOT NULL CHECK (subtotal >= 0),
    desconto        INTEGER NOT NULL DEFAULT 0 CHECK (desconto >= 0),
    total           INTEGER NOT NULL CHECK (total >= 0),
    forma_pagamento TEXT NOT NULL,
    obs             TEXT NOT NULL DEFAULT '',
    status          TEXT NOT NULL DEFAULT 'CONCLUIDA' CHECK (status IN ('CONCLUIDA', 'CANCELADA')),
    cancelada_em    TEXT,
    motivo_cancelamento TEXT NOT NULL DEFAULT '',
    emitente_json   TEXT NOT NULL DEFAULT '{}'
);
CREATE INDEX ix_vendas_data ON vendas(data);
CREATE INDEX ix_vendas_cliente ON vendas(cliente_id);

CREATE TABLE venda_itens (
    id         INTEGER PRIMARY KEY,
    venda_id   INTEGER NOT NULL REFERENCES vendas(id) ON DELETE CASCADE,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    codigo     TEXT NOT NULL,
    descricao  TEXT NOT NULL,
    unidade    TEXT NOT NULL DEFAULT 'un',
    quantidade INTEGER NOT NULL CHECK (quantidade > 0),
    preco_unit INTEGER NOT NULL CHECK (preco_unit >= 0),
    custo_unit INTEGER NOT NULL DEFAULT 0 CHECK (custo_unit >= 0)
);
CREATE INDEX ix_venda_itens_venda ON venda_itens(venda_id);
CREATE INDEX ix_venda_itens_produto ON venda_itens(produto_id);

CREATE TABLE movimentos (
    id         INTEGER PRIMARY KEY,
    data       TEXT NOT NULL,
    produto_id INTEGER NOT NULL REFERENCES produtos(id),
    tipo       TEXT NOT NULL CHECK (tipo IN
               ('ENTRADA', 'VENDA', 'CANCELAMENTO_VENDA', 'CANCELAMENTO_ENTRADA', 'AJUSTE')),
    quantidade INTEGER NOT NULL CHECK (quantidade <> 0),
    saldo_apos INTEGER NOT NULL,
    referencia TEXT NOT NULL DEFAULT '',
    obs        TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_movimentos_produto ON movimentos(produto_id, id);

PRAGMA user_version = 1;
COMMIT;
""";
}
