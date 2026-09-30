namespace RodajeIA.Web.Gestion;

/// <summary>
/// Resultado de crear o editar: el id guardado, o los errores por campo (clave = nombre de la propiedad;
/// <see cref="General"/> para errores que no son de un campo). Si hay errores no se guardó nada.
/// </summary>
public sealed record ResultadoGuardado(int? Id, IReadOnlyDictionary<string, string> Errores)
{
    public const string General = "";

    public bool Exito => Errores.Count == 0;

    public static ResultadoGuardado Guardado(int id) => new(id, new Dictionary<string, string>());

    public static ResultadoGuardado ConErrores(IReadOnlyDictionary<string, string> errores) => new(null, errores);

    public static ResultadoGuardado ConError(string campo, string mensaje) =>
        ConErrores(new Dictionary<string, string> { [campo] = mensaje });
}
