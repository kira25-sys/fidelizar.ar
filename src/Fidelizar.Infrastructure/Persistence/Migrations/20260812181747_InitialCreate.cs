using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Fidelizar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionesPrograma",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    PorcentajeAcumulacion = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    ObjetivoMensual = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    GraciaHabilitada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MesesDeGracia = table.Column<int>(type: "integer", nullable: true),
                    UmbralMesMalo = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    TopeMesesCongelados = table.Column<int>(type: "integer", nullable: true),
                    VigenteDesde = table.Column<DateOnly>(type: "date", nullable: false),
                    VigenteHasta = table.Column<DateOnly>(type: "date", nullable: true),
                    CreadoPorUsuarioId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesPrograma", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cortes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    DeclaradoPorUsuarioId = table.Column<int>(type: "integer", nullable: false),
                    DeclaradoEn = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cortes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Miembros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    ClienteExternoId = table.Column<string>(type: "text", nullable: true),
                    NumeroSocio = table.Column<string>(type: "text", nullable: true),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    NombreNormalizado = table.Column<string>(type: "text", nullable: false),
                    Telefono = table.Column<string>(type: "text", nullable: true),
                    Dni = table.Column<string>(type: "text", nullable: true),
                    FechaNacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    SucursalId = table.Column<int>(type: "integer", nullable: true),
                    FechaAlta = table.Column<DateOnly>(type: "date", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    ActualizadoEn = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Miembros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCredito",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    MiembroId = table.Column<int>(type: "integer", nullable: false),
                    FechaEfectiva = table.Column<DateOnly>(type: "date", nullable: false),
                    RegistradoEn = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    Periodo = table.Column<string>(type: "char(7)", fixedLength: true, maxLength: 7, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ReferenciaVenta = table.Column<string>(type: "text", nullable: true),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<string>(type: "text", nullable: true),
                    SaldoResultante = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ConfiguracionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCredito", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Negocios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Cuit = table.Column<string>(type: "text", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoEn = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Negocios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NegocioId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    CodigoExterno = table.Column<string>(type: "text", nullable: true),
                    Activa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPrograma_NegocioId",
                table: "ConfiguracionesPrograma",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPrograma_NegocioId_Vigente",
                table: "ConfiguracionesPrograma",
                column: "NegocioId",
                unique: true,
                filter: "\"VigenteHasta\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cortes_NegocioId",
                table: "Cortes",
                column: "NegocioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_NegocioId",
                table: "Miembros",
                column: "NegocioId");

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_NegocioId_ClienteExternoId",
                table: "Miembros",
                columns: new[] { "NegocioId", "ClienteExternoId" },
                unique: true,
                filter: "\"ClienteExternoId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Miembros_NombreNormalizado",
                table: "Miembros",
                column: "NombreNormalizado");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCredito_Acumulacion_Unica",
                table: "MovimientosCredito",
                columns: new[] { "NegocioId", "MiembroId", "Tipo", "ReferenciaVenta" },
                unique: true,
                filter: "\"Tipo\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCredito_NegocioId_MiembroId",
                table: "MovimientosCredito",
                columns: new[] { "NegocioId", "MiembroId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCredito_NegocioId_Periodo",
                table: "MovimientosCredito",
                columns: new[] { "NegocioId", "Periodo" });

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_NegocioId",
                table: "Sucursales",
                column: "NegocioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionesPrograma");

            migrationBuilder.DropTable(
                name: "Cortes");

            migrationBuilder.DropTable(
                name: "Miembros");

            migrationBuilder.DropTable(
                name: "MovimientosCredito");

            migrationBuilder.DropTable(
                name: "Negocios");

            migrationBuilder.DropTable(
                name: "Sucursales");
        }
    }
}
