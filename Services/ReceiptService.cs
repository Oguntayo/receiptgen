using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReceiptGen.Data;
using ReceiptGen.Models;
using System.Security.Claims;

namespace ReceiptGen.Services
{
    public interface IReceiptService
    {
        Task<byte[]> GenerateReceiptPdfAsync(Guid orderId);
        Task SendReceiptJobAsync(Guid orderId);
    }

    public class ReceiptService : IReceiptService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public ReceiptService(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task SendReceiptJobAsync(Guid orderId)
        {
            try
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] Starting receipt job for order {orderId}{Environment.NewLine}");
                
                var pdfContent = await GenerateReceiptPdfAsync(orderId);
                
                var order = await _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] Order {orderId} not found!{Environment.NewLine}");
                    return;
                }

                if (order.User == null)
                {
                    File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] User for order {orderId} not found!{Environment.NewLine}");
                    return;
                }

                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] Sending receipt to {order.User.Email}{Environment.NewLine}");
                await _emailService.SendReceiptEmailAsync(order.User.Email, order.User.Username, pdfContent, orderId);
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] Receipt sent successfully to {order.User.Email}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                File.AppendAllText("email_logs.txt", $"[{DateTime.Now}] Receipt job failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
                throw;
            }
        }

        public async Task<byte[]> GenerateReceiptPdfAsync(Guid orderId)
        {
            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Store)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new Exception("Order not found");

            // Assuming a single store for the receipt header (standard behavior)
            var store = order.OrderItems.FirstOrDefault()?.Product?.Store;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text(store?.Name ?? "ReceiptGen Store").FontSize(20).SemiBold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"{store?.Address ?? "Digital Store"}");
                            col.Item().Text($"{store?.PhoneNumber ?? "Customer Support"}");
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("RECEIPT").FontSize(20).SemiBold();
                            col.Item().Text($"Date: {order.CreatedAt:yyyy-MM-dd HH:mm}");
                            col.Item().Text($"Order ID: {order.Id}");
                        });
                    });

                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        col.Item().PaddingBottom(5).Text("Customer Details").SemiBold();
                        col.Item().Text($"Name: {order.User?.Username}");
                        col.Item().Text($"Email: {order.User?.Email}");
                        col.Item().PaddingVertical(10).LineHorizontal(1);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Product");
                                header.Cell().AlignRight().Text("Unit Price");
                                header.Cell().AlignRight().Text("Quantity");
                                header.Cell().AlignRight().Text("Total");

                                header.Cell().ColumnSpan(4).PaddingVertical(5).LineHorizontal(1);
                            });

                            foreach (var item in order.OrderItems)
                            {
                                table.Cell().Text(item.Product?.Name ?? "Unknown Product");
                                table.Cell().AlignRight().Text($"{item.UnitPrice:N2}");
                                table.Cell().AlignRight().Text($"{item.Quantity}");
                                table.Cell().AlignRight().Text($"{(item.UnitPrice * item.Quantity):N2}");
                            }
                        });

                        col.Item().AlignRight().PaddingTop(10).Column(innerCol =>
                        {
                            innerCol.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Subtotal:");
                                row.ConstantItem(80).AlignRight().Text($"{order.Subtotal:N2}");
                            });

                            if (order.DiscountAmount > 0)
                            {
                                innerCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text("Discount:");
                                    row.ConstantItem(80).AlignRight().Text($"-{order.DiscountAmount:N2}");
                                });
                            }

                            innerCol.Item().Row(row =>
                            {
                                row.RelativeItem().Text("VAT:");
                                row.ConstantItem(80).AlignRight().Text($"{order.VatAmount:N2}");
                            });

                            innerCol.Item().Row(row =>
                            {
                                row.RelativeItem().Text("Total:").FontSize(12).SemiBold();
                                row.ConstantItem(80).AlignRight().Text($"{order.TotalAmount:N2}").FontSize(12).SemiBold();
                            });
                        });

                        col.Item().PaddingTop(20).Column(footerCol =>
                        {
                            footerCol.Item().Text($"Payment Method: {order.PaymentMethod}");
                            footerCol.Item().PaddingTop(10).Text("Thank you for your business!").Italic().AlignCenter();
                        });
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
