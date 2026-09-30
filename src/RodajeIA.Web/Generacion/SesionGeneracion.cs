using RodajeIA.Web.Dominio;
using RodajeIA.Web.Guion;
using RodajeIA.Web.Prompt;

namespace RodajeIA.Web.Generacion;

public enum EstadoGeneracion
{
    Pendiente,
    Generando,
    Lista,
    Error,
}

/// <summary>Estado de una escena dentro de la sesión: se actualiza a medida que avanza su generación.</summary>
public sealed class EscenaEnSesion(Escena escena, PersonajesDetectados personajes)
{
    public Escena Escena { get; } = escena;

    public PersonajesDetectados Personajes { get; } = personajes;

    public EstadoGeneracion Estado { get; internal set; } = EstadoGeneracion.Pendiente;

    /// <summary>Intento en curso (1 a <see cref="GeneradorEscena.MaxIntentos"/>) mientras está generando.</summary>
    public int Intento { get; internal set; }

    /// <summary>Prompt final armado por plantilla, cuando la escena está lista.</summary>
    public string? Prompt { get; internal set; }

    /// <summary>Motivo de cada intento fallido de la última generación.</summary>
    public IReadOnlyList<string> Fallos { get; internal set; } = [];
}

/// <summary>
/// Guion cargado y generación de sus escenas, en memoria (vive mientras dure la sesión del navegador).
/// Al cargar un guion válido se generan todas las escenas, una por una (RF-06c); cada una queda lista con su
/// prompt final en cuanto termina (RF-10a), o en error si agota los intentos (RF-10b), y se puede reintentar (RF-10c).
/// </summary>
public sealed class SesionGeneracion(GeneradorEscena generador, ArmadorPrompt armador) : IDisposable
{
    private CancellationTokenSource _cancelacion = new();

    public IReadOnlyList<EscenaEnSesion> Escenas { get; private set; } = [];

    /// <summary>Se dispara en cada cambio de estado de una escena.</summary>
    public event Action? Cambio;

    /// <summary>
    /// Valida y divide el guion. Si es válido, reemplaza las escenas de la sesión (cancelando la generación anterior)
    /// y devuelve el resultado; si es inválido, la sesión no cambia (RF-03c).
    /// </summary>
    public ResultadoGuion Cargar(string guion, IReadOnlyList<HojaPersonaje> hojas)
    {
        var resultado = ParserGuion.Procesar(guion);
        if (!resultado.EsValido)
        {
            return resultado;
        }

        _cancelacion.Cancel();
        _cancelacion.Dispose();
        _cancelacion = new CancellationTokenSource();

        // Sin variantes todavía (RF-09): la hoja efectiva es la hoja base.
        Escenas = resultado.Escenas
            .Select(e => new EscenaEnSesion(e, DetectorPersonajes.Detectar(e, hojas)))
            .ToList();
        Cambio?.Invoke();
        return resultado;
    }

    /// <summary>Genera, en orden, las escenas que todavía no se generaron.</summary>
    public async Task GenerarPendientesAsync()
    {
        var token = _cancelacion.Token;
        foreach (var escena in Escenas.Where(e => e.Estado == EstadoGeneracion.Pendiente).ToList())
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await GenerarAsync(escena, token);
        }
    }

    /// <summary>Vuelve a generar una escena en estado error. Si no está en error, no hace nada.</summary>
    public Task ReintentarAsync(EscenaEnSesion escena) =>
        escena.Estado == EstadoGeneracion.Error && Escenas.Contains(escena)
            ? GenerarAsync(escena, _cancelacion.Token)
            : Task.CompletedTask;

    public void Dispose()
    {
        _cancelacion.Cancel();
        _cancelacion.Dispose();
    }

    private async Task GenerarAsync(EscenaEnSesion escena, CancellationToken token)
    {
        escena.Estado = EstadoGeneracion.Generando;
        escena.Prompt = null;
        escena.Fallos = [];

        ResultadoGeneracion resultado;
        try
        {
            resultado = await generador.GenerarAsync(
                escena.Escena,
                escena.Personajes.EnEscena,
                token,
                intento =>
                {
                    escena.Intento = intento;
                    Cambio?.Invoke();
                });
        }
        catch (OperationCanceledException)
        {
            // Se cargó otro guion o se cerró la sesión: esta escena ya no se muestra.
            return;
        }

        escena.Fallos = resultado.Fallos;
        escena.Estado = EstadoGeneracion.Error;
        if (resultado.Exito)
        {
            try
            {
                escena.Prompt = armador.Armar(escena.Escena, escena.Personajes.EnEscena, resultado.Bloques!);
                escena.Estado = EstadoGeneracion.Lista;
            }
            catch (InvalidOperationException ex)
            {
                escena.Fallos = [.. resultado.Fallos, $"No se pudo armar el prompt: {ex.Message}"];
            }
        }

        Cambio?.Invoke();
    }
}
