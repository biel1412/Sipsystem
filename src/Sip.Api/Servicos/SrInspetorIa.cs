using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Sip.Shared;

namespace Sip.Api.Servicos;

public sealed class AnthropicOpcoes
{
    /// <summary>Chave da API Anthropic (console.anthropic.com). Fica só no servidor.</summary>
    public string? ChaveApi { get; set; }
    public string Modelo { get; set; } = "claude-sonnet-5-5";
    public int MaxTokens { get; set; } = 2000;
    public string Url { get; set; } = "https://api.anthropic.com/v1/messages";
}

/// <summary>Tudo que o Sr. Inspetor precisa saber para montar o desvio.</summary>
public sealed record ContextoDesvio(
    PromptPronto Prompt,
    string Descricao,
    string? InstrucaoExtra,
    string? NomeColaborador,
    string? NomeInspetor,
    IReadOnlyDictionary<string, string?> DadosProcesso,
    IReadOnlyList<ImagemValida> Imagens);

/// <summary>
/// "Sr. Inspetor": chama a API do Claude (Messages API) com os prints e a descrição,
/// e devolve assunto + corpo do e-mail de desvio prontos para revisar.
/// </summary>
public sealed class SrInspetorIa(HttpClient http, IOptions<AnthropicOpcoes> opcoes, ILogger<SrInspetorIa> log)
{
    public const string PromptSistema = """
        Você é o Sr. Inspetor, o assistente de qualidade do SIP (Sistema Integrado de Inspeção).
        Sua função é redigir e-mails de registro de desvio de processo para colaboradores, a partir da
        descrição feita pelo inspetor de qualidade e dos prints (capturas de tela) anexados.

        Regras:
        - Escreva em português do Brasil, com tom profissional, respeitoso e claro. Nunca seja ríspido ou irônico.
        - Use somente fatos presentes na descrição, nos dados do processo e no que é visível nos prints.
          Não invente números, nomes, normas ou datas. Se algo importante não estiver claro, escreva
          "[confirmar: ...]" no ponto exato para o inspetor completar antes de enviar.
        - Ao citar um print, refira-se a ele como "print 1", "print 2" etc., na ordem em que foram anexados.
        - Não repita CPF/CNPJ ou outros dados pessoais de clientes que aparecerem nos prints.
        - Comece com uma saudação ao colaborador pelo primeiro nome (se souber) e termine com uma despedida
          seguida do nome do inspetor (se souber) e "Equipe de Qualidade".
        - Não use markdown (sem **, #, tabelas). Para listas, use "- " ou "1. " no início da linha.

        Responda APENAS com um objeto JSON válido, sem texto antes ou depois, no formato:
        {"assunto": "assunto curto do e-mail", "corpo": "texto completo do e-mail"}
        """;

    public async Task<DesvioGerado> GerarAsync(ContextoDesvio ctx, CancellationToken ct = default)
    {
        var cfg = opcoes.Value;
        if (string.IsNullOrWhiteSpace(cfg.ChaveApi))
            throw new ErroValidacao("O Sr. Inspetor não está configurado: falta Anthropic:ChaveApi no servidor.");

        using var req = new HttpRequestMessage(HttpMethod.Post, cfg.Url)
        {
            Content = new StringContent(MontarCorpo(ctx, cfg).ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", cfg.ChaveApi);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await http.SendAsync(req, ct);
        var texto = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            log.LogWarning("Anthropic respondeu {Status}: {Corpo}", (int)resp.StatusCode, texto.Length > 500 ? texto[..500] : texto);
            throw new ErroValidacao(resp.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "A chave da API do Claude é inválida ou não tem permissão.",
                HttpStatusCode.TooManyRequests or (HttpStatusCode)529 => "O Sr. Inspetor está ocupado agora. Tente de novo em alguns segundos.",
                HttpStatusCode.RequestEntityTooLarge => "Os prints são grandes demais. Tente enviar menos imagens ou imagens menores.",
                HttpStatusCode.BadRequest => "O Claude recusou a requisição. Verifique o modelo configurado (Anthropic:Modelo) e as imagens.",
                _ => "O Sr. Inspetor não conseguiu responder agora. Tente novamente."
            });
        }

        return InterpretarResposta(texto, ctx.NomeColaborador);
    }

    public static JsonObject MontarCorpo(ContextoDesvio ctx, AnthropicOpcoes cfg)
    {
        var conteudo = new JsonArray();
        for (var i = 0; i < ctx.Imagens.Count; i++)
        {
            var img = ctx.Imagens[i];
            conteudo.Add(new JsonObject { ["type"] = "text", ["text"] = $"Print {i + 1} ({img.NomeArquivo}):" });
            conteudo.Add(new JsonObject
            {
                ["type"] = "image",
                ["source"] = new JsonObject
                {
                    ["type"] = "base64",
                    ["media_type"] = img.ContentType,
                    ["data"] = Convert.ToBase64String(img.Conteudo),
                }
            });
        }
        conteudo.Add(new JsonObject { ["type"] = "text", ["text"] = MontarPedido(ctx) });

        return new JsonObject
        {
            ["model"] = cfg.Modelo,
            ["max_tokens"] = cfg.MaxTokens,
            ["system"] = PromptSistema,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "user", ["content"] = conteudo }
            }
        };
    }

    public static string MontarPedido(ContextoDesvio ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tarefa: {ctx.Prompt.Instrucao}");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(ctx.NomeColaborador)) sb.AppendLine($"Colaborador: {ctx.NomeColaborador}");
        if (!string.IsNullOrWhiteSpace(ctx.NomeInspetor)) sb.AppendLine($"Inspetor (quem assina): {ctx.NomeInspetor}");

        var dados = ctx.DadosProcesso.Where(d => !string.IsNullOrWhiteSpace(d.Value)).ToList();
        if (dados.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Dados do processo registrado:");
            foreach (var (campo, valor) in dados) sb.AppendLine($"- {campo}: {valor}");
        }

        sb.AppendLine();
        sb.AppendLine("Descrição do erro feita pelo inspetor:");
        sb.AppendLine(string.IsNullOrWhiteSpace(ctx.Descricao) ? "(sem descrição; baseie-se nos prints)" : ctx.Descricao.Trim());

        if (!string.IsNullOrWhiteSpace(ctx.InstrucaoExtra))
        {
            sb.AppendLine();
            sb.AppendLine($"Instrução adicional do inspetor: {ctx.InstrucaoExtra.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine(ctx.Imagens.Count > 0
            ? $"Foram anexados {ctx.Imagens.Count} print(s) acima, na ordem."
            : "Nenhum print foi anexado.");
        return sb.ToString();
    }

    public static DesvioGerado InterpretarResposta(string respostaApi, string? nomeColaborador)
    {
        using var doc = JsonDocument.Parse(respostaApi);
        var texto = new StringBuilder();
        if (doc.RootElement.TryGetProperty("content", out var blocos))
            foreach (var bloco in blocos.EnumerateArray())
                if (bloco.TryGetProperty("type", out var tipo) && tipo.GetString() == "text")
                    texto.Append(bloco.GetProperty("text").GetString());

        return ExtrairDesvio(texto.ToString(), nomeColaborador);
    }

    /// <summary>Lê o JSON {assunto, corpo}; se o modelo fugir do formato, usa o texto inteiro como corpo.</summary>
    public static DesvioGerado ExtrairDesvio(string texto, string? nomeColaborador)
    {
        var assuntoPadrao = string.IsNullOrWhiteSpace(nomeColaborador)
            ? "Registro de desvio de processo"
            : $"Registro de desvio de processo – {nomeColaborador}";

        var inicio = texto.IndexOf('{');
        var fim = texto.LastIndexOf('}');
        if (inicio >= 0 && fim > inicio)
        {
            try
            {
                using var json = JsonDocument.Parse(texto[inicio..(fim + 1)]);
                var raiz = json.RootElement;
                var assunto = raiz.TryGetProperty("assunto", out var a) ? a.GetString() : null;
                var corpo = raiz.TryGetProperty("corpo", out var c) ? c.GetString() : null;
                if (!string.IsNullOrWhiteSpace(corpo))
                    return new DesvioGerado(string.IsNullOrWhiteSpace(assunto) ? assuntoPadrao : assunto.Trim(), corpo.Trim());
            }
            catch (JsonException) { /* cai no texto puro abaixo */ }
        }

        if (string.IsNullOrWhiteSpace(texto))
            throw new ErroValidacao("O Sr. Inspetor devolveu uma resposta vazia. Tente novamente.");
        return new DesvioGerado(assuntoPadrao, texto.Trim());
    }
}
