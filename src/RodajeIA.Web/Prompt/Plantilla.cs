using System.Text;
using System.Text.RegularExpressions;

namespace RodajeIA.Web.Prompt;

/// <summary>
/// Aplica las plantillas de <c>config/molde-prompt.json</c>: <c>{{variable}}</c> se reemplaza literal por su valor
/// y <c>[...]</c> se omite completo si alguna variable que contiene está vacía. Una variable vacía fuera de
/// <c>[...]</c> es un campo obligatorio vacío y es un error (RN-01).
/// </summary>
public static partial class Plantilla
{
    public static string Aplicar(string plantilla, IReadOnlyDictionary<string, string?> valores)
    {
        var resultado = new StringBuilder();
        var posicion = 0;

        while (posicion < plantilla.Length)
        {
            var inicioOpcional = plantilla.IndexOf('[', posicion);
            if (inicioOpcional < 0)
            {
                resultado.Append(Reemplazar(plantilla[posicion..], valores, plantilla));
                break;
            }

            var finOpcional = plantilla.IndexOf(']', inicioOpcional);
            if (finOpcional < 0)
            {
                throw new InvalidOperationException($"Plantilla con '[' sin cerrar: {plantilla}");
            }

            resultado.Append(Reemplazar(plantilla[posicion..inicioOpcional], valores, plantilla));

            var opcional = plantilla[(inicioOpcional + 1)..finOpcional];
            if (Variable().Matches(opcional).All(v => !string.IsNullOrEmpty(Valor(v, valores, plantilla))))
            {
                resultado.Append(Reemplazar(opcional, valores, plantilla));
            }

            posicion = finOpcional + 1;
        }

        return resultado.ToString();
    }

    private static string Reemplazar(string texto, IReadOnlyDictionary<string, string?> valores, string plantilla) =>
        Variable().Replace(texto, v =>
        {
            var valor = Valor(v, valores, plantilla);
            return string.IsNullOrEmpty(valor)
                ? throw new InvalidOperationException($"El campo obligatorio '{v.Groups["nombre"].Value}' está vacío (RN-01).")
                : valor;
        });

    private static string? Valor(Match variable, IReadOnlyDictionary<string, string?> valores, string plantilla)
    {
        var nombre = variable.Groups["nombre"].Value;
        return valores.TryGetValue(nombre, out var valor)
            ? valor
            : throw new InvalidOperationException($"Variable desconocida '{nombre}' en la plantilla: {plantilla}");
    }

    [GeneratedRegex(@"\{\{(?<nombre>[\w.]+)\}\}")]
    private static partial Regex Variable();
}
