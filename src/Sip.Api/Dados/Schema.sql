-- Esquema do SIP. Roda automaticamente quando a API inicia (idempotente).
-- Campos terminados em _cripto guardam dados criptografados com AES-256-GCM.

CREATE TABLE IF NOT EXISTS colaborador (
    id          SERIAL PRIMARY KEY,
    nome        VARCHAR(100) NOT NULL,
    email       VARCHAR(120) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS usuario (
    id                   SERIAL PRIMARY KEY,
    email                VARCHAR(120) NOT NULL UNIQUE,
    senha_hash           VARCHAR(255) NOT NULL,
    precisa_trocar_senha BOOLEAN NOT NULL DEFAULT FALSE,
    criado_em            TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS processo (
    id              SERIAL PRIMARY KEY,
    colaborador_id  INTEGER NOT NULL REFERENCES colaborador(id) ON DELETE CASCADE,
    data_analise    TIMESTAMP NOT NULL,
    descricao       TEXT NOT NULL DEFAULT '',
    status          VARCHAR(20) NOT NULL,
    observacoes     TEXT
);

CREATE INDEX IF NOT EXISTS ix_processo_data ON processo (data_analise DESC);
CREATE INDEX IF NOT EXISTS ix_processo_colaborador ON processo (colaborador_id);

CREATE TABLE IF NOT EXISTS detalhes_processo (
    id                SERIAL PRIMARY KEY,
    processo_id       INTEGER NOT NULL UNIQUE REFERENCES processo(id) ON DELETE CASCADE,
    coordenacao       VARCHAR(120),
    lideranca         VARCHAR(120),
    data_inspecao     DATE,
    inspetor          VARCHAR(120),
    cpf_cnpj_cripto   TEXT,
    coop              VARCHAR(120),
    tipo              VARCHAR(120),
    data_execucao     DATE,
    interno_externo   VARCHAR(50),
    tipo_autorizacao  VARCHAR(120),
    tipo_erro         VARCHAR(120),
    gravidade         VARCHAR(120),
    desvio_atencao    VARCHAR(10),
    reversao          VARCHAR(20),
    fluxos            VARCHAR(255),
    solicitante       VARCHAR(120)
);

-- Prints/evidências: o arquivo inteiro fica criptografado dentro do banco.
CREATE TABLE IF NOT EXISTS imagem_processo (
    id               SERIAL PRIMARY KEY,
    processo_id      INTEGER NOT NULL REFERENCES processo(id) ON DELETE CASCADE,
    nome_arquivo     VARCHAR(200) NOT NULL,
    content_type     VARCHAR(50) NOT NULL,
    conteudo_cripto  BYTEA NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_imagem_processo ON imagem_processo (processo_id);

-- Histórico dos desvios enviados pelo Sr. Inspetor.
CREATE TABLE IF NOT EXISTS envio_desvio (
    id              SERIAL PRIMARY KEY,
    processo_id     INTEGER REFERENCES processo(id) ON DELETE SET NULL,
    enviado_por     VARCHAR(120) NOT NULL,
    para            TEXT NOT NULL,
    cc              TEXT,
    assunto         VARCHAR(200) NOT NULL,
    corpo_cripto    TEXT NOT NULL,
    qtd_anexos      INTEGER NOT NULL DEFAULT 0,
    enviado_em      TIMESTAMP NOT NULL DEFAULT NOW()
);
