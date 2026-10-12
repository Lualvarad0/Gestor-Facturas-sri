using System.ComponentModel.DataAnnotations;

namespace Back.Dtos
{
    /// <param name="Gastado">Neto (las notas de crédito restan) de los proveedores del rubro en el mes consultado.</param>
    public record RubroDto(int Id, string Nombre, decimal Limite, decimal Gastado, int Proveedores);

    public record AsignacionRubroDto(string Ruc, int RubroId);

    /// <param name="GastoSinRubro">Gasto del mes de proveedores que aún no tienen rubro.</param>
    public record PresupuestoDto(
        int Anio,
        int Mes,
        decimal? IngresoMensual,
        decimal TotalLimites,
        decimal TotalGastado,
        decimal GastoSinRubro,
        List<RubroDto> Rubros,
        List<AsignacionRubroDto> Asignaciones);

    /// <param name="Id">Null para un rubro nuevo.</param>
    public record RubroRequest(
        int? Id,
        [Required, StringLength(40, MinimumLength = 2)] string Nombre,
        [Range(typeof(decimal), "0", "1000000")] decimal Limite);

    /// <summary>Reemplaza el presupuesto: los rubros que no vengan en la lista se eliminan.</summary>
    public record PresupuestoRequest(
        [Range(typeof(decimal), "0", "10000000")] decimal? IngresoMensual,
        [Required, MaxLength(30)] List<RubroRequest> Rubros);

    /// <param name="RubroId">Null quita el rubro del proveedor.</param>
    public record AsignarRubroRequest(int? RubroId);
}
