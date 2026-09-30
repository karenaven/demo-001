using System.Text;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Web.Generacion;

/// <summary>
/// Arma el texto que recibe Gemini para una escena: la instrucción fija de <c>config/instruccion-generacion.txt</c>
/// con la escena completa (campos, clips e ids de cada línea de diálogo) y las hojas efectivas presentes (Anexo D).
/// Las hojas van con el mismo texto literal que el prompt final (RN-03).
/// </summary>
public sealed class ConstructorInstruccion(ConfiguracionRodaje configuracion, ArmadorPrompt armador)
{
    public string Construir(Escena escena, IReadOnlyList<HojaPersonaje> personajes)
    {
        var textoPersonajes = personajes.Count == 0
            ? "Ninguno."
            : string.Join("\n", personajes.Select(armador.DescribirPersonaje));

        return configuracion.Instruccion
            .Replace(ConfiguracionRodaje.MarcaEscena, DescribirEscena(escena), StringComparison.Ordinal)
            .Replace(ConfiguracionRodaje.MarcaPersonajes, textoPersonajes, StringComparison.Ordinal);
    }

    private static string DescribirEscena(Escena escena)
    {
        var tipo = escena.Tipo == TipoLocacion.Int ? "INT." : "EXT.";
        var texto = new StringBuilder()
            .Append($"ESCENA {escena.Numero} — {tipo} {escena.Lugar} — {escena.MomentoDelDia}\n")
            .Append($"Locación: {escena.Locacion}\n")
            .Append($"Iluminación base: {escena.Iluminacion}\n")
            .Append($"Puesta en escena: {escena.PuestaEnEscena}\n")
            .Append($"Audio: {escena.Audio}\n");

        foreach (var clip in escena.Clips)
        {
            texto.Append($"\nCLIP {clip.Numero}\n");
            if (clip.Accion.Count > 0)
            {
                texto.Append("Acción:\n");
                foreach (var linea in clip.Accion)
                {
                    texto.Append($"- {linea}\n");
                }
            }

            if (clip.Dialogos.Count > 0)
            {
                texto.Append("Diálogo (id entre corchetes):\n");
                foreach (var linea in clip.Dialogos)
                {
                    var acotacion = linea.Acotacion is null ? "" : $" ({linea.Acotacion})";
                    texto.Append($"- [{linea.Id}] {linea.Personaje}{acotacion}: \"{linea.Texto}\"\n");
                }
            }
            else
            {
                texto.Append("Diálogo: ninguno.\n");
            }

            if (clip.TextoEnPantalla is not null)
            {
                texto.Append($"Texto en pantalla: \"{clip.TextoEnPantalla}\"\n");
            }
        }

        return texto.ToString().TrimEnd();
    }
}
