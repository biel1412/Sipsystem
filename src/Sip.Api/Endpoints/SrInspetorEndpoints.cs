using System.Security.Claims;
using Sip.Api.Dados;
using Sip.Api.Seguranca;
using Sip.Api.Servicos;
using Sip.Shared;

namespace Sip.Api.Endpoints;

public static class SrInspetorEndpoints
{
    public const int MaxDestinatarios = 10;

    public static void MapSrInspetor(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/sr-inspetor");

        g.MapPost("/gerar", async (GerarDesvioRequest req, Db db, CriptografiaAes aes, SrInspetorIa ia,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var prompt = PromptsProntos.Buscar(req.PromptId);
            if (prompt is null) return Results.BadRequest(new Mensagem("Prompt desconhecido."));

            var imagens = Imagens.Validar(req.Imagens);
            if (string.IsNullOrWhiteSpace(req.Descricao) && imagens.Count == 0 && req.ProcessoId is null)
                return Results.BadRequest(new Mensagem("Descreva o erro ou anexe pelo menos um print."));

            string? nomeColaborador = null;
            var dados = new Dictionary<string, string?>();

            if (req.ProcessoId is int pid)
            {
                var p = await ProcessosEndpoints.BuscarAsync(db, aes, pid);
                if (p is null) return Results.BadRequest(new Mensagem("Processo não encontrado."));
                nomeColaborador = p.Colaborador;
                dados["Processo"] = $"#{p.Id} de {p.DataAnalise:dd/MM/yyyy}";
                dados["Status"] = p.Status;
                dados["Tipo"] = p.Tipo;
                dados["Tipo de autorização"] = p.TipoAutorizacao;
                dados["Tipo de erro"] = Opcoes.Texto(Opcoes.TipoErro, p.TipoErro);
                dados["Gravidade"] = Opcoes.Texto(Opcoes.Gravidade, p.Gravidade);
                dados["Desvio de atenção"] = Opcoes.Texto(Opcoes.DesvioAtencao, p.DesvioAtencao);
                dados["Reversão"] = Opcoes.Texto(Opcoes.Reversao, p.Reversao);
                dados["Fluxos"] = p.Fluxos;
                dados["Data de execução"] = p.DataExecucao?.ToString("dd/MM/yyyy");
                dados["Cooperativa"] = p.Coop;
                // CPF/CNPJ vai mascarado: a IA não precisa do número completo.
                dados["CPF/CNPJ do associado"] = Formatacao.MascararDocumento(p.CpfCnpj);
                dados["Descrição registrada"] = p.Descricao;
                dados["Procedimento correto registrado"] = p.Observacoes;
            }

            if (nomeColaborador is null && req.ColaboradorId is int cid)
                nomeColaborador = await db.EscalarAsync<string>("SELECT nome FROM colaborador WHERE id = @id", ("id", cid));

            var ctx = new ContextoDesvio(prompt, req.Descricao ?? "", req.InstrucaoExtra, nomeColaborador,
                user.Identity?.Name, dados, imagens);
            return Results.Ok(await ia.GerarAsync(ctx, ct));
        }).RequireRateLimiting("ia");

        g.MapPost("/enviar", async (EnviarDesvioRequest req, Db db, CriptografiaAes aes, IEmailServico email,
            ClaimsPrincipal user, CancellationToken ct) =>
        {
            var para = Formatacao.SepararEmails(req.Para);
            var cc = Formatacao.SepararEmails(req.Cc).Except(para, StringComparer.OrdinalIgnoreCase).ToList();
            var invalidos = para.Concat(cc).Where(e => !Formatacao.EmailValido(e)).ToList();

            if (para.Count == 0) return Results.BadRequest(new Mensagem("Informe pelo menos um destinatário."));
            if (invalidos.Count > 0) return Results.BadRequest(new Mensagem($"E-mail inválido: {string.Join(", ", invalidos)}"));
            if (para.Count + cc.Count > MaxDestinatarios)
                return Results.BadRequest(new Mensagem($"No máximo {MaxDestinatarios} destinatários por envio."));
            if (string.IsNullOrWhiteSpace(req.Assunto) || string.IsNullOrWhiteSpace(req.Corpo))
                return Results.BadRequest(new Mensagem("Assunto e corpo são obrigatórios."));

            var imagens = Imagens.Validar(req.Imagens);
            var anexos = imagens.Select((img, i) => new Anexo($"print_{i + 1}_{img.NomeArquivo}", img.ContentType, img.Conteudo)).ToList();

            await email.EnviarAsync(new EmailParaEnviar(para, cc, user.Email(), req.Assunto.Trim(), req.Corpo.Trim(), anexos), ct);

            int? processoId = req.ProcessoId is int pid &&
                await db.EscalarAsync<long>("SELECT COUNT(*) FROM processo WHERE id = @id", ("id", pid)) > 0 ? pid : null;

            await db.ExecutarAsync(
                """
                INSERT INTO envio_desvio (processo_id, enviado_por, para, cc, assunto, corpo_cripto, qtd_anexos, enviado_em)
                VALUES (@p, @por, @para, @cc, @a, @c, @q, @em)
                """,
                ("p", processoId), ("por", user.Email()), ("para", string.Join(", ", para)),
                ("cc", cc.Count == 0 ? null : string.Join(", ", cc)), ("a", req.Assunto.Trim()),
                ("c", aes.CriptografarTexto(req.Corpo.Trim())), ("q", anexos.Count), ("em", Relogio.AgoraBrasil()));

            return Results.Ok(new Mensagem($"Desvio enviado para {string.Join(", ", para)}."));
        }).RequireRateLimiting("ia");

        g.MapGet("/historico", (Db db) => db.ListarAsync(
            "SELECT id, enviado_em, enviado_por, para, assunto, processo_id FROM envio_desvio ORDER BY enviado_em DESC LIMIT 50",
            r => new EnvioHistorico(r.Inteiro("id"), r.DataHora("enviado_em"), r.Texto("enviado_por"),
                r.Texto("para"), r.Texto("assunto"), r.InteiroOuNulo("processo_id"))));
    }
}
