using System.Text.RegularExpressions;
using Npgsql;

namespace Sip.Api.Dados;

/// <summary>
/// Monta a string de conexão do PostgreSQL. Aceita os dois formatos:
/// - "Host=...;Database=...;Username=...;Password=..." (formato .NET)
/// - "postgres://usuario:senha@host:porta/banco" (formato URL, como o Render mostra)
/// Se Banco:Schema estiver configurado, as tabelas do SIP ficam nesse schema
/// (útil para dividir o mesmo banco com outro sistema sem misturar tabelas).
/// </summary>
public static partial class ConexaoBanco
{
    public static string Montar(string? valor, string? schema)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Configure ConnectionStrings:Sip (ou DATABASE_URL) com a conexão do PostgreSQL.");

        var builder = valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                      || valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? DeUrl(valor.Trim())
            : new NpgsqlConnectionStringBuilder(valor);

        if (!string.IsNullOrWhiteSpace(schema))
            builder.SearchPath = SchemaValido(schema);

        return builder.ConnectionString;
    }

    public static string SchemaValido(string schema)
    {
        var s = schema.Trim().ToLowerInvariant();
        if (!NomeSchema().IsMatch(s))
            throw new InvalidOperationException("Banco:Schema deve ter só letras minúsculas, números e _ (ex.: sip).");
        return s;
    }

    private static NpgsqlConnectionStringBuilder DeUrl(string url)
    {
        var uri = new Uri(url);
        var credenciais = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(credenciais[0]),
            Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null,
        };

        // Endereços externos (fora da rede interna do provedor) exigem SSL.
        if (uri.Host.Contains('.'))
            builder.SslMode = SslMode.Require;

        foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = par.Split('=', 2);
            if (kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && kv.Length == 2
                && Enum.TryParse<SslMode>(kv[1].Replace("-", ""), true, out var modo))
                builder.SslMode = modo;
        }
        return builder;
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex NomeSchema();
}
