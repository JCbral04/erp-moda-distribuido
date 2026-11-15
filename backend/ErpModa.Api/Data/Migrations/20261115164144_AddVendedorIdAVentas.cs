using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpModa.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendedorIdAVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "vendedor_id",
                table: "ventas",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ventas_vendedor_id",
                table: "ventas",
                column: "vendedor_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ventas_vendedor_id",
                table: "ventas");

            migrationBuilder.DropColumn(
                name: "vendedor_id",
                table: "ventas");
        }
    }
}
