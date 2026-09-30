using System.Text.RegularExpressions;

using RodajeIA.Web.Datos;

namespace RodajeIA.Web.Gestion;

/// <summary>Campos editables de una hoja de personaje (Anexo B). Todos obligatorios salvo <see cref="Heridas"/>.</summary>
public sealed partial class DatosPersonaje
{
    public string Nombre { get; set; } = "";

    public string Edad { get; set; } = "";

    public string DescripcionFisica { get; set; } = "";

    public string Peinado { get; set; } = "";

    public string Vestuario { get; set; } = "";

    /// <summary>Opcional: vacío significa sin heridas/marcas.</summary>
    public string Heridas { get; set; } = "";

    public string Personalidad { get; set; } = "";

    public string Rol { get; set; } = "";

    public static DatosPersonaje Desde(Personaje personaje) => new()
    {
        Nombre = personaje.Nombre,
        Edad = personaje.Edad,
        DescripcionFisica = personaje.DescripcionFisica,
        Peinado = personaje.Peinado,
        Vestuario = personaje.Vestuario,
        Heridas = personaje.Heridas ?? "",
        Personalidad = personaje.Personalidad,
        Rol = personaje.Rol,
    };

    /// <summary>Errores por campo. Un campo con solo espacios cuenta como vacío.</summary>
    public Dictionary<string, string> Validar()
    {
        var errores = new Dictionary<string, string>();
        Exigir(errores, nameof(Nombre), Nombre, "Falta el nombre.");
        Exigir(errores, nameof(Edad), Edad, "Falta la edad.");
        Exigir(errores, nameof(DescripcionFisica), DescripcionFisica, "Falta la descripción física.");
        Exigir(errores, nameof(Peinado), Peinado, "Falta el peinado.");
        Exigir(errores, nameof(Vestuario), Vestuario, "Falta el vestuario.");
        Exigir(errores, nameof(Personalidad), Personalidad, "Falta la personalidad.");
        Exigir(errores, nameof(Rol), Rol, "Falta el rol.");

        // El nombre se busca en la acción tal cual y en los marcadores de diálogo en mayúsculas (RF-05),
        // así que tiene que poder escribirse como marcador del guion.
        if (!errores.ContainsKey(nameof(Nombre)) && !NombreValido().IsMatch(Nombre.Trim()))
        {
            errores[nameof(Nombre)] =
                "El nombre debe empezar con mayúscula y tener solo letras, puntos y espacios simples (por ejemplo \"José Daniel\").";
        }

        return errores;
    }

    /// <summary>Copia los valores sin espacios en los extremos; heridas/marcas vacío se guarda como null.</summary>
    public void CopiarA(Personaje personaje)
    {
        personaje.Nombre = Nombre.Trim();
        personaje.Edad = Edad.Trim();
        personaje.DescripcionFisica = DescripcionFisica.Trim();
        personaje.Peinado = Peinado.Trim();
        personaje.Vestuario = Vestuario.Trim();
        personaje.Heridas = string.IsNullOrWhiteSpace(Heridas) ? null : Heridas.Trim();
        personaje.Personalidad = Personalidad.Trim();
        personaje.Rol = Rol.Trim();
    }

    private static void Exigir(Dictionary<string, string> errores, string campo, string? valor, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            errores[campo] = mensaje;
        }
    }

    [GeneratedRegex(@"^\p{Lu}[\p{L}.]*(?: [\p{L}.]+)*$")]
    private static partial Regex NombreValido();
}
