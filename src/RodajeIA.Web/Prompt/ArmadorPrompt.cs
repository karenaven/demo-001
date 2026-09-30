using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Prompt;

/// <summary>
/// Arma el prompt final de una escena por plantilla determinística (RF-08a, Anexo D). Las hojas de personaje,
/// el diálogo y el texto en pantalla se insertan literales (RN-03, RN-04); de la IA solo salen las tomas.
/// </summary>
public sealed class ArmadorPrompt(ConfiguracionRodaje configuracion)
{
    /// <param name="escena">Escena tal como salió del guion.</param>
    /// <param name="personajes">Hojas efectivas de los personajes presentes en la escena, en el orden en que se muestran.</param>
    /// <param name="bloques">Un bloque por clip, en el orden de los clips.</param>
    public string Armar(Escena escena, IReadOnlyList<HojaPersonaje> personajes, IReadOnlyList<Bloque> bloques)
    {
        var molde = configuracion.Molde;
        var valoresEscena = new Dictionary<string, string?>
        {
            ["estilo.texto"] = configuracion.Estilo,
            ["escena.locacion"] = escena.Locacion,
            ["escena.iluminacion"] = escena.Iluminacion,
            ["escena.puestaEnEscena"] = escena.PuestaEnEscena,
            ["escena.audio"] = escena.Audio,
        };

        var secciones = new List<string>();
        foreach (var seccion in molde.Secciones)
        {
            var texto = seccion switch
            {
                { PorPersonaje: not null } => personajes.Count == 0
                    ? null // Sin personajes con hoja, la sección se omite (RN-01).
                    : string.Join("\n", personajes.Select(DescribirPersonaje)),
                { Toma: not null } => ArmarBloques(seccion, escena, bloques),
                { Lineas: not null } => string.Join("\n", seccion.Lineas.Select(l => Plantilla.Aplicar(l, valoresEscena))),
                _ => Plantilla.Aplicar(seccion.Contenido!, valoresEscena),
            };

            if (texto is not null)
            {
                secciones.Add(seccion.Titulo is null ? texto : $"{seccion.Titulo}\n{texto}");
            }
        }

        return string.Join(molde.SeparadorSecciones, secciones);
    }

    /// <summary>Descripción literal de una hoja, idéntica en todos los prompts donde aparece (RN-03).</summary>
    public string DescribirPersonaje(HojaPersonaje hoja) =>
        Plantilla.Aplicar(configuracion.Molde.Seccion("personajes").PorPersonaje!, ValoresPersonaje(hoja));

    private static string ArmarBloques(SeccionMolde seccion, Escena escena, IReadOnlyList<Bloque> bloques)
    {
        if (bloques.Count != escena.Clips.Count)
        {
            throw new InvalidOperationException(
                $"La escena {escena.Numero} tiene {escena.Clips.Count} clips y se recibieron {bloques.Count} bloques.");
        }

        var textos = new List<string>();
        for (var i = 0; i < escena.Clips.Count; i++)
        {
            var clip = escena.Clips[i];
            var bloque = bloques[i];
            if (bloque.Clip != clip.Numero)
            {
                throw new InvalidOperationException(
                    $"El bloque {i + 1} de la escena {escena.Numero} corresponde al clip {bloque.Clip}, no al clip {clip.Numero}.");
            }

            if (bloque.Tomas.Count == 0)
            {
                throw new InvalidOperationException($"El bloque del clip {clip.Numero} de la escena {escena.Numero} no tiene tomas.");
            }

            var titulo = Plantilla.Aplicar(seccion.TituloBloque!, new Dictionary<string, string?>
            {
                ["bloque.numero"] = clip.Numero.ToString(),
            });
            var tomas = bloque.Tomas.Select(t => Plantilla.Aplicar(seccion.Toma!, ValoresToma(seccion, clip, t)));
            var cierre = clip.TextoEnPantalla is null
                ? seccion.CierreSinTextoEnPantalla!
                : Plantilla.Aplicar(seccion.CierreConTextoEnPantalla!, new Dictionary<string, string?>
                {
                    ["clip.textoEnPantalla"] = clip.TextoEnPantalla,
                });

            textos.Add($"{titulo}\n{string.Join(seccion.SeparadorTomas, tomas)} {cierre}");
        }

        return string.Join(seccion.SeparadorBloques, textos);
    }

    private static Dictionary<string, string?> ValoresToma(SeccionMolde seccion, Clip clip, Toma toma)
    {
        var dialogos = toma.Dialogos.Select(id =>
        {
            var linea = clip.Dialogos.SingleOrDefault(d => d.Id == id)
                ?? throw new InvalidOperationException($"La línea de diálogo '{id}' no pertenece al clip {clip.Numero}.");
            return Plantilla.Aplicar(seccion.LineaDialogo!, new Dictionary<string, string?>
            {
                ["linea.personaje"] = linea.Personaje,
                ["linea.acotacion"] = linea.Acotacion,
                ["linea.texto"] = linea.Texto,
            });
        });

        return new Dictionary<string, string?>
        {
            ["toma.plano"] = toma.Plano,
            ["toma.movimiento"] = toma.Movimiento,
            ["toma.angulo"] = toma.Angulo,
            ["toma.optica"] = toma.Optica,
            ["toma.accion"] = toma.Accion,
            ["toma.dialogos"] = string.Join(seccion.SeparadorDialogos, dialogos),
            ["toma.iluminacion"] = toma.Iluminacion,
        };
    }

    private static Dictionary<string, string?> ValoresPersonaje(HojaPersonaje hoja) => new()
    {
        ["personaje.nombre"] = hoja.Nombre,
        ["personaje.edad"] = hoja.Edad,
        ["personaje.descripcionFisica"] = hoja.DescripcionFisica,
        ["personaje.peinado"] = hoja.Peinado,
        ["personaje.vestuario"] = hoja.Vestuario,
        ["personaje.heridas"] = hoja.Heridas,
        ["personaje.personalidad"] = hoja.Personalidad,
        ["personaje.rol"] = hoja.Rol,
    };
}
