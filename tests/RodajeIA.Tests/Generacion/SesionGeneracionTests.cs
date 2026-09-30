using System.Text;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Generacion;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Generacion;

public sealed class SesionGeneracionTests : IDisposable
{
    private static readonly ConfiguracionRodaje Configuracion =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config"));

    private static readonly ArmadorPrompt Armador = new(Configuracion);

    private static readonly HojaPersonaje Ana = new(
        "Ana", "30 años", "Mujer delgada, voz suave.", "trenza larga", "chaqueta negra", null, "Irónica", "Protagonista");

    private static readonly HojaPersonaje Luis = Ana with { Nombre = "Luis", DescripcionFisica = "Hombre alto." };

    private readonly ClienteFalso _cliente = new();
    private readonly SesionGeneracion _sesion;

    public SesionGeneracionTests()
    {
        var generador = new GeneradorEscena(
            _cliente,
            new ConstructorInstruccion(Configuracion, Armador),
            Configuracion,
            Options.Create(new OpcionesGemini { PausasEntreIntentos = [], Timeout = TimeSpan.FromSeconds(5) }),
            TimeProvider.System,
            NullLogger<GeneradorEscena>.Instance);
        _sesion = new SesionGeneracion(generador, Armador);
    }

    public void Dispose() => _sesion.Dispose();

    [Fact]
    public void Cargar_GuionValido_EscenasEnOrdenPendientesYConSusPersonajes()
    {
        var resultado = _sesion.Cargar(Guion(escenas: 3, clips: 2), [Ana, Luis]);

        Assert.True(resultado.EsValido);
        Assert.Equal([1, 2, 3], _sesion.Escenas.Select(e => e.Escena.Numero));
        Assert.All(_sesion.Escenas, e => Assert.Equal(EstadoGeneracion.Pendiente, e.Estado));
        Assert.Equal([Ana], _sesion.Escenas[0].Personajes.EnEscena);
    }

    [Fact]
    public void Cargar_GuionInvalidoSinGuionPrevio_NoQuedaNingunaEscena() // AC-03f
    {
        var resultado = _sesion.Cargar("Ana camina.", [Ana]);

        Assert.False(resultado.EsValido);
        Assert.Empty(_sesion.Escenas);
        Assert.Equal(0, _cliente.Llamadas);
    }

    [Fact]
    public async Task Cargar_GuionInvalidoConGuionPrevio_ConservaElAnterior() // AC-02d
    {
        _sesion.Cargar(Guion(escenas: 2, clips: 1), [Ana]);
        ResponderBienATodas();
        await _sesion.GenerarPendientesAsync();

        var resultado = _sesion.Cargar("ESCENA 1 — INT. CALLE", [Ana]);

        Assert.False(resultado.EsValido);
        Assert.Equal(2, _sesion.Escenas.Count);
        Assert.All(_sesion.Escenas, e => Assert.Equal(EstadoGeneracion.Lista, e.Estado));
    }

    [Fact]
    public async Task GenerarPendientes_TodasBien_CadaEscenaListaConSuPromptFinal() // AC-06f, RF-08a
    {
        _sesion.Cargar(Guion(escenas: 2, clips: 3), [Ana]);
        ResponderBienATodas();

        await _sesion.GenerarPendientesAsync();

        Assert.Equal(2, _cliente.Llamadas);
        Assert.All(_sesion.Escenas, e =>
        {
            Assert.Equal(EstadoGeneracion.Lista, e.Estado);
            Assert.Contains("Bloque 3:", e.Prompt);
            Assert.Contains(Armador.DescribirPersonaje(Ana), e.Prompt);
            Assert.Contains("ANA: \"Hola.\"", e.Prompt);
        });
    }

    [Fact]
    public async Task GenerarPendientes_TerceraFalla_UnoYDosListasYTresEnError() // AC-10b, AC-11e
    {
        _sesion.Cargar(Guion(escenas: 3, clips: 1), [Ana]);
        _cliente.Responder(GeneradorEscenaTests.RespuestaValida(_sesion.Escenas[0].Escena));
        _cliente.Responder(GeneradorEscenaTests.RespuestaValida(_sesion.Escenas[1].Escena));
        _cliente.Fallar(3);

        await _sesion.GenerarPendientesAsync();

        Assert.Equal([1, 2, 3], _sesion.Escenas.Select(e => e.Escena.Numero));
        Assert.NotNull(_sesion.Escenas[0].Prompt);
        Assert.NotNull(_sesion.Escenas[1].Prompt);
        Assert.Equal(EstadoGeneracion.Error, _sesion.Escenas[2].Estado);
        Assert.Null(_sesion.Escenas[2].Prompt);
        Assert.Equal(3, _sesion.Escenas[2].Fallos.Count);
    }

    [Fact]
    public async Task Reintentar_EscenaEnErrorConRespuestaCorrecta_QuedaLista() // AC-10d
    {
        _sesion.Cargar(Guion(escenas: 1, clips: 2), [Ana]);
        _cliente.Fallar(3);
        await _sesion.GenerarPendientesAsync();
        var escena = _sesion.Escenas[0];
        Assert.Equal(EstadoGeneracion.Error, escena.Estado);

        _cliente.Responder(GeneradorEscenaTests.RespuestaValida(escena.Escena));
        await _sesion.ReintentarAsync(escena);

        Assert.Equal(EstadoGeneracion.Lista, escena.Estado);
        Assert.Empty(escena.Fallos);
        Assert.Contains("Bloque 2:", escena.Prompt);
    }

    [Fact]
    public async Task Reintentar_EscenaQueNoEstaEnError_NoLlamaAGemini()
    {
        _sesion.Cargar(Guion(escenas: 1, clips: 1), [Ana]);
        ResponderBienATodas();
        await _sesion.GenerarPendientesAsync();

        await _sesion.ReintentarAsync(_sesion.Escenas[0]);

        Assert.Equal(1, _cliente.Llamadas);
    }

    [Fact]
    public async Task GenerarPendientes_AvisaCadaIntentoYElResultado()
    {
        _sesion.Cargar(Guion(escenas: 1, clips: 1), [Ana]);
        var escena = _sesion.Escenas[0];
        _cliente.Fallar(1);
        _cliente.Responder(GeneradorEscenaTests.RespuestaValida(escena.Escena));
        var vistos = new List<string>();
        _sesion.Cambio += () => vistos.Add($"{escena.Estado} {escena.Intento}");

        await _sesion.GenerarPendientesAsync();

        Assert.Equal(["Generando 1", "Generando 2", "Lista 2"], vistos);
    }

    [Fact]
    public async Task Cargar_OtroGuionMientrasGenera_CancelaLaGeneracionAnterior()
    {
        _sesion.Cargar(Guion(escenas: 2, clips: 1), [Ana]);
        _cliente.Colgarse(1);
        var generacionAnterior = _sesion.GenerarPendientesAsync();

        _sesion.Cargar(Guion(escenas: 1, clips: 4), [Ana]);
        await generacionAnterior;

        Assert.Equal(1, _cliente.Llamadas);
        var nueva = Assert.Single(_sesion.Escenas);
        Assert.Equal(4, nueva.Escena.Clips.Count);
        Assert.Equal(EstadoGeneracion.Pendiente, nueva.Estado);
    }

    private void ResponderBienATodas()
    {
        foreach (var escena in _sesion.Escenas)
        {
            _cliente.Responder(GeneradorEscenaTests.RespuestaValida(escena.Escena));
        }
    }

    /// <summary>Guion válido: en cada clip, Ana camina y dice "Hola.".</summary>
    private static string Guion(int escenas, int clips)
    {
        var texto = new StringBuilder();
        for (var e = 1; e <= escenas; e++)
        {
            texto.Append($"ESCENA {e} — INT. CALLE — DÍA\n")
                .Append("LOCACIÓN: Una calle.\nILUMINACIÓN: Sol.\nPUESTA EN ESCENA: Ana en el centro.\nAUDIO: Tráfico.\n");
            for (var c = 1; c <= clips; c++)
            {
                texto.Append($"CLIP {c}\nAna camina.\nANA: \"Hola.\"\n");
            }

            texto.Append('\n');
        }

        return texto.ToString();
    }
}
