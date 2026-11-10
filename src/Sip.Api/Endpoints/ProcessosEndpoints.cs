using System.Data.Common;
using Sip.Api.Dados;
using Sip.Api.Seguranca;
using Sip.Api.Servicos;
using Sip.Shared;

namespace Sip.Api.Endpoints;

public static class ProcessosEndpoints
{
    public const int PorPagina = 10;

    public static void MapProcessos(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/processos");

        g.MapGet("/", (Db db, int? pagina) => ListarAsync(db, new Filtro(), pagina ?? 1));

        g.MapGet("/{id:int}", async (int id, Db db, CriptografiaAes aes) =>
            await BuscarAsync(db, aes, id) is { } p ? Results.Ok(p) : Results.NotFound(new Mensagem("Processo não encontrado.")));

        g.MapPost("/", async (NovoProcessoRequest req, Db db, CriptografiaAes aes) =>
        {
            if (!Opcoes.Status.Contains(req.Status))
                return Results.BadRequest(new Mensagem("Status inválido."));
            var existe = await db.EscalarAsync<long>("SELECT COUNT(*) FROM colaborador WHERE id = @id", ("id", req.ColaboradorId));
            if (existe == 0) return Results.BadRequest(new Mensagem("Colaborador não encontrado."));

            var imagens = Imagens.Validar(req.Imagens);
            var cpfCripto = aes.CriptografarTexto(string.IsNullOrWhiteSpace(req.CpfCnpj) ? null : req.CpfCnpj.Trim());

            var id = await db.TransacaoAsync(async tx =>
            {
                var processoId = await tx.EscalarAsync<int>(
                    """
                    INSERT INTO processo (colaborador_id, data_analise, descricao, status, observacoes)
                    VALUES (@c, @d, @desc, @s, @o) RETURNING id
                    """,
                    ("c", req.ColaboradorId), ("d", Relogio.AgoraBrasil()), ("desc", req.Descricao?.Trim() ?? ""),
                    ("s", req.Status), ("o", Vazio(req.Observacoes)));

                await tx.ExecutarAsync(
                    """
                    INSERT INTO detalhes_processo (processo_id, coordenacao, lideranca, data_inspecao, inspetor, cpf_cnpj_cripto,
                        coop, tipo, data_execucao, interno_externo, tipo_autorizacao, tipo_erro, gravidade, desvio_atencao,
                        reversao, fluxos, solicitante)
                    VALUES (@p, @coord, @lid, @dins, @insp, @cpf, @coop, @tipo, @dexe, @ie, @taut, @terr, @grav, @da, @rev, @flu, @sol)
                    """,
                    ("p", processoId), ("coord", Vazio(req.Coordenacao)), ("lid", Vazio(req.Lideranca)),
                    ("dins", req.DataInspecao), ("insp", Vazio(req.Inspetor)), ("cpf", cpfCripto),
                    ("coop", Vazio(req.Coop)), ("tipo", Vazio(req.Tipo)), ("dexe", req.DataExecucao),
                    ("ie", Vazio(req.InternoExterno)), ("taut", Vazio(req.TipoAutorizacao)), ("terr", Vazio(req.TipoErro)),
                    ("grav", Vazio(req.Gravidade)), ("da", Vazio(req.DesvioAtencao)), ("rev", Vazio(req.Reversao)),
                    ("flu", Vazio(req.Fluxos)), ("sol", Vazio(req.Solicitante)));

                foreach (var img in imagens)
                    await tx.ExecutarAsync(
                        "INSERT INTO imagem_processo (processo_id, nome_arquivo, content_type, conteudo_cripto) VALUES (@p, @n, @t, @c)",
                        ("p", processoId), ("n", img.NomeArquivo), ("t", img.ContentType), ("c", aes.Criptografar(img.Conteudo)));

                return processoId;
            });

            return Results.Created($"/api/processos/{id}", new ProcessoCriado(id, $"Processo #{id} registrado com sucesso!"));
        });

        g.MapDelete("/{id:int}", async (int id, Db db) =>
            await db.ExecutarAsync("DELETE FROM processo WHERE id = @id", ("id", id)) > 0
                ? Results.Ok(new DeletarResultado(1))
                : Results.NotFound(new Mensagem("Processo não encontrado.")));

        g.MapPost("/deletar-por-colaborador", async (DeletarPorColaboradorRequest req, Db db) =>
            Results.Ok(new DeletarResultado(
                await db.ExecutarAsync("DELETE FROM processo WHERE colaborador_id = @c", ("c", req.ColaboradorId)))));

        // Apaga tudo: exige ?confirmar=APAGAR para evitar clique acidental.
        g.MapPost("/deletar-todos", async (string? confirmar, Db db) =>
            confirmar != "APAGAR"
                ? Results.BadRequest(new Mensagem("Confirmação ausente."))
                : Results.Ok(new DeletarResultado(await db.ExecutarAsync("DELETE FROM processo"))));

        api.MapGet("/imagens/{id:int}", async (int id, Db db, CriptografiaAes aes, HttpContext http) =>
        {
            var img = await db.PrimeiroAsync(
                "SELECT nome_arquivo, content_type, conteudo_cripto FROM imagem_processo WHERE id = @id",
                r => (Nome: r.Texto("nome_arquivo"), Tipo: r.Texto("content_type"), Dados: r.Bytes("conteudo_cripto")),
                ("id", id));
            if (img.Dados is null) return Results.NotFound();

            http.Response.Headers.CacheControl = "private, max-age=3600";
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(aes.Descriptografar(img.Dados), img.Tipo, img.Nome);
        });
    }

    public sealed record Filtro(
        int? ColaboradorId = null, DateOnly? DataInicio = null, DateOnly? DataFim = null, string? Status = null, string? Palavra = null);

    /// <summary>Monta o WHERE com parâmetros a partir dos filtros do painel.</summary>
    public static (string Where, (string, object?)[] Parametros) MontarWhere(Filtro f)
    {
        var condicoes = new List<string>();
        var ps = new List<(string, object?)>();
        if (f.ColaboradorId is > 0) { condicoes.Add("p.colaborador_id = @col"); ps.Add(("col", f.ColaboradorId)); }
        if (!string.IsNullOrWhiteSpace(f.Status)) { condicoes.Add("p.status = @st"); ps.Add(("st", f.Status)); }
        if (!string.IsNullOrWhiteSpace(f.Palavra)) { condicoes.Add("p.descricao ILIKE '%' || @pal || '%'"); ps.Add(("pal", f.Palavra.Trim())); }
        if (f.DataInicio is { } ini) { condicoes.Add("p.data_analise >= @ini"); ps.Add(("ini", ini.ToDateTime(TimeOnly.MinValue))); }
        // Inclui o dia final inteiro (até 23:59:59).
        if (f.DataFim is { } fim) { condicoes.Add("p.data_analise < @fim"); ps.Add(("fim", fim.AddDays(1).ToDateTime(TimeOnly.MinValue))); }
        return (condicoes.Count == 0 ? "" : "WHERE " + string.Join(" AND ", condicoes), ps.ToArray());
    }

    public static async Task<Pagina<ProcessoResumo>> ListarAsync(Db db, Filtro filtro, int pagina)
    {
        var (where, ps) = MontarWhere(filtro);
        var total = (int)await db.EscalarAsync<long>($"SELECT COUNT(*) FROM processo p {where}", ps);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)PorPagina));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        var itens = await db.ListarAsync(
            $"""
            SELECT p.id, p.data_analise, p.colaborador_id, c.nome AS colaborador, p.status, p.descricao,
                   (SELECT COUNT(*) FROM imagem_processo i WHERE i.processo_id = p.id) AS qtd_imagens
            FROM processo p JOIN colaborador c ON c.id = p.colaborador_id
            {where}
            ORDER BY p.data_analise DESC, p.id DESC
            LIMIT {PorPagina} OFFSET {(pagina - 1) * PorPagina}
            """,
            r => new ProcessoResumo(r.Inteiro("id"), r.DataHora("data_analise"), r.Inteiro("colaborador_id"),
                r.Texto("colaborador"), r.Texto("status"), r.Texto("descricao"), r.Inteiro("qtd_imagens")),
            ps);

        return new Pagina<ProcessoResumo>(itens, pagina, totalPaginas, total);
    }

    public static async Task<ProcessoDetalhe?> BuscarAsync(Db db, CriptografiaAes aes, int id)
    {
        var imagens = await db.ListarAsync(
            "SELECT id, nome_arquivo FROM imagem_processo WHERE processo_id = @id ORDER BY id",
            r => new ImagemInfo(r.Inteiro("id"), r.Texto("nome_arquivo")), ("id", id));

        return await db.PrimeiroAsync(
            """
            SELECT p.id, p.data_analise, p.colaborador_id, c.nome AS colaborador, c.email AS colaborador_email,
                   p.status, p.descricao, p.observacoes, d.*
            FROM processo p
            JOIN colaborador c ON c.id = p.colaborador_id
            LEFT JOIN detalhes_processo d ON d.processo_id = p.id
            WHERE p.id = @id
            """,
            r => Mapear(r, aes, imagens), ("id", id));
    }

    private static ProcessoDetalhe Mapear(DbDataReader r, CriptografiaAes aes, IReadOnlyList<ImagemInfo> imagens) => new(
        r.GetInt32(0), r.DataHora("data_analise"), r.Inteiro("colaborador_id"), r.Texto("colaborador"),
        r.Texto("colaborador_email"), r.Texto("status"), r.Texto("descricao"), r.TextoOuNulo("observacoes"),
        r.TextoOuNulo("coordenacao"), r.TextoOuNulo("lideranca"), r.DataOuNula("data_inspecao"), r.TextoOuNulo("inspetor"),
        aes.DescriptografarTexto(r.TextoOuNulo("cpf_cnpj_cripto")), r.TextoOuNulo("coop"), r.TextoOuNulo("tipo"),
        r.DataOuNula("data_execucao"), r.TextoOuNulo("interno_externo"), r.TextoOuNulo("tipo_autorizacao"),
        r.TextoOuNulo("tipo_erro"), r.TextoOuNulo("gravidade"), r.TextoOuNulo("desvio_atencao"), r.TextoOuNulo("reversao"),
        r.TextoOuNulo("fluxos"), r.TextoOuNulo("solicitante"), imagens);

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
