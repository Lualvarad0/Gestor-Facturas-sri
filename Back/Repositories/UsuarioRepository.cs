using Back.Data;
using Back.Interfaces;
using Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Back.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly AppDbContext _context;

        public UsuarioRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Usuario?> GetByIdAsync(int id) =>
            _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

        public Task<Usuario?> GetByCorreoAsync(string correo) =>
            _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);

        public Task<bool> AnyAsync() => _context.Usuarios.AnyAsync();

        public async Task AddAsync(Usuario usuario)
        {
            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
        }

        public Task UpdateAsync(Usuario usuario) => _context.SaveChangesAsync();
    }
}
