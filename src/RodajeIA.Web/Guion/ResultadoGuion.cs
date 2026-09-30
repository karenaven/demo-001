using RodajeIA.Web.Dominio;

namespace RodajeIA.Web.Guion;

/// <summary>Error de validación del guion, con el número de línea (base 1) donde ocurre (RF-03b).</summary>
public sealed record ErrorGuion(int Linea, string Motivo);

/// <summary>
/// Resultado de procesar un guion: o todas sus escenas, o la lista de errores. Nunca las dos cosas:
/// un guion inválido se rechaza completo (RF-03c).
/// </summary>
public sealed class ResultadoGuion
{
    private ResultadoGuion(IReadOnlyList<Escena> escenas, IReadOnlyList<ErrorGuion> errores)
    {
        Escenas = escenas;
        Errores = errores;
    }

    public IReadOnlyList<Escena> Escenas { get; }

    public IReadOnlyList<ErrorGuion> Errores { get; }

    public bool EsValido => Errores.Count == 0;

    internal static ResultadoGuion Valido(IReadOnlyList<Escena> escenas) => new(escenas, []);

    internal static ResultadoGuion Invalido(IReadOnlyList<ErrorGuion> errores) => new([], errores);
}
