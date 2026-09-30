using System.Text.RegularExpressions;

using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Guion;

/// <summary>
/// Personajes con hoja presentes en una escena y en cada uno de sus clips, en el orden de las hojas de la serie.
/// </summary>
public sealed record PersonajesDetectados(
    IReadOnlyList<HojaPersonaje> EnEscena,
    IReadOnlyDictionary<int, IReadOnlyList<HojaPersonaje>> PorClip);

/// <summary>
/// Detecta de forma determinística, sin IA, qué hojas de la serie aparecen en una escena (RF-05).
/// Un personaje está presente en un clip si su nombre aparece en la acción, escrito como en la hoja
/// (palabra completa, respetando mayúsculas), o si es el marcador de una línea de diálogo, en mayúsculas.
/// Los personajes sin hoja no se detectan (RN-07).
/// </summary>
public static class DetectorPersonajes
{
    public static PersonajesDetectados Detectar(Escena escena, IReadOnlyList<HojaPersonaje> hojas)
    {
        // Los nombres más largos se buscan primero para que "Ana María" no cuente también como "Ana".
        var busquedas = hojas
            .OrderByDescending(h => h.Nombre.Length)
            .Select(h => (Hoja: h, Patron: PatronNombre(h.Nombre)))
            .ToList();

        var porClip = new Dictionary<int, IReadOnlyList<HojaPersonaje>>();
        foreach (var clip in escena.Clips)
        {
            var presentes = new HashSet<HojaPersonaje>();

            foreach (var linea in clip.Accion)
            {
                presentes.UnionWith(MencionadosEnAccion(linea, busquedas));
            }

            foreach (var dialogo in clip.Dialogos)
            {
                var hoja = hojas.FirstOrDefault(h => h.Nombre.ToUpperInvariant() == dialogo.Personaje);
                if (hoja is not null)
                {
                    presentes.Add(hoja);
                }
            }

            porClip[clip.Numero] = hojas.Where(presentes.Contains).ToList();
        }

        var enEscena = hojas.Where(h => porClip.Values.Any(p => p.Contains(h))).ToList();
        return new PersonajesDetectados(enEscena, porClip);
    }

    private static Regex PatronNombre(string nombre) =>
        new($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(nombre)}(?![\p{{L}}\p{{N}}])", RegexOptions.CultureInvariant);

    private static List<HojaPersonaje> MencionadosEnAccion(string linea, List<(HojaPersonaje Hoja, Regex Patron)> busquedas)
    {
        var mencionados = new List<HojaPersonaje>();
        var ocupado = new bool[linea.Length];

        foreach (var (hoja, patron) in busquedas)
        {
            foreach (Match mencion in patron.Matches(linea))
            {
                var rango = ocupado.AsSpan(mencion.Index, mencion.Length);
                if (rango.Contains(true))
                {
                    continue;
                }

                rango.Fill(true);
                mencionados.Add(hoja);
            }
        }

        return mencionados;
    }
}
