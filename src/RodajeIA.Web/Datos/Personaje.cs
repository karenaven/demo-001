using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Datos;

/// <summary>Hoja base de un personaje guardada en la base (Anexo B). Pertenece a una serie.</summary>
public sealed class Personaje
{
    public int Id { get; set; }

    public int SerieId { get; set; }

    public Serie? Serie { get; set; }

    public string Nombre { get; set; } = "";

    public string Edad { get; set; } = "";

    public string DescripcionFisica { get; set; } = "";

    public string Peinado { get; set; } = "";

    public string Vestuario { get; set; } = "";

    public string? Heridas { get; set; }

    public string Personalidad { get; set; } = "";

    public string Rol { get; set; } = "";

    public HojaPersonaje AHoja() =>
        new(Nombre, Edad, DescripcionFisica, Peinado, Vestuario, Heridas, Personalidad, Rol);
}
