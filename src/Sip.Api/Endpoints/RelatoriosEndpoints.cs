using Sip.Api.Dados;
using Sip.Api.Seguranca;
using Sip.Api.Servicos;
using Sip.Shared;

namespace Sip.Api.Endpoints;

public static class RelatoriosEndpoints
{
    // Mesma ordem de colunas da planilha da qualidade.
    public static readonly string[] CabecalhosXlsx =
    [
        "COORDENACAO", "LIDERANCA", "DATA DA INSPECAO", "INSPETOR", "CPF/CNPJ", "COOP", "TIPO",
        "DATA DE EXECUCAO", "INTERNO OU EXTERNO", "TIPO DE AUTORIZACAO", "COLABORADOR",
        "TIPO DE ERRO", "DESCRICAO", "GRAVIDADE", "DESVIO DE ATENCAO", "REVERSAO",
        "QUAIS FLUXOS", "OBSERVACOES", "SOLICITANTE"
    ];

    public static void MapRelatorios(this RouteGroupBuilder api)
    {
        api.MapGet("/painel", async (Db db, int? colaboradorId, DateOnly? dataInicio, DateOnly? dataFim,
            string? status, string? palavra, int? pagina) =>
        {
            var filtro = new ProcessosEndpoints.Filtro(colaboradorId, dataInicio, dataFim, status, palavra);
            // Os contadores ignoram o filtro de status, como no painel original (total x conformes x desvios).
            var (where, ps) = ProcessosEndpoints.MontarWhere(filtro with { Status = null });
            var contagem = await db.PrimeiroAsync(
                $"""
                SELECT COUNT(*) AS total,
                       COUNT(*) FILTER (WHERE p.status = 'Conforme') AS conformes,
                       COUNT(*) FILTER (WHERE p.status = 'Desvio') AS desvios
                FROM processo p {where}
                """,
                r => (Total: r.Inteiro("total"), Conformes: r.Inteiro("conformes"), Desvios: r.Inteiro("desvios")), ps);

            var lista = await ProcessosEndpoints.ListarAsync(db, filtro, pagina ?? 1);
            return new PainelResultado(contagem.Total, contagem.Conformes, contagem.Desvios, lista);
        });

        api.MapGet("/perfil", async (Db db) =>
        {
            var hoje = Relogio.AgoraBrasil().Date;
            var inicioAtual = new DateTime(hoje.Year, hoje.Month, 1);
            var inicioAnterior = inicioAtual.AddMonths(-1);

            var c = await db.PrimeiroAsync(
                """
                SELECT
                  COUNT(*) FILTER (WHERE data_analise >= @atual) AS total_atual,
                  COUNT(*) FILTER (WHERE data_analise >= @atual AND status = 'Conforme') AS conf_atual,
                  COUNT(*) FILTER (WHERE data_analise >= @atual AND status = 'Desvio') AS desv_atual,
                  COUNT(*) FILTER (WHERE data_analise >= @anterior AND data_analise < @atual) AS total_ant,
                  COUNT(*) FILTER (WHERE data_analise >= @anterior AND data_analise < @atual AND status = 'Conforme') AS conf_ant,
                  COUNT(*) FILTER (WHERE data_analise >= @anterior AND data_analise < @atual AND status = 'Desvio') AS desv_ant
                FROM processo
                """,
                r => new
                {
                    TA = r.Inteiro("total_atual"), CA = r.Inteiro("conf_atual"), DA = r.Inteiro("desv_atual"),
                    TP = r.Inteiro("total_ant"), CP = r.Inteiro("conf_ant"), DP = r.Inteiro("desv_ant")
                },
                ("atual", inicioAtual), ("anterior", inicioAnterior));

            return new PerfilResumo(c!.TA, c.CA, c.DA, c.TA - c.TP, c.CA - c.CP, c.DA - c.DP);
        });

        api.MapGet("/grafico", async (Db db) => await db.PrimeiroAsync(
            """
            SELECT COUNT(*) FILTER (WHERE status = 'Conforme') AS conformes,
                   COUNT(*) FILTER (WHERE status = 'Desvio') AS desvios
            FROM processo
            """,
            r => new GraficoResumo(r.Inteiro("conformes"), r.Inteiro("desvios"))));

        api.MapGet("/exportar-xlsx", async (Db db, CriptografiaAes aes, DateOnly? dataInicio, DateOnly? dataFim) =>
        {
            if (dataInicio is null || dataFim is null)
                return Results.BadRequest(new Mensagem("Informe o intervalo de datas para exportar."));
            if (dataFim < dataInicio)
                return Results.BadRequest(new Mensagem("A data final é anterior à data inicial."));

            var linhas = await db.ListarAsync(
                """
                SELECT d.coordenacao, d.lideranca, d.data_inspecao, d.inspetor, d.cpf_cnpj_cripto, d.coop, d.tipo,
                       d.data_execucao, d.interno_externo, d.tipo_autorizacao, c.nome AS colaborador, d.tipo_erro,
                       p.descricao, d.gravidade, d.desvio_atencao, d.reversao, d.fluxos, p.observacoes, d.solicitante
                FROM processo p
                JOIN colaborador c ON c.id = p.colaborador_id
                LEFT JOIN detalhes_processo d ON d.processo_id = p.id
                WHERE p.data_analise >= @ini AND p.data_analise < @fim
                ORDER BY p.data_analise
                """,
                r => (IReadOnlyList<string?>)
                [
                    r.TextoOuNulo("coordenacao"), r.TextoOuNulo("lideranca"), Data(r.DataOuNula("data_inspecao")),
                    r.TextoOuNulo("inspetor"), aes.DescriptografarTexto(r.TextoOuNulo("cpf_cnpj_cripto")), r.TextoOuNulo("coop"),
                    r.TextoOuNulo("tipo"), Data(r.DataOuNula("data_execucao")), r.TextoOuNulo("interno_externo"),
                    r.TextoOuNulo("tipo_autorizacao"), r.TextoOuNulo("colaborador"), r.TextoOuNulo("tipo_erro"),
                    r.TextoOuNulo("descricao"), r.TextoOuNulo("gravidade"), r.TextoOuNulo("desvio_atencao"),
                    r.TextoOuNulo("reversao"), r.TextoOuNulo("fluxos"), r.TextoOuNulo("observacoes"), r.TextoOuNulo("solicitante"),
                ],
                ("ini", dataInicio.Value.ToDateTime(TimeOnly.MinValue)),
                ("fim", dataFim.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)));

            var periodo = $"{dataInicio:yyyy-MM-dd}_a_{dataFim:yyyy-MM-dd}";
            var arquivo = GeradorXlsx.Gerar(periodo, CabecalhosXlsx, linhas);
            return Results.File(arquivo,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"processos_{periodo}.xlsx");
        });
    }

    private static string? Data(DateOnly? d) => d?.ToString("dd/MM/yyyy");
}
