using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidelizar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeysAUsuario : Migration
    {
        /// <summary>
        /// Usuario Id=0, Rol=Sistema (4): the row F0-09's UsuarioPlaceholderMigracion=0 already
        /// points at from Cortes.DeclaradoPorUsuarioId and ConfiguracionesPrograma.CreadoPorUsuarioId
        /// for the 293 migrated members. Usuarios.Id is IdentityByDefaultColumn, so an explicit 0
        /// inserts with no OVERRIDING clause. NegocioId picks the sole active Negocio when one
        /// exists (mirrors INegocioRepository.ObtenerUnicoAsync); on a fresh, business-less
        /// database this is a no-op — there is nothing yet for Id=0 to be wrong about. Idempotent
        /// via ON CONFLICT, so re-running this migration is safe.
        /// </summary>
        private const string InsertUsuarioSistemaSql = """
            DO $$
            DECLARE
                negocio_id integer;
                negocio_count integer;
            BEGIN
                SELECT count(*) INTO negocio_count FROM "Negocios" WHERE "Activo";

                IF negocio_count > 1 THEN
                    RAISE EXCEPTION
                        'Se esperaba como maximo un Negocio activo para crear el Usuario sistema (Id=0); se encontraron %.',
                        negocio_count;
                END IF;

                IF negocio_count = 1 THEN
                    SELECT "Id" INTO negocio_id FROM "Negocios" WHERE "Activo" LIMIT 1;

                    INSERT INTO "Usuarios"
                        ("Id", "NegocioId", "NombreCompleto", "Email", "PasswordHash", "Rol", "SucursalId", "Activo", "CreadoEn")
                    VALUES
                        (0, negocio_id, 'Sistema', 'usuario-sistema@fidelizar.local',
                         'SIN-LOGIN:USUARIO-SISTEMA-NO-SE-AUTENTICA', 4, NULL, true, now())
                    ON CONFLICT ("Id") DO NOTHING;
                END IF;
            END $$;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Must run before the AddForeignKey calls below: Cortes.DeclaradoPorUsuarioId and
            // ConfiguracionesPrograma.CreadoPorUsuarioId already hold literal 0s for the 293
            // migrated members (F0-09), and a strict FK on existing data needs Usuarios.Id=0 to
            // exist first.
            migrationBuilder.Sql(InsertUsuarioSistemaSql);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_UsuarioId",
                table: "RegistrosAuditoria",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCredito_UsuarioId",
                table: "MovimientosCredito",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Cortes_DeclaradoPorUsuarioId",
                table: "Cortes",
                column: "DeclaradoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Consentimientos_RegistradoPorUsuarioId",
                table: "Consentimientos",
                column: "RegistradoPorUsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracionesPrograma_CreadoPorUsuarioId",
                table: "ConfiguracionesPrograma",
                column: "CreadoPorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConfiguracionesPrograma_Usuarios_CreadoPorUsuarioId",
                table: "ConfiguracionesPrograma",
                column: "CreadoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Consentimientos_Usuarios_RegistradoPorUsuarioId",
                table: "Consentimientos",
                column: "RegistradoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cortes_Usuarios_DeclaradoPorUsuarioId",
                table: "Cortes",
                column: "DeclaradoPorUsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCredito_Usuarios_UsuarioId",
                table: "MovimientosCredito",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosAuditoria_Usuarios_UsuarioId",
                table: "RegistrosAuditoria",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConfiguracionesPrograma_Usuarios_CreadoPorUsuarioId",
                table: "ConfiguracionesPrograma");

            migrationBuilder.DropForeignKey(
                name: "FK_Consentimientos_Usuarios_RegistradoPorUsuarioId",
                table: "Consentimientos");

            migrationBuilder.DropForeignKey(
                name: "FK_Cortes_Usuarios_DeclaradoPorUsuarioId",
                table: "Cortes");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCredito_Usuarios_UsuarioId",
                table: "MovimientosCredito");

            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosAuditoria_Usuarios_UsuarioId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosAuditoria_UsuarioId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCredito_UsuarioId",
                table: "MovimientosCredito");

            migrationBuilder.DropIndex(
                name: "IX_Cortes_DeclaradoPorUsuarioId",
                table: "Cortes");

            migrationBuilder.DropIndex(
                name: "IX_Consentimientos_RegistradoPorUsuarioId",
                table: "Consentimientos");

            migrationBuilder.DropIndex(
                name: "IX_ConfiguracionesPrograma_CreadoPorUsuarioId",
                table: "ConfiguracionesPrograma");

            // Safe only now that the FKs above are gone: nothing still points at Id=0 through a
            // constraint, even though the literal 0s from F0-09 remain in Cortes and
            // ConfiguracionesPrograma (this migration never touches those columns' values).
            migrationBuilder.Sql("""DELETE FROM "Usuarios" WHERE "Id" = 0 AND "Rol" = 4;""");
        }
    }
}
