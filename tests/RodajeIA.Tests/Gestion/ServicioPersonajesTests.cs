using RodajeIA.Tests.Datos;
using RodajeIA.Web.Dominio;
using RodajeIA.Web.Gestion;

namespace RodajeIA.Tests.Gestion;

public sealed class ServicioPersonajesTests : IDisposable
{
    private readonly BaseDeDatosDePrueba _base = new();
    private readonly ServicioSeries _series;
    private readonly ServicioPersonajes _personajes;

    public ServicioPersonajesTests()
    {
        _series = new ServicioSeries(_base);
        _personajes = new ServicioPersonajes(_base);
    }

    public void Dispose() => _base.Dispose();

    [Fact]
    public async Task Crear_ConTodosLosCampos_GuardaExactamenteEsosValores() // AC-01e
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna());

        Assert.True(resultado.Exito);
        var guardado = await _personajes.ObtenerAsync(resultado.Id!.Value);
        Assert.Equal(serieId, guardado!.SerieId);
        Assert.Equal(
            new HojaPersonaje("Ana", "30 años", "Mujer delgada, voz suave.", "trenza larga", "chaqueta negra",
                "cicatriz en la ceja", "Irónica", "Protagonista"),
            guardado.AHoja());
    }

    [Fact]
    public async Task Listar_SerieConDosHojas_DevuelveLasDosEnOrdenDeCreacion() // AC-01f
    {
        var serieId = await CrearSerieAsync("Serie A");
        var otraSerieId = await CrearSerieAsync("Serie B");
        await _personajes.CrearAsync(serieId, DatosAna());
        await _personajes.CrearAsync(otraSerieId, DatosAna());
        await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = "José Daniel"));

        var lista = await _personajes.ListarAsync(serieId);

        Assert.Equal(["Ana", "José Daniel"], lista.Select(p => p.Nombre));
    }

    [Fact]
    public async Task Editar_Vestuario_GuardaElNuevoValor() // AC-01g
    {
        var id = await CrearAnaAsync();

        var resultado = await _personajes.EditarAsync(id, DatosAna(d => d.Vestuario = "camisa blanca"));

        Assert.True(resultado.Exito);
        Assert.Equal("camisa blanca", (await _personajes.ObtenerAsync(id))!.Vestuario);
    }

    [Fact]
    public async Task Eliminar_HojaExistente_YaNoExisteEnLaBase() // AC-01h (las variantes llegan con RF-09)
    {
        var id = await CrearAnaAsync();

        var eliminado = await _personajes.EliminarAsync(id);

        Assert.True(eliminado);
        Assert.Null(await _personajes.ObtenerAsync(id));
    }

    [Fact]
    public async Task Eliminar_HojaInexistente_DevuelveFalse()
    {
        Assert.False(await _personajes.EliminarAsync(999));
    }

    [Fact]
    public async Task Crear_ConPeinadoVacio_NoGuardaEIndicaQueFaltaElPeinado() // AC-01i
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Peinado = ""));

        Assert.False(resultado.Exito);
        Assert.Equal("Falta el peinado.", resultado.Errores[nameof(DatosPersonaje.Peinado)]);
        Assert.Empty(await _personajes.ListarAsync(serieId));
    }

    [Fact]
    public async Task Crear_SinHeridas_SeGuarda() // AC-01j
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Heridas = ""));

        Assert.True(resultado.Exito);
        Assert.Null((await _personajes.ObtenerAsync(resultado.Id!.Value))!.Heridas);
    }

    [Fact]
    public async Task Editar_DejandoPeinadoVacio_NoGuardaYConservaElAnterior() // AC-01k
    {
        var id = await CrearAnaAsync();

        var resultado = await _personajes.EditarAsync(id, DatosAna(d =>
        {
            d.Peinado = "";
            d.Vestuario = "camisa blanca";
        }));

        Assert.False(resultado.Exito);
        Assert.Equal("Falta el peinado.", resultado.Errores[nameof(DatosPersonaje.Peinado)]);
        var guardado = await _personajes.ObtenerAsync(id);
        Assert.Equal("trenza larga", guardado!.Peinado);
        Assert.Equal("chaqueta negra", guardado.Vestuario);
    }

    [Fact]
    public async Task Crear_ConVariosCamposVacios_IndicaCadaUno()
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, new DatosPersonaje());

        Assert.Equal(
            [
                nameof(DatosPersonaje.Nombre), nameof(DatosPersonaje.Edad), nameof(DatosPersonaje.DescripcionFisica),
                nameof(DatosPersonaje.Peinado), nameof(DatosPersonaje.Vestuario), nameof(DatosPersonaje.Personalidad),
                nameof(DatosPersonaje.Rol),
            ],
            resultado.Errores.Keys);
    }

    [Fact]
    public async Task Crear_CampoSoloConEspacios_CuentaComoVacio()
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Rol = "   "));

        Assert.Equal("Falta el rol.", resultado.Errores[nameof(DatosPersonaje.Rol)]);
    }

    [Fact]
    public async Task Crear_ConEspaciosEnLosExtremos_LosQuita()
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d =>
        {
            d.Nombre = "  Ana ";
            d.Heridas = "   ";
        }));

        var guardado = await _personajes.ObtenerAsync(resultado.Id!.Value);
        Assert.Equal("Ana", guardado!.Nombre);
        Assert.Null(guardado.Heridas);
    }

    [Theory]
    [InlineData("Ana")]
    [InlineData("ANA")]
    [InlineData(" Ana ")]
    public async Task Crear_NombreRepetidoEnLaSerie_SinDistinguirMayusculas_NoGuarda(string nombre)
    {
        var serieId = await CrearSerieAsync("Serie A");
        await _personajes.CrearAsync(serieId, DatosAna());

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = nombre));

        Assert.Contains("Ya existe", resultado.Errores[nameof(DatosPersonaje.Nombre)]);
        Assert.Single(await _personajes.ListarAsync(serieId));
    }

    [Fact]
    public async Task Crear_NombreQueSoloDifiereEnMayusculas_NoGuarda()
    {
        var serieId = await CrearSerieAsync("Serie A");
        await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = "José Daniel"));

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = "José DANIEL"));

        Assert.Contains("Ya existe", resultado.Errores[nameof(DatosPersonaje.Nombre)]);
    }

    [Fact]
    public async Task Crear_MismoNombreEnOtraSerie_SeGuarda()
    {
        await _personajes.CrearAsync(await CrearSerieAsync("Serie A"), DatosAna());

        var resultado = await _personajes.CrearAsync(await CrearSerieAsync("Serie B"), DatosAna());

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task Editar_ConservandoSuPropioNombre_SeGuarda()
    {
        var id = await CrearAnaAsync();

        var resultado = await _personajes.EditarAsync(id, DatosAna(d => d.Rol = "Antagonista"));

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task Editar_ANombreDeOtroPersonaje_NoGuarda()
    {
        var serieId = await CrearSerieAsync("Serie A");
        await _personajes.CrearAsync(serieId, DatosAna());
        var joseId = (await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = "José Daniel"))).Id!.Value;

        var resultado = await _personajes.EditarAsync(joseId, DatosAna(d => d.Nombre = "ANA"));

        Assert.Contains("Ya existe", resultado.Errores[nameof(DatosPersonaje.Nombre)]);
        Assert.Equal("José Daniel", (await _personajes.ObtenerAsync(joseId))!.Nombre);
    }

    [Theory]
    [InlineData("ana")]
    [InlineData("María-José")]
    [InlineData("Ana  María")]
    [InlineData("Ana2")]
    [InlineData("Ana: la jefa")]
    public async Task Crear_NombreQueNoPuedeSerMarcadorDelGuion_NoGuarda(string nombre)
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = nombre));

        Assert.Contains("debe empezar con mayúscula", resultado.Errores[nameof(DatosPersonaje.Nombre)]);
    }

    [Theory]
    [InlineData("José Daniel")]
    [InlineData("María de la O")]
    [InlineData("Dr. Pérez")]
    [InlineData("Ñandú")]
    public async Task Crear_NombreValidoParaElGuion_SeGuarda(string nombre)
    {
        var serieId = await CrearSerieAsync("Serie A");

        var resultado = await _personajes.CrearAsync(serieId, DatosAna(d => d.Nombre = nombre));

        Assert.True(resultado.Exito, string.Join(" ", resultado.Errores.Values));
    }

    [Fact]
    public async Task Crear_EnSerieInexistente_NoGuarda()
    {
        var resultado = await _personajes.CrearAsync(999, DatosAna());

        Assert.Equal("La serie no existe.", resultado.Errores[ResultadoGuardado.General]);
    }

    [Fact]
    public async Task Editar_PersonajeInexistente_Error()
    {
        var resultado = await _personajes.EditarAsync(999, DatosAna());

        Assert.Equal("El personaje no existe.", resultado.Errores[ResultadoGuardado.General]);
    }

    [Fact]
    public async Task CrearSerie_SeListaYSePuedeObtener() // AC-01a, AC-01b
    {
        await CrearSerieAsync("Serie A");
        await CrearSerieAsync("Serie B");

        var series = await _series.ListarAsync();

        Assert.Equal(["Serie A", "Serie B"], series.Select(s => s.Nombre));
        Assert.Equal("Serie B", (await _series.ObtenerAsync(series[1].Id))!.Nombre);
    }

    [Fact]
    public async Task CrearSerie_SinNombre_NoGuarda()
    {
        var resultado = await _series.CrearAsync("  ");

        Assert.Equal("Falta el nombre.", resultado.Errores["Nombre"]);
        Assert.Empty(await _series.ListarAsync());
    }

    private async Task<int> CrearSerieAsync(string nombre) => (await _series.CrearAsync(nombre)).Id!.Value;

    private async Task<int> CrearAnaAsync()
    {
        var resultado = await _personajes.CrearAsync(await CrearSerieAsync("Serie A"), DatosAna());
        return resultado.Id!.Value;
    }

    private static DatosPersonaje DatosAna(Action<DatosPersonaje>? cambio = null)
    {
        var datos = new DatosPersonaje
        {
            Nombre = "Ana",
            Edad = "30 años",
            DescripcionFisica = "Mujer delgada, voz suave.",
            Peinado = "trenza larga",
            Vestuario = "chaqueta negra",
            Heridas = "cicatriz en la ceja",
            Personalidad = "Irónica",
            Rol = "Protagonista",
        };
        cambio?.Invoke(datos);
        return datos;
    }
}
