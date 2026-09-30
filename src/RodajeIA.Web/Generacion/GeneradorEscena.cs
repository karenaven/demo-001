using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using RodajeIA.Web.Configuracion;
using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Generacion;

/// <summary>Bloques generados para una escena, o el motivo de cada intento fallido.</summary>
public sealed record ResultadoGeneracion(IReadOnlyList<Bloque>? Bloques, IReadOnlyList<string> Fallos)
{
    public bool Exito => Bloques is not null;

    public int Intentos => Fallos.Count + (Exito ? 1 : 0);
}

/// <summary>
/// Genera los bloques de una escena con Gemini (RF-06a) y valida cada respuesta (RF-06b). Ante error, timeout
/// o respuesta inválida reintenta hasta completar <see cref="MaxIntentos"/> (RNF-07); si se agotan, la escena
/// queda en error (RF-10b).
/// </summary>
public sealed class GeneradorEscena(
    IClienteGemini cliente,
    ConstructorInstruccion constructor,
    ConfiguracionRodaje configuracion,
    IOptions<OpcionesGemini> opciones,
    TimeProvider reloj,
    ILogger<GeneradorEscena> logger)
{
    public const int MaxIntentos = 3;

    private readonly JsonObject _schema = SchemaGemini.Generar(configuracion.Vocabulario);

    /// <param name="alEmpezarIntento">Se llama con el número de intento (1 a <see cref="MaxIntentos"/>) justo antes de llamar a Gemini.</param>
    public async Task<ResultadoGeneracion> GenerarAsync(
        Escena escena,
        IReadOnlyList<HojaPersonaje> personajes,
        CancellationToken cancelacion = default,
        Action<int>? alEmpezarIntento = null)
    {
        var instruccion = constructor.Construir(escena, personajes);
        var fallos = new List<string>();

        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            if (intento > 1)
            {
                await Task.Delay(opciones.Value.PausaAntesDelIntento(intento), reloj, cancelacion);
            }

            alEmpezarIntento?.Invoke(intento);
            string motivo;
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(opciones.Value.Timeout);
            try
            {
                var json = await cliente.GenerarAsync(instruccion, _schema, limite.Token);
                var resultado = ValidadorRespuesta.Validar(escena, json, configuracion.Vocabulario);
                if (resultado.EsValida)
                {
                    return new ResultadoGeneracion(resultado.Bloques, fallos);
                }

                motivo = "Respuesta inválida: " + string.Join(" ", resultado.Errores);
            }
            catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                motivo = $"Sin respuesta en {opciones.Value.Timeout.TotalSeconds:0} s.";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                motivo = ex.Message;
            }

            fallos.Add(motivo);
            logger.LogWarning(
                "Escena {Escena}: intento {Intento} de {Max} fallido. {Motivo}", escena.Numero, intento, MaxIntentos, motivo);
        }

        return new ResultadoGeneracion(null, fallos);
    }
}
