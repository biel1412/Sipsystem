using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sip.Api.Dados;
using Sip.Api.Seguranca;
using Sip.Shared;

namespace Sip.Api.Endpoints;

public static class AuthEndpoints
{
    public const string ClaimTrocarSenha = "sip:trocar_senha";
    public const string PoliticaLiberado = "Liberado";

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth");

        g.MapPost("/login", async (LoginRequest req, Db db, HttpContext http, IConfiguration cfg) =>
        {
            var email = req.Email.Trim().ToLowerInvariant();
            var usuario = await db.PrimeiroAsync(
                "SELECT senha_hash, precisa_trocar_senha FROM usuario WHERE lower(email) = @e",
                r => (Hash: r.Texto("senha_hash"), Trocar: r.GetBoolean(r.GetOrdinal("precisa_trocar_senha"))),
                ("e", email));

            bool precisaTrocar;
            if (usuario.Hash is not null)
            {
                if (!SenhaHasher.Verificar(req.Senha, usuario.Hash))
                    return Results.BadRequest(new Mensagem("E-mail ou senha incorretos."));
                precisaTrocar = usuario.Trocar;
            }
            else
            {
                // Primeiro acesso: colaborador cadastrado entra com a senha padrão e é obrigado a trocar.
                var senhaPadrao = cfg["Seguranca:SenhaPadrao"];
                var ehColaborador = await db.EscalarAsync<long>(
                    "SELECT COUNT(*) FROM colaborador WHERE lower(email) = @e", ("e", email)) > 0;
                if (!ehColaborador || string.IsNullOrEmpty(senhaPadrao) || req.Senha != senhaPadrao)
                    return Results.BadRequest(new Mensagem("E-mail ou senha incorretos."));

                await db.ExecutarAsync(
                    "INSERT INTO usuario (email, senha_hash, precisa_trocar_senha) VALUES (@e, @h, TRUE) ON CONFLICT (email) DO NOTHING",
                    ("e", email), ("h", SenhaHasher.Gerar(senhaPadrao)));
                precisaTrocar = true;
            }

            var info = new UsuarioInfo(email, await NomeAsync(db, email), precisaTrocar);
            await EntrarAsync(http, info);
            return Results.Ok(info);
        }).RequireRateLimiting("login");

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok(new Mensagem("Sessão encerrada com sucesso."));
        });

        g.MapGet("/eu", (ClaimsPrincipal user) => Results.Ok(Info(user))).RequireAuthorization();

        g.MapPost("/trocar-senha", async (TrocarSenhaRequest req, Db db, HttpContext http, ClaimsPrincipal user) =>
        {
            var email = user.Email();
            var hash = await db.EscalarAsync<string>("SELECT senha_hash FROM usuario WHERE lower(email) = @e", ("e", email));
            if (!SenhaHasher.Verificar(req.SenhaAtual, hash))
                return Results.BadRequest(new Mensagem("Senha atual incorreta."));
            if (req.NovaSenha.Length < 8)
                return Results.BadRequest(new Mensagem("A nova senha precisa de pelo menos 8 caracteres."));
            if (req.NovaSenha == req.SenhaAtual)
                return Results.BadRequest(new Mensagem("A nova senha precisa ser diferente da atual."));

            await db.ExecutarAsync(
                "UPDATE usuario SET senha_hash = @h, precisa_trocar_senha = FALSE WHERE lower(email) = @e",
                ("h", SenhaHasher.Gerar(req.NovaSenha)), ("e", email));

            var info = Info(user) with { PrecisaTrocarSenha = false };
            await EntrarAsync(http, info);
            return Results.Ok(info);
        }).RequireAuthorization().RequireRateLimiting("login");
    }

    /// <summary>Cria o usuário administrador definido em Admin:Email / Admin:Senha, se ainda não existir.</summary>
    public static async Task CriarAdminAsync(Db db, IConfiguration cfg, ILogger log)
    {
        var email = cfg["Admin:Email"]?.Trim().ToLowerInvariant();
        var senha = cfg["Admin:Senha"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(senha)) return;

        var criados = await db.ExecutarAsync(
            "INSERT INTO usuario (email, senha_hash, precisa_trocar_senha) VALUES (@e, @h, FALSE) ON CONFLICT (email) DO NOTHING",
            ("e", email), ("h", SenhaHasher.Gerar(senha)));
        if (criados > 0) log.LogInformation("Usuário administrador {Email} criado.", email);
    }

    private static async Task<string> NomeAsync(Db db, string email) =>
        await db.EscalarAsync<string>("SELECT nome FROM colaborador WHERE lower(email) = @e", ("e", email))
        ?? email.Split('@')[0];

    private static Task EntrarAsync(HttpContext http, UsuarioInfo info)
    {
        var identidade = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, info.Email),
            new Claim(ClaimTypes.Name, info.Nome),
            new Claim(ClaimTrocarSenha, info.PrecisaTrocarSenha ? "true" : "false"),
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade),
            new AuthenticationProperties { IsPersistent = false });
    }

    private static UsuarioInfo Info(ClaimsPrincipal user) =>
        new(user.Email(), user.Identity?.Name ?? "", user.FindFirstValue(ClaimTrocarSenha) == "true");

    public static string Email(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Email) ?? "";
}
