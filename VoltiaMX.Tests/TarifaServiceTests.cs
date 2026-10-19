using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class TarifaServiceTests : IAsyncLifetime
{
    private FabricaDePrueba _fabrica = null!;

    public async Task InitializeAsync() => _fabrica = await FabricaDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ObtenerTarifasAsync_DevuelveLasTarifasOrdenadasPorNombre()
    {
        var tarifas = await new TarifaService(_fabrica).ObtenerTarifasAsync();

        Assert.Equal(["1", "1C", "1F", "DAC", "PDBT"], tarifas.Select(t => t.Nombre));
    }
}
