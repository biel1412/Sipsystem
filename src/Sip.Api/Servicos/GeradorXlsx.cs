using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Sip.Api.Servicos;

/// <summary>
/// Gera um .xlsx simples (uma aba, cabeçalho cinza em negrito, bordas, quebra de texto,
/// primeira linha congelada) usando só o .NET, sem biblioteca externa.
/// </summary>
public static class GeradorXlsx
{
    public static byte[] Gerar(string nomeAba, IReadOnlyList<string> cabecalhos, IEnumerable<IReadOnlyList<string?>> linhas)
    {
        var dados = linhas.ToList();
        var larguras = cabecalhos.Select(c => (double)c.Length).ToArray();
        foreach (var linha in dados)
            for (var i = 0; i < larguras.Length && i < linha.Count; i++)
                larguras[i] = Math.Max(larguras[i], Math.Min(60, (linha[i] ?? "").Length));

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escrever(zip, "[Content_Types].xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
            Escrever(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Escrever(zip, "xl/_rels/workbook.xml.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
            Escrever(zip, "xl/styles.xml",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><color rgb="FF000000"/><name val="Calibri"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFD9D9D9"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"/><right style="thin"/><top style="thin"/><bottom style="thin"/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="left" vertical="top" wrapText="1"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
            Escrever(zip, "xl/workbook.xml", Workbook(NomeAbaValido(nomeAba)));
            Escrever(zip, "xl/worksheets/sheet1.xml", Planilha(cabecalhos, dados, larguras));
        }
        return memoria.ToArray();
    }

    public static string NomeAbaValido(string nome)
    {
        var limpo = new string(nome.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray()).Trim('\'');
        if (limpo.Length == 0) limpo = "Planilha";
        return limpo.Length > 31 ? limpo[..31] : limpo;
    }

    private static string Workbook(string aba)
    {
        var sb = new StringBuilder();
        using (var w = NovoWriter(sb))
        {
            w.WriteStartDocument(true);
            w.WriteStartElement("workbook", Ns);
            w.WriteAttributeString("xmlns", "r", null, NsR);
            w.WriteStartElement("sheets", Ns);
            w.WriteStartElement("sheet", Ns);
            w.WriteAttributeString("name", aba);
            w.WriteAttributeString("sheetId", "1");
            w.WriteAttributeString("id", NsR, "rId1");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }
        return sb.ToString();
    }

    private static string Planilha(IReadOnlyList<string> cabecalhos, List<IReadOnlyList<string?>> linhas, double[] larguras)
    {
        var sb = new StringBuilder();
        using (var w = NovoWriter(sb))
        {
            w.WriteStartDocument(true);
            w.WriteStartElement("worksheet", Ns);

            // Congela a primeira linha (cabeçalho)
            w.WriteStartElement("sheetViews", Ns);
            w.WriteStartElement("sheetView", Ns);
            w.WriteAttributeString("workbookViewId", "0");
            w.WriteStartElement("pane", Ns);
            w.WriteAttributeString("ySplit", "1");
            w.WriteAttributeString("topLeftCell", "A2");
            w.WriteAttributeString("activePane", "bottomLeft");
            w.WriteAttributeString("state", "frozen");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();

            w.WriteStartElement("cols", Ns);
            for (var i = 0; i < larguras.Length; i++)
            {
                w.WriteStartElement("col", Ns);
                w.WriteAttributeString("min", (i + 1).ToString());
                w.WriteAttributeString("max", (i + 1).ToString());
                w.WriteAttributeString("width", (larguras[i] + 2).ToString(System.Globalization.CultureInfo.InvariantCulture));
                w.WriteAttributeString("customWidth", "1");
                w.WriteEndElement();
            }
            w.WriteEndElement();

            w.WriteStartElement("sheetData", Ns);
            EscreverLinha(w, 1, cabecalhos, estilo: 1);
            var numero = 2;
            foreach (var linha in linhas) EscreverLinha(w, numero++, linha, estilo: 2);
            w.WriteEndElement();

            w.WriteEndElement();
        }
        return sb.ToString();
    }

    private static void EscreverLinha(XmlWriter w, int numero, IReadOnlyList<string?> valores, int estilo)
    {
        w.WriteStartElement("row", Ns);
        w.WriteAttributeString("r", numero.ToString());
        for (var i = 0; i < valores.Count; i++)
        {
            w.WriteStartElement("c", Ns);
            w.WriteAttributeString("r", Coluna(i) + numero);
            w.WriteAttributeString("s", estilo.ToString());
            w.WriteAttributeString("t", "inlineStr");
            w.WriteStartElement("is", Ns);
            w.WriteStartElement("t", Ns);
            w.WriteAttributeString("xml", "space", null, "preserve");
            w.WriteString(Limpar(valores[i]));
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }

    /// <summary>0 → A, 25 → Z, 26 → AA...</summary>
    public static string Coluna(int indice)
    {
        var nome = "";
        for (var n = indice + 1; n > 0; n = (n - 1) / 26) nome = (char)('A' + (n - 1) % 26) + nome;
        return nome;
    }

    /// <summary>Remove caracteres de controle que o XML não aceita.</summary>
    private static string Limpar(string? texto) =>
        string.IsNullOrEmpty(texto) ? "" : new string(texto.Where(c => c >= 0x20 || c is '\t' or '\n' or '\r').ToArray());

    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static XmlWriter NovoWriter(StringBuilder sb) =>
        XmlWriter.Create(new StringWriterUtf8(sb), new XmlWriterSettings { Encoding = new UTF8Encoding(false) });

    private static void Escrever(ZipArchive zip, string caminho, string conteudo)
    {
        var entrada = zip.CreateEntry(caminho, CompressionLevel.Optimal);
        using var s = new StreamWriter(entrada.Open(), new UTF8Encoding(false));
        s.Write(conteudo);
    }

    private sealed class StringWriterUtf8(StringBuilder sb) : StringWriter(sb)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
