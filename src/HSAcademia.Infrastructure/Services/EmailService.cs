using HSAcademia.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;

namespace HSAcademia.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string academyName, string adminName, string tempPassword)
    {
        var subject = $"¡Bienvenido a ADHSOFT SPORT! - Academia {academyName} aprobada";
        var body = $@"
<html><body style='font-family:Arial,sans-serif;color:#333;'>
  <div style='max-width:600px;margin:auto;padding:30px;border:1px solid #e0e0e0;border-radius:8px;'>
    <h1 style='color:#4F46E5;'>¡Bienvenido, {adminName}!</h1>
    <p>Su academia <strong>{academyName}</strong> ha sido aprobada exitosamente en la plataforma <strong>ADHSOFT SPORT</strong>.</p>
    <h3>Credenciales de acceso:</h3>
    <ul>
      <li><strong>Email:</strong> {toEmail}</li>
      <li><strong>Contraseña temporal:</strong> <code style='background:#f4f4f4;padding:4px 8px;border-radius:4px;'>{tempPassword}</code></li>
    </ul>
    <p style='color:#e53e3e;'><strong>⚠️ Por seguridad, cambie su contraseña al primer inicio de sesión.</strong></p>
    <p>Acceda a la plataforma en: <a href='http://localhost:3000'>ADHSOFT SPORT Portal</a></p>
    <hr style='border:none;border-top:1px solid #e0e0e0;margin:20px 0;'/>
    <p style='color:#888;font-size:12px;'>ADHSOFT SPORT - Plataforma de Gestión Deportiva</p>
  </div>
</body></html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendRejectionEmailAsync(string toEmail, string academyName, string reason)
    {
        var subject = $"Solicitud de registro - Academia {academyName}";
        var body = $@"
<html><body style='font-family:Arial,sans-serif;color:#333;'>
  <div style='max-width:600px;margin:auto;padding:30px;border:1px solid #e0e0e0;border-radius:8px;'>
    <h1 style='color:#E53E3E;'>Solicitud no aprobada</h1>
    <p>Lamentamos informarle que la solicitud de registro para la academia <strong>{academyName}</strong> no ha sido aprobada.</p>
    <h3>Motivo:</h3>
    <p style='background:#fff5f5;padding:15px;border-radius:6px;border-left:4px solid #E53E3E;'>{reason}</p>
    <p>Si tiene alguna consulta, puede volver a enviar una solicitud con la información corregida.</p>
    <hr style='border:none;border-top:1px solid #e0e0e0;margin:20px 0;'/>
    <p style='color:#888;font-size:12px;'>ADHSOFT SPORT - Plataforma de Gestión Deportiva</p>
  </div>
</body></html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    public async Task SendSuspensionEmailAsync(string toEmail, string academyName, string reason)
    {
        var subject = $"Academia {academyName} - Suspensión temporal";
        var body = $@"
<html><body style='font-family:Arial,sans-serif;color:#333;'>
  <div style='max-width:600px;margin:auto;padding:30px;border:1px solid #e0e0e0;border-radius:8px;'>
    <h1 style='color:#D97706;'>Academia suspendida temporalmente</h1>
    <p>La academia <strong>{academyName}</strong> ha sido suspendida temporalmente de la plataforma <strong>ADHSOFT SPORT</strong>.</p>
    <h3>Motivo de suspensión:</h3>
    <p style='background:#fffbeb;padding:15px;border-radius:6px;border-left:4px solid #D97706;'>{reason}</p>
    <p>Para obtener más información o apelar esta decisión, contacte al equipo de ADHSOFT SPORT.</p>
    <hr style='border:none;border-top:1px solid #e0e0e0;margin:20px 0;'/>
    <p style='color:#888;font-size:12px;'>ADHSOFT SPORT - Plataforma de Gestión Deportiva</p>
  </div>
</body></html>";

        await SendEmailAsync(toEmail, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var smtpSection = _config.GetSection("Smtp");
        var host = smtpSection["Host"];
        var port = int.Parse(smtpSection["Port"] ?? "587");
        var fromEmail = smtpSection["FromEmail"];
        var fromName = smtpSection["FromName"] ?? "ADHSOFT SPORT";
        var username = smtpSection["Username"];
        var password = smtpSection["Password"];

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail))
        {
            // Email not configured - log to console for dev
            Console.WriteLine($"[EMAIL NOT CONFIGURED] To: {to} | Subject: {subject}");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
        if (!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
