using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;

namespace Sip.Api.Servicos;

public sealed class EmailOpcoes
{
    public string Servidor { get; set; } = "smtp.gmail.com";
    public int Porta { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string? Usuario { get; set; }
    /// <summary>No Gmail, use uma "senha de app", não a senha da conta.</summary>
    public string? Senha { get; set; }
    public string? Remetente { get; set; }
    public string NomeRemetente { get; set; } = "SIP - Sr. Inspetor";

    public bool Configurado => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrWhiteSpace(Senha);
}

public sealed record Anexo(string Nome, string ContentType, byte[] Conteudo);

public sealed record EmailParaEnviar(
    IReadOnlyList<string> Para,
    IReadOnlyList<string> Cc,
    string? ResponderPara,
    string Assunto,
    string Corpo,
    IReadOnlyList<Anexo> Anexos);

public interface IEmailServico
{
    Task EnviarAsync(EmailParaEnviar email, CancellationToken ct = default);
}

public sealed class EmailSmtpServico(IOptions<EmailOpcoes> opcoes, ILogger<EmailSmtpServico> log) : IEmailServico
{
    public async Task EnviarAsync(EmailParaEnviar email, CancellationToken ct = default)
    {
        var cfg = opcoes.Value;
        if (!cfg.Configurado)
            throw new ErroValidacao("O envio de e-mail não está configurado no servidor (Email:Usuario e Email:Senha).");

        using var msg = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(cfg.Remetente) ? cfg.Usuario! : cfg.Remetente, cfg.NomeRemetente, Encoding.UTF8),
            Subject = email.Assunto,
            SubjectEncoding = Encoding.UTF8,
            Body = email.Corpo,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        foreach (var p in email.Para) msg.To.Add(p);
        foreach (var c in email.Cc) msg.CC.Add(c);
        if (!string.IsNullOrWhiteSpace(email.ResponderPara)) msg.ReplyToList.Add(email.ResponderPara);

        // Versão HTML para ficar bonita no Outlook/Gmail; a versão texto fica como alternativa.
        var html = AlternateView.CreateAlternateViewFromString(CorpoHtml(email.Corpo), Encoding.UTF8, MediaTypeNames.Text.Html);
        msg.AlternateViews.Add(html);

        foreach (var anexo in email.Anexos)
            msg.Attachments.Add(new Attachment(new MemoryStream(anexo.Conteudo), anexo.Nome, anexo.ContentType));

        using var smtp = new SmtpClient(cfg.Servidor, cfg.Porta)
        {
            EnableSsl = cfg.UsarSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(cfg.Usuario, cfg.Senha),
            Timeout = 60_000,
        };

        try
        {
            await smtp.SendMailAsync(msg, ct);
        }
        catch (SmtpException ex)
        {
            log.LogError(ex, "Falha ao enviar e-mail para {Para}", string.Join(", ", email.Para));
            throw new ErroValidacao("Não foi possível enviar o e-mail. Verifique a configuração de SMTP e os destinatários.");
        }
    }

    public static string CorpoHtml(string texto)
    {
        var paragrafos = WebUtility.HtmlEncode(texto.Replace("\r\n", "\n"))
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => $"<p style=\"margin:0 0 12px\">{p.Replace("\n", "<br>")}</p>");
        return "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#2c3e50;line-height:1.5\">"
             + string.Concat(paragrafos)
             + "<p style=\"margin-top:24px;font-size:12px;color:#7f8c8d\">Enviado pelo SIP – Sistema Integrado de Inspeção</p></div>";
    }
}
