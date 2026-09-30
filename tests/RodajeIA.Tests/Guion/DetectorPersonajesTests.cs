using RodajeIA.Web.Dominio;
using RodajeIA.Web.Guion;

namespace RodajeIA.Tests.Guion;

public sealed class DetectorPersonajesTests
{
    private static readonly HojaPersonaje Ana = Hoja("Ana");
    private static readonly HojaPersonaje JoseDaniel = Hoja("José Daniel");

    [Fact]
    public void Detectar_NombreEnLaAccionSinDialogo_PresenteEnClipYEscena() // AC-05a
    {
        var escena = EscenaCon(Clip(1, accion: ["Ana mira por la ventana."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Equal([Ana], detectados.PorClip[1]);
        Assert.Equal([Ana], detectados.EnEscena);
    }

    [Fact]
    public void Detectar_MarcadorDeDialogoSinMencionEnAccion_PresenteEnClipYEscena() // AC-05b
    {
        var escena = EscenaCon(Clip(1, accion: ["Alguien sirve el vino."], dialogos: [Linea("ANA", "Claro.")]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Equal([Ana], detectados.PorClip[1]);
        Assert.Equal([Ana], detectados.EnEscena);
    }

    [Fact]
    public void Detectar_PersonajeSinHojaEnLaAccion_NoSeDetecta() // AC-05c
    {
        var escena = EscenaCon(Clip(1, accion: ["El Mesero deja dos platos."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Empty(detectados.PorClip[1]);
        Assert.Empty(detectados.EnEscena);
    }

    [Fact]
    public void Detectar_DialogoDePersonajeSinHoja_GuionValidoYNoSeDetecta() // AC-05d
    {
        var resultado = ParserGuion.Procesar("""
            ESCENA 1 — INT. RESTAURANTE — NOCHE
            LOCACIÓN: Un restaurante.
            ILUMINACIÓN: Luz cálida.
            PUESTA EN ESCENA: Ana sentada.
            AUDIO: Murmullo.
            CLIP 1
            MESERO: "¿Algo más?"
            """);
        Assert.True(resultado.EsValido);
        var escena = Assert.Single(resultado.Escenas);

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Equal("MESERO", Assert.Single(escena.Clips[0].Dialogos).Personaje);
        Assert.Empty(detectados.PorClip[1]);
        Assert.Empty(detectados.EnEscena);
    }

    [Fact]
    public void Detectar_NombreCompuestoConTilde_SeDetectaEnAccionYDialogo()
    {
        var escena = EscenaCon(
            Clip(1, accion: ["José Daniel sonríe."]),
            Clip(2, accion: ["Se oye una voz."], dialogos: [Linea("JOSÉ DANIEL", "Bueno...")]));

        var detectados = DetectorPersonajes.Detectar(escena, [JoseDaniel]);

        Assert.Equal([JoseDaniel], detectados.PorClip[1]);
        Assert.Equal([JoseDaniel], detectados.PorClip[2]);
    }

    [Theory]
    [InlineData("Mariana camina.")]
    [InlineData("Anabel camina.")]
    [InlineData("La ana camina.")]
    [InlineData("ANA camina.")]
    public void Detectar_NombreDentroDeOtraPalabraOConOtrasMayusculas_NoSeDetecta(string accion)
    {
        var escena = EscenaCon(Clip(1, accion: [accion]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Empty(detectados.PorClip[1]);
    }

    [Theory]
    [InlineData("Ana, divertida, niega.")]
    [InlineData("José Daniel mira a Ana.")]
    [InlineData("(Ana) duda.")]
    public void Detectar_NombreJuntoAPuntuacion_SeDetecta(string accion)
    {
        var escena = EscenaCon(Clip(1, accion: [accion]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Equal([Ana], detectados.PorClip[1]);
    }

    [Fact]
    public void Detectar_NombreQueContieneAOtro_SoloDetectaElMasLargo()
    {
        var anaMaria = Hoja("Ana María");
        var escena = EscenaCon(Clip(1, accion: ["Ana María entra al restaurante."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana, anaMaria]);

        Assert.Equal([anaMaria], detectados.PorClip[1]);
    }

    [Fact]
    public void Detectar_AmbosNombresMencionados_DetectaLosDos()
    {
        var anaMaria = Hoja("Ana María");
        var escena = EscenaCon(Clip(1, accion: ["Ana María saluda a Ana."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana, anaMaria]);

        Assert.Equal([Ana, anaMaria], detectados.PorClip[1]);
    }

    [Fact]
    public void Detectar_NombreSoloEnTextoEnPantalla_NoSeDetecta()
    {
        var escena = EscenaCon(Clip(1, accion: ["Un celular vibra."], textoEnPantalla: "Ana, ¿dónde estás?"));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Empty(detectados.PorClip[1]);
    }

    [Fact]
    public void Detectar_VariosClips_CadaClipConLosSuyosYLaEscenaConTodos()
    {
        var escena = EscenaCon(
            Clip(1, accion: ["José Daniel espera solo."]),
            Clip(2, accion: ["Ana llega."]),
            Clip(3, accion: ["La lluvia golpea la ventana."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana, JoseDaniel]);

        Assert.Equal([JoseDaniel], detectados.PorClip[1]);
        Assert.Equal([Ana], detectados.PorClip[2]);
        Assert.Empty(detectados.PorClip[3]);
        Assert.Equal([Ana, JoseDaniel], detectados.EnEscena);
    }

    [Fact]
    public void Detectar_MencionadoVariasVeces_ApareceUnaSolaVez()
    {
        var escena = EscenaCon(Clip(
            1,
            accion: ["Ana sonríe.", "Ana niega con la cabeza."],
            dialogos: [Linea("ANA", "Mentiroso.")]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana]);

        Assert.Equal([Ana], detectados.PorClip[1]);
    }

    [Fact]
    public void Detectar_OrdenDelResultado_EsElDeLasHojas()
    {
        var escena = EscenaCon(Clip(1, accion: ["José Daniel y Ana brindan."]));

        var detectados = DetectorPersonajes.Detectar(escena, [Ana, JoseDaniel]);

        Assert.Equal([Ana, JoseDaniel], detectados.PorClip[1]);
        Assert.Equal([Ana, JoseDaniel], detectados.EnEscena);
    }

    [Fact]
    public void Detectar_SerieSinHojas_NadieDetectado()
    {
        var escena = EscenaCon(Clip(1, accion: ["Ana sonríe."], dialogos: [Linea("ANA", "Hola.")]));

        var detectados = DetectorPersonajes.Detectar(escena, []);

        Assert.Empty(detectados.PorClip[1]);
        Assert.Empty(detectados.EnEscena);
    }

    private static HojaPersonaje Hoja(string nombre) =>
        new(nombre, "30 años", "Descripción.", "Peinado.", "Vestuario.", null, "Personalidad.", "Rol.");

    private static LineaDialogo Linea(string personaje, string texto) => new("c1-l1", personaje, null, texto);

    private static Clip Clip(int numero, string[] accion, LineaDialogo[]? dialogos = null, string? textoEnPantalla = null) =>
        new(numero, accion, dialogos ?? [], textoEnPantalla);

    private static Escena EscenaCon(params Clip[] clips) =>
        new(1, TipoLocacion.Int, "RESTAURANTE", "NOCHE", "Locación.", "Iluminación.", "Puesta.", "Audio.", clips);
}
