using System.ComponentModel.DataAnnotations;

namespace Sip.Shared;

/// <summary>Um botão de prompt pronto do Sr. Inspetor.</summary>
public sealed record PromptPronto(string Id, string Titulo, string Dica, string Instrucao);

public static class PromptsProntos
{
    public static readonly IReadOnlyList<PromptPronto> Todos =
    [
        new("montar", "Montar desvio",
            "Desvio completo: o que foi analisado, o erro e o impacto.",
            "Monte o e-mail de desvio completo: contexto do que foi analisado, qual foi o erro identificado, " +
            "o que as evidências (prints) mostram e qual o impacto do erro."),

        new("melhoria", "Montar desvio com pontos de melhoria",
            "Inclui uma lista de pontos de melhoria.",
            "Monte o e-mail de desvio e inclua uma seção \"Pontos de melhoria\" com 2 a 5 itens práticos " +
            "que ajudem o colaborador a não repetir o erro."),

        new("forma_correta", "Montar desvio e informar forma correta",
            "Explica o procedimento correto passo a passo.",
            "Monte o e-mail de desvio e inclua uma seção \"Forma correta\" explicando, em passos numerados, " +
            "como o procedimento deveria ter sido feito. Use o procedimento correto informado, se houver."),

        new("objetivo", "Montar desvio de forma objetiva",
            "Curto e direto, até 6 linhas.",
            "Monte o e-mail de desvio de forma objetiva e direta: no máximo 6 linhas no corpo, sem rodeios, " +
            "mantendo o tom respeitoso."),

        new("educativo", "Montar desvio com feedback educativo",
            "Tom acolhedor, foco em aprendizado.",
            "Monte o e-mail de desvio com tom educativo e acolhedor: reconheça o esforço, explique o erro como " +
            "oportunidade de aprendizado e termine se colocando à disposição para tirar dúvidas."),

        new("lideranca", "Resumo do desvio para a liderança",
            "Resumo executivo para enviar ao líder.",
            "Monte um resumo executivo do desvio para a liderança do colaborador: colaborador, tipo de erro, " +
            "gravidade, impacto e ação recomendada, em tópicos curtos."),
    ];

    public static PromptPronto? Buscar(string? id) => Todos.FirstOrDefault(p => p.Id == id);
}

public sealed class GerarDesvioRequest
{
    [Required(ErrorMessage = "Escolha um dos prompts.")]
    public string PromptId { get; set; } = "montar";

    /// <summary>Descrição do erro escrita pelo inspetor.</summary>
    public string Descricao { get; set; } = "";

    /// <summary>Instrução extra opcional ("cite o manual X", "trate por você"...).</summary>
    [MaxLength(1000)]
    public string? InstrucaoExtra { get; set; }

    public int? ColaboradorId { get; set; }

    /// <summary>Processo já registrado para usar como contexto (tipo de erro, gravidade etc.).</summary>
    public int? ProcessoId { get; set; }

    public List<ImagemUpload> Imagens { get; set; } = [];
}

public sealed record DesvioGerado(string Assunto, string Corpo);

public sealed class EnviarDesvioRequest
{
    [Required(ErrorMessage = "Informe o destinatário.")]
    public string Para { get; set; } = "";

    /// <summary>Cópias separadas por vírgula ou ponto e vírgula.</summary>
    public string? Cc { get; set; }

    [Required(ErrorMessage = "Informe o assunto."), MaxLength(200)]
    public string Assunto { get; set; } = "";

    [Required(ErrorMessage = "O corpo do e-mail está vazio.")]
    public string Corpo { get; set; } = "";

    public int? ProcessoId { get; set; }

    /// <summary>Prints que vão como anexo.</summary>
    public List<ImagemUpload> Imagens { get; set; } = [];
}

public sealed record EnvioHistorico(int Id, DateTime EnviadoEm, string EnviadoPor, string Para, string Assunto, int? ProcessoId);
