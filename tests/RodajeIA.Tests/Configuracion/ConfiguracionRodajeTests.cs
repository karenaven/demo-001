using System.Text.Json.Nodes;

using RodajeIA.Web.Configuracion;

namespace RodajeIA.Tests.Configuracion;

public sealed class ConfiguracionRodajeTests : IDisposable
{
    private static readonly string DirectorioConfig = Path.Combine(AppContext.BaseDirectory, "config");

    private readonly string _directorioTemporal =
        Directory.CreateTempSubdirectory("rodajeia-config-").FullName;

    public void Dispose() => Directory.Delete(_directorioTemporal, recursive: true);

    [Fact]
    public void Cargar_ConfigDelRepo_TraeLasCincoCategoriasConSus29Valores()
    {
        var vocabulario = ConfiguracionRodaje.Cargar(DirectorioConfig).Vocabulario;

        Assert.Equal(Vocabulario.CategoriasEsperadas.Keys.Order(), vocabulario.Categorias.Keys.Order());
        Assert.Equal(29, vocabulario.Categorias.Values.Sum(c => c.Valores.Count));
        Assert.True(vocabulario.Categorias[Vocabulario.Plano].Obligatorio);
        Assert.True(vocabulario.Categorias[Vocabulario.Optica].Obligatorio);
        Assert.True(vocabulario.Categorias[Vocabulario.Iluminacion].Obligatorio);
        Assert.False(vocabulario.Categorias[Vocabulario.Angulo].Obligatorio);
        Assert.False(vocabulario.Categorias[Vocabulario.Movimiento].Obligatorio);
    }

    [Fact]
    public void Permite_ValorDelVocabulario_True()
    {
        var vocabulario = ConfiguracionRodaje.Cargar(DirectorioConfig).Vocabulario;

        Assert.True(vocabulario.Permite(Vocabulario.Plano, "plano medio"));
    }

    [Fact]
    public void Permite_ValorFueraDelVocabulario_False()
    {
        var vocabulario = ConfiguracionRodaje.Cargar(DirectorioConfig).Vocabulario;

        Assert.False(vocabulario.Permite(Vocabulario.Plano, "plano americano"));
        Assert.False(vocabulario.Permite(Vocabulario.Optica, "plano medio"));
        Assert.False(vocabulario.Permite("camara", "ARRI Alexa Mini LF"));
    }

    [Fact]
    public void Cargar_ConfigDelRepo_MoldeTieneLasSeisSeccionesEnOrden()
    {
        var molde = ConfiguracionRodaje.Cargar(DirectorioConfig).Molde;

        Assert.Equal(MoldePrompt.SeccionesEsperadas, molde.Secciones.Select(s => s.Id));
    }

    [Fact]
    public void Cargar_ConfigDelRepo_EstiloIncluyeLaCamaraFija()
    {
        var estilo = ConfiguracionRodaje.Cargar(DirectorioConfig).Estilo;

        Assert.Contains("ARRI Alexa Mini LF", estilo);
    }

    [Fact]
    public void Cargar_VocabularioConValorRepetido_Falla()
    {
        CopiarConfig();
        ModificarVocabulario(v => v["categorias"]!["optica"]!["valores"]!.AsArray().Add("35mm"));

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("repite el valor '35mm'", ex.Message);
    }

    [Fact]
    public void Cargar_VocabularioSinCategoriaObligatoria_Falla()
    {
        CopiarConfig();
        ModificarVocabulario(v => v["categorias"]!.AsObject().Remove("plano"));

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("falta la categoría 'plano'", ex.Message);
    }

    [Fact]
    public void Cargar_VocabularioConCategoriaExtra_Falla()
    {
        CopiarConfig();
        ModificarVocabulario(v => v["categorias"]!["camara"] = new JsonObject
        {
            ["obligatorio"] = false,
            ["valores"] = new JsonArray("ARRI Alexa Mini LF"),
        });

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("camara", ex.Message);
    }

    [Fact]
    public void Cargar_VocabularioConObligatoriedadCambiada_Falla()
    {
        CopiarConfig();
        ModificarVocabulario(v => v["categorias"]!["angulo"]!["obligatorio"] = true);

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("'angulo'", ex.Message);
    }

    [Fact]
    public void Cargar_EstiloVacio_Falla()
    {
        CopiarConfig();
        File.WriteAllText(Path.Combine(_directorioTemporal, ConfiguracionRodaje.ArchivoEstilo), "  \n");

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("estilo global está vacío", ex.Message);
    }

    [Theory]
    [InlineData("{{escena}}")]
    [InlineData("{{personajes}}")]
    public void Cargar_InstruccionSinMarca_Falla(string marca)
    {
        CopiarConfig();
        var ruta = Path.Combine(_directorioTemporal, ConfiguracionRodaje.ArchivoInstruccion);
        File.WriteAllText(ruta, File.ReadAllText(ruta).Replace(marca, ""));

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains(marca, ex.Message);
    }

    [Fact]
    public void Cargar_MoldeSinSeccionDeAudio_Falla()
    {
        CopiarConfig();
        var ruta = Path.Combine(_directorioTemporal, ConfiguracionRodaje.ArchivoMolde);
        var molde = JsonNode.Parse(File.ReadAllText(ruta))!;
        var secciones = molde["secciones"]!.AsArray();
        secciones.Remove(secciones.Single(s => (string?)s!["id"] == "audio"));
        File.WriteAllText(ruta, molde.ToJsonString());

        var ex = Assert.Throws<InvalidOperationException>(() => ConfiguracionRodaje.Cargar(_directorioTemporal));

        Assert.Contains("las secciones deben ser", ex.Message);
    }

    private void CopiarConfig()
    {
        foreach (var archivo in Directory.GetFiles(DirectorioConfig))
        {
            File.Copy(archivo, Path.Combine(_directorioTemporal, Path.GetFileName(archivo)));
        }
    }

    private void ModificarVocabulario(Action<JsonNode> cambio)
    {
        var ruta = Path.Combine(_directorioTemporal, ConfiguracionRodaje.ArchivoVocabulario);
        var vocabulario = JsonNode.Parse(File.ReadAllText(ruta))!;
        cambio(vocabulario);
        File.WriteAllText(ruta, vocabulario.ToJsonString());
    }
}
