using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GearsHouse.Migrations
{
    /// <inheritdoc />
    public partial class AddInforSpecs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductInfo",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalSpecs",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductInfo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TechnicalSpecs",
                table: "Products");
        }
    }
}
