using System.Net;
using System.Net.Http.Json;
using Sip.Shared;

namespace Sip.Client.Servicos;

/// <summary>Resultado de uma chamada: ou veio o dado, ou veio uma mensagem de erro amigável.</summary>
public sealed record Resposta<T>(T? Dado, string? Erro, HttpStatusCode Status)
{
    public bool Ok => Erro is null;
}

/// <summary>
/// Todas as chamadas à API passam por aqui: trata erro, sessão expirada e mensagens do servidor.
/// </summary>
public sealed class ApiCliente(HttpClient http)
{
    /// <summary>Disparado quando a API responde 401 (sessão expirou).</summary>
    public event Action? SessaoExpirada;

    public Task<Resposta<T>> GetAsync<T>(string url) => EnviarAsync<T>(() => http.GetAsync(url));

    public Task<Resposta<T>> PostAsync<T>(string url, object? corpo = null) =>
        EnviarAsync<T>(() => http.PostAsJsonAsync(url, corpo));

    public Task<Resposta<T>> DeleteAsync<T>(string url) => EnviarAsync<T>(() => http.DeleteAsync(url));

    public async Task<Resposta<byte[]>> BaixarAsync(string url)
    {
        try
        {
            using var resp = await http.GetAsync(url);
            if (resp.IsSuccessStatusCode) return new(await resp.Content.ReadAsByteArrayAsync(), null, resp.StatusCode);
            return new(null, await LerErroAsync(resp), resp.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new(null, "Sem conexão com o servidor.", 0);
        }
    }

    private async Task<Resposta<T>> EnviarAsync<T>(Func<Task<HttpResponseMessage>> chamada)
    {
        try
        {
            using var resp = await chamada();
            if (resp.IsSuccessStatusCode)
            {
                var dado = resp.Content.Headers.ContentLength == 0 ? default : await resp.Content.ReadFromJsonAsync<T>();
                return new(dado, null, resp.StatusCode);
            }
            return new(default, await LerErroAsync(resp), resp.StatusCode);
        }
        catch (HttpRequestException)
        {
            return new(default, "Sem conexão com o servidor. Verifique sua internet.", 0);
        }
        catch (TaskCanceledException)
        {
            return new(default, "O servidor demorou demais para responder. Tente novamente.", HttpStatusCode.RequestTimeout);
        }
    }

    private async Task<string> LerErroAsync(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            SessaoExpirada?.Invoke();
            return "Sua sessão expirou. Entre novamente.";
        }
        if (resp.StatusCode == HttpStatusCode.Forbidden) return "Troque sua senha para continuar.";
        if (resp.StatusCode == HttpStatusCode.TooManyRequests) return "Muitas tentativas seguidas. Aguarde um minuto.";
        if (resp.StatusCode == HttpStatusCode.RequestEntityTooLarge) return "Os arquivos são grandes demais.";

        try
        {
            var msg = await resp.Content.ReadFromJsonAsync<Mensagem>();
            if (!string.IsNullOrWhiteSpace(msg?.Texto)) return msg.Texto;
        }
        catch (Exception) { /* resposta sem JSON */ }
        return $"Erro inesperado ({(int)resp.StatusCode}).";
    }
}
