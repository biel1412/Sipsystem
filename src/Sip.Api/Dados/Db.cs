using System.Data.Common;
using System.Reflection;

namespace Sip.Api.Dados;

/// <summary>
/// Acesso ao PostgreSQL com SQL direto e parâmetros (@nome), sem ORM.
/// Todo valor vindo do usuário passa por parâmetro: nada de concatenar SQL.
/// </summary>
public sealed class Db(DbDataSource fonte)
{
    public async Task<List<T>> ListarAsync<T>(string sql, Func<DbDataReader, T> mapear, params (string Nome, object? Valor)[] parametros)
    {
        await using var cmd = Criar(sql, parametros);
        await using var leitor = await cmd.ExecuteReaderAsync();
        var lista = new List<T>();
        while (await leitor.ReadAsync()) lista.Add(mapear(leitor));
        return lista;
    }

    public async Task<T?> PrimeiroAsync<T>(string sql, Func<DbDataReader, T> mapear, params (string Nome, object? Valor)[] parametros)
    {
        await using var cmd = Criar(sql, parametros);
        await using var leitor = await cmd.ExecuteReaderAsync();
        return await leitor.ReadAsync() ? mapear(leitor) : default;
    }

    public async Task<T> EscalarAsync<T>(string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var cmd = Criar(sql, parametros);
        var valor = await cmd.ExecuteScalarAsync();
        return valor is null or DBNull ? default! : (T)Convert.ChangeType(valor, typeof(T));
    }

    public async Task<int> ExecutarAsync(string sql, params (string Nome, object? Valor)[] parametros)
    {
        await using var cmd = Criar(sql, parametros);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Executa vários comandos na mesma transação (tudo ou nada).</summary>
    public async Task<T> TransacaoAsync<T>(Func<Transacao, Task<T>> trabalho)
    {
        await using var conexao = await fonte.OpenConnectionAsync();
        await using var tx = await conexao.BeginTransactionAsync();
        var resultado = await trabalho(new Transacao(conexao, tx));
        await tx.CommitAsync();
        return resultado;
    }

    public async Task CriarEsquemaAsync()
    {
        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Sip.Api.Dados.Schema.sql")
            ?? throw new InvalidOperationException("Schema.sql não encontrado nos recursos.");
        using var leitor = new StreamReader(stream);
        await ExecutarAsync(await leitor.ReadToEndAsync());
    }

    private DbCommand Criar(string sql, (string Nome, object? Valor)[] parametros)
    {
        var cmd = fonte.CreateCommand(sql);
        AdicionarParametros(cmd, parametros);
        return cmd;
    }

    internal static void AdicionarParametros(DbCommand cmd, (string Nome, object? Valor)[] parametros)
    {
        foreach (var (nome, valor) in parametros)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = nome;
            p.Value = valor ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    public sealed class Transacao(DbConnection conexao, DbTransaction tx)
    {
        public async Task<T> EscalarAsync<T>(string sql, params (string Nome, object? Valor)[] parametros)
        {
            await using var cmd = Criar(sql, parametros);
            var valor = await cmd.ExecuteScalarAsync();
            return valor is null or DBNull ? default! : (T)Convert.ChangeType(valor, typeof(T));
        }

        public async Task<int> ExecutarAsync(string sql, params (string Nome, object? Valor)[] parametros)
        {
            await using var cmd = Criar(sql, parametros);
            return await cmd.ExecuteNonQueryAsync();
        }

        private DbCommand Criar(string sql, (string Nome, object? Valor)[] parametros)
        {
            var cmd = conexao.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            AdicionarParametros(cmd, parametros);
            return cmd;
        }
    }
}

/// <summary>Atalhos para ler colunas que podem ser nulas.</summary>
public static class LeitorExtensoes
{
    public static string? TextoOuNulo(this DbDataReader r, string coluna)
    {
        var i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static string Texto(this DbDataReader r, string coluna) => r.TextoOuNulo(coluna) ?? "";

    public static int Inteiro(this DbDataReader r, string coluna) => Convert.ToInt32(r.GetValue(r.GetOrdinal(coluna)));

    public static int? InteiroOuNulo(this DbDataReader r, string coluna)
    {
        var i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    public static DateTime DataHora(this DbDataReader r, string coluna) => r.GetDateTime(r.GetOrdinal(coluna));

    public static DateOnly? DataOuNula(this DbDataReader r, string coluna)
    {
        var i = r.GetOrdinal(coluna);
        return r.IsDBNull(i) ? null : r.GetFieldValue<DateOnly>(i);
    }

    public static byte[] Bytes(this DbDataReader r, string coluna) => r.GetFieldValue<byte[]>(r.GetOrdinal(coluna));
}
