namespace RodajeIA.Web.Datos;

public sealed class Serie
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    public List<Personaje> Personajes { get; } = [];
}
