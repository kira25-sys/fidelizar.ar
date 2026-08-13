using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fidelizar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Consentimientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    MiembroId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Otorgado = table.Column<bool>(type: "boolean", nullable: false),
                    VersionTexto = table.Column<string>(type: "text", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    RegistradoPorUsuarioId = table.Column<int>(type: "integer", nullable: true),
                    OcurridoEn = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Consentimientos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Consentimientos_MiembroId_Tipo",
                table: "Consentimientos",
                columns: new[] { "MiembroId", "Tipo" });

            migrationBuilder.CreateIndex(
                name: "IX_Consentimientos_NegocioId",
                table: "Consentimientos",
                column: "NegocioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Consentimientos");
        }
    }
}
