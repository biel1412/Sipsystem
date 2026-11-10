using System.Security.Cryptography;
using System.Text;

namespace Sip.Api.Seguranca;

/// <summary>
/// Criptografia AES-256-GCM (autenticada: detecta se o dado foi alterado).
/// Formato gravado: [versão 1 byte][nonce 12 bytes][tag 16 bytes][dados cifrados].
/// A chave (32 bytes em base64) vem da configuração "Seguranca:ChaveAes" e nunca vai para o banco.
/// </summary>
public sealed class CriptografiaAes
{
    private const byte Versao = 1;
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;
    private const int Cabecalho = 1 + TamanhoNonce + TamanhoTag;

    private readonly byte[] _chave;

    public CriptografiaAes(byte[] chave)
    {
        if (chave.Length != 32)
            throw new ArgumentException("A chave AES-256 precisa ter exatamente 32 bytes.", nameof(chave));
        _chave = chave;
    }

    public static CriptografiaAes DeBase64(string? chaveBase64)
    {
        if (string.IsNullOrWhiteSpace(chaveBase64))
            throw new InvalidOperationException(
                "Configure Seguranca:ChaveAes (32 bytes em base64). Gere uma com: openssl rand -base64 32");
        byte[] chave;
        try { chave = Convert.FromBase64String(chaveBase64.Trim()); }
        catch (FormatException) { throw new InvalidOperationException("Seguranca:ChaveAes não é um base64 válido."); }
        return new CriptografiaAes(chave);
    }

    public static string GerarChaveBase64() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public byte[] Criptografar(ReadOnlySpan<byte> dados)
    {
        var saida = new byte[Cabecalho + dados.Length];
        saida[0] = Versao;
        var nonce = saida.AsSpan(1, TamanhoNonce);
        var tag = saida.AsSpan(1 + TamanhoNonce, TamanhoTag);
        var cifrado = saida.AsSpan(Cabecalho);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Encrypt(nonce, dados, cifrado, tag);
        return saida;
    }

    public byte[] Descriptografar(ReadOnlySpan<byte> pacote)
    {
        if (pacote.Length < Cabecalho || pacote[0] != Versao)
            throw new CryptographicException("Dado criptografado inválido ou de versão desconhecida.");

        var nonce = pacote.Slice(1, TamanhoNonce);
        var tag = pacote.Slice(1 + TamanhoNonce, TamanhoTag);
        var cifrado = pacote[Cabecalho..];
        var dados = new byte[cifrado.Length];

        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Decrypt(nonce, cifrado, tag, dados);
        return dados;
    }

    public string? CriptografarTexto(string? texto) =>
        string.IsNullOrEmpty(texto) ? null : Convert.ToBase64String(Criptografar(Encoding.UTF8.GetBytes(texto)));

    public string? DescriptografarTexto(string? base64) =>
        string.IsNullOrEmpty(base64) ? null : Encoding.UTF8.GetString(Descriptografar(Convert.FromBase64String(base64)));
}
