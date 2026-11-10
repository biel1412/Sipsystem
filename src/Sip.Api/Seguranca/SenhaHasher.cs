using System.Security.Cryptography;

namespace Sip.Api.Seguranca;

/// <summary>
/// Hash de senha com PBKDF2-SHA256 + sal aleatório. Senha não se criptografa (não precisa voltar
/// ao original), só se compara o hash. Formato: pbkdf2-sha256$iteracoes$sal$hash.
/// </summary>
public static class SenhaHasher
{
    private const int Iteracoes = 210_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;
    private const string Prefixo = "pbkdf2-sha256";

    public static string Gerar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        return $"{Prefixo}${Iteracoes}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string senha, string? armazenado)
    {
        if (string.IsNullOrEmpty(armazenado)) return false;
        var partes = armazenado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefixo || !int.TryParse(partes[1], out var iteracoes)) return false;

        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
