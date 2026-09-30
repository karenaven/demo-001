using System.Text.Json;

namespace RodajeIA.Web.Configuracion;

/// <summary>
/// Archivos versionados en <c>config/</c>: vocabulario, molde del prompt, estilo global (RN-06)
/// e instrucción que recibe Gemini por escena (Anexo D).
/// Se cargan y validan al iniciar la app; si alguno es inválido, la app no arranca.
/// </summary>
public sealed record ConfiguracionRodaje(Vocabulario Vocabulario, MoldePrompt Molde, string Estilo, string Instruccion)
{
    public const string MarcaEscena = "{{escena}}";
    public const string MarcaPersonajes = "{{personajes}}";

    public const string ArchivoVocabulario = "vocabulario.json";
    public const string ArchivoMolde = "molde-prompt.json";
    public const string ArchivoEstilo = "estilo.txt";
    public const string ArchivoInstruccion = "instruccion-generacion.txt";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public static ConfiguracionRodaje Cargar(string directorio)
    {
        var vocabulario = LeerJson<Vocabulario>(directorio, ArchivoVocabulario);
        vocabulario.Validar(ArchivoVocabulario);

        var molde = LeerJson<MoldePrompt>(directorio, ArchivoMolde);
        molde.Validar(ArchivoMolde);

        var estilo = File.ReadAllText(Path.Combine(directorio, ArchivoEstilo)).Trim();
        if (estilo.Length == 0)
        {
            throw new InvalidOperationException($"{ArchivoEstilo}: el estilo global está vacío.");
        }

        var instruccion = File.ReadAllText(Path.Combine(directorio, ArchivoInstruccion)).Trim();
        foreach (var marca in new[] { MarcaEscena, MarcaPersonajes })
        {
            if (!instruccion.Contains(marca, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{ArchivoInstruccion}: falta la marca {marca}.");
            }
        }

        return new ConfiguracionRodaje(vocabulario, molde, estilo, instruccion);
    }

    private static T LeerJson<T>(string directorio, string archivo)
    {
        using var stream = File.OpenRead(Path.Combine(directorio, archivo));
        try
        {
            return JsonSerializer.Deserialize<T>(stream, OpcionesJson)
                ?? throw new InvalidOperationException($"{archivo}: el archivo está vacío.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{archivo}: JSON inválido ({ex.Message}).", ex);
        }
    }
}
