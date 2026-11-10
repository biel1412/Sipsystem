using System.ComponentModel.DataAnnotations;

namespace Sip.Shared;

// ---------- Autenticação ----------

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha.")]
    public string Senha { get; set; } = "";
}

public sealed class TrocarSenhaRequest
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    public string SenhaAtual { get; set; } = "";

    [Required(ErrorMessage = "Informe a nova senha."), MinLength(8, ErrorMessage = "A nova senha precisa de pelo menos 8 caracteres.")]
    public string NovaSenha { get; set; } = "";

    [Compare(nameof(NovaSenha), ErrorMessage = "As senhas não conferem.")]
    public string ConfirmarSenha { get; set; } = "";
}

public sealed record UsuarioInfo(string Email, string Nome, bool PrecisaTrocarSenha);

public sealed record Mensagem(string Texto);

// ---------- Colaboradores ----------

public sealed class NovoColaboradorRequest
{
    [Required(ErrorMessage = "Informe o nome."), MaxLength(100)]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail."), EmailAddress(ErrorMessage = "E-mail inválido."), MaxLength(120)]
    public string Email { get; set; } = "";
}

public sealed record ColaboradorDto(int Id, string Nome, string Email, int TotalProcessos);

// ---------- Processos ----------

public sealed class ImagemUpload
{
    public string NomeArquivo { get; set; } = "";
    public string ContentType { get; set; } = "";
    /// <summary>Conteúdo do arquivo em base64.</summary>
    public string Base64 { get; set; } = "";
}

public sealed class NovoProcessoRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o colaborador.")]
    public int ColaboradorId { get; set; }

    public string Descricao { get; set; } = "";

    [Required]
    public string Status { get; set; } = Opcoes.StatusConforme;

    /// <summary>"Procedimento correto".</summary>
    public string? Observacoes { get; set; }

    public string? Coordenacao { get; set; }
    public string? Lideranca { get; set; }
    public DateOnly? DataInspecao { get; set; }
    public string? Inspetor { get; set; }

    [MaxLength(18)]
    public string? CpfCnpj { get; set; }

    public string? Coop { get; set; }
    public string? Tipo { get; set; } = Opcoes.Tipo[0];
    public DateOnly? DataExecucao { get; set; }
    public string? InternoExterno { get; set; } = Opcoes.InternoExterno[0];
    public string? TipoAutorizacao { get; set; } = Opcoes.TipoAutorizacao[0];
    public string? TipoErro { get; set; } = Opcoes.TipoErro[0].Valor;
    public string? Gravidade { get; set; } = Opcoes.Gravidade[0].Valor;
    public string? DesvioAtencao { get; set; } = Opcoes.DesvioAtencao[0].Valor;
    public string? Reversao { get; set; } = Opcoes.Reversao[0].Valor;
    public string? Fluxos { get; set; }
    public string? Solicitante { get; set; }

    public List<ImagemUpload> Imagens { get; set; } = [];
}

public sealed record ProcessoCriado(int Id, string Texto);

public sealed record ProcessoResumo(
    int Id, DateTime DataAnalise, int ColaboradorId, string Colaborador, string Status, string Descricao, int QtdImagens);

public sealed record ImagemInfo(int Id, string NomeArquivo);

public sealed record ProcessoDetalhe(
    int Id,
    DateTime DataAnalise,
    int ColaboradorId,
    string Colaborador,
    string ColaboradorEmail,
    string Status,
    string Descricao,
    string? Observacoes,
    string? Coordenacao,
    string? Lideranca,
    DateOnly? DataInspecao,
    string? Inspetor,
    string? CpfCnpj,
    string? Coop,
    string? Tipo,
    DateOnly? DataExecucao,
    string? InternoExterno,
    string? TipoAutorizacao,
    string? TipoErro,
    string? Gravidade,
    string? DesvioAtencao,
    string? Reversao,
    string? Fluxos,
    string? Solicitante,
    IReadOnlyList<ImagemInfo> Imagens);

public sealed record Pagina<T>(IReadOnlyList<T> Itens, int PaginaAtual, int TotalPaginas, int TotalItens);

public sealed record PainelResultado(int Total, int Conformes, int Desvios, Pagina<ProcessoResumo> Processos);

public sealed record PerfilResumo(
    int TotalMesAtual, int ConformesMesAtual, int DesviosMesAtual,
    int DiffTotal, int DiffConformes, int DiffDesvios);

public sealed record GraficoResumo(int Conformes, int Desvios);

public sealed class DeletarPorColaboradorRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Selecione o colaborador.")]
    public int ColaboradorId { get; set; }
}

public sealed record DeletarResultado(int ProcessosRemovidos);
