namespace RodajeIA.Web.Dominio;

/// <summary>Bloque generado para un clip: lista ordenada de tomas.</summary>
public sealed record Bloque(int Clip, IReadOnlyList<Toma> Tomas);

/// <summary>
/// Toma generada por la IA. Los campos técnicos salen del vocabulario cerrado (RN-02);
/// <see cref="Accion"/> es el único texto libre y <see cref="Dialogos"/> son ids de <see cref="LineaDialogo"/>.
/// </summary>
public sealed record Toma(
    string Plano,
    string Optica,
    string Iluminacion,
    string? Angulo,
    string? Movimiento,
    string Accion,
    IReadOnlyList<string> Dialogos);
