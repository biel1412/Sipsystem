using Microsoft.JSInterop;

namespace Sip.Client.Servicos;

/// <summary>Pequenas funções do navegador (download, imprimir, copiar, colar prints).</summary>
public sealed class Navegador(IJSRuntime js)
{
    public ValueTask BaixarAsync(string nomeArquivo, string contentType, byte[] conteudo) =>
        js.InvokeVoidAsync("sip.baixar", nomeArquivo, contentType, Convert.ToBase64String(conteudo));

    public ValueTask ImprimirAsync() => js.InvokeVoidAsync("sip.imprimir");

    public ValueTask<bool> CopiarAsync(string texto) => js.InvokeAsync<bool>("sip.copiar", texto);

    public ValueTask<bool> ConfirmarAsync(string mensagem) => js.InvokeAsync<bool>("confirm", mensagem);

    /// <summary>Passa a escutar Ctrl+V de imagens dentro do elemento e avisa o componente .NET.</summary>
    public ValueTask EscutarColarAsync<T>(string idElemento, DotNetObjectReference<T> componente) where T : class =>
        js.InvokeVoidAsync("sip.escutarColar", idElemento, componente);

    public ValueTask PararDeEscutarColarAsync(string idElemento) => js.InvokeVoidAsync("sip.pararColar", idElemento);
}
