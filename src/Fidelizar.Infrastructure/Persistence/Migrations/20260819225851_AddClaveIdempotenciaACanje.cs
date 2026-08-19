using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidelizar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaveIdempotenciaACanje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClaveIdempotencia",
                table: "MovimientosCredito",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCredito_NegocioId_ClaveIdempotencia",
                table: "MovimientosCredito",
                columns: new[] { "NegocioId", "ClaveIdempotencia" },
                unique: true,
                filter: "\"ClaveIdempotencia\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosCredito_NegocioId_ClaveIdempotencia",
                table: "MovimientosCredito");

            migrationBuilder.DropColumn(
                name: "ClaveIdempotencia",
                table: "MovimientosCredito");
        }
    }
}
