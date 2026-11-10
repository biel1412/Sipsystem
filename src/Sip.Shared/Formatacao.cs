namespace Sip.Shared;

public static class Formatacao
{
    /// <summary>
    /// Mascara CPF/CNPJ deixando só os 2 últimos dígitos visíveis (ex.: ***.***.***-45).
    /// Usado na tela e em tudo que é enviado para a IA.
    /// </summary>
    public static string? MascararDocumento(string? documento)
    {
        if (string.IsNullOrWhiteSpace(documento)) return documento;
        var digitos = new string(documento.Where(char.IsDigit).ToArray());
        if (digitos.Length <= 2) return new string('*', digitos.Length);
        var fim = digitos[^2..];
        return digitos.Length > 11 ? $"**.***.***/****-{fim}" : $"***.***.***-{fim}";
    }

    private static readonly char[] Separadores = [',', ';', ' ', '\n', '\r', '\t'];

    /// <summary>Separa uma lista de e-mails digitada com vírgula, ponto e vírgula ou espaço.</summary>
    public static List<string> SepararEmails(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(Separadores, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Distinct(StringComparer.OrdinalIgnoreCase)
                   .ToList();

    public static bool EmailValido(string email) =>
        System.Net.Mail.MailAddress.TryCreate(email, out var endereco) && endereco.Address == email && email.Contains('.');

    public static string TamanhoLegivel(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}
