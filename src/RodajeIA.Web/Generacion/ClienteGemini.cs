using System.Net.Http.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace RodajeIA.Web.Generacion;

/// <summary>Configuración de la sección <c>Gemini</c>. La key va en user-secrets, nunca en el repo.</summary>
public sealed class OpcionesGemini
{
    public const string Seccion = "Gemini";

    public string ApiKey { get; set; } = "";

    public string Modelo { get; set; } = "";

    /// <summary>Tiempo máximo por llamada (RNF-06).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Pausa antes de cada reintento, en orden (antes del 2.º intento, antes del 3.º). Da tiempo a que se
    /// recupere un modelo saturado (503) o un límite de cuota (429). Null usa <see cref="PausasPorDefecto"/>.
    /// </summary>
    public TimeSpan[]? PausasEntreIntentos { get; set; }

    public static readonly TimeSpan[] PausasPorDefecto = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    /// <summary>Pausa antes del intento indicado (2 o más); si hay menos pausas que reintentos, repite la última.</summary>
    public TimeSpan PausaAntesDelIntento(int intento)
    {
        var pausas = PausasEntreIntentos ?? PausasPorDefecto;
        return pausas.Length == 0 ? TimeSpan.Zero : pausas[Math.Min(intento - 2, pausas.Length - 1)];
    }
}

/// <summary>Una llamada a Gemini con salida estructurada. Devuelve el JSON generado, sin validar.</summary>
public interface IClienteGemini
{
    Task<string> GenerarAsync(string instruccion, JsonObject schema, CancellationToken cancelacion);
}

public sealed class ClienteGemini(HttpClient http, IOptions<OpcionesGemini> opciones) : IClienteGemini
{
    public async Task<string> GenerarAsync(string instruccion, JsonObject schema, CancellationToken cancelacion)
    {
        var config = opciones.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            throw new InvalidOperationException(
                "Falta la API key de Gemini. Configurala con: dotnet user-secrets set \"Gemini:ApiKey\" \"<key>\" --project src/RodajeIA.Web");
        }

        var cuerpo = new JsonObject
        {
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = instruccion }),
            }),
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = schema.DeepClone(),
            },
        };

        using var pedido = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{config.Modelo}:generateContent")
        {
            Content = JsonContent.Create(cuerpo),
        };
        pedido.Headers.Add("x-goog-api-key", config.ApiKey);

        using var respuesta = await http.SendAsync(pedido, cancelacion);
        var texto = await respuesta.Content.ReadAsStringAsync(cancelacion);
        if (!respuesta.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini respondió {(int)respuesta.StatusCode}: {Recortar(texto)}", null, respuesta.StatusCode);
        }

        var candidato = JsonNode.Parse(texto)?["candidates"]?[0];
        var partes = candidato?["content"]?["parts"]?.AsArray();
        if (partes is null || partes.Count == 0)
        {
            var motivo = (string?)candidato?["finishReason"] ?? (string?)JsonNode.Parse(texto)?["promptFeedback"]?["blockReason"];
            throw new InvalidOperationException($"Gemini no devolvió contenido (motivo: {motivo ?? "desconocido"}).");
        }

        return string.Concat(partes.Select(p => (string?)p?["text"]));
    }

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500] + "…";
}
