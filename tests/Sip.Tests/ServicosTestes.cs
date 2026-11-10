using System.IO.Compression;
using System.Text.Json.Nodes;
using Sip.Api.Servicos;
using Sip.Shared;

namespace Sip.Tests;

public class ServicosTestes
{
    private static readonly byte[] PngMinimo =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    [Fact]
    public void Imagens_AceitaPngEDetectaTipoReal()
    {
        var lista = Imagens.Validar([new ImagemUpload { NomeArquivo = "erro tela.jpg", ContentType = "image/jpeg", Base64 = Convert.ToBase64String(PngMinimo) }]);
        var img = Assert.Single(lista);
        Assert.Equal("image/png", img.ContentType); // vale o conteúdo, não o que o navegador disse
        Assert.Equal("erro_tela.png", img.NomeArquivo);
    }

    [Fact]
    public void Imagens_RecusaArquivoQueNaoEImagem()
    {
        var exe = Convert.ToBase64String("MZ\u0090 isto é um executável"u8.ToArray());
        Assert.Throws<ErroValidacao>(() => Imagens.Validar([new ImagemUpload { NomeArquivo = "x.png", Base64 = exe }]));
    }

    [Fact]
    public void Imagens_RecusaMaisQueOLimite()
    {
        var muitas = Enumerable.Range(0, Opcoes.MaxImagens + 1)
            .Select(_ => new ImagemUpload { NomeArquivo = "a.png", Base64 = Convert.ToBase64String(PngMinimo) }).ToList();
        Assert.Throws<ErroValidacao>(() => Imagens.Validar(muitas));
    }

    [Theory]
    [InlineData("../../etc/passwd", "image/png", "passwd.png")]
    [InlineData("", "image/jpeg", "print.jpg")]
    [InlineData("Captura de tela 2026.png", "image/webp", "Captura_de_tela_2026.webp")]
    public void Imagens_NomeSeguro(string nome, string tipo, string esperado) =>
        Assert.Equal(esperado, Imagens.NomeSeguro(nome, tipo));

    [Fact]
    public void Xlsx_GeraPlanilhaValida()
    {
        var bytes = GeradorXlsx.Gerar("2026-01-01_a_2026-01-31", ["A", "B"], [["olá & <teste>", null], ["x\u0001y", "2"]]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
        using var leitor = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var xml = leitor.ReadToEnd();
        Assert.Contains("olá &amp; &lt;teste&gt;", xml);
        Assert.Contains("xy", xml); // caractere de controle removido
        Assert.Contains("state=\"frozen\"", xml);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(18, "S")]
    public void Xlsx_LetraDaColuna(int indice, string letra) => Assert.Equal(letra, GeradorXlsx.Coluna(indice));

    [Fact]
    public void Xlsx_NomeDeAbaLimitadoA31()
    {
        Assert.Equal(31, GeradorXlsx.NomeAbaValido(new string('a', 40)).Length);
        Assert.Equal("ab", GeradorXlsx.NomeAbaValido("a/b"));
    }

    [Fact]
    public void SrInspetor_LeJsonDaResposta()
    {
        var d = SrInspetorIa.ExtrairDesvio("Claro! {\"assunto\": \"Desvio X\", \"corpo\": \"Olá\\n\\nTexto\"} ", "Maria");
        Assert.Equal("Desvio X", d.Assunto);
        Assert.Equal("Olá\n\nTexto", d.Corpo);
    }

    [Fact]
    public void SrInspetor_SemJson_UsaTextoComoCorpo()
    {
        var d = SrInspetorIa.ExtrairDesvio("Olá Maria, identificamos...", "Maria");
        Assert.Equal("Registro de desvio de processo – Maria", d.Assunto);
        Assert.StartsWith("Olá Maria", d.Corpo);
    }

    [Fact]
    public void SrInspetor_InterpretaRespostaDaApi()
    {
        const string api = """{"content":[{"type":"text","text":"{\"assunto\":\"A\",\"corpo\":\"B\"}"}]}""";
        Assert.Equal(new DesvioGerado("A", "B"), SrInspetorIa.InterpretarResposta(api, null));
    }

    [Fact]
    public void SrInspetor_MontaRequisicaoComImagensEContexto()
    {
        var ctx = new ContextoDesvio(PromptsProntos.Buscar("forma_correta")!, "Aprovou sem certidão", "cite o manual",
            "Maria", "Gabriel", new Dictionary<string, string?> { ["Gravidade"] = "Grave", ["Vazio"] = null },
            [new ImagemValida("p.png", "image/png", PngMinimo)]);

        var corpo = SrInspetorIa.MontarCorpo(ctx, new AnthropicOpcoes { Modelo = "modelo-x", MaxTokens = 123 });
        Assert.Equal("modelo-x", (string?)corpo["model"]);
        Assert.Equal(123, (int?)corpo["max_tokens"]);

        var conteudo = corpo["messages"]![0]!["content"]!.AsArray();
        Assert.Equal(["text", "image", "text"], conteudo.Select(b => (string?)b!["type"]));
        Assert.Equal("image/png", (string?)conteudo[1]!["source"]!["media_type"]);

        var pedido = (string)conteudo[2]!["text"]!;
        Assert.Contains("Forma correta", pedido);
        Assert.Contains("Aprovou sem certidão", pedido);
        Assert.Contains("cite o manual", pedido);
        Assert.Contains("- Gravidade: Grave", pedido);
        Assert.DoesNotContain("Vazio", pedido);
    }

    [Theory]
    [InlineData("123.456.789-01", "***.***.***-01")]
    [InlineData("12.345.678/0001-99", "**.***.***/****-99")]
    [InlineData(null, null)]
    public void Formatacao_MascaraDocumento(string? doc, string? esperado) =>
        Assert.Equal(esperado, Formatacao.MascararDocumento(doc));

    [Fact]
    public void Formatacao_SeparaEmails()
    {
        Assert.Equal("a@x.com|b@y.com", string.Join("|", Formatacao.SepararEmails(" a@x.com; b@y.com, A@x.com ")));
        Assert.True(Formatacao.EmailValido("maria@empresa.com.br"));
        Assert.False(Formatacao.EmailValido("Maria <maria@empresa.com>"));
        Assert.False(Formatacao.EmailValido("sem-arroba"));
    }

    [Fact]
    public void EmailHtml_EscapaConteudo()
    {
        var html = EmailSmtpServico.CorpoHtml("Olá <b>\n\nfim");
        Assert.Contains("&lt;b&gt;", html);
        Assert.Contains("<p", html);
    }
}
