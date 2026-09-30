using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Generacion;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Generacion;

public sealed class ConstructorInstruccionTests
{
    private static readonly ConfiguracionRodaje Configuracion =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config"));

    private static readonly ArmadorPrompt Armador = new(Configuracion);

    private static readonly ConstructorInstruccion Constructor = new(Configuracion, Armador);

    private static readonly HojaPersonaje Ana = new(
        "Ana", "30 años", "Mujer delgada.", "trenza larga", "chaqueta negra", null, "Irónica", "Protagonista");

    private static readonly Escena EscenaEjemplo = new(
        1, TipoLocacion.Int, "RESTAURANTE", "NOCHE",
        "Restaurante pequeño.", "Luz cálida.", "Ana a la izquierda.", "Murmullo.",
        [
            new Clip(
                1,
                ["Ana y José Daniel conversan."],
                [
                    new LineaDialogo("c1-l1", "ANA", null, "Mentiroso."),
                    new LineaDialogo("c1-l2", "JOSÉ DANIEL", "sonriendo", "Bueno... pasó parecido."),
                ],
                null),
            new Clip(2, ["José Daniel lee el mensaje."], [], "Hermano, ¿puedes salir conmigo?"),
        ]);

    [Fact]
    public void Construir_IncluyeLaEscenaConCamposClipsEIdsDeDialogo()
    {
        var instruccion = Constructor.Construir(EscenaEjemplo, [Ana]);

        Assert.Contains(
            """
            ESCENA 1 — INT. RESTAURANTE — NOCHE
            Locación: Restaurante pequeño.
            Iluminación base: Luz cálida.
            Puesta en escena: Ana a la izquierda.
            Audio: Murmullo.

            CLIP 1
            Acción:
            - Ana y José Daniel conversan.
            Diálogo (id entre corchetes):
            - [c1-l1] ANA: "Mentiroso."
            - [c1-l2] JOSÉ DANIEL (sonriendo): "Bueno... pasó parecido."

            CLIP 2
            Acción:
            - José Daniel lee el mensaje.
            Diálogo: ninguno.
            Texto en pantalla: "Hermano, ¿puedes salir conmigo?"
            """.ReplaceLineEndings("\n"),
            instruccion);
    }

    [Fact]
    public void Construir_HojasConElMismoTextoQueElPromptFinal() // RN-03
    {
        var instruccion = Constructor.Construir(EscenaEjemplo, [Ana]);

        Assert.Contains(Armador.DescribirPersonaje(Ana), instruccion);
    }

    [Fact]
    public void Construir_SinPersonajes_LoIndica()
    {
        var instruccion = Constructor.Construir(EscenaEjemplo, []);

        Assert.Contains("(hojas efectivas):\nNinguno.", instruccion);
    }

    [Fact]
    public void Construir_NoQuedanMarcasSinReemplazar()
    {
        var instruccion = Constructor.Construir(EscenaEjemplo, [Ana]);

        Assert.DoesNotContain("{{", instruccion);
        Assert.StartsWith("Eres un experto", instruccion);
    }
}
