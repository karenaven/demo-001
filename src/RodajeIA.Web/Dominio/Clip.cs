namespace RodajeIA.Web.Dominio;

/// <summary>Un clip del guion: acción (líneas sin marcador), diálogo y, como máximo, un texto en pantalla.</summary>
public sealed record Clip(
    int Numero,
    IReadOnlyList<string> Accion,
    IReadOnlyList<LineaDialogo> Dialogos,
    string? TextoEnPantalla);

/// <summary>
/// Línea de diálogo literal del guion. La IA solo la referencia por <see cref="Id"/>;
/// el texto se inserta tal cual en el prompt final (RN-04).
/// </summary>
public sealed record LineaDialogo(string Id, string Personaje, string? Acotacion, string Texto);
