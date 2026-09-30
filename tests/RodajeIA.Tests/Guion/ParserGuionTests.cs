using System.Diagnostics;
using System.Text;
using System.Text.Json;

using RodajeIA.Web.Dominio;
using RodajeIA.Web.Guion;

namespace RodajeIA.Tests.Guion;

public sealed class ParserGuionTests
{
    /// <summary>Escena válida de ejemplo del Anexo A del PRD.</summary>
    private const string EscenaEjemplo = """
        ESCENA 1 — INT. RESTAURANTE — NOCHE
        LOCACIÓN: Restaurante pequeño y acogedor; pared de ladrillo visto, mesas de madera oscura, vela en portavelas de cristal.
        ILUMINACIÓN: Luz cálida ambiental (aprox. 2700 K), principal suave cenital de la lámpara sobre la mesa, reflejo de la vela en los rostros.
        PUESTA EN ESCENA: Sentados frente a frente en una mesa para dos. Ana siempre a la IZQUIERDA del encuadre, José Daniel siempre a la DERECHA.
        AUDIO: Ambiente tranquilo de restaurante, murmullo lejano y cubiertos; SIN música. Diálogo en español, acento latinoamericano neutro.
        CLIP 1
        Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.
        ANA: "Mentiroso."
        JOSÉ DANIEL (sonriendo): "Bueno... pasó parecido."
        Ana niega con la cabeza, divertida, con una sonrisa amplia.
        CLIP 2
        Un mesero deja dos platos sobre la mesa. Ana y José Daniel agradecen con gestos breves.
        """;

    // --- AC-04: división del guion ---

    [Fact]
    public void Procesar_EscenaEjemplo_ExtraeEncabezadoYCampos() // AC-04a
    {
        var escena = Assert.Single(ProcesarValido(EscenaEjemplo));

        Assert.Equal(1, escena.Numero);
        Assert.Equal(TipoLocacion.Int, escena.Tipo);
        Assert.Equal("RESTAURANTE", escena.Lugar);
        Assert.Equal("NOCHE", escena.MomentoDelDia);
        Assert.Equal("Restaurante pequeño y acogedor; pared de ladrillo visto, mesas de madera oscura, vela en portavelas de cristal.", escena.Locacion);
        Assert.Equal("Luz cálida ambiental (aprox. 2700 K), principal suave cenital de la lámpara sobre la mesa, reflejo de la vela en los rostros.", escena.Iluminacion);
        Assert.Equal("Sentados frente a frente en una mesa para dos. Ana siempre a la IZQUIERDA del encuadre, José Daniel siempre a la DERECHA.", escena.PuestaEnEscena);
        Assert.Equal("Ambiente tranquilo de restaurante, murmullo lejano y cubiertos; SIN música. Diálogo en español, acento latinoamericano neutro.", escena.Audio);
    }

    [Fact]
    public void Procesar_EscenaEjemplo_TieneDosClips() // AC-04b
    {
        var escena = Assert.Single(ProcesarValido(EscenaEjemplo));

        Assert.Equal([1, 2], escena.Clips.Select(c => c.Numero));
    }

    [Fact]
    public void Procesar_EscenaEjemplo_SeparaAccionYDialogo() // AC-04c
    {
        var clip = ProcesarValido(EscenaEjemplo)[0].Clips[0];

        Assert.Equal(
            [
                "Ana y José Daniel conversan en una mesa junto a la ventana. José Daniel sonríe pícaramente.",
                "Ana niega con la cabeza, divertida, con una sonrisa amplia.",
            ],
            clip.Accion);
        Assert.Equal(
            [
                new LineaDialogo("c1-l1", "ANA", null, "Mentiroso."),
                new LineaDialogo("c1-l2", "JOSÉ DANIEL", "sonriendo", "Bueno... pasó parecido."),
            ],
            clip.Dialogos);
        Assert.Null(clip.TextoEnPantalla);
    }

    [Fact]
    public void Procesar_ClipConTextoEnPantalla_LoSeparaDeAccionYDialogo() // AC-04d
    {
        var guion = EscenaEjemplo + "\n" + """
            CLIP 3
            José Daniel toma el celular y lee el mensaje.
            TEXTO EN PANTALLA: "Hermano, ¿puedes salir conmigo?"
            """;

        var clip = ProcesarValido(guion)[0].Clips[2];

        Assert.Equal("Hermano, ¿puedes salir conmigo?", clip.TextoEnPantalla);
        Assert.Equal(["José Daniel toma el celular y lee el mensaje."], clip.Accion);
        Assert.Empty(clip.Dialogos);
    }

    [Fact]
    public void Procesar_MismoGuionDosVeces_ResultadosIguales() // AC-04e
    {
        var guion = LeerEjemploDelRepo();

        var primero = JsonSerializer.Serialize(ProcesarValido(guion));
        var segundo = JsonSerializer.Serialize(ProcesarValido(guion));

        Assert.Equal(primero, segundo);
    }

    [Fact]
    public void Procesar_EjemploDelRepo_EsValido()
    {
        var escenas = ProcesarValido(LeerEjemploDelRepo());

        Assert.Equal(Enumerable.Range(1, escenas.Count), escenas.Select(e => e.Numero));
        Assert.All(escenas, e => Assert.NotEmpty(e.Clips));
    }

    [Fact]
    public void Procesar_AcotacionYTextoConParentesis_RespetaElTextoLiteral()
    {
        var guion = Escena(1, 1).Replace(
            "Ana camina.",
            "JOSÉ DANIEL (sonriendo): \"Bueno... (pausa) pasó parecido.\"");

        var linea = Assert.Single(ProcesarValido(guion)[0].Clips[0].Dialogos);

        Assert.Equal("sonriendo", linea.Acotacion);
        Assert.Equal("Bueno... (pausa) pasó parecido.", linea.Texto);
    }

    [Fact]
    public void Procesar_DialogoDePersonajeSinHoja_EsValido() // parte de AC-05d
    {
        var guion = Escena(1, 1).Replace("Ana camina.", "MESERO: \"¿Algo más?\"");

        var linea = Assert.Single(ProcesarValido(guion)[0].Clips[0].Dialogos);

        Assert.Equal(new LineaDialogo("c1-l1", "MESERO", null, "¿Algo más?"), linea);
    }

    [Fact]
    public void Procesar_IdsDeDialogo_IndicanClipYOrden()
    {
        var guion = Escena(1, 2)
            .Replace("CLIP 2\nAna camina.", "CLIP 2\nANA: \"Uno.\"\nANA: \"Dos.\"");

        var dialogos = ProcesarValido(guion)[0].Clips[1].Dialogos;

        Assert.Equal(["c2-l1", "c2-l2"], dialogos.Select(d => d.Id));
    }

    [Fact]
    public void Procesar_GuionDeTamanoMaximo_TardaMenosDeUnSegundo() // RNF-03
    {
        var guion = Guion(escenas: ParserGuion.MaxEscenas, clipsPorEscena: ParserGuion.MaxClipsPorEscena);
        ParserGuion.Procesar(guion); // calentamiento

        var reloj = Stopwatch.StartNew();
        var resultado = ParserGuion.Procesar(guion);
        reloj.Stop();

        Assert.True(resultado.EsValido);
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(1), $"Tardó {reloj.Elapsed}.");
    }

    // --- AC-03: validación ---

    [Fact]
    public void Procesar_FaltaAudioYEscenaSinClips_MuestraLosDosErrores() // AC-03a
    {
        var guion = Escena(1, 1).Replace("AUDIO: Murmullo.\n", "") + "\n"
            + Escena(2, 0);

        var errores = ProcesarInvalido(guion);

        Assert.Equal(2, errores.Count);
        Assert.Equal(1, errores[0].Linea);
        Assert.Contains("AUDIO:", errores[0].Motivo);
        Assert.Equal(NumeroDeLinea(guion, "ESCENA 2 — INT. CALLE — DÍA"), errores[1].Linea);
        Assert.Contains("CLIP", errores[1].Motivo);
    }

    [Fact]
    public void Procesar_SinEncabezadoDeEscena_UnErrorConLinea() // AC-03b
    {
        var errores = ProcesarInvalido("\nAna camina por la calle.\nANA: \"Hola.\"");

        var error = Assert.Single(errores);
        Assert.Equal(2, error.Linea);
        Assert.Contains("ningún encabezado de escena", error.Motivo);
    }

    [Fact]
    public void Procesar_OnceEscenas_ErrorEnElEncabezadoDeLaOnce() // AC-03c
    {
        var guion = Guion(escenas: 11, clipsPorEscena: 1);
        var lineaEscena11 = NumeroDeLinea(guion, "ESCENA 11 — INT. CALLE — DÍA");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(lineaEscena11, error.Linea);
        Assert.Contains("límite de 10 escenas", error.Motivo);
    }

    [Fact]
    public void Procesar_EscenaDe21Clips_ErrorEnElClip21() // AC-03d
    {
        var guion = Guion(escenas: 1, clipsPorEscena: 21);
        var lineaClip21 = NumeroDeLinea(guion, "CLIP 21");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(lineaClip21, error.Linea);
        Assert.Contains("límite de 20 clips", error.Motivo);
    }

    [Fact]
    public void Procesar_DosTextosEnPantallaEnUnClip_ErrorEnElSegundo() // AC-03e
    {
        var guion = Escena(1, 1) + "\nTEXTO EN PANTALLA: \"Uno.\"\nTEXTO EN PANTALLA: \"Dos.\"";
        var lineaSegundo = NumeroDeLinea(guion, "TEXTO EN PANTALLA: \"Dos.\"");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(lineaSegundo, error.Linea);
        Assert.Contains("TEXTO EN PANTALLA", error.Motivo);
    }

    [Fact]
    public void Procesar_GuionInvalido_NoDevuelveNingunaEscena() // RF-03c
    {
        var guion = Escena(1, 1) + "\n" + Escena(2, 0);

        var resultado = ParserGuion.Procesar(guion);

        Assert.False(resultado.EsValido);
        Assert.Empty(resultado.Escenas);
    }

    [Fact]
    public void Procesar_GuionVacio_Error()
    {
        var error = Assert.Single(ProcesarInvalido("  \n\n"));

        Assert.Equal(1, error.Linea);
        Assert.Contains("vacío", error.Motivo);
    }

    [Theory]
    [InlineData("ESCENA 1 - INT. RESTAURANTE - NOCHE")]
    [InlineData("ESCENA 1 — INTERIOR RESTAURANTE — NOCHE")]
    [InlineData("ESCENA 1 — INT. RESTAURANTE")]
    [InlineData("ESCENA UNO — INT. RESTAURANTE — NOCHE")]
    public void Procesar_EncabezadoMalFormado_Error(string encabezado)
    {
        var guion = Escena(1, 1).Replace("ESCENA 1 — INT. CALLE — DÍA", encabezado);

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(1, error.Linea);
        Assert.Contains("Encabezado de escena mal formado", error.Motivo);
    }

    [Fact]
    public void Procesar_EncabezadoExterior_EsExt()
    {
        var guion = Escena(1, 1).Replace("INT. CALLE", "EXT. CALLE");

        Assert.Equal(TipoLocacion.Ext, ProcesarValido(guion)[0].Tipo);
    }

    [Fact]
    public void Procesar_EscenasFueraDeOrden_Error()
    {
        var guion = Escena(1, 1) + "\n" + Escena(3, 1);

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Contains("se esperaba ESCENA 2", error.Motivo);
    }

    [Fact]
    public void Procesar_ClipsFueraDeOrden_Error()
    {
        var guion = Escena(1, 2).Replace("CLIP 2", "CLIP 3");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Contains("se esperaba CLIP 2", error.Motivo);
    }

    [Fact]
    public void Procesar_CampoDeEscenaDentroDeUnClip_Error()
    {
        var guion = Escena(1, 1) + "\nAUDIO: Lluvia.";

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(NumeroDeLinea(guion, "AUDIO: Lluvia."), error.Linea);
        Assert.Contains("antes del primer CLIP", error.Motivo);
    }

    [Fact]
    public void Procesar_CampoDeEscenaRepetido_Error()
    {
        var guion = Escena(1, 1).Replace("AUDIO: Murmullo.", "AUDIO: Murmullo.\nAUDIO: Lluvia.");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(NumeroDeLinea(guion, "AUDIO: Lluvia."), error.Linea);
        Assert.Contains("repetido", error.Motivo);
    }

    [Fact]
    public void Procesar_CampoDeEscenaVacio_Error()
    {
        var guion = Escena(1, 1).Replace("AUDIO: Murmullo.", "AUDIO:");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Contains("vacío", error.Motivo);
    }

    [Fact]
    public void Procesar_LineaSueltaAntesDelPrimerClip_Error()
    {
        var guion = Escena(1, 1).Replace("AUDIO: Murmullo.", "AUDIO: Murmullo.\nAna entra.");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(NumeroDeLinea(guion, "Ana entra."), error.Linea);
        Assert.Contains("antes del primer CLIP", error.Motivo);
    }

    [Theory]
    [InlineData("ANA: Mentiroso.")]
    [InlineData("ANA: “Mentiroso.\"")]
    [InlineData("ANA: \"Mentiroso.”")]
    [InlineData("ANA (sonriendo) \"Mentiroso.\"")]
    [InlineData("ANA:\"Mentiroso.\"")]
    public void Procesar_DialogoMalFormado_Error(string linea)
    {
        var guion = Escena(1, 1).Replace("Ana camina.", "Ana camina.\n" + linea);

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(NumeroDeLinea(guion, linea), error.Linea);
    }

    [Fact]
    public void Procesar_DialogoConComillasTipograficas_EsValido()
    {
        var guion = Escena(1, 1).Replace("Ana camina.", "JOSÉ DANIEL (sonriendo): “Bueno... pasó parecido.”");

        var linea = Assert.Single(ProcesarValido(guion)[0].Clips[0].Dialogos);

        Assert.Equal(new LineaDialogo("c1-l1", "JOSÉ DANIEL", "sonriendo", "Bueno... pasó parecido."), linea);
    }

    [Fact]
    public void Procesar_TextoEnPantallaConComillasTipograficas_EsValido()
    {
        var guion = Escena(1, 1) + "\nTEXTO EN PANTALLA: “Hermano, ¿puedes salir conmigo?”";

        var clip = ProcesarValido(guion)[0].Clips[0];

        Assert.Equal("Hermano, ¿puedes salir conmigo?", clip.TextoEnPantalla);
    }

    [Fact]
    public void Procesar_TextoEnPantallaConComillasMezcladas_Error()
    {
        var guion = Escena(1, 1) + "\nTEXTO EN PANTALLA: “Hermano.\"";

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Contains("Texto en pantalla mal formado", error.Motivo);
    }

    [Fact]
    public void Procesar_ClipVacio_Error()
    {
        var guion = Escena(1, 2).Replace("CLIP 1\nAna camina.", "CLIP 1");

        var error = Assert.Single(ProcesarInvalido(guion));

        Assert.Equal(NumeroDeLinea(guion, "CLIP 1"), error.Linea);
        Assert.Contains("vacío", error.Motivo);
    }

    [Fact]
    public void Procesar_FinDeLineaWindows_EsValido()
    {
        var escena = Assert.Single(ProcesarValido(EscenaEjemplo.Replace("\n", "\r\n")));

        Assert.Equal("NOCHE", escena.MomentoDelDia);
        Assert.Equal("Mentiroso.", escena.Clips[0].Dialogos[0].Texto);
    }

    // --- Ayudas ---

    private static IReadOnlyList<Escena> ProcesarValido(string guion)
    {
        var resultado = ParserGuion.Procesar(guion);
        Assert.True(resultado.EsValido, string.Join("\n", resultado.Errores));
        return resultado.Escenas;
    }

    private static IReadOnlyList<ErrorGuion> ProcesarInvalido(string guion)
    {
        var resultado = ParserGuion.Procesar(guion);
        Assert.False(resultado.EsValido);
        Assert.Empty(resultado.Escenas);
        return resultado.Errores;
    }

    /// <summary>Escena mínima válida: encabezado en su primera línea y cada clip con la acción "Ana camina.".</summary>
    private static string Escena(int numero, int clips)
    {
        var texto = new StringBuilder()
            .Append($"ESCENA {numero} — INT. CALLE — DÍA\n")
            .Append("LOCACIÓN: Una calle.\n")
            .Append("ILUMINACIÓN: Sol de mediodía.\n")
            .Append("PUESTA EN ESCENA: Ana en el centro.\n")
            .Append("AUDIO: Murmullo.\n");
        for (var c = 1; c <= clips; c++)
        {
            texto.Append($"CLIP {c}\nAna camina.\n");
        }

        return texto.ToString().TrimEnd('\n');
    }

    private static string Guion(int escenas, int clipsPorEscena) =>
        string.Join("\n\n", Enumerable.Range(1, escenas).Select(n => Escena(n, clipsPorEscena)));

    private static int NumeroDeLinea(string guion, string contenido) =>
        Array.FindIndex(guion.Split('\n'), l => l == contenido) + 1;

    private static string LeerEjemploDelRepo() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Datos", "ejemplo-guion.txt"));
}
