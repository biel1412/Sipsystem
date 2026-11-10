using System.Data.Common;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Npgsql;
using Sip.Api.Dados;
using Sip.Api.Endpoints;
using Sip.Api.Seguranca;
using Sip.Api.Servicos;
using Sip.Shared;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Prints em base64 podem somar ~70 MB (10 imagens de até 5 MB).
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 80 * 1024 * 1024);

// ---------- Banco e segurança ----------
var schemaBanco = cfg["Banco:Schema"];
var conexao = ConexaoBanco.Montar(
    string.IsNullOrWhiteSpace(cfg.GetConnectionString("Sip")) ? cfg["DATABASE_URL"] : cfg.GetConnectionString("Sip"),
    schemaBanco);
var fonteDados = NpgsqlDataSource.Create(conexao);
builder.Services.AddSingleton<DbDataSource>(fonteDados);
builder.Services.AddSingleton<Db>();
var aes = CriptografiaAes.DeBase64(cfg["Seguranca:ChaveAes"]);
builder.Services.AddSingleton(aes);

// Chaves do cookie de login ficam no banco: ninguém é deslogado quando o servidor reinicia.
builder.Services.AddDataProtection().SetApplicationName("SIP");
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(o => o.XmlRepository = new ChavesNoBanco(fonteDados, aes)));

// Atrás de proxy HTTPS (Render, Azure, Nginx): respeita o X-Forwarded-Proto para o cookie sair como Secure.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// ---------- E-mail e IA ----------
builder.Services.Configure<EmailOpcoes>(cfg.GetSection("Email"));
builder.Services.Configure<AnthropicOpcoes>(cfg.GetSection("Anthropic"));
builder.Services.AddSingleton<IEmailServico, EmailSmtpServico>();
builder.Services.AddHttpClient<SrInspetorIa>(c => c.Timeout = TimeSpan.FromMinutes(2));

// ---------- Login por cookie (HttpOnly, não acessível por JavaScript) ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "sip.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // API: responde 401/403 em vez de redirecionar para uma página de login.
        o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
        o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    });

builder.Services.AddAuthorizationBuilder()
    // Quem ainda está com a senha padrão só pode trocar a senha.
    .AddPolicy(AuthEndpoints.PoliticaLiberado, p => p
        .RequireAuthenticatedUser()
        .RequireAssertion(c => c.User.FindFirstValue(AuthEndpoints.ClaimTrocarSenha) != "true"));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("ia", http => RateLimitPartition.GetFixedWindowLimiter(
        http.User.Email() is { Length: > 0 } e ? e : http.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);

var app = builder.Build();

// ---------- Inicialização do banco ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Db>();
    if (!string.IsNullOrWhiteSpace(schemaBanco))
        await db.ExecutarAsync($"CREATE SCHEMA IF NOT EXISTS {ConexaoBanco.SchemaValido(schemaBanco)}");
    await db.CriarEsquemaAsync();
    await db.ExecutarAsync(ChavesNoBanco.Sql);
    await AuthEndpoints.CriarAdminAsync(db, cfg, app.Logger);
}

app.UseForwardedHeaders();

// Erros de validação viram 400 com mensagem amigável; o resto vira 500 sem detalhes internos.
app.Use(async (http, next) =>
{
    try { await next(http); }
    catch (ErroValidacao ex) when (!http.Response.HasStarted)
    {
        http.Response.StatusCode = 400;
        await http.Response.WriteAsJsonAsync(new Mensagem(ex.Message));
    }
    catch (BadHttpRequestException ex) when (!http.Response.HasStarted)
    {
        http.Response.StatusCode = ex.StatusCode;
        await http.Response.WriteAsJsonAsync(new Mensagem("Requisição inválida ou grande demais."));
    }
    catch (Exception ex) when (!http.Response.HasStarted && ex is not OperationCanceledException)
    {
        app.Logger.LogError(ex, "Erro não tratado em {Caminho}", http.Request.Path);
        http.Response.StatusCode = 500;
        await http.Response.WriteAsJsonAsync(new Mensagem("Erro interno. Tente novamente."));
    }
});

app.Use(async (http, next) =>
{
    var h = http.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "same-origin";
    await next(http);
});

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();
else
    app.UseHsts();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuth();

var api = app.MapGroup("/api").RequireAuthorization(AuthEndpoints.PoliticaLiberado);
api.MapColaboradores();
api.MapProcessos();
api.MapRelatorios();
api.MapSrInspetor();

app.MapGet("/saude", () => Results.Ok("ok")).AllowAnonymous();
app.MapFallbackToFile("index.html");

app.Run();
