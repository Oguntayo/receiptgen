using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace ReceiptGen.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendWelcomeEmailAsync(string email, string username)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("ReceiptGen", emailSettings["Email"]));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = "Welcome to ReceiptGen!";

            message.Body = new TextPart("plain")
            {
                Text = $@"Hi {username},

Welcome to ReceiptGen! We're glad to have you on board.

Best regards,
The ReceiptGen Team"
            };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]!), SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // In a real app, you might want to log this
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw;
            }
        }
    }
}
