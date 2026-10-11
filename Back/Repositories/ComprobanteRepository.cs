using Back.Data;
using Back.Interfaces;
using Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Back.Repositories
{
    public class ComprobanteRepository : IComprobanteRepository
    {
        private readonly AppDbContext _context;

        public ComprobanteRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HashSet<string>> GetClavesExistentesAsync(int usuarioId, IEnumerable<string> claves)
        {
            var lista = claves.ToList();
            var existentes = await _context.Comprobantes
                .Where(c => c.UsuarioId == usuarioId && lista.Contains(c.ClaveAcceso))
                .Select(c => c.ClaveAcceso)
                .ToListAsync();
            return existentes.ToHashSet();
        }

        public Task<List<Comprobante>> GetAllAsync(int usuarioId, FiltroComprobantes filtro)
        {
            var query = _context.Comprobantes.AsNoTracking().Where(c => c.UsuarioId == usuarioId);

            if (filtro.Direccion.HasValue) query = query.Where(c => c.Direccion == filtro.Direccion.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Cliente)) query = query.Where(c => c.IdentificacionComprador == filtro.Cliente);
            if (filtro.Anio.HasValue)
            {
                var desde = new DateOnly(filtro.Anio.Value, filtro.Mes ?? 1, 1);
                var hasta = filtro.Mes.HasValue ? desde.AddMonths(1) : desde.AddYears(1);
                query = query.Where(c => c.FechaEmision >= desde && c.FechaEmision < hasta);
            }
            if (filtro.Desde.HasValue) query = query.Where(c => c.FechaEmision >= filtro.Desde.Value);
            if (filtro.Hasta.HasValue) query = query.Where(c => c.FechaEmision <= filtro.Hasta.Value);
            if (filtro.MontoMinimo.HasValue) query = query.Where(c => c.Total >= filtro.MontoMinimo.Value);
            if (filtro.MontoMaximo.HasValue) query = query.Where(c => c.Total <= filtro.MontoMaximo.Value);
            if (filtro.Categoria.HasValue) query = query.Where(c => c.Categoria == filtro.Categoria.Value);
            if (!string.IsNullOrWhiteSpace(filtro.Ruc)) query = query.Where(c => c.RucEmisor == filtro.Ruc);
            if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            {
                var patron = $"%{filtro.Busqueda.Trim()}%";
                query = query.Where(c => EF.Functions.ILike(c.RazonSocialEmisor, patron)
                                         || (c.NombreComercial != null && EF.Functions.ILike(c.NombreComercial, patron))
                                         || EF.Functions.ILike(c.RazonSocialComprador, patron)
                                         || c.NumeroComprobante.Contains(filtro.Busqueda.Trim()));
            }

            return query.OrderByDescending(c => c.FechaEmision).ThenByDescending(c => c.Id).ToListAsync();
        }

        public Task<Comprobante?> GetByIdAsync(int usuarioId, int id, bool conDetalles = false)
        {
            var query = _context.Comprobantes.Where(c => c.UsuarioId == usuarioId && c.Id == id);
            if (conDetalles) query = query.Include(c => c.Detalles.OrderBy(d => d.Id));
            return query.FirstOrDefaultAsync();
        }

        public Task<string?> GetXmlAsync(int usuarioId, int id) =>
            _context.Comprobantes
                .Where(c => c.UsuarioId == usuarioId && c.Id == id)
                .Select(c => c.Xml!.Contenido)
                .FirstOrDefaultAsync();

        public async Task AddRangeAsync(IEnumerable<Comprobante> comprobantes)
        {
            _context.Comprobantes.AddRange(comprobantes);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Comprobante comprobante)
        {
            _context.Comprobantes.Remove(comprobante);
            await _context.SaveChangesAsync();
        }

        public Task SaveChangesAsync() => _context.SaveChangesAsync();

        public Task<Dictionary<string, CategoriaGasto>> GetCategoriasProveedorAsync(int usuarioId, IEnumerable<string>? rucs = null)
        {
            var query = _context.ProveedoresCategoria.AsNoTracking().Where(p => p.UsuarioId == usuarioId);
            if (rucs is not null)
            {
                var lista = rucs.ToList();
                query = query.Where(p => lista.Contains(p.Ruc));
            }
            return query.ToDictionaryAsync(p => p.Ruc, p => p.Categoria);
        }

        /// <summary>Guarda la categoría del proveedor y la aplica a todos sus comprobantes.</summary>
        public async Task AsignarCategoriaProveedorAsync(int usuarioId, string ruc, CategoriaGasto categoria)
        {
            await using var transaccion = await _context.Database.BeginTransactionAsync();

            var regla = await _context.ProveedoresCategoria.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId && p.Ruc == ruc);
            if (regla is null)
                _context.ProveedoresCategoria.Add(new ProveedorCategoria { UsuarioId = usuarioId, Ruc = ruc, Categoria = categoria });
            else
                regla.Categoria = categoria;
            await _context.SaveChangesAsync();

            await _context.Comprobantes
                .Where(c => c.UsuarioId == usuarioId && c.RucEmisor == ruc && c.Direccion == Direccion.Recibido)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Categoria, categoria));

            await transaccion.CommitAsync();
        }

        public async Task<(int Emitidos, int Recibidos)> ReclasificarAsync(int usuarioId, string? rucUsuario)
        {
            var delUsuario = _context.Comprobantes.Where(c => c.UsuarioId == usuarioId);
            await using var transaccion = await _context.Database.BeginTransactionAsync();

            var emitidos = string.IsNullOrEmpty(rucUsuario) ? 0 : await delUsuario
                .Where(c => c.RucEmisor == rucUsuario)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Direccion, Direccion.Emitido)
                    .SetProperty(c => c.Categoria, CategoriaGasto.SinCategoria));
            var recibidos = await delUsuario
                .Where(c => rucUsuario == null || c.RucEmisor != rucUsuario)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Direccion, Direccion.Recibido));

            await transaccion.CommitAsync();
            return (emitidos, recibidos);
        }
    }
}
