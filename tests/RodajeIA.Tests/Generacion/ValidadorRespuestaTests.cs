using System.Text.Json.Nodes;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Generacion;

namespace RodajeIA.Tests.Generacion;

public sealed class ValidadorRespuestaTests
{
    private static readonly Vocabulario Vocabulario =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config")).Vocabulario;

    [Fact]
    public void Validar_RespuestaCorrecta_DevuelveUnBloquePorClipConValoresDelVocabulario() // AC-06a
    {
        var escena = EscenaCon(
            ClipCon(1, "c1-l1", "c1-l2"),
            ClipCon(2),
            ClipCon(3, "c3-l1"));
        var json = Respuesta(
            Bloque(1, TomaJson(dialogos: ["c1-l1"]), TomaJson(dialogos: ["c1-l2"], angulo: "picado", movimiento: "paneo")),
            Bloque(2, TomaJson()),
            Bloque(3, TomaJson(dialogos: ["c3-l1"])));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.True(resultado.EsValida, string.Join("\n", resultado.Errores));
        Assert.Equal([1, 2, 3], resultado.Bloques.Select(b => b.Clip));
        var tomas = resultado.Bloques.SelectMany(b => b.Tomas).ToList();
        Assert.All(tomas, t =>
        {
            Assert.True(Vocabulario.Permite(Vocabulario.Plano, t.Plano));
            Assert.True(Vocabulario.Permite(Vocabulario.Optica, t.Optica));
            Assert.True(Vocabulario.Permite(Vocabulario.Iluminacion, t.Iluminacion));
        });
        var conOpcionales = resultado.Bloques[0].Tomas[1];
        Assert.Equal("picado", conOpcionales.Angulo);
        Assert.Equal("paneo", conOpcionales.Movimiento);
        Assert.Equal(["c1-l2"], conOpcionales.Dialogos);
    }

    [Fact]
    public void Validar_ClipConDosLineasYSoloUnaReferenciada_Fallida() // AC-06b
    {
        var escena = EscenaCon(ClipCon(1, "c1-l1", "c1-l2"));
        var json = Respuesta(Bloque(1, TomaJson(dialogos: ["c1-l1"])));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains(resultado.Errores, e => e.Contains("falta la línea 'c1-l2'"));
    }

    [Fact]
    public void Validar_LineaReferenciadaEnDosTomas_Fallida() // AC-06c
    {
        var escena = EscenaCon(ClipCon(1, "c1-l1"));
        var json = Respuesta(Bloque(1, TomaJson(dialogos: ["c1-l1"]), TomaJson(dialogos: ["c1-l1"])));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains(resultado.Errores, e => e.Contains("'c1-l1'") && e.Contains("2 veces"));
    }

    [Fact]
    public void Validar_LineaRepetidaEnLaMismaToma_Fallida()
    {
        var escena = EscenaCon(ClipCon(1, "c1-l1"));
        var json = Respuesta(Bloque(1, TomaJson(dialogos: ["c1-l1", "c1-l1"])));

        Assert.False(ValidadorRespuesta.Validar(escena, json, Vocabulario).EsValida);
    }

    [Theory]
    [InlineData("c2-l1")] // de otro clip
    [InlineData("c1-l9")] // inexistente
    public void Validar_IdQueNoPerteneceAlClip_Fallida(string idAjeno) // AC-06d
    {
        var escena = EscenaCon(ClipCon(1, "c1-l1"), ClipCon(2, "c2-l1"));
        var json = Respuesta(
            Bloque(1, TomaJson(dialogos: ["c1-l1", idAjeno])),
            Bloque(2, TomaJson(dialogos: ["c2-l1"])));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains(resultado.Errores, e => e.Contains($"'{idAjeno}'") && e.Contains("no pertenece al clip 1"));
    }

    [Fact]
    public void Validar_TresClipsYDosBloques_Fallida() // AC-06e
    {
        var escena = EscenaCon(ClipCon(1), ClipCon(2), ClipCon(3));
        var json = Respuesta(Bloque(1, TomaJson()), Bloque(2, TomaJson()));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains("Se esperaban 3 bloques", Assert.Single(resultado.Errores));
        Assert.Empty(resultado.Bloques);
    }

    [Fact]
    public void Validar_MasBloquesQueClips_Fallida() // RN-08
    {
        var escena = EscenaCon(ClipCon(1));
        var json = Respuesta(Bloque(1, TomaJson()), Bloque(2, TomaJson()));

        Assert.False(ValidadorRespuesta.Validar(escena, json, Vocabulario).EsValida);
    }

    [Fact]
    public void Validar_BloquesFueraDeOrden_Fallida() // RN-08: no se reordenan ni se unen clips
    {
        var escena = EscenaCon(ClipCon(1), ClipCon(2));
        var json = Respuesta(Bloque(2, TomaJson()), Bloque(1, TomaJson()));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains(resultado.Errores, e => e.Contains("debía ser el clip 1"));
    }

    [Fact]
    public void Validar_BloqueSinTomas_Fallida()
    {
        var escena = EscenaCon(ClipCon(1));
        var json = Respuesta(Bloque(1));

        Assert.False(ValidadorRespuesta.Validar(escena, json, Vocabulario).EsValida);
    }

    [Theory]
    [InlineData("plano", "plano americano")]
    [InlineData("optica", "gran angular 14mm")]
    [InlineData("iluminacion", "luz de neón")]
    [InlineData("angulo", "holandés")]
    [InlineData("movimiento", "dron")]
    public void Validar_ValorFueraDelVocabulario_Fallida(string campo, string valor) // RN-02
    {
        var escena = EscenaCon(ClipCon(1));
        var toma = TomaJson();
        toma[campo] = valor;

        var resultado = ValidadorRespuesta.Validar(escena, Respuesta(Bloque(1, toma)), Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.Contains(resultado.Errores, e => e.Contains($"'{valor}'") && e.Contains($"'{campo}'"));
    }

    [Theory]
    [InlineData("plano")]
    [InlineData("optica")]
    [InlineData("iluminacion")]
    public void Validar_FaltaCampoObligatorio_Fallida(string campo) // RN-01
    {
        var escena = EscenaCon(ClipCon(1));
        var toma = TomaJson();
        toma.Remove(campo);

        var resultado = ValidadorRespuesta.Validar(escena, Respuesta(Bloque(1, toma)), Vocabulario);

        Assert.Contains(resultado.Errores, e => e.Contains($"falta '{campo}'"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_AccionVacia_Fallida(string accion) // RN-01
    {
        var escena = EscenaCon(ClipCon(1));

        var resultado = ValidadorRespuesta.Validar(escena, Respuesta(Bloque(1, TomaJson(accion: accion))), Vocabulario);

        Assert.Contains(resultado.Errores, e => e.Contains("acción está vacía"));
    }

    [Fact]
    public void Validar_OpcionalesVaciosOAusentes_QuedanNull()
    {
        var escena = EscenaCon(ClipCon(1));
        var toma = TomaJson();
        toma["angulo"] = "";

        var resultado = ValidadorRespuesta.Validar(escena, Respuesta(Bloque(1, toma)), Vocabulario);

        var unica = Assert.Single(Assert.Single(resultado.Bloques).Tomas);
        Assert.Null(unica.Angulo);
        Assert.Null(unica.Movimiento);
    }

    [Fact]
    public void Validar_AccionConEspacios_SeRecorta()
    {
        var escena = EscenaCon(ClipCon(1));

        var resultado = ValidadorRespuesta.Validar(escena, Respuesta(Bloque(1, TomaJson(accion: "  Ana sonríe.\n"))), Vocabulario);

        Assert.Equal("Ana sonríe.", resultado.Bloques[0].Tomas[0].Accion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("{\"bloques\": [")]
    [InlineData("{}")]
    [InlineData("null")]
    public void Validar_JsonInvalidoOSinBloques_Fallida(string json)
    {
        var resultado = ValidadorRespuesta.Validar(EscenaCon(ClipCon(1)), json, Vocabulario);

        Assert.False(resultado.EsValida);
        Assert.NotEmpty(resultado.Errores);
    }

    [Fact]
    public void Validar_VariosProblemas_LosReportaTodos()
    {
        var escena = EscenaCon(ClipCon(1, "c1-l1"), ClipCon(2));
        var tomaMala = TomaJson();
        tomaMala["plano"] = "plano americano";
        var json = Respuesta(Bloque(1, TomaJson()), Bloque(2, tomaMala));

        var resultado = ValidadorRespuesta.Validar(escena, json, Vocabulario);

        Assert.Equal(2, resultado.Errores.Count);
    }

    private static Escena EscenaCon(params Clip[] clips) =>
        new(1, TipoLocacion.Int, "RESTAURANTE", "NOCHE", "Locación.", "Iluminación.", "Puesta.", "Audio.", clips);

    private static Clip ClipCon(int numero, params string[] idsDialogo) => new(
        numero,
        ["Ana camina."],
        idsDialogo.Select(id => new LineaDialogo(id, "ANA", null, "Hola.")).ToList(),
        null);

    private static JsonObject TomaJson(
        string[]? dialogos = null, string? angulo = null, string? movimiento = null, string accion = "Ana sonríe.")
    {
        var toma = new JsonObject
        {
            ["plano"] = "plano medio",
            ["optica"] = "50mm",
            ["iluminacion"] = "continuidad con la luz base",
            ["accion"] = accion,
            ["dialogos"] = new JsonArray([.. (dialogos ?? []).Select(d => JsonValue.Create(d))]),
        };
        if (angulo is not null)
        {
            toma["angulo"] = angulo;
        }

        if (movimiento is not null)
        {
            toma["movimiento"] = movimiento;
        }

        return toma;
    }

    private static JsonObject Bloque(int clip, params JsonObject[] tomas) => new()
    {
        ["clip"] = clip,
        ["tomas"] = new JsonArray([.. tomas]),
    };

    private static string Respuesta(params JsonObject[] bloques) =>
        new JsonObject { ["bloques"] = new JsonArray([.. bloques]) }.ToJsonString();
}
