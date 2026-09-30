using System.Text.Json.Nodes;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Generacion;

namespace RodajeIA.Tests.Generacion;

public sealed class SchemaGeminiTests
{
    private static readonly Vocabulario Vocabulario =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config")).Vocabulario;

    [Fact]
    public void Generar_CadaCategoriaEsUnEnumConLosValoresDelVocabulario() // RN-02
    {
        var toma = Toma(SchemaGemini.Generar(Vocabulario));

        foreach (var (nombre, categoria) in Vocabulario.Categorias)
        {
            var propiedad = toma["properties"]![nombre]!;
            Assert.Equal("STRING", (string?)propiedad["type"]);
            Assert.Equal(categoria.Valores, propiedad["enum"]!.AsArray().Select(v => (string?)v));
        }
    }

    [Fact]
    public void Generar_SoloLasCategoriasObligatoriasSonRequeridas()
    {
        var requeridas = Toma(SchemaGemini.Generar(Vocabulario))["required"]!.AsArray().Select(v => (string?)v);

        Assert.Equal(["plano", "optica", "iluminacion", "accion", "dialogos"], requeridas);
    }

    [Fact]
    public void Generar_ElUnicoTextoLibreEsLaAccion()
    {
        var propiedades = Toma(SchemaGemini.Generar(Vocabulario))["properties"]!.AsObject();

        var textosLibres = propiedades
            .Where(p => (string?)p.Value!["type"] == "STRING" && p.Value["enum"] is null)
            .Select(p => p.Key);
        Assert.Equal(["accion"], textosLibres);
        Assert.Equal("ARRAY", (string?)propiedades["dialogos"]!["type"]);
    }

    [Fact]
    public void Generar_NoTieneCamposDeCamaraNiProfundidadDeCampo()
    {
        var propiedades = Toma(SchemaGemini.Generar(Vocabulario))["properties"]!.AsObject().Select(p => p.Key);

        Assert.Equal(["plano", "optica", "iluminacion", "angulo", "movimiento", "accion", "dialogos"], propiedades);
    }

    [Fact]
    public void Generar_ValorAgregadoAlVocabulario_ApareceEnElEnum()
    {
        var plano = Vocabulario.Categorias[Vocabulario.Plano];
        var ampliado = Vocabulario with
        {
            Categorias = new Dictionary<string, CategoriaVocabulario>(Vocabulario.Categorias)
            {
                [Vocabulario.Plano] = plano with { Valores = [.. plano.Valores, "plano americano"] },
            },
        };

        var enumPlano = Toma(SchemaGemini.Generar(ampliado))["properties"]!["plano"]!["enum"]!.AsArray();

        Assert.Contains("plano americano", enumPlano.Select(v => (string?)v));
    }

    [Fact]
    public void Generar_BloquesConClipYTomasRequeridos()
    {
        var schema = SchemaGemini.Generar(Vocabulario);

        Assert.Equal(["bloques"], schema["required"]!.AsArray().Select(v => (string?)v));
        var bloque = schema["properties"]!["bloques"]!["items"]!;
        Assert.Equal(["clip", "tomas"], bloque["required"]!.AsArray().Select(v => (string?)v));
        Assert.Equal("INTEGER", (string?)bloque["properties"]!["clip"]!["type"]);
    }

    private static JsonNode Toma(JsonObject schema) =>
        schema["properties"]!["bloques"]!["items"]!["properties"]!["tomas"]!["items"]!;
}
