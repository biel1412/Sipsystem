using Sip.Shared;

namespace Sip.Api.Servicos;

public sealed record ImagemValida(string NomeArquivo, string ContentType, byte[] Conteudo);

/// <summary>
/// Valida os prints recebidos: base64, tamanho e tipo real do arquivo (pelos primeiros bytes,
/// não pela extensão, para ninguém mandar um .exe renomeado para .png).
/// </summary>
public static class Imagens
{
    public static List<ImagemValida> Validar(IReadOnlyList<ImagemUpload>? uploads)
    {
        var lista = new List<ImagemValida>();
        if (uploads is null || uploads.Count == 0) return lista;
        if (uploads.Count > Opcoes.MaxImagens)
            throw new ErroValidacao($"Envie no máximo {Opcoes.MaxImagens} imagens.");

        foreach (var up in uploads)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(up.Base64); }
            catch (FormatException) { throw new ErroValidacao($"A imagem '{up.NomeArquivo}' está corrompida."); }

            if (bytes.Length == 0) throw new ErroValidacao($"A imagem '{up.NomeArquivo}' está vazia.");
            if (bytes.Length > Opcoes.MaxBytesImagem)
                throw new ErroValidacao($"A imagem '{up.NomeArquivo}' passa de {Formatacao.TamanhoLegivel(Opcoes.MaxBytesImagem)}.");

            var tipo = DetectarTipo(bytes)
                ?? throw new ErroValidacao($"'{up.NomeArquivo}' não é PNG, JPG, WEBP ou GIF.");

            lista.Add(new ImagemValida(NomeSeguro(up.NomeArquivo, tipo), tipo, bytes));
        }
        return lista;
    }

    public static string? DetectarTipo(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 6 && b[0] == 'G' && b[1] == 'I' && b[2] == 'F' && b[3] == '8') return "image/gif";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F'
            && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }

    public static string NomeSeguro(string? nome, string contentType)
    {
        var baseNome = Path.GetFileNameWithoutExtension(nome ?? "");
        var limpo = new string(baseNome.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray()).Trim('_', '.');
        if (string.IsNullOrEmpty(limpo)) limpo = "print";
        if (limpo.Length > 80) limpo = limpo[..80];
        var ext = contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ""
        };
        return limpo + ext;
    }
}

/// <summary>Erro de entrada do usuário: vira HTTP 400 com a mensagem.</summary>
public sealed class ErroValidacao(string mensagem) : Exception(mensagem);

public static class Relogio
{
    private static readonly TimeZoneInfo? Fuso = BuscarFuso();

    private static TimeZoneInfo? BuscarFuso()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (Exception) { return null; }
    }

    /// <summary>Hora oficial de Brasília (sem fuso, como o banco guarda).</summary>
    public static DateTime AgoraBrasil()
    {
        var agora = Fuso is null
            ? DateTime.UtcNow.AddHours(-3)
            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso);
        return DateTime.SpecifyKind(agora, DateTimeKind.Unspecified);
    }
}
