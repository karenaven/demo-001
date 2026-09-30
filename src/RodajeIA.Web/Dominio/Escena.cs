namespace RodajeIA.Web.Dominio;

public enum TipoLocacion
{
    Int,
    Ext,
}

/// <summary>Escena tal como sale del guion (Anexo A): encabezado, cuatro campos obligatorios y sus clips.</summary>
public sealed record Escena(
    int Numero,
    TipoLocacion Tipo,
    string Lugar,
    string MomentoDelDia,
    string Locacion,
    string Iluminacion,
    string PuestaEnEscena,
    string Audio,
    IReadOnlyList<Clip> Clips);
