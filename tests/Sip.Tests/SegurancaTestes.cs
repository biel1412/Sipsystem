using System.Security.Cryptography;
using System.Text;
using Sip.Api.Seguranca;

namespace Sip.Tests;

public class SegurancaTestes
{
    private static readonly CriptografiaAes Aes = CriptografiaAes.DeBase64(CriptografiaAes.GerarChaveBase64());

    [Fact]
    public void Aes_IdaEVolta_Texto()
    {
        var cifrado = Aes.CriptografarTexto("123.456.789-01");
        Assert.NotNull(cifrado);
        Assert.DoesNotContain("123", cifrado);
        Assert.Equal("123.456.789-01", Aes.DescriptografarTexto(cifrado));
    }

    [Fact]
    public void Aes_MesmoTexto_GeraCifrasDiferentes()
    {
        Assert.NotEqual(Aes.CriptografarTexto("igual"), Aes.CriptografarTexto("igual"));
    }

    [Fact]
    public void Aes_DadoAlterado_Falha()
    {
        var pacote = Aes.Criptografar(Encoding.UTF8.GetBytes("evidência"));
        pacote[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => Aes.Descriptografar(pacote));
    }

    [Fact]
    public void Aes_ChaveErrada_Falha()
    {
        var outra = CriptografiaAes.DeBase64(CriptografiaAes.GerarChaveBase64());
        var pacote = Aes.Criptografar([1, 2, 3]);
        Assert.ThrowsAny<CryptographicException>(() => outra.Descriptografar(pacote));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nao-e-base64!!")]
    [InlineData("AAAA")] // base64 válido, mas não tem 32 bytes
    public void Aes_ChaveInvalida_Recusada(string? chave)
    {
        Assert.ThrowsAny<Exception>(() => CriptografiaAes.DeBase64(chave));
    }

    [Fact]
    public void Aes_Nulo_ContinuaNulo()
    {
        Assert.Null(Aes.CriptografarTexto(null));
        Assert.Null(Aes.DescriptografarTexto(null));
    }

    [Fact]
    public void Senha_HashVerifica()
    {
        var hash = SenhaHasher.Gerar("MinhaSenha#1");
        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.True(SenhaHasher.Verificar("MinhaSenha#1", hash));
        Assert.False(SenhaHasher.Verificar("minhasenha#1", hash));
        Assert.False(SenhaHasher.Verificar("MinhaSenha#1", null));
        Assert.False(SenhaHasher.Verificar("MinhaSenha#1", "lixo"));
        Assert.NotEqual(hash, SenhaHasher.Gerar("MinhaSenha#1"));
    }
}
