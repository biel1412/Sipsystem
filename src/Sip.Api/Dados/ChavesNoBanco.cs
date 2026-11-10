using System.Data.Common;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Sip.Api.Seguranca;

namespace Sip.Api.Dados;

/// <summary>
/// Guarda no PostgreSQL as chaves que o ASP.NET usa para proteger o cookie de login.
/// Sem isso, em hospedagens como o Render (disco temporário), todo mundo seria deslogado
/// a cada reinício ou deploy. As chaves são gravadas criptografadas com a mesma chave AES-256.
/// </summary>
public sealed class ChavesNoBanco(DbDataSource fonte, CriptografiaAes aes) : IXmlRepository
{
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS chave_protecao (
            nome       VARCHAR(200) PRIMARY KEY,
            xml_cripto TEXT NOT NULL,
            criado_em  TIMESTAMP NOT NULL DEFAULT NOW()
        );
        """;

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var cmd = fonte.CreateCommand("SELECT xml_cripto FROM chave_protecao ORDER BY criado_em");
        using var r = cmd.ExecuteReader();
        var lista = new List<XElement>();
        while (r.Read()) lista.Add(XElement.Parse(aes.DescriptografarTexto(r.GetString(0))!));
        return lista;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        using var cmd = fonte.CreateCommand(
            "INSERT INTO chave_protecao (nome, xml_cripto) VALUES (@n, @x) ON CONFLICT (nome) DO UPDATE SET xml_cripto = EXCLUDED.xml_cripto");
        Db.AdicionarParametros(cmd, [("n", friendlyName), ("x", aes.CriptografarTexto(element.ToString(SaveOptions.DisableFormatting)))]);
        cmd.ExecuteNonQuery();
    }
}
