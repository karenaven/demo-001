using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Generacion;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Generacion;

public sealed class GeneradorEscenaTests
{
    private static readonly ConfiguracionRodaje Configuracion =
        ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config"));

    private static readonly HojaPersonaje Ana = new(
        "Ana", "30 años", "Mujer delgada.", "trenza larga", "chaqueta negra", null, "Irónica", "Protagonista");

    private readonly ClienteFalso _cliente = new();

    [Fact]
    public async Task Generar_RespuestaCorrectaAlPrimerIntento_NBloques() // AC-06a
    {
        var escena = Escena(1, clips: 4);
        _cliente.Responder(RespuestaValida(escena));

        var resultado = await Generador().GenerarAsync(escena, [Ana], TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.Equal(4, resultado.Bloques!.Count);
        Assert.Equal(1, resultado.Intentos);
        Assert.Equal(1, _cliente.Llamadas);
    }

    [Fact]
    public async Task Generar_ErrorEnLosTresIntentos_ExactamenteTresLlamadasYError() // AC-10a
    {
        _cliente.Fallar(3);

        var resultado = await Generador().GenerarAsync(Escena(3, clips: 2), [], TestContext.Current.CancellationToken);

        Assert.False(resultado.Exito);
        Assert.Null(resultado.Bloques);
        Assert.Equal(3, _cliente.Llamadas);
        Assert.Equal(3, resultado.Fallos.Count);
    }

    [Fact]
    public async Task Generar_ErrorYLuegoRespuestaCorrecta_QuedaConSusBloques() // AC-10c
    {
        var escena = Escena(1, clips: 2);
        _cliente.Fallar(1);
        _cliente.Responder(RespuestaValida(escena));

        var resultado = await Generador().GenerarAsync(escena, [], TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.Equal(2, resultado.Intentos);
        Assert.Single(resultado.Fallos);
    }

    [Fact]
    public async Task Generar_EscenaEnErrorReintentadaConExito_QuedaConSusBloques() // AC-10d (RF-10c)
    {
        var escena = Escena(1, clips: 2);
        var generador = Generador();
        _cliente.Fallar(3);
        Assert.False((await generador.GenerarAsync(escena, [], TestContext.Current.CancellationToken)).Exito);

        _cliente.Responder(RespuestaValida(escena));
        var reintento = await generador.GenerarAsync(escena, [], TestContext.Current.CancellationToken);

        Assert.True(reintento.Exito);
        Assert.Equal(2, reintento.Bloques!.Count);
    }

    [Fact]
    public async Task Generar_RespuestaInvalidaYLuegoCorrecta_Reintenta() // AC-06b–e: "se trata como fallida y se reintenta"
    {
        var escena = Escena(1, clips: 3);
        _cliente.Responder(new JsonObject { ["bloques"] = new JsonArray() }.ToJsonString());
        _cliente.Responder(RespuestaValida(escena));

        var resultado = await Generador().GenerarAsync(escena, [], TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.Equal(2, _cliente.Llamadas);
        Assert.Contains("Se esperaban 3 bloques", resultado.Fallos[0]);
    }

    [Fact]
    public async Task Generar_SinRespuestaAntesDelTimeout_CuentaComoIntentoFallido() // RNF-06
    {
        _cliente.Colgarse(3);

        var resultado = await Generador(timeout: TimeSpan.FromMilliseconds(50)).GenerarAsync(Escena(1, clips: 1), [], TestContext.Current.CancellationToken);

        Assert.False(resultado.Exito);
        Assert.Equal(3, _cliente.Llamadas);
        Assert.All(resultado.Fallos, f => Assert.StartsWith("Sin respuesta", f));
    }

    [Fact]
    public async Task Generar_CanceladoDesdeAfuera_NoReintenta()
    {
        _cliente.Colgarse(3);
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Generador().GenerarAsync(Escena(1, clips: 1), [], cancelacion.Token));

        Assert.Equal(1, _cliente.Llamadas);
    }

    [Fact]
    public async Task Generar_EnviaLaInstruccionDeLaEscenaYElSchemaDelVocabulario()
    {
        var escena = Escena(1, clips: 1);
        _cliente.Responder(RespuestaValida(escena));

        await Generador().GenerarAsync(escena, [Ana], TestContext.Current.CancellationToken);

        Assert.Contains("[c1-l1] ANA: \"Hola.\"", _cliente.UltimaInstruccion);
        Assert.Contains(new ArmadorPrompt(Configuracion).DescribirPersonaje(Ana), _cliente.UltimaInstruccion);
        var enumPlano = _cliente.UltimoSchema!["properties"]!["bloques"]!["items"]!["properties"]!["tomas"]!["items"]!["properties"]!["plano"]!["enum"];
        Assert.NotNull(enumPlano);
    }

    [Fact]
    public async Task Generar_TresIntentosFallidos_EsperaDiezYTreintaSegundosAntesDeReintentar()
    {
        _cliente.Fallar(3);
        var reloj = new RelojFalso();

        await Generador(reloj: reloj, pausasPorDefecto: true)
            .GenerarAsync(Escena(1, clips: 1), [], TestContext.Current.CancellationToken);

        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)], reloj.Esperas);
    }

    [Fact]
    public async Task Generar_BienAlPrimerIntento_NoEspera()
    {
        var escena = Escena(1, clips: 1);
        _cliente.Responder(RespuestaValida(escena));
        var reloj = new RelojFalso();

        await Generador(reloj: reloj, pausasPorDefecto: true).GenerarAsync(escena, [], TestContext.Current.CancellationToken);

        Assert.Empty(reloj.Esperas);
    }

    [Fact]
    public void Appsettings_PausasEntreIntentos_SonDiezYTreintaSegundos()
    {
        var opciones = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build()
            .GetSection(OpcionesGemini.Seccion)
            .Get<OpcionesGemini>()!;

        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)], opciones.PausasEntreIntentos!);
        Assert.Equal(TimeSpan.FromSeconds(10), opciones.PausaAntesDelIntento(2));
        Assert.Equal(TimeSpan.FromSeconds(30), opciones.PausaAntesDelIntento(3));
    }

    private GeneradorEscena Generador(TimeSpan? timeout = null, TimeProvider? reloj = null, bool pausasPorDefecto = false) => new(
        _cliente,
        new ConstructorInstruccion(Configuracion, new ArmadorPrompt(Configuracion)),
        Configuracion,
        Options.Create(new OpcionesGemini
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5),
            PausasEntreIntentos = pausasPorDefecto ? null : [],
        }),
        reloj ?? TimeProvider.System,
        NullLogger<GeneradorEscena>.Instance);

    internal static Escena Escena(int numero, int clips) => new(
        numero, TipoLocacion.Int, "RESTAURANTE", "NOCHE", "Locación.", "Iluminación.", "Puesta.", "Audio.",
        Enumerable.Range(1, clips)
            .Select(c => new Clip(c, ["Ana camina."], [new LineaDialogo($"c{c}-l1", "ANA", null, "Hola.")], null))
            .ToList());

    /// <summary>Respuesta que cumple RN-08 y RN-09: una toma por clip que referencia todas sus líneas.</summary>
    internal static string RespuestaValida(Escena escena) => new JsonObject
    {
        ["bloques"] = new JsonArray([.. escena.Clips.Select(c => (JsonNode)new JsonObject
        {
            ["clip"] = c.Numero,
            ["tomas"] = new JsonArray(new JsonObject
            {
                ["plano"] = "plano medio",
                ["optica"] = "50mm",
                ["iluminacion"] = "continuidad con la luz base",
                ["accion"] = "Ana camina.",
                ["dialogos"] = new JsonArray([.. c.Dialogos.Select(d => JsonValue.Create(d.Id))]),
            }),
        })]),
    }.ToJsonString();
}

/// <summary>Reloj que no espera: registra cada pausa pedida y la da por cumplida enseguida.</summary>
internal sealed class RelojFalso : TimeProvider
{
    public List<TimeSpan> Esperas { get; } = [];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            Esperas.Add(dueTime);
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }

        return new TimerNulo();
    }

    private sealed class TimerNulo : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Cliente de Gemini simulado: cada llamada consume la próxima respuesta encolada.</summary>
internal sealed class ClienteFalso : IClienteGemini
{
    private readonly Queue<Func<string, CancellationToken, Task<string>>> _respuestas = new();

    public int Llamadas { get; private set; }

    public string UltimaInstruccion { get; private set; } = "";

    public JsonObject? UltimoSchema { get; private set; }

    public void Responder(string json) => _respuestas.Enqueue((_, _) => Task.FromResult(json));

    public void Responder(Func<string, string> segunInstruccion) =>
        _respuestas.Enqueue((instruccion, _) => Task.FromResult(segunInstruccion(instruccion)));

    public void Fallar(int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            _respuestas.Enqueue((_, _) => throw new HttpRequestException("Gemini respondió 503."));
        }
    }

    public void Colgarse(int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            _respuestas.Enqueue(async (_, cancelacion) =>
            {
                await Task.Delay(Timeout.Infinite, cancelacion);
                return "";
            });
        }
    }

    public Task<string> GenerarAsync(string instruccion, JsonObject schema, CancellationToken cancelacion)
    {
        Llamadas++;
        UltimaInstruccion = instruccion;
        UltimoSchema = schema;
        return _respuestas.Dequeue()(instruccion, cancelacion);
    }
}
