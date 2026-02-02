using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;
using ReceiptGen.Models;
using System;
using System.IO;
using System.Linq;

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
        public async Task SendReceiptEmailAsync(string email, string username, byte[] pdfContent, Order order)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("ReceiptGen", emailSettings["Email"]));
            message.To.Add(new MailboxAddress(username, email));
            message.Subject = $"Your Receipt for Order #{order.Id}";

            var orderItemsHtml = string.Join("", order.OrderItems.Select(item => 
                $@"<tr>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{item.Product?.Name ?? "Unknown"}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{item.Quantity}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{item.UnitPrice:N2}</td>
                    <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{(item.UnitPrice * item.Quantity):N2}</td>
                </tr>"));

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $@"Hi {username},

Thank you for your purchase! 

Order Summary for #{order.Id}:
{string.Join("\n", order.OrderItems.Select(item => $"- {item.Product?.Name ?? "Unknown"}: {item.Quantity} x {item.UnitPrice:N2} = {(item.UnitPrice * item.Quantity):N2}"))}

Total: {order.TotalAmount:N2}

Please find your full receipt attached to this email.

Best regards,
The ReceiptGen Team",
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #f0f0f0;'>
                    <h2 style='color: #333;'>Thank you for your purchase!</h2>
                    <p>Your order <strong>#{order.Id}</strong> has been completed successfully.</p>
                    
                    <h3 style='border-bottom: 2px solid #eee; padding-bottom: 10px;'>Order Summary</h3>
                    <table style='width: 100%; border-collapse: collapse;'>
                        <thead>
                            <tr style='background-color: #f8f8f8;'>
                                <th style='padding: 8px; text-align: left; border-bottom: 2px solid #ddd;'>Product</th>
                                <th style='padding: 8px; text-align: center; border-bottom: 2px solid #ddd;'>Qty</th>
                                <th style='padding: 8px; text-align: right; border-bottom: 2px solid #ddd;'>Price</th>
                                <th style='padding: 8px; text-align: right; border-bottom: 2px solid #ddd;'>Total</th>
                            </tr>
                        </thead>
                        <tbody>
                            {orderItemsHtml}
                        </tbody>
                        <tfoot>
                            <tr>
                                <td colspan='3' style='padding: 8px; text-align: right; font-weight: bold;'>Subtotal</td>
                                <td style='padding: 8px; text-align: right;'>{order.Subtotal:N2}</td>
                            </tr>
                            { (order.DiscountAmount > 0 ? $"<tr><td colspan='3' style='padding: 8px; text-align: right; font-weight: bold;'>Discount</td><td style='padding: 8px; text-align: right; color: red;'>-{order.DiscountAmount:N2}</td></tr>" : "" ) }
                            <tr>
                                <td colspan='3' style='padding: 8px; text-align: right; font-weight: bold;'>VAT</td>
                                <td style='padding: 8px; text-align: right;'>{order.VatAmount:N2}</td>
                            </tr>
                            <tr style='background-color: #eee;'>
                                <td colspan='3' style='padding: 8px; text-align: right; font-weight: bold;'>Total</td>
                                <td style='padding: 8px; text-align: right; font-weight: bold;'>{order.TotalAmount:N2}</td>
                            </tr>
                        </tfoot>
                    </table>

                    <p style='margin-top: 20px;'>Please find your detailed receipt attached as a PDF.</p>
                    <p>Best regards,<br><strong>The ReceiptGen Team</strong></p>
                </div>"
            };

            bodyBuilder.Attachments.Add($"Receipt_{order.Id}.pdf", pdfContent, ContentType.Parse("application/pdf"));
            message.Body = bodyBuilder.ToMessageBody();

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
