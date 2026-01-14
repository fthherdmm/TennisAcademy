using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using KirikkaleTenisAkademi.Application.Interfaces; // Interface'i buradan görüyor

namespace KirikkaleTenisAkademi.Infrastructure.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");

            var senderEmail = emailSettings["SenderEmail"];
            var senderName = emailSettings["SenderName"];
            var password = emailSettings["Password"];
            var smtpServer = emailSettings["SmtpServer"];
            var port = int.Parse(emailSettings["Port"]);

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
            };
            
            mailMessage.To.Add(toEmail);

            using var smtpClient = new SmtpClient(smtpServer)
            {
                Port = port,
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true,
            };

            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}