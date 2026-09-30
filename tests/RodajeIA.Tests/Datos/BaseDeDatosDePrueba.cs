using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using RodajeIA.Web.Datos;

namespace RodajeIA.Tests.Datos;

/// <summary>SQLite en memoria con la migración real aplicada. Vive mientras viva la instancia.</summary>
public sealed class BaseDeDatosDePrueba : IDbContextFactory<RodajeDbContext>, IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public BaseDeDatosDePrueba()
    {
        _conexion.Open();
        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public RodajeDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RodajeDbContext>().UseSqlite(_conexion).Options);

    public void Dispose() => _conexion.Dispose();
}
