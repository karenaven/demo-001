using Microsoft.EntityFrameworkCore;

using RodajeIA.Web.Datos;
using RodajeIA.Web.Dominio;

namespace RodajeIA.Tests.Datos;

/// <summary>Usa SQLite en memoria con la migración real, no un proveedor falso.</summary>
public sealed class RodajeDbContextTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();

    public void Dispose() => _base.Dispose();

    [Fact]
    public void GuardarSerieConPersonaje_SeLeeConLosMismosValores()
    {
        using (var db = NuevoContexto())
        {
            var serie = new Serie { Nombre = "Serie A" };
            serie.Personajes.Add(Personaje("Ana", heridas: null));
            db.Series.Add(serie);
            db.SaveChanges();
        }

        using var lectura = NuevoContexto();
        var guardado = lectura.Personajes.Include(p => p.Serie).Single();

        Assert.Equal("Serie A", guardado.Serie!.Nombre);
        Assert.Equal(
            new HojaPersonaje("Ana", "30 años", "Mujer delgada.", "trenza larga", "chaqueta negra", null, "Irónica", "Protagonista"),
            guardado.AHoja());
    }

    [Fact]
    public void EliminarSerie_EliminaSusPersonajesEnLaBase()
    {
        int idSerieA;
        using (var db = NuevoContexto())
        {
            var serieA = new Serie { Nombre = "Serie A" };
            serieA.Personajes.Add(Personaje("Ana"));
            serieA.Personajes.Add(Personaje("José Daniel"));
            var serieB = new Serie { Nombre = "Serie B" };
            serieB.Personajes.Add(Personaje("Ana"));
            db.Series.AddRange(serieA, serieB);
            db.SaveChanges();
            idSerieA = serieA.Id;
        }

        // Se borra directo en la base, sin cargar los personajes, para probar la cascada de SQLite.
        using (var db = NuevoContexto())
        {
            db.Series.Where(s => s.Id == idSerieA).ExecuteDelete();
        }

        using var lectura = NuevoContexto();
        Assert.DoesNotContain(lectura.Personajes, p => p.SerieId == idSerieA);
        Assert.Equal("Serie B", lectura.Personajes.Include(p => p.Serie).Single().Serie!.Nombre);
    }

    [Fact]
    public void NombreRepetidoEnLaMismaSerie_NoSeGuarda()
    {
        using var db = NuevoContexto();
        var serie = new Serie { Nombre = "Serie A" };
        serie.Personajes.Add(Personaje("Ana"));
        serie.Personajes.Add(Personaje("Ana"));
        db.Series.Add(serie);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void MismoNombreEnSeriesDistintas_SeGuarda()
    {
        using var db = NuevoContexto();
        var serieA = new Serie { Nombre = "Serie A" };
        serieA.Personajes.Add(Personaje("Ana"));
        var serieB = new Serie { Nombre = "Serie B" };
        serieB.Personajes.Add(Personaje("Ana"));
        db.Series.AddRange(serieA, serieB);

        db.SaveChanges();

        Assert.Equal(2, db.Personajes.Count(p => p.Nombre == "Ana"));
    }

    [Fact]
    public void PersonajeDeSerieInexistente_NoSeGuarda()
    {
        using var db = NuevoContexto();
        var personaje = Personaje("Ana");
        personaje.SerieId = 999;
        db.Personajes.Add(personaje);

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Migraciones_AlDiaConElModelo()
    {
        using var db = NuevoContexto();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    private RodajeDbContext NuevoContexto() => _base.CreateDbContext();

    private static Personaje Personaje(string nombre, string? heridas = "cicatriz en la ceja") => new()
    {
        Nombre = nombre,
        Edad = "30 años",
        DescripcionFisica = "Mujer delgada.",
        Peinado = "trenza larga",
        Vestuario = "chaqueta negra",
        Heridas = heridas,
        Personalidad = "Irónica",
        Rol = "Protagonista",
    };
}
