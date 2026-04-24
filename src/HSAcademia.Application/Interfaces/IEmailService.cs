namespace HSAcademia.Application.Interfaces;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string academyName, string adminName, string tempPassword);
    Task SendRejectionEmailAsync(string toEmail, string academyName, string reason);
    Task SendSuspensionEmailAsync(string toEmail, string academyName, string reason);
}
