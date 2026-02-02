using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace ReceiptGen.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
        Task SendReceiptEmailAsync(string email, string username, byte[] pdfContent, Guid orderId);
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
            client.Timeout = 30000; // 30 seconds
            try
            {
                await client.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]!), SecureSocketOptions.SslOnConnect);
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
        public async Task SendReceiptEmailAsync(string email, string username, byte[] pdfContent, Guid orderId)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("ReceiptGen", emailSettings["Email"]));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = $"Your Receipt for Order #{orderId}";

            var body = new TextPart("plain")
            {
                Text = $@"Hi {username},

Thank you for your purchase! Please find your receipt attached to this email.

Best regards,
The ReceiptGen Team"
            };

            var attachment = new MimePart("application", "pdf")
            {
                Content = new MimeContent(new MemoryStream(pdfContent)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = $"Receipt_{orderId}.pdf"
            };

            var multipart = new Multipart("mixed");
            multipart.Add(body);
            multipart.Add(attachment);

            message.Body = multipart;

            using var client = new SmtpClient();
            client.Timeout = 30000; // 30 seconds
            try
            {
                await client.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]!), SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                var logMessage = $"[{DateTime.Now}] Error sending receipt email to {email}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
                File.AppendAllText("email_logs.txt", logMessage);
                Console.WriteLine($"Error sending receipt email: {ex.Message}");
                throw;
            }
        }
    }
}
