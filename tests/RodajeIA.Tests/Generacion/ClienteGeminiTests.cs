using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using RodajeIA.Web.Generacion;

namespace RodajeIA.Tests.Generacion;

public sealed class ClienteGeminiTests
{
    private static readonly JsonObject Schema = new() { ["type"] = "OBJECT" };

    [Fact]
    public async Task Generar_EnviaModeloKeyYSalidaEstructurada()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespuestaConTexto("{\"bloques\":[]}"));

        await Cliente(handler).GenerarAsync("Instrucción.", Schema, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Pedido!.Method);
        Assert.Equal(
            "https://generativelanguage.googleapis.com/v1beta/models/modelo-prueba:generateContent",
            handler.Pedido.RequestUri!.ToString());
        Assert.Equal("clave-prueba", Assert.Single(handler.Pedido.Headers.GetValues("x-goog-api-key")));
        Assert.DoesNotContain("clave-prueba", handler.Pedido.RequestUri.ToString());
        var cuerpo = JsonNode.Parse(handler.Cuerpo!)!;
        Assert.Equal("Instrucción.", (string?)cuerpo["contents"]![0]!["parts"]![0]!["text"]);
        Assert.Equal("application/json", (string?)cuerpo["generationConfig"]!["responseMimeType"]);
        Assert.Equal("OBJECT", (string?)cuerpo["generationConfig"]!["responseSchema"]!["type"]);
    }

    [Fact]
    public async Task Generar_DevuelveElTextoDelCandidato()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespuestaConTexto("{\"bloques\":[]}"));

        var json = await Cliente(handler).GenerarAsync("x", Schema, CancellationToken.None);

        Assert.Equal("{\"bloques\":[]}", json);
    }

    [Fact]
    public async Task Generar_ErrorHttp_LanzaConElCodigo()
    {
        var handler = new HandlerFalso(HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"Quota exceeded\"}}");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => Cliente(handler).GenerarAsync("x", Schema, CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains("429", ex.Message);
        Assert.Contains("Quota exceeded", ex.Message);
    }

    [Fact]
    public async Task Generar_SinContenido_LanzaConElMotivo()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, "{\"candidates\":[{\"finishReason\":\"SAFETY\"}]}");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Cliente(handler).GenerarAsync("x", Schema, CancellationToken.None));

        Assert.Contains("SAFETY", ex.Message);
    }

    [Fact]
    public async Task Generar_SinApiKey_NoLlamaYExplicaComoConfigurarla()
    {
        var handler = new HandlerFalso(HttpStatusCode.OK, RespuestaConTexto("{}"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Cliente(handler, apiKey: "").GenerarAsync("x", Schema, CancellationToken.None));

        Assert.Contains("user-secrets", ex.Message);
        Assert.Null(handler.Pedido);
    }

    private static ClienteGemini Cliente(HandlerFalso handler, string apiKey = "clave-prueba") =>
        new(new HttpClient(handler), Options.Create(new OpcionesGemini { ApiKey = apiKey, Modelo = "modelo-prueba" }));

    private static string RespuestaConTexto(string texto) => new JsonObject
    {
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["content"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = texto }) },
            ["finishReason"] = "STOP",
        }),
    }.ToJsonString();

    private sealed class HandlerFalso(HttpStatusCode codigo, string respuesta) : HttpMessageHandler
    {
        public HttpRequestMessage? Pedido { get; private set; }

        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedido = request;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(codigo) { Content = new StringContent(respuesta, Encoding.UTF8, "application/json") };
        }
    }
}
