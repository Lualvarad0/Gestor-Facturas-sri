using System.Net;
using System.Net.Http.Json;
using Back.Dtos;

namespace Back.Tests;

public partial class ImportacionSriTests
{
    [Fact]
    public async Task Presupuesto_GuardaRubrosYSumaElGastoDeSusProveedores()
    {
        using var http = await ClienteAutenticadoAsync("presupuesto@test.ec");
        var oct = (int dia) => new DateOnly(2026, 10, dia);
        var farmacia = SriFixtures.Clave(oct(3), "01", SriFixtures.RucFarmacia, 31);
        Autorizar(farmacia, SriFixtures.FacturaXml(farmacia, SriFixtures.RucFarmacia, "FARMACIA DE PRUEBA S.A.", oct(3)));
        var super = SriFixtures.Clave(oct(10), "01", SriFixtures.RucSupermercado, 32);
        Autorizar(super, SriFixtures.FacturaXml(super, SriFixtures.RucSupermercado, "SUPERMERCADO DE PRUEBA", oct(10)));
        await LeerAsync<ImportacionResponse>(await http.PostAsJsonAsync("/api/comprobantes/importar/claves", new { texto = $"{farmacia} {super}" }));

        var guardado = await http.PutAsJsonAsync("/api/presupuesto", new
        {
            ingresoMensual = 1200.50m,
            rubros = new object[] { new { nombre = " Medicinas ", limite = 50m }, new { nombre = "Comida", limite = 300m } },
        });
        Assert.Equal(HttpStatusCode.NoContent, guardado.StatusCode);
        var inicial = await LeerAsync<PresupuestoDto>(await http.GetAsync("/api/presupuesto?anio=2026&mes=10"));
        Assert.Equal(1200.50m, inicial.IngresoMensual);
        Assert.Equal(new[] { "Medicinas", "Comida" }, inicial.Rubros.Select(r => r.Nombre));
        Assert.Equal(350m, inicial.TotalLimites);
        Assert.Equal(66m, inicial.GastoSinRubro);

        var medicinas = inicial.Rubros[0].Id;
        var comida = inicial.Rubros[1].Id;
        Assert.Equal(HttpStatusCode.NoContent, (await http.PutAsJsonAsync(
            $"/api/presupuesto/proveedores/{SriFixtures.RucFarmacia}/rubro", new { rubroId = medicinas })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await http.PutAsJsonAsync(
            $"/api/presupuesto/proveedores/{SriFixtures.RucSupermercado}/rubro", new { rubroId = comida })).StatusCode);
        var asignado = await LeerAsync<PresupuestoDto>(await http.GetAsync("/api/presupuesto?anio=2026&mes=10"));
        Assert.Equal(33m, asignado.Rubros.Single(r => r.Id == medicinas).Gastado);
        Assert.Equal(1, asignado.Rubros.Single(r => r.Id == medicinas).Proveedores);
        Assert.Equal(0m, asignado.GastoSinRubro);
        Assert.Equal(0m, (await LeerAsync<PresupuestoDto>(await http.GetAsync("/api/presupuesto?anio=2026&mes=9"))).TotalGastado);

        // Intercambiar nombres no choca con el índice único; quitar un rubro deja a su proveedor sin rubro.
        Assert.Equal(HttpStatusCode.NoContent, (await http.PutAsJsonAsync("/api/presupuesto", new
        {
            ingresoMensual = (decimal?)null,
            rubros = new object[] { new { id = medicinas, nombre = "Comida", limite = 80m }, new { nombre = "Internet", limite = 30m } },
        })).StatusCode);
        var final = await LeerAsync<PresupuestoDto>(await http.GetAsync("/api/presupuesto?anio=2026&mes=10"));
        Assert.Null(final.IngresoMensual);
        Assert.Equal(new[] { "Comida", "Internet" }, final.Rubros.Select(r => r.Nombre));
        Assert.Equal(medicinas, final.Rubros[0].Id);
        Assert.Equal(33m, final.GastoSinRubro);
        Assert.DoesNotContain(final.Asignaciones, a => a.Ruc == SriFixtures.RucSupermercado);

        var duplicado = await http.PutAsJsonAsync("/api/presupuesto", new
        {
            ingresoMensual = 100m,
            rubros = new object[] { new { nombre = "Luz", limite = 10m }, new { nombre = "luz", limite = 20m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, duplicado.StatusCode);
        var ajeno = await http.PutAsJsonAsync($"/api/presupuesto/proveedores/{SriFixtures.RucFarmacia}/rubro", new { rubroId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, ajeno.StatusCode);
    }
}
