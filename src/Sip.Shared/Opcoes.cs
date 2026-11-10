namespace Sip.Shared;

/// <summary>
/// Listas fixas usadas nos formulários (mesmas opções do SIP em Flask).
/// Para mudar uma lista, edite aqui: a API e o front usam o mesmo arquivo.
/// </summary>
public static class Opcoes
{
    public const string StatusConforme = "Conforme";
    public const string StatusDesvio = "Desvio";

    public static readonly IReadOnlyList<string> Status = [StatusConforme, StatusDesvio];

    public static readonly IReadOnlyList<string> Coordenacao =
        ["larissa.gregoria", "eluane.machado", "lilian.flores"];

    public static readonly IReadOnlyList<string> Lideranca =
    [
        "gabriel.militz", "tacio.pozzati", "diogo.machado", "tais.melo", "ilto.rodrigues",
        "nathalia.moraes", "mauricio.konig", "kerolin.vargas", "gabriela.doring"
    ];

    public static readonly IReadOnlyList<string> Inspetor =
    [
        "andressa.castro", "andressa.belmiro", "andrieli.bock", "emilie.vollmann", "fabiana.soares",
        "erika.figueira", "gabrielle.pippi", "guilherme.losekann", "marivane.fernandes", "monica.laucksen"
    ];

    public static readonly IReadOnlyList<string> Tipo = ["Cooperativa", "Autorizar", "Autoatendimento"];

    public static readonly IReadOnlyList<string> InternoExterno = ["Interno", "Externo", "Chamado"];

    public static readonly IReadOnlyList<string> TipoAutorizacao =
    [
        "BEM_NOVO", "CERTIDAO", "ENDERECO", "FONTE_RENDA", "PESSOA",
        "PRODUTIVIDADE", "PRODUTOR", "RELACIONAMENTO", "TRIBUTACAO"
    ];

    public static readonly IReadOnlyList<Opcao> TipoErro =
    [
        new("Sem erro", "Sem erro"),
        new("Aprovacao Incorreta", "Aprovação Incorreta"),
        new("Preenchimento Incorreto", "Preenchimento Incorreto"),
        new("Reprovacao Incorreta", "Reprovação Incorreta"),
        new("Reprovacao Incompleta", "Reprovação Incompleta"),
        new("Documento Invalido", "Documento Inválido"),
    ];

    public static readonly IReadOnlyList<Opcao> Gravidade =
    [
        new("Sem Erro", "Sem Erro"), new("Grave", "Grave"), new("Gravissimo", "Gravíssimo")
    ];

    public static readonly IReadOnlyList<Opcao> DesvioAtencao = [new("Sim", "Sim"), new("Nao", "Não")];

    public static readonly IReadOnlyList<Opcao> Reversao =
    [
        new("Grave", "Grave"), new("Gravissimo", "Gravíssimo"), new("Nao", "Não")
    ];

    /// <summary>Tipos de imagem aceitos nos prints (os mesmos que o Claude consegue ler).</summary>
    public static readonly IReadOnlyList<string> TiposImagem = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    /// <summary>Texto de exibição de um valor salvo (ex.: "Gravissimo" → "Gravíssimo").</summary>
    public static string? Texto(IReadOnlyList<Opcao> lista, string? valor) =>
        lista.FirstOrDefault(o => o.Valor == valor)?.Texto ?? valor;

    public const int MaxImagens = 10;
    public const long MaxBytesImagem = 5 * 1024 * 1024;
}

public sealed record Opcao(string Valor, string Texto);
