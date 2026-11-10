using Npgsql;
using Sip.Api.Dados;
using Sip.Shared;

namespace Sip.Api.Endpoints;

public static class ColaboradoresEndpoints
{
    public static void MapColaboradores(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/colaboradores");

        g.MapGet("/", (Db db) => db.ListarAsync(
            """
            SELECT c.id, c.nome, c.email, COUNT(p.id) AS total
            FROM colaborador c LEFT JOIN processo p ON p.colaborador_id = c.id
            GROUP BY c.id, c.nome, c.email
            ORDER BY c.nome
            """,
            r => new ColaboradorDto(r.Inteiro("id"), r.Texto("nome"), r.Texto("email"), r.Inteiro("total"))));

        g.MapPost("/", async (NovoColaboradorRequest req, Db db) =>
        {
            var nome = req.Nome.Trim();
            var email = req.Email.Trim().ToLowerInvariant();
            if (nome.Length == 0 || !Formatacao.EmailValido(email))
                return Results.BadRequest(new Mensagem("Informe nome e um e-mail válido."));

            try
            {
                var id = await db.EscalarAsync<int>(
                    "INSERT INTO colaborador (nome, email) VALUES (@n, @e) RETURNING id", ("n", nome), ("e", email));
                return Results.Ok(new ColaboradorDto(id, nome, email, 0));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new Mensagem("Já existe um colaborador com esse e-mail."));
            }
        });
    }
}
