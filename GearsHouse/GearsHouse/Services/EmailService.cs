using MailKit.Net.Smtp;
using MimeKit;
using System.Threading.Tasks;
using GearsHouse.Models;


namespace GearsHouse.Services
{
    public class EmailService
    {
        public async Task SendEmailAsync(string toEmail, string subject, string body, string attachmentPath = null)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse("your-email@gmail.com")); // Địa chỉ gửi
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };

            if (!string.IsNullOrEmpty(attachmentPath) && File.Exists(attachmentPath))
            {
                builder.Attachments.Add(attachmentPath);
            }

            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, false);
            await smtp.AuthenticateAsync("gearshouse0696@gmail.com", "dyyp gsii jwpb kbpa"); // App Password
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
