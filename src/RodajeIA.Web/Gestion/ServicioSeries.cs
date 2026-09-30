using Microsoft.EntityFrameworkCore;

using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Gestion;

/// <summary>Alta y listado de series; lo mínimo para poder cargar personajes (RF-01a, RF-01b).</summary>
public sealed class ServicioSeries(IDbContextFactory<RodajeDbContext> fabrica)
{
    public async Task<IReadOnlyList<Serie>> ListarAsync()
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Series.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
    }

    public async Task<Serie?> ObtenerAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Series.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
    }

    public async Task<ResultadoGuardado> CrearAsync(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return ResultadoGuardado.ConError(nameof(Serie.Nombre), "Falta el nombre.");
        }

        await using var db = await fabrica.CreateDbContextAsync();
        var serie = new Serie { Nombre = nombre.Trim() };
        db.Series.Add(serie);
        await db.SaveChangesAsync();
        return ResultadoGuardado.Guardado(serie.Id);
    }
}
