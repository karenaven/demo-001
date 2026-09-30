namespace RodajeIA.Web.Configuracion;

/// <summary>Plantillas del prompt final (Anexo D), cargadas desde <c>config/molde-prompt.json</c>.</summary>
public sealed record MoldePrompt(string SeparadorSecciones, IReadOnlyList<SeccionMolde> Secciones)
{
    /// <summary>Las seis secciones del Anexo D, en su orden fijo.</summary>
    public static readonly IReadOnlyList<string> SeccionesEsperadas =
        ["estilo", "locacionEIluminacionBase", "personajes", "puestaEnEscena", "audio", "bloques"];

    public SeccionMolde Seccion(string id) => Secciones.Single(s => s.Id == id);

    internal void Validar(string archivo)
    {
        if (string.IsNullOrEmpty(SeparadorSecciones))
        {
            throw new InvalidOperationException($"{archivo}: falta 'separadorSecciones'.");
        }

        var ids = (Secciones ?? []).Select(s => s.Id).ToList();
        if (!ids.SequenceEqual(SeccionesEsperadas))
        {
            throw new InvalidOperationException(
                $"{archivo}: las secciones deben ser, en este orden: {string.Join(", ", SeccionesEsperadas)}.");
        }

        Exigir(archivo, "estilo", s => s.Contenido, "contenido");
        Exigir(archivo, "locacionEIluminacionBase", s => s.Lineas is { Count: > 0 } ? "ok" : null, "lineas");
        Exigir(archivo, "personajes", s => s.PorPersonaje, "porPersonaje");
        Exigir(archivo, "puestaEnEscena", s => s.Contenido, "contenido");
        Exigir(archivo, "audio", s => s.Contenido, "contenido");
        Exigir(archivo, "bloques", s => s.TituloBloque, "tituloBloque");
        Exigir(archivo, "bloques", s => s.Toma, "toma");
        Exigir(archivo, "bloques", s => s.SeparadorTomas, "separadorTomas");
        Exigir(archivo, "bloques", s => s.SeparadorBloques, "separadorBloques");
        Exigir(archivo, "bloques", s => s.LineaDialogo, "lineaDialogo");
        Exigir(archivo, "bloques", s => s.SeparadorDialogos, "separadorDialogos");
        Exigir(archivo, "bloques", s => s.CierreConTextoEnPantalla, "cierreConTextoEnPantalla");
        Exigir(archivo, "bloques", s => s.CierreSinTextoEnPantalla, "cierreSinTextoEnPantalla");
    }

    private void Exigir(string archivo, string seccion, Func<SeccionMolde, string?> campo, string nombreCampo)
    {
        if (string.IsNullOrEmpty(campo(Seccion(seccion))))
        {
            throw new InvalidOperationException($"{archivo}: a la sección '{seccion}' le falta '{nombreCampo}'.");
        }
    }
}

/// <summary>
/// Una sección del molde. Cada sección usa solo algunos campos; el resto queda en null.
/// </summary>
public sealed record SeccionMolde(
    string Id,
    string? Titulo = null,
    string? Contenido = null,
    IReadOnlyList<string>? Lineas = null,
    string? PorPersonaje = null,
    string? TituloBloque = null,
    string? Toma = null,
    string? SeparadorTomas = null,
    string? SeparadorBloques = null,
    string? LineaDialogo = null,
    string? SeparadorDialogos = null,
    string? CierreConTextoEnPantalla = null,
    string? CierreSinTextoEnPantalla = null);
