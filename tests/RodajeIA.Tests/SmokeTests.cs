namespace RodajeIA.Tests;

public class SmokeTests
{
    [Fact]
    public void WebAssembly_IsReferenced()
    {
        Assert.Equal("RodajeIA.Web", typeof(Program).Assembly.GetName().Name);
    }
}
