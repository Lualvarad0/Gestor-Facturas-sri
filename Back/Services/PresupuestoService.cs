using Back.Data;
using Back.Dtos;
using Back.Helpers;
using Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Back.Services
{
    /// <summary>Ingreso mensual, rubros con límite y su gasto del mes según los proveedores asignados.</summary>
    public class PresupuestoService
    {
        private readonly AppDbContext _db;

        public PresupuestoService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<PresupuestoDto> ObtenerAsync(int usuarioId, int anio, int mes)
        {
            var ingreso = await _db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.IngresoMensual).SingleAsync();
            var rubros = await _db.Rubros.AsNoTracking().Where(r => r.UsuarioId == usuarioId)
                .OrderBy(r => r.Orden).ThenBy(r => r.Id).ToListAsync();
            var asignaciones = await _db.ProveedoresRubro.AsNoTracking().Where(p => p.UsuarioId == usuarioId)
                .Select(p => new AsignacionRubroDto(p.Ruc, p.RubroId)).ToListAsync();

            var desde = new DateOnly(anio, mes, 1);
            var hasta = desde.AddMonths(1).AddDays(-1);
            var gastos = await _db.Comprobantes.AsNoTracking()
                .Where(c => c.UsuarioId == usuarioId && c.Direccion == Direccion.Recibido && c.FechaEmision >= desde && c.FechaEmision <= hasta)
                .Select(c => new { c.RucEmisor, c.TipoComprobante, c.Total })
                .ToListAsync();
            var rubroDe = asignaciones.ToDictionary(a => a.Ruc, a => a.RubroId);
            var porRubro = gastos
                .GroupBy(g => rubroDe.TryGetValue(g.RucEmisor, out var id) ? id : 0)
                .ToDictionary(g => g.Key, g => g.Sum(c => (c.TipoComprobante == TiposComprobante.NotaCredito ? -1 : 1) * c.Total));

            var dtos = rubros.Select(r => new RubroDto(
                r.Id, r.Nombre, r.Limite, porRubro.GetValueOrDefault(r.Id), asignaciones.Count(a => a.RubroId == r.Id))).ToList();
            return new PresupuestoDto(
                anio, mes, ingreso, dtos.Sum(r => r.Limite), dtos.Sum(r => r.Gastado), porRubro.GetValueOrDefault(0), dtos, asignaciones);
        }

        public async Task GuardarAsync(int usuarioId, PresupuestoRequest request)
        {
            var nombres = request.Rubros.Select(r => r.Nombre.Trim()).ToList();
            if (nombres.Any(n => n.Length < 2))
                throw new BusinessException("Cada rubro necesita un nombre de al menos 2 caracteres.");
            if (nombres.Distinct(StringComparer.OrdinalIgnoreCase).Count() != nombres.Count)
                throw new BusinessException("Hay rubros con el mismo nombre. Usa un nombre distinto para cada uno.");

            var ids = request.Rubros.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToList();
            if (ids.Distinct().Count() != ids.Count)
                throw new BusinessException("Un rubro aparece dos veces.");

            await using var transaccion = await _db.Database.BeginTransactionAsync();
            var usuario = await _db.Usuarios.SingleAsync(u => u.Id == usuarioId);
            var existentes = await _db.Rubros.Where(r => r.UsuarioId == usuarioId).ToDictionaryAsync(r => r.Id);
            if (ids.Any(id => !existentes.ContainsKey(id)))
                throw BusinessException.NotFound("Uno de los rubros ya no existe. Recarga la página.");

            // Nombres temporales en la base: permiten intercambiar nombres entre rubros sin chocar con el índice único.
            await _db.Rubros.Where(r => r.UsuarioId == usuarioId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Nombre, r => "~" + r.Id.ToString()));

            usuario.IngresoMensual = request.IngresoMensual;
            _db.Rubros.RemoveRange(existentes.Values.Where(r => !ids.Contains(r.Id)));
            for (var i = 0; i < request.Rubros.Count; i++)
            {
                var dato = request.Rubros[i];
                var rubro = dato.Id is { } id ? existentes[id] : _db.Rubros.Add(new Rubro { UsuarioId = usuarioId }).Entity;
                rubro.Nombre = nombres[i];
                rubro.Limite = Math.Round(dato.Limite, 2);
                rubro.Orden = i;
                // EF no ve el nombre temporal: se fuerza a escribir el definitivo aunque no haya cambiado.
                if (dato.Id.HasValue) _db.Entry(rubro).Property(r => r.Nombre).IsModified = true;
            }
            await _db.SaveChangesAsync();
            await transaccion.CommitAsync();
        }

        public async Task AsignarProveedorAsync(int usuarioId, string ruc, int? rubroId)
        {
            if (rubroId is { } id && !await _db.Rubros.AnyAsync(r => r.Id == id && r.UsuarioId == usuarioId))
                throw BusinessException.NotFound("Rubro no encontrado.");

            var asignacion = await _db.ProveedoresRubro.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId && p.Ruc == ruc);
            if (rubroId is null)
            {
                if (asignacion is not null) _db.ProveedoresRubro.Remove(asignacion);
            }
            else if (asignacion is null)
                _db.ProveedoresRubro.Add(new ProveedorRubro { UsuarioId = usuarioId, Ruc = ruc, RubroId = rubroId.Value });
            else
                asignacion.RubroId = rubroId.Value;
            await _db.SaveChangesAsync();
        }
    }
}
