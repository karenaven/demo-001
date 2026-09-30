using Microsoft.EntityFrameworkCore;

using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Gestion;

/// <summary>Alta, listado, edición y baja de hojas de personaje de una serie (RF-01e–h).</summary>
public sealed class ServicioPersonajes(IDbContextFactory<RodajeDbContext> fabrica)
{
    /// <summary>Hojas de la serie en orden de creación, que es el orden en que salen en el prompt.</summary>
    public async Task<IReadOnlyList<Personaje>> ListarAsync(int serieId)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Personajes.AsNoTracking().Where(p => p.SerieId == serieId).OrderBy(p => p.Id).ToListAsync();
    }

    public async Task<Personaje?> ObtenerAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Personajes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ResultadoGuardado> CrearAsync(int serieId, DatosPersonaje datos)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        if (!await db.Series.AnyAsync(s => s.Id == serieId))
        {
            return ResultadoGuardado.ConError(ResultadoGuardado.General, "La serie no existe.");
        }

        var errores = await ValidarAsync(db, serieId, idActual: null, datos);
        if (errores.Count > 0)
        {
            return ResultadoGuardado.ConErrores(errores);
        }

        var personaje = new Personaje { SerieId = serieId };
        datos.CopiarA(personaje);
        db.Personajes.Add(personaje);
        await db.SaveChangesAsync();
        return ResultadoGuardado.Guardado(personaje.Id);
    }

    /// <summary>Si hay errores, la hoja guardada no cambia (AC-01k).</summary>
    public async Task<ResultadoGuardado> EditarAsync(int id, DatosPersonaje datos)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var personaje = await db.Personajes.SingleOrDefaultAsync(p => p.Id == id);
        if (personaje is null)
        {
            return ResultadoGuardado.ConError(ResultadoGuardado.General, "El personaje no existe.");
        }

        var errores = await ValidarAsync(db, personaje.SerieId, id, datos);
        if (errores.Count > 0)
        {
            return ResultadoGuardado.ConErrores(errores);
        }

        datos.CopiarA(personaje);
        await db.SaveChangesAsync();
        return ResultadoGuardado.Guardado(id);
    }

    /// <summary>Elimina la hoja de la base. Devuelve false si no existía.</summary>
    public async Task<bool> EliminarAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Personajes.Where(p => p.Id == id).ExecuteDeleteAsync() > 0;
    }

    private static async Task<Dictionary<string, string>> ValidarAsync(
        RodajeDbContext db, int serieId, int? idActual, DatosPersonaje datos)
    {
        var errores = datos.Validar();
        if (errores.ContainsKey(nameof(DatosPersonaje.Nombre)))
        {
            return errores;
        }

        // "Ana" y "ana" serían el mismo marcador de diálogo (ANA:), así que se comparan en mayúsculas.
        var nombre = datos.Nombre.Trim().ToUpperInvariant();
        var otros = await db.Personajes
            .Where(p => p.SerieId == serieId && p.Id != idActual)
            .Select(p => p.Nombre)
            .ToListAsync();
        if (otros.Any(o => o.ToUpperInvariant() == nombre))
        {
            errores[nameof(DatosPersonaje.Nombre)] = "Ya existe un personaje con ese nombre en la serie.";
        }

        return errores;
    }
}
