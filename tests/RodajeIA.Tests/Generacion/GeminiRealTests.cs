using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Generacion;
using RodajeIA.Web.Guion;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Generacion;

/// <summary>
/// Llama a Gemini de verdad con la key de user-secrets. Es explícito: no corre con <c>dotnet test</c>,
/// solo con <c>dotnet test -- --explicit only</c>. Gasta cuota.
/// </summary>
public sealed class GeminiRealTests
{
    [Fact(Explicit = true)]
    public async Task GenerarEscenaDelEjemplo_ConGeminiReal_DevuelveBloquesValidosYArmaElPrompt()
    {
        var configuracion = ConfiguracionRodaje.Cargar(Path.Combine(AppContext.BaseDirectory, "config"));
        var appsettings = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .AddUserSecrets<Program>()
            .Build();
        var opciones = appsettings.GetSection(OpcionesGemini.Seccion).Get<OpcionesGemini>()!;
        var armador = new ArmadorPrompt(configuracion);
        var generador = new GeneradorEscena(
            new ClienteGemini(new HttpClient(), Options.Create(opciones)),
            new ConstructorInstruccion(configuracion, armador),
            configuracion,
            Options.Create(opciones),
            TimeProvider.System,
            NullLogger<GeneradorEscena>.Instance);

        var guion = ParserGuion.Procesar(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Datos", "ejemplo-guion.txt")));
        var escena = guion.Escenas[0];
        HojaPersonaje[] hojas =
        [
            new("Ana", "28 años", "Mujer latina de piel morena clara, ojos café, rostro ovalado; voz cálida y suave.",
                "cabello castaño oscuro largo, suelto con ondas", "blusa de seda color vino y aretes dorados pequeños",
                null, "Ingeniosa, cálida, un poco burlona", "Protagonista"),
            new("José Daniel", "30 años", "Hombre latino de piel trigueña, barba corta cuidada, complexión media; voz grave y relajada.",
                "cabello negro corto peinado hacia atrás", "camisa azul marino arremangada y reloj plateado",
                null, "Pícaro, encantador, leal", "Coprotagonista"),
        ];
        var personajes = DetectorPersonajes.Detectar(escena, hojas).EnEscena;

        var reloj = Stopwatch.StartNew();
        var resultado = await generador.GenerarAsync(escena, personajes, TestContext.Current.CancellationToken);
        reloj.Stop();

        // Un test que pasa no muestra su salida, así que el resumen también queda en un archivo.
        var resumen = string.Join("\n", [
            $"Modelo: {opciones.Modelo} · {reloj.Elapsed.TotalSeconds:0.0} s · intentos: {resultado.Intentos}",
            .. resultado.Fallos.Select(f => $"Fallo: {f}")]);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "gemini-real-resumen.txt"), resumen);
        var salida = TestContext.Current.TestOutputHelper!;
        salida.WriteLine(resumen);

        Assert.True(resultado.Exito, string.Join("\n", resultado.Fallos));
        var prompt = armador.Armar(escena, personajes, resultado.Bloques!);
        salida.WriteLine("");
        salida.WriteLine(prompt);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "prompt-gemini-real.txt"), prompt);
    }
}
