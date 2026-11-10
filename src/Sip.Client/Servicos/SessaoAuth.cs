using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Sip.Shared;

namespace Sip.Client.Servicos;

/// <summary>
/// Diz ao Blazor quem está logado. O login de verdade é um cookie HttpOnly emitido pela API;
/// aqui só perguntamos "quem sou eu?" (/api/auth/eu) e guardamos a resposta.
/// </summary>
public sealed class SessaoAuth : AuthenticationStateProvider
{
    private readonly ApiCliente _api;
    private UsuarioInfo? _usuario;
    private bool _carregado;

    public SessaoAuth(ApiCliente api)
    {
        _api = api;
        _api.SessaoExpirada += () => Definir(null);
    }

    public UsuarioInfo? Usuario => _usuario;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_carregado)
        {
            var r = await _api.GetAsync<UsuarioInfo>("api/auth/eu");
            _usuario = r.Ok ? r.Dado : null;
            _carregado = true;
        }
        return Estado(_usuario);
    }

    public async Task<string?> EntrarAsync(LoginRequest req)
    {
        var r = await _api.PostAsync<UsuarioInfo>("api/auth/login", req);
        if (r.Ok) Definir(r.Dado);
        return r.Erro;
    }

    public async Task<string?> TrocarSenhaAsync(TrocarSenhaRequest req)
    {
        var r = await _api.PostAsync<UsuarioInfo>("api/auth/trocar-senha", req);
        if (r.Ok) Definir(r.Dado);
        return r.Erro;
    }

    public async Task SairAsync()
    {
        await _api.PostAsync<Mensagem>("api/auth/logout");
        Definir(null);
    }

    private void Definir(UsuarioInfo? usuario)
    {
        _usuario = usuario;
        _carregado = true;
        NotifyAuthenticationStateChanged(Task.FromResult(Estado(usuario)));
    }

    private static AuthenticationState Estado(UsuarioInfo? u)
    {
        if (u is null) return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        var id = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, u.Nome),
            new Claim(ClaimTypes.Email, u.Email),
        ], authenticationType: "cookie");
        return new AuthenticationState(new ClaimsPrincipal(id));
    }
}
