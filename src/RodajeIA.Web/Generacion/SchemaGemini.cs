using System.Text.Json.Nodes;

using RodajeIA.Web.Configuracion;

namespace RodajeIA.Web.Generacion;

/// <summary>
/// Genera el schema de salida estructurada de Gemini (<c>responseSchema</c>) a partir del vocabulario (RN-02):
/// cada categoría técnica es un enum con los valores del archivo y solo las obligatorias van en <c>required</c>.
/// La acción es el único texto libre; el diálogo se referencia por id (RN-04).
/// </summary>
public static class SchemaGemini
{
    public const string Accion = "accion";
    public const string Dialogos = "dialogos";

    public static JsonObject Generar(Vocabulario vocabulario)
    {
        var propiedadesToma = new JsonObject();
        var requeridasToma = new JsonArray();
        var ordenToma = new JsonArray();

        foreach (var nombre in Vocabulario.CategoriasEsperadas.Keys)
        {
            var categoria = vocabulario.Categorias[nombre];
            propiedadesToma[nombre] = new JsonObject
            {
                ["type"] = "STRING",
                ["enum"] = new JsonArray([.. categoria.Valores.Select(v => JsonValue.Create(v))]),
            };
            ordenToma.Add(nombre);
            if (categoria.Obligatorio)
            {
                requeridasToma.Add(nombre);
            }
        }

        propiedadesToma[Accion] = new JsonObject { ["type"] = "STRING" };
        propiedadesToma[Dialogos] = new JsonObject
        {
            ["type"] = "ARRAY",
            ["items"] = new JsonObject { ["type"] = "STRING" },
        };
        requeridasToma.Add(Accion);
        requeridasToma.Add(Dialogos);
        ordenToma.Add(Accion);
        ordenToma.Add(Dialogos);

        var toma = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = propiedadesToma,
            ["required"] = requeridasToma,
            ["propertyOrdering"] = ordenToma,
        };

        var bloque = new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["clip"] = new JsonObject { ["type"] = "INTEGER" },
                ["tomas"] = new JsonObject { ["type"] = "ARRAY", ["items"] = toma },
            },
            ["required"] = new JsonArray("clip", "tomas"),
            ["propertyOrdering"] = new JsonArray("clip", "tomas"),
        };

        return new JsonObject
        {
            ["type"] = "OBJECT",
            ["properties"] = new JsonObject
            {
                ["bloques"] = new JsonObject { ["type"] = "ARRAY", ["items"] = bloque },
            },
            ["required"] = new JsonArray("bloques"),
        };
    }
}
