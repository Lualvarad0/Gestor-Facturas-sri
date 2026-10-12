using Back.Models;
using Microsoft.EntityFrameworkCore;

namespace Back.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<Comprobante> Comprobantes => Set<Comprobante>();
        public DbSet<ComprobanteXml> ComprobantesXml => Set<ComprobanteXml>();
        public DbSet<ProveedorCategoria> ProveedoresCategoria => Set<ProveedorCategoria>();
        public DbSet<Rubro> Rubros => Set<Rubro>();
        public DbSet<ProveedorRubro> ProveedoresRubro => Set<ProveedorRubro>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Usuario>(e =>
            {
                e.Property(u => u.Nombre).HasMaxLength(121);
                e.Property(u => u.Nombres).HasMaxLength(60);
                e.Property(u => u.Apellidos).HasMaxLength(60);
                e.Property(u => u.IngresoMensual).HasPrecision(12, 2);
                e.Property(u => u.Correo).HasMaxLength(150);
                e.Property(u => u.Rol).HasMaxLength(20);
                e.Property(u => u.ResetTokenHash).HasMaxLength(64);
                e.Property(u => u.Cedula).HasMaxLength(10);
                e.Property(u => u.Ruc).HasMaxLength(13);
                e.Property(u => u.RazonSocial).HasMaxLength(300);
                e.Property(u => u.Regimen).HasConversion<string>().HasMaxLength(30);
                e.HasIndex(u => u.Correo).IsUnique();
            });

            modelBuilder.Entity<Comprobante>(e =>
            {
                e.ToTable("comprobantes");
                e.Property(c => c.ClaveAcceso).HasMaxLength(49).IsFixedLength();
                e.Property(c => c.TipoComprobante).HasMaxLength(2).IsFixedLength();
                e.Property(c => c.NumeroComprobante).HasMaxLength(17);
                e.Property(c => c.NumeroAutorizacion).HasMaxLength(49);
                e.Property(c => c.RucEmisor).HasMaxLength(13);
                e.Property(c => c.RazonSocialEmisor).HasMaxLength(300);
                e.Property(c => c.NombreComercial).HasMaxLength(300);
                e.Property(c => c.IdentificacionComprador).HasMaxLength(20);
                e.Property(c => c.RazonSocialComprador).HasMaxLength(300);
                e.Property(c => c.DocumentoModificado).HasMaxLength(17);
                e.Property(c => c.Categoria).HasConversion<string>().HasMaxLength(20);
                e.Property(c => c.Direccion).HasConversion<string>().HasMaxLength(10);
                foreach (var monto in new[] { "Subtotal", "BaseIva0", "BaseIvaGravada", "Iva", "OtrosImpuestos", "Descuento", "Propina", "Total" })
                    e.Property(monto).HasPrecision(12, 2);
                e.Ignore(c => c.Signo);

                e.HasIndex(c => new { c.UsuarioId, c.ClaveAcceso }).IsUnique();
                e.HasIndex(c => new { c.UsuarioId, c.Direccion, c.FechaEmision });
                e.HasIndex(c => new { c.UsuarioId, c.RucEmisor });
                e.HasOne(c => c.Usuario).WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(c => c.Detalles).WithOne().HasForeignKey(d => d.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(c => c.Xml).WithOne().HasForeignKey<ComprobanteXml>(x => x.ComprobanteId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ComprobanteDetalle>(e =>
            {
                e.ToTable("comprobantes_detalle");
                e.Property(d => d.Codigo).HasMaxLength(50);
                e.Property(d => d.Descripcion).HasMaxLength(500);
                e.Property(d => d.Cantidad).HasPrecision(18, 6);
                e.Property(d => d.PrecioUnitario).HasPrecision(18, 6);
                e.Property(d => d.Descuento).HasPrecision(12, 2);
                e.Property(d => d.Subtotal).HasPrecision(12, 2);
            });

            modelBuilder.Entity<ComprobanteXml>(e =>
            {
                e.ToTable("comprobantes_xml");
                e.HasKey(x => x.ComprobanteId);
            });

            modelBuilder.Entity<ProveedorCategoria>(e =>
            {
                e.ToTable("proveedores_categoria");
                e.Property(p => p.Ruc).HasMaxLength(13);
                e.Property(p => p.Categoria).HasConversion<string>().HasMaxLength(20);
                e.HasIndex(p => new { p.UsuarioId, p.Ruc }).IsUnique();
                e.HasOne<Usuario>().WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Rubro>(e =>
            {
                e.ToTable("rubros");
                e.Property(r => r.Nombre).HasMaxLength(40);
                e.Property(r => r.Limite).HasPrecision(12, 2);
                e.HasIndex(r => new { r.UsuarioId, r.Nombre }).IsUnique();
                e.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProveedorRubro>(e =>
            {
                e.ToTable("proveedores_rubro");
                e.Property(p => p.Ruc).HasMaxLength(13);
                e.HasIndex(p => new { p.UsuarioId, p.Ruc }).IsUnique();
                e.HasOne<Usuario>().WithMany().HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Cascade);
                // Al borrar un rubro, sus proveedores quedan sin rubro.
                e.HasOne<Rubro>().WithMany().HasForeignKey(p => p.RubroId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
