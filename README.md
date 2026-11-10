# SIP.Net — Sistema Integrado de Inspeção (.NET 8 + Blazor WebAssembly)

Reescrita do [SIP](https://github.com/Gabrielmilitz/SIP) (antes em Flask/Python) em **C# / .NET 8**, com front-end em **Blazor WebAssembly**,
API em **ASP.NET Core**, banco **PostgreSQL**, dados sensíveis criptografados com **AES-256-GCM**
e o assistente **Sr. Inspetor**, que usa o Claude para montar e enviar e-mails de desvio.

![Sr. Inspetor](src/Sip.Client/wwwroot/img/sr-inspetor.svg)

## Estrutura

```
.
├── SIP.sln
├── Dockerfile
├── src/
│   ├── Sip.Shared/   DTOs, listas de opções e prompts prontos (usados pela API e pelo front)
│   ├── Sip.Api/      API ASP.NET Core: login, processos, relatórios, Excel, Sr. Inspetor, e-mail
│   │   ├── Dados/        Schema.sql (criado sozinho ao iniciar) e acesso ao PostgreSQL
│   │   ├── Endpoints/    rotas /api/...
│   │   ├── Seguranca/    AES-256-GCM e hash de senha (PBKDF2)
│   │   └── Servicos/     e-mail (SMTP), Claude (Sr. Inspetor), gerador de .xlsx, validação de imagens
│   └── Sip.Client/   Blazor WebAssembly (telas). É servido pela própria API: um único site.
└── tests/Sip.Tests/  testes de criptografia, senha, Excel, imagens e Sr. Inspetor
```

## O que mudou em relação à versão Flask

| Antes (Flask) | Agora (.NET 8) |
|---|---|
| SQLite em arquivo | PostgreSQL; as tabelas são criadas automaticamente na 1ª execução |
| CPF/CNPJ e prints em texto/arquivo aberto | CPF/CNPJ e **prints criptografados com AES-256-GCM** dentro do banco; histórico de e-mails também |
| Senha padrão `123456` liberava o sistema | Senha padrão só serve no **primeiro acesso** e obriga a criar uma senha nova (mín. 8 caracteres) |
| Sessão Flask | Cookie HttpOnly + SameSite=Strict, limite de tentativas de login (10/min) |
| PDF com ReportLab | Página "Parecer" pronta para **Imprimir / Salvar como PDF** no navegador |
| Excel com openpyxl | Mesmo layout (colunas da planilha da qualidade, cabeçalho cinza, 1ª linha congelada) |
| — | **Sr. Inspetor**: monta e envia o e-mail de desvio com os prints em anexo |

Todas as telas antigas existem: início, login, trocar senha, processos (lista + exportar Excel),
novo processo (com vários prints), novo colaborador, colaboradores, painel com filtros, meu perfil,
gráfico, deletar registros, imagens do processo e parecer.

## Sr. Inspetor

Menu **Sr. Inspetor** (ou o link **Enviar desvio** em qualquer processo com status Desvio):

1. **Evidências** — escolha o colaborador, anexe os prints (clicar, arrastar ou **Ctrl+V** em qualquer lugar
   da página depois de um Win+Shift+S) e descreva o erro. Se veio de um processo, tudo já vem preenchido.
2. **Como montar o desvio?** — botões de prompt prontos:
   - Montar desvio
   - Montar desvio com pontos de melhoria
   - Montar desvio e informar forma correta
   - Montar desvio de forma objetiva
   - Montar desvio com feedback educativo
   - Resumo do desvio para a liderança
3. **Revise e envie** — o Claude devolve assunto e texto; você edita à vontade, ajusta Para/CC e envia.
   Os prints vão como anexo e o envio fica registrado no histórico.

Para criar ou mudar um prompt, edite `src/Sip.Shared/SrInspetor.cs` (lista `PromptsProntos.Todos`).

Cuidados que já vêm prontos: o CPF/CNPJ vai **mascarado** para a IA, o Claude é instruído a não inventar fatos
e marcar dúvidas como `[confirmar: …]` (a tela avisa antes de enviar), e a chave da API fica só no servidor.

## Configuração

Tudo pode vir do `appsettings.json`, de variáveis de ambiente (use `__` no lugar de `:`, ex.: `Email__Senha`)
ou do *user-secrets* no desenvolvimento. **Nunca coloque senhas e chaves no Git.**

| Configuração | Para que serve |
|---|---|
| `ConnectionStrings:Sip` | Conexão PostgreSQL. Ex.: `Host=localhost;Database=sip;Username=sip;Password=...` |
| `Seguranca:ChaveAes` | **Obrigatória.** 32 bytes em base64. Gere com `openssl rand -base64 32` (ou PowerShell abaixo). **Guarde bem: sem ela os dados criptografados não podem ser lidos.** |
| `Seguranca:SenhaPadrao` | Senha do primeiro acesso dos colaboradores (padrão `123456`). Deixe vazio para desativar. |
| `Admin:Email` / `Admin:Senha` | Cria um usuário administrador na primeira execução (opcional). |
| `Email:Usuario` / `Email:Senha` | Conta SMTP. No Gmail use uma **senha de app** (Conta Google → Segurança → Verificação em duas etapas → Senhas de app). |
| `Email:Remetente`, `Email:NomeRemetente`, `Email:Servidor`, `Email:Porta` | Padrão: Gmail, porta 587 com TLS. |
| `Anthropic:ChaveApi` | Chave da API do Claude (console.anthropic.com). |
| `Anthropic:Modelo` | Modelo usado pelo Sr. Inspetor (padrão `claude-sonnet-5-5`). |

Gerar a chave AES no Windows (PowerShell 7):

```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

## Rodar no computador

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e PostgreSQL
(ou Docker: `docker run -d --name sip-db -e POSTGRES_USER=sip -e POSTGRES_PASSWORD=sip -e POSTGRES_DB=sip -p 5432:5432 postgres:16`).

```bash
cd src/Sip.Api
dotnet user-secrets set "Seguranca:ChaveAes" "$(openssl rand -base64 32)"
dotnet user-secrets set "Admin:Email" "seu.email@empresa.com"
dotnet user-secrets set "Admin:Senha" "UmaSenhaForte#2026"
dotnet user-secrets set "Email:Usuario" "conta@gmail.com"
dotnet user-secrets set "Email:Senha" "senha-de-app-do-gmail"
dotnet user-secrets set "Anthropic:ChaveApi" "sk-ant-..."
dotnet run
```

Abra o endereço mostrado no terminal (ex.: https://localhost:7173). A conexão de desenvolvimento
(`appsettings.Development.json`) aponta para `Host=localhost;Database=sip;Username=sip;Password=sip`.

Testes: `dotnet test` (na raiz do repositório)

## Publicar (Docker / Render)

```bash
docker build -t sip-net .
docker run -p 8080:8080 \
  -e ConnectionStrings__Sip="Host=...;Database=sip;Username=...;Password=...;SSL Mode=Require" \
  -e Seguranca__ChaveAes="..." -e Email__Usuario="..." -e Email__Senha="..." -e Anthropic__ChaveApi="..." \
  sip-net
```

No Render: crie um PostgreSQL, um *Web Service* do tipo Docker apontando para este repositório,
e cadastre as variáveis acima em *Environment*. O endereço `/saude` responde `ok` para o health check.
As chaves do cookie de login ficam no banco (criptografadas), então ninguém é deslogado a cada deploy.

## Banco de dados

O `Schema.sql` roda a cada inicialização e só cria o que ainda não existe. Tabelas:
`colaborador`, `usuario`, `processo`, `detalhes_processo`, `imagem_processo` (prints criptografados),
`envio_desvio` (histórico do Sr. Inspetor) e `chave_protecao` (chaves do cookie de login).
