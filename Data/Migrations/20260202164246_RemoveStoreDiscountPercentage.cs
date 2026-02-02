using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReceiptGen.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStoreDiscountPercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "Stores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "Stores",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
