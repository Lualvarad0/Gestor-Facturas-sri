using System.ComponentModel.DataAnnotations;
using Back.Models;

namespace Back.Dtos
{
    public record PerfilDto(
        string Nombre,
        string Email,
        string? Cedula,
        string? Ruc,
        string? RazonSocial,
        RegimenTributario? Regimen,
        int CargasFamiliares,
        bool EnfermedadCatastrofica,
        bool Completo)
    {
        public static PerfilDto From(Usuario u) => new(
            u.Nombre, u.Correo, u.Cedula, u.Ruc, u.RazonSocial, u.Regimen, u.CargasFamiliares, u.EnfermedadCatastrofica,
            Completo: !string.IsNullOrEmpty(u.Ruc) && u.Regimen.HasValue);
    }

    public record PerfilRequest(
        [StringLength(20)] string? Cedula, // admite espacios o guiones; se normaliza en PerfilService
        [StringLength(20)] string? Ruc,
        [StringLength(300)] string? RazonSocial,
        [EnumDataType(typeof(RegimenTributario))] RegimenTributario? Regimen,
        [Range(0, 20)] int CargasFamiliares,
        bool EnfermedadCatastrofica);

    /// <summary>Respuesta al guardar el perfil: cuántos comprobantes quedaron como ingreso tras reclasificar.</summary>
    public record PerfilActualizadoDto(PerfilDto Perfil, int ComprobantesEmitidos, int ComprobantesRecibidos);
}
