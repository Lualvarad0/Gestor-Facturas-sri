using Back.Models;

namespace Back.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<Usuario?> GetByIdAsync(int id);
        Task<Usuario?> GetByCorreoAsync(string correo);
        Task<bool> AnyAsync();
        Task AddAsync(Usuario usuario);
        Task UpdateAsync(Usuario usuario);
    }
}
