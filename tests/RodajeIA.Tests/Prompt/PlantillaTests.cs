using RodajeIA.Web.Prompt;

namespace RodajeIA.Tests.Prompt;

public sealed class PlantillaTests
{
    [Fact]
    public void Aplicar_ReemplazaVariablesLiteralmente()
    {
        var texto = Plantilla.Aplicar("Hola, {{p.nombre}}.", new Dictionary<string, string?> { ["p.nombre"] = "Ana [la de {{x}}]" });

        Assert.Equal("Hola, Ana [la de {{x}}].", texto);
    }

    [Fact]
    public void Aplicar_OpcionalConValor_LoIncluye()
    {
        var texto = Plantilla.Aplicar("A[, {{b}}] C", new Dictionary<string, string?> { ["b"] = "B" });

        Assert.Equal("A, B C", texto);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Aplicar_OpcionalSinValor_LoOmiteCompleto(string? valor)
    {
        var texto = Plantilla.Aplicar("A[, {{b}}] C", new Dictionary<string, string?> { ["b"] = valor });

        Assert.Equal("A C", texto);
    }

    [Fact]
    public void Aplicar_OpcionalConUnaDeDosVariablesVacia_LoOmiteCompleto()
    {
        var texto = Plantilla.Aplicar("A[ {{b}} y {{c}}]", new Dictionary<string, string?> { ["b"] = "B", ["c"] = null });

        Assert.Equal("A", texto);
    }

    [Fact]
    public void Aplicar_ObligatoriaVacia_Falla()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Plantilla.Aplicar("A {{b}}", new Dictionary<string, string?> { ["b"] = "" }));

        Assert.Contains("'b' está vacío", ex.Message);
    }

    [Fact]
    public void Aplicar_VariableDesconocida_Falla()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => Plantilla.Aplicar("A [{{z}}]", new Dictionary<string, string?>()));

        Assert.Contains("desconocida 'z'", ex.Message);
    }
}
