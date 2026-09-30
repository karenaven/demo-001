namespace RodajeIA.Web.Dominio;

/// <summary>
/// Hoja de personaje (Anexo B). Todos los campos son obligatorios salvo <see cref="Heridas"/>.
/// Se inserta literal en el prompt final (RN-03).
/// </summary>
public sealed record HojaPersonaje(
    string Nombre,
    string Edad,
    string DescripcionFisica,
    string Peinado,
    string Vestuario,
    string? Heridas,
    string Personalidad,
    string Rol);
