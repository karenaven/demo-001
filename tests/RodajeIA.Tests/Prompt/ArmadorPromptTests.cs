using System.Diagnostics;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Prompt;

public sealed class ArmadorPromptTests
{
    private static readonly ConfiguracionRodaje Configuracion =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config"));

    private static readonly ArmadorPrompt Armador = new(Configuracion);

    private static readonly HojaPersonaje Ana = new(
        "Ana", "30 años", "Mujer delgada, piel morena, voz suave y grave.", "trenza larga", "chaqueta negra",
        null, "Irónica y cálida", "Protagonista");

    private static readonly HojaPersonaje JoseDaniel = new(
        "José Daniel", "32 años", "Hombre alto, barba corta, voz clara.", "pelo corto ondulado", "camisa azul",
        "cicatriz en la ceja izquierda", "Pícaro", "Coprotagonista");

    [Fact]
    public void Armar_EscenaCompleta_CoincideConLaPlantillaLiteral()
    {
        var escena = Escena(
            new Clip(1, ["Ana y José Daniel conversan."], [new("c1-l1", "ANA", null, "Mentiroso.")], null),
            new Clip(2, ["Un celular vibra."], [], "Hola."));
        Bloque[] bloques =
        [
            new(1,
            [
                new("plano medio", "50mm", "continuidad con la luz base", null, null, "Ana mira a José Daniel.", ["c1-l1"]),
                new("primer plano", "85mm", "contraluz o luz de borde", "picado", "paneo", "Ana sonríe.", []),
            ]),
            new(2, [new("plano detalle", "macro 100mm", "fuente práctica fría (pantalla)", null, null, "El celular vibra.", [])]),
        ];

        var prompt = Armador.Armar(escena, [Ana], bloques);

        Assert.Equal(
            $"""
            {Configuracion.Estilo}

            Locación: Un restaurante pequeño.
            Iluminación base: Luz cálida ambiental.

            Personajes:
            Ana, 30 años. Mujer delgada, piel morena, voz suave y grave. Peinado: trenza larga. Vestuario: chaqueta negra. Personalidad: Irónica y cálida. Rol: Protagonista.

            Puesta en escena:
            Ana a la izquierda del encuadre.

            Audio:
            Murmullo lejano; sin música.

            Bloque 1:
            plano medio, óptica 50mm. Ana mira a José Daniel. ANA: "Mentiroso." Iluminación: continuidad con la luz base. Corte a primer plano, paneo, picado, óptica 85mm. Ana sonríe. Iluminación: contraluz o luz de borde. Sin subtítulos ni texto en pantalla.

            Bloque 2:
            plano detalle, óptica macro 100mm. El celular vibra. Iluminación: fuente práctica fría (pantalla). Único texto visible en pantalla: "Hola."
            """.ReplaceLineEndings("\n"),
            prompt);
    }

    [Fact]
    public void Armar_IncluyeTodasLasSeccionesSinCamposVacios() // AC-08a
    {
        var escena = Escena(ClipSimple(1), ClipSimple(2));

        var prompt = Armador.Armar(escena, [Ana], [BloqueSimple(1), BloqueSimple(2)]);

        var partes = new[]
        {
            Configuracion.Estilo,
            "Locación: Un restaurante pequeño.",
            "Iluminación base: Luz cálida ambiental.",
            "Personajes:\nAna, 30 años.",
            "Puesta en escena:\nAna a la izquierda del encuadre.",
            "Audio:\nMurmullo lejano; sin música.",
            "Bloque 1:",
            "Bloque 2:",
        };
        AssertEnOrden(prompt, partes);
        Assert.DoesNotContain("{{", prompt);
        Assert.DoesNotContain("[", prompt);
        Assert.DoesNotContain(": .", prompt);
        Assert.DoesNotContain("..", prompt);
    }

    [Fact]
    public void Armar_TresClips_BloquesNumeradosEnOrdenConSusTomas() // AC-08b
    {
        var escena = Escena(ClipSimple(1), ClipSimple(2), ClipSimple(3));
        Bloque[] bloques =
        [
            new(1, [TomaSimple("Toma del clip uno")]),
            new(2, [TomaSimple("Toma del clip dos")]),
            new(3, [TomaSimple("Toma del clip tres")]),
        ];

        var prompt = Armador.Armar(escena, [], bloques);

        AssertEnOrden(prompt, ["Bloque 1:", "Toma del clip uno", "Bloque 2:", "Toma del clip dos", "Bloque 3:", "Toma del clip tres"]);
    }

    [Fact]
    public void Armar_HojaSinHeridas_NoMencionaHeridas() // AC-08c
    {
        var prompt = Armador.Armar(Escena(ClipSimple(1)), [Ana], [BloqueSimple(1)]);

        Assert.DoesNotContain("Heridas/marcas", prompt);
    }

    [Fact]
    public void Armar_HojaConHeridas_LasIncluye()
    {
        var prompt = Armador.Armar(Escena(ClipSimple(1)), [JoseDaniel], [BloqueSimple(1)]);

        Assert.Contains("Vestuario: camisa azul. Heridas/marcas: cicatriz en la ceja izquierda. Personalidad: Pícaro.", prompt);
    }

    [Fact]
    public void Armar_MismoPersonajeEnDosEscenasDistintas_DescripcionIdentica() // AC-08e
    {
        var escena1 = Escena(ClipSimple(1)) with { Numero = 1, Locacion = "Un restaurante.", Audio = "Murmullo." };
        var escena2 = Escena(ClipSimple(1), ClipSimple(2)) with { Numero = 2, Locacion = "Una calle.", Audio = "Tráfico." };

        var prompt1 = Armador.Armar(escena1, [Ana, JoseDaniel], [BloqueSimple(1)]);
        var prompt2 = Armador.Armar(escena2, [Ana], [BloqueSimple(1), BloqueSimple(2)]);

        var descripcion1 = Assert.Single(prompt1.Split('\n'), l => l.StartsWith("Ana, "));
        var descripcion2 = Assert.Single(prompt2.Split('\n'), l => l.StartsWith("Ana, "));
        Assert.Equal(descripcion1, descripcion2);
        Assert.Contains("chaqueta negra", descripcion1);
    }

    [Fact]
    public void Armar_ClipConDialogo_CadaLineaIdenticaAlGuionConSuAcotacion() // AC-08f
    {
        var clip = new Clip(
            1,
            ["Conversan."],
            [
                new("c1-l1", "ANA", null, "Mentiroso."),
                new("c1-l2", "JOSÉ DANIEL", "sonriendo", "Bueno... (pausa) pasó parecido."),
            ],
            null);
        Bloque[] bloques =
        [
            new(1,
            [
                TomaSimple("Ana lo mira") with { Dialogos = ["c1-l1"] },
                TomaSimple("José Daniel sonríe") with { Dialogos = ["c1-l2"] },
            ]),
        ];

        var prompt = Armador.Armar(Escena(clip), [], bloques);

        Assert.Contains("ANA: \"Mentiroso.\"", prompt);
        Assert.Contains("JOSÉ DANIEL (sonriendo): \"Bueno... (pausa) pasó parecido.\"", prompt);
    }

    [Fact]
    public void Armar_VariasLineasEnUnaToma_EnElOrdenIndicado()
    {
        var clip = new Clip(1, ["Conversan."], [new("c1-l1", "ANA", null, "Uno."), new("c1-l2", "ANA", null, "Dos.")], null);
        Bloque[] bloques = [new(1, [TomaSimple("Ana habla.") with { Dialogos = ["c1-l1", "c1-l2"] }])];

        var prompt = Armador.Armar(Escena(clip), [], bloques);

        Assert.Contains("Ana habla. ANA: \"Uno.\" ANA: \"Dos.\" Iluminación:", prompt);
    }

    [Fact]
    public void Armar_ClipConTextoEnPantalla_EsElUnicoTextoVisible() // AC-08g
    {
        var clip = new Clip(1, ["José Daniel lee el mensaje."], [], "Hermano, ¿puedes salir conmigo?");

        var prompt = Armador.Armar(Escena(clip), [], [BloqueSimple(1)]);

        Assert.Contains("Único texto visible en pantalla: \"Hermano, ¿puedes salir conmigo?\"", prompt);
        Assert.DoesNotContain("Sin subtítulos", prompt);
    }

    [Fact]
    public void Armar_ClipSinTextoEnPantalla_CierraSinSubtitulos()
    {
        var prompt = Armador.Armar(Escena(ClipSimple(1)), [], [BloqueSimple(1)]);

        Assert.EndsWith("Sin subtítulos ni texto en pantalla.", prompt);
    }

    [Fact]
    public void Armar_SinPersonajesConHoja_OmiteLaSeccionYMantieneElResto() // AC-08h
    {
        var escena = Escena(ClipSimple(1), ClipSimple(2));

        var prompt = Armador.Armar(escena, [], [BloqueSimple(1), BloqueSimple(2)]);

        Assert.DoesNotContain("Personajes:", prompt);
        AssertEnOrden(prompt,
        [
            Configuracion.Estilo,
            "Locación: Un restaurante pequeño.",
            "Iluminación base: Luz cálida ambiental.",
            "Puesta en escena:",
            "Audio:",
            "Bloque 1:",
            "Bloque 2:",
        ]);
    }

    [Fact]
    public void Armar_TomaSinAnguloNiMovimiento_LosOmite()
    {
        var prompt = Armador.Armar(Escena(ClipSimple(1)), [], [BloqueSimple(1)]);

        Assert.Contains("Bloque 1:\nplano medio, óptica 50mm. Ana camina. Iluminación: continuidad con la luz base.", prompt);
    }

    [Fact]
    public void Armar_TomaSoloConAngulo_OmiteSoloElMovimiento()
    {
        Bloque[] bloques = [new(1, [TomaSimple("Ana camina") with { Angulo = "contrapicado" }])];

        var prompt = Armador.Armar(Escena(ClipSimple(1)), [], bloques);

        Assert.Contains("plano medio, contrapicado, óptica 50mm.", prompt);
    }

    [Fact]
    public void Armar_CantidadDeBloquesDistintaALaDeClips_Falla()
    {
        var escena = Escena(ClipSimple(1), ClipSimple(2));

        Assert.Throws<InvalidOperationException>(() => Armador.Armar(escena, [], [BloqueSimple(1)]));
    }

    [Fact]
    public void Armar_BloquesFueraDeOrden_Falla()
    {
        var escena = Escena(ClipSimple(1), ClipSimple(2));

        Assert.Throws<InvalidOperationException>(() => Armador.Armar(escena, [], [BloqueSimple(2), BloqueSimple(1)]));
    }

    [Fact]
    public void Armar_DialogoDeOtroClip_Falla()
    {
        Bloque[] bloques = [new(1, [TomaSimple("Ana habla") with { Dialogos = ["c2-l1"] }])];

        var ex = Assert.Throws<InvalidOperationException>(() => Armador.Armar(Escena(ClipSimple(1)), [], bloques));

        Assert.Contains("c2-l1", ex.Message);
    }

    [Fact]
    public void Armar_TomaConPlanoVacio_Falla() // RN-01
    {
        Bloque[] bloques = [new(1, [TomaSimple("Ana camina") with { Plano = "" }])];

        var ex = Assert.Throws<InvalidOperationException>(() => Armador.Armar(Escena(ClipSimple(1)), [], bloques));

        Assert.Contains("toma.plano", ex.Message);
    }

    [Fact]
    public void Armar_HojaConCampoObligatorioVacio_Falla() // RN-01
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Armador.Armar(Escena(ClipSimple(1)), [Ana with { Vestuario = "" }], [BloqueSimple(1)]));

        Assert.Contains("personaje.vestuario", ex.Message);
    }

    [Fact]
    public void Armar_EscenaDe20Clips_TardaMenosDeUnSegundo() // RNF-04
    {
        var numeros = Enumerable.Range(1, 20).ToList();
        var escena = Escena([.. numeros.Select(ClipSimple)]);
        var bloques = numeros.Select(n => new Bloque(n, [TomaSimple("Uno"), TomaSimple("Dos"), TomaSimple("Tres")])).ToList();
        Armador.Armar(escena, [Ana, JoseDaniel], bloques); // calentamiento

        var reloj = Stopwatch.StartNew();
        Armador.Armar(escena, [Ana, JoseDaniel], bloques);
        reloj.Stop();

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(1), $"Tardó {reloj.Elapsed}.");
    }

    private static Escena Escena(params Clip[] clips) => new(
        1, TipoLocacion.Int, "RESTAURANTE", "NOCHE",
        "Un restaurante pequeño.", "Luz cálida ambiental.", "Ana a la izquierda del encuadre.", "Murmullo lejano; sin música.",
        clips);

    private static Clip ClipSimple(int numero) => new(numero, ["Ana camina."], [], null);

    private static Toma TomaSimple(string accion) =>
        new("plano medio", "50mm", "continuidad con la luz base", null, null, accion, []);

    private static Bloque BloqueSimple(int clip) => new(clip, [TomaSimple("Ana camina.")]);

    private static void AssertEnOrden(string texto, IEnumerable<string> partes)
    {
        var desde = 0;
        foreach (var parte in partes)
        {
            var indice = texto.IndexOf(parte, desde, StringComparison.Ordinal);
            Assert.True(indice >= 0, $"No se encontró, o está fuera de orden: \"{parte}\"");
            desde = indice + parte.Length;
        }
    }
}
