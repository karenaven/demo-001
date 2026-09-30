using System.Text.Json;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Generacion;

/// <summary>Bloques válidos de una escena, o los motivos por los que la respuesta se trata como fallida.</summary>
public sealed class ResultadoRespuesta
{
    private ResultadoRespuesta(IReadOnlyList<Bloque> bloques, IReadOnlyList<string> errores)
    {
        Bloques = bloques;
        Errores = errores;
    }

    public IReadOnlyList<Bloque> Bloques { get; }

    public IReadOnlyList<string> Errores { get; }

    public bool EsValida => Errores.Count == 0;

    internal static ResultadoRespuesta Valida(IReadOnlyList<Bloque> bloques) => new(bloques, []);

    internal static ResultadoRespuesta Fallida(IReadOnlyList<string> errores) => new([], errores);
}

/// <summary>
/// Valida la respuesta JSON de Gemini para una escena (RF-06b) y la convierte en bloques del dominio.
/// Cualquier incumplimiento hace fallida la respuesta completa, para reintentar (RNF-07):
/// RN-08 (un bloque por clip, en orden), RN-09 (cada línea de diálogo del clip exactamente una vez),
/// RN-02 (valores del vocabulario) y RN-01 (campos obligatorios y acción no vacíos).
/// </summary>
public static class ValidadorRespuesta
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static ResultadoRespuesta Validar(Escena escena, string json, Vocabulario vocabulario)
    {
        RespuestaJson? respuesta;
        try
        {
            respuesta = JsonSerializer.Deserialize<RespuestaJson>(json, OpcionesJson);
        }
        catch (JsonException ex)
        {
            return ResultadoRespuesta.Fallida([$"La respuesta no es JSON válido: {ex.Message}"]);
        }

        if (respuesta?.Bloques is null)
        {
            return ResultadoRespuesta.Fallida(["La respuesta no tiene 'bloques'."]);
        }

        if (respuesta.Bloques.Count != escena.Clips.Count)
        {
            return ResultadoRespuesta.Fallida(
                [$"Se esperaban {escena.Clips.Count} bloques (uno por clip) y llegaron {respuesta.Bloques.Count}."]);
        }

        var errores = new List<string>();
        var bloques = new List<Bloque>();
        for (var i = 0; i < escena.Clips.Count; i++)
        {
            var clip = escena.Clips[i];
            var bloque = respuesta.Bloques[i];
            var prefijo = $"Bloque {i + 1}";

            if (bloque.Clip != clip.Numero)
            {
                errores.Add($"{prefijo}: corresponde al clip {bloque.Clip?.ToString() ?? "(sin número)"} y debía ser el clip {clip.Numero}.");
                continue;
            }

            if (bloque.Tomas is null || bloque.Tomas.Count == 0)
            {
                errores.Add($"{prefijo}: no tiene tomas.");
                continue;
            }

            var tomas = new List<Toma>();
            for (var t = 0; t < bloque.Tomas.Count; t++)
            {
                var toma = ValidarToma(bloque.Tomas[t], $"{prefijo}, toma {t + 1}", vocabulario, errores);
                if (toma is not null)
                {
                    tomas.Add(toma);
                }
            }

            ValidarDialogos(clip, bloque.Tomas, prefijo, errores);
            bloques.Add(new Bloque(clip.Numero, tomas));
        }

        return errores.Count > 0 ? ResultadoRespuesta.Fallida(errores) : ResultadoRespuesta.Valida(bloques);
    }

    private static Toma? ValidarToma(TomaJson toma, string prefijo, Vocabulario vocabulario, List<string> errores)
    {
        var cantidadAntes = errores.Count;
        var plano = ValorTecnico(toma.Plano, Vocabulario.Plano, prefijo, vocabulario, errores);
        var optica = ValorTecnico(toma.Optica, Vocabulario.Optica, prefijo, vocabulario, errores);
        var iluminacion = ValorTecnico(toma.Iluminacion, Vocabulario.Iluminacion, prefijo, vocabulario, errores);
        var angulo = ValorTecnico(toma.Angulo, Vocabulario.Angulo, prefijo, vocabulario, errores);
        var movimiento = ValorTecnico(toma.Movimiento, Vocabulario.Movimiento, prefijo, vocabulario, errores);

        var accion = toma.Accion?.Trim();
        if (string.IsNullOrEmpty(accion))
        {
            errores.Add($"{prefijo}: la acción está vacía.");
        }

        return errores.Count > cantidadAntes
            ? null
            : new Toma(plano!, optica!, iluminacion!, angulo, movimiento, accion!, toma.Dialogos ?? []);
    }

    /// <summary>Devuelve el valor si es del vocabulario; null si falta y la categoría es opcional.</summary>
    private static string? ValorTecnico(
        string? valor, string categoria, string prefijo, Vocabulario vocabulario, List<string> errores)
    {
        if (string.IsNullOrEmpty(valor))
        {
            if (vocabulario.Categorias[categoria].Obligatorio)
            {
                errores.Add($"{prefijo}: falta '{categoria}'.");
            }

            return null;
        }

        if (!vocabulario.Permite(categoria, valor))
        {
            errores.Add($"{prefijo}: '{valor}' no es un valor de '{categoria}' en el vocabulario.");
            return null;
        }

        return valor;
    }

    private static void ValidarDialogos(Clip clip, List<TomaJson> tomas, string prefijo, List<string> errores)
    {
        var referencias = tomas.SelectMany(t => t.Dialogos ?? []).ToList();
        var propias = clip.Dialogos.Select(d => d.Id).ToHashSet();

        foreach (var ajena in referencias.Where(id => !propias.Contains(id)).Distinct())
        {
            errores.Add($"{prefijo}: referencia la línea '{ajena}', que no pertenece al clip {clip.Numero}.");
        }

        foreach (var linea in clip.Dialogos)
        {
            var veces = referencias.Count(id => id == linea.Id);
            if (veces == 0)
            {
                errores.Add($"{prefijo}: falta la línea '{linea.Id}' ({linea.Personaje}).");
            }
            else if (veces > 1)
            {
                errores.Add($"{prefijo}: la línea '{linea.Id}' ({linea.Personaje}) está referenciada {veces} veces.");
            }
        }
    }

    private sealed class RespuestaJson
    {
        public List<BloqueJson>? Bloques { get; set; }
    }

    private sealed class BloqueJson
    {
        public int? Clip { get; set; }

        public List<TomaJson>? Tomas { get; set; }
    }

    private sealed class TomaJson
    {
        public string? Plano { get; set; }

        public string? Optica { get; set; }

        public string? Iluminacion { get; set; }

        public string? Angulo { get; set; }

        public string? Movimiento { get; set; }

        public string? Accion { get; set; }

        public List<string>? Dialogos { get; set; }
    }
}
