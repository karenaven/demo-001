using Microsoft.EntityFrameworkCore;

namespace RodajeIA.Web.Datos;

public sealed class RodajeDbContext(DbContextOptions<RodajeDbContext> options) : DbContext(options)
{
    public DbSet<Serie> Series => Set<Serie>();

    public DbSet<Personaje> Personajes => Set<Personaje>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Al eliminar una serie se eliminan sus hojas de personaje (RF-01d).
        modelBuilder.Entity<Serie>()
            .HasMany(s => s.Personajes)
            .WithOne(p => p.Serie)
            .HasForeignKey(p => p.SerieId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un nombre identifica al personaje en el guion (RF-05), así que no se repite dentro de la serie.
        modelBuilder.Entity<Personaje>()
            .HasIndex(p => new { p.SerieId, p.Nombre })
            .IsUnique();
    }
}
