using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ReceiptGen.Models;
using System.Security.Claims;
using System.Linq;
using System;
using System.IO;

namespace ReceiptGen.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string email, string username);
        Task SendReceiptEmailAsync(string email, string username, byte[] pdfContent, Order order);
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
                Text = $"Hello {username}, welcome to ReceiptGen! Start managing your stores and generating receipts easily."
            };

            using var client = new SmtpClient();
            client.Timeout = 20000; // Increase to 20s
            
            // IPv6 can cause timeouts in some cloud networks. Forcing IPv4 can help.
            client.LocalDomain = "localhost";
            
            try
            {
                var host = emailSettings["Host"];
                var port = int.Parse(emailSettings["Port"]!);
                var options = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

                Console.WriteLine($"[SMTP DEBUG] Connecting to {host}:{port} ({options}, IPv4 Preferred)...");
                
                // Disable certificate revocation check which can also cause timeouts
                client.CheckCertificateRevocation = false;
                
                await client.ConnectAsync(host, port, options);
                await client.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SMTP ERROR] {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public async Task SendReceiptEmailAsync(string email, string username, byte[] pdfContent, Order order)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("ReceiptGen", emailSettings["Email"]));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = $"Your Receipt for Order #{order.Id}";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <h2>Thank you for your order, {username}!</h2>
                    <p>Your order #{order.Id} has been successfully processed.</p>
                    <h3>Order Summary</h3>
                    <table style='width: 100%; border-collapse: collapse;'>
                        <thead>
                            <tr style='background-color: #f2f2f2;'>
                                <th style='padding: 8px; border: 1px solid #ddd; text-align: left;'>Product</th>
                                <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Quantity</th>
                                <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Unit Price</th>
                                <th style='padding: 8px; border: 1px solid #ddd; text-align: right;'>Total</th>
                            </tr>
                        </thead>
                        <tbody>
                            {string.Join("", order.OrderItems.Select(item => $@"
                                <tr>
                                    <td style='padding: 8px; border: 1px solid #ddd;'>{item.Product?.Name ?? "Unknown Product"}</td>
                                    <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{item.Quantity}</td>
                                    <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{item.UnitPrice:N2}</td>
                                    <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{(item.Quantity * item.UnitPrice):N2}</td>
                                </tr>"))}
                        </tbody>
                        <tfoot>
                            <tr>
                                <td colspan='3' style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>Subtotal</strong></td>
                                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{order.Subtotal:N2}</td>
                            </tr>
                            <tr>
                                <td colspan='3' style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>Discount</strong></td>
                                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>-{order.DiscountAmount:N2}</td>
                            </tr>
                            <tr>
                                <td colspan='3' style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>VAT</strong></td>
                                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'>{order.VatAmount:N2}</td>
                            </tr>
                            <tr style='background-color: #f9f9f9;'>
                                <td colspan='3' style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>Total</strong></td>
                                <td style='padding: 8px; border: 1px solid #ddd; text-align: right;'><strong>{order.TotalAmount:N2}</strong></td>
                            </tr>
                        </tfoot>
                    </table>
                    <p>Please find your PDF receipt attached.</p>"
            };

            bodyBuilder.Attachments.Add($"Receipt_{order.Id}.pdf", pdfContent);
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.Timeout = 20000; // Increase to 20s
            
            // IPv6 can cause timeouts in some cloud networks
            client.LocalDomain = "localhost";
            
            try
            {
                var host = emailSettings["Host"];
                var port = int.Parse(emailSettings["Port"]!);
                var options = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

                var debugText = $"Connecting to {host}:{port} ({options}, IPv4 Preferred)...";
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] [SMTP DEBUG] {debugText}{Environment.NewLine}");
                Console.WriteLine($"[SMTP DEBUG] {debugText}");
                
                // Disable certificate revocation check which can also cause timeouts
                client.CheckCertificateRevocation = false;
                
                await client.ConnectAsync(host, port, options);
                await client.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                var logMessage = $"[{DateTime.Now}] [SMTP ERROR] {ex.GetType().Name} for {email}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
                File.AppendAllText("email_logs.txt", logMessage);
                Console.WriteLine($"[SMTP ERROR] {ex.Message}");
                throw;
            }
        }
    }
}
