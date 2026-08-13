using Fidelizar.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fidelizar.Infrastructure.Persistence;

/// <summary>
/// EF Core migrations are the schema's source of truth (DATA-MODEL §0). Entity configuration
/// lives in <c>Persistence/Configurations</c>, one <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
/// per table, applied by <see cref="OnModelCreating"/> so this class stays a table of contents.
/// </summary>
public sealed class FidelizarDbContext(DbContextOptions<FidelizarDbContext> options) : DbContext(options)
{
    public DbSet<Negocio> Negocios => Set<Negocio>();

    public DbSet<Sucursal> Sucursales => Set<Sucursal>();

    public DbSet<Miembro> Miembros => Set<Miembro>();

    public DbSet<MovimientoCredito> MovimientosCredito => Set<MovimientoCredito>();

    public DbSet<Corte> Cortes => Set<Corte>();

    public DbSet<ConfiguracionPrograma> ConfiguracionesPrograma => Set<ConfiguracionPrograma>();

    public DbSet<Consentimiento> Consentimientos => Set<Consentimiento>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Usuario.Email is citext (DATA-MODEL §2, UsuarioConfiguration) so it collides
        // case-insensitively at the database level — the extension has to exist before any
        // column can use the type.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FidelizarDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
