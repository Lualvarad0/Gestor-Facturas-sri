using System.ComponentModel.DataAnnotations;
using Back.Models;

namespace Back.Dtos
{
    public record UsuarioDto(int Id, string Nombre, string Email, string Rol)
    {
        public static UsuarioDto From(Usuario u) => new(u.Id, u.Nombre, u.Correo, u.Rol);
    }
}
