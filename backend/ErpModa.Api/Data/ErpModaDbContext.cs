using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Ventas.Models;
using Microsoft.EntityFrameworkCore;

namespace ErpModa.Api.Data
{
    public class ErpModaDbContext : DbContext
    {
        public ErpModaDbContext(DbContextOptions<ErpModaDbContext> options)
            : base(options)
        {
        }

        // Inventario
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Variante> Variantes { get; set; }
        public DbSet<MovimientoStock> MovimientosStock { get; set; }

        // Ventas
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetalleVenta { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<ContadorFactura> ContadoresFactura { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Inventario - Producto 1:N Variantes
            modelBuilder.Entity<Producto>()
                .HasMany(p => p.Variantes)
                .WithOne(v => v.Producto)
                .HasForeignKey(v => v.ProductoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inventario - Variante 1:N MovimientosStock
            modelBuilder.Entity<Variante>()
                .HasMany(v => v.MovimientosStock)
                .WithOne()
                .HasForeignKey(m => m.VarianteId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ventas - Venta 1:N DetalleVenta
            modelBuilder.Entity<Venta>()
                .HasMany(v => v.Detalles)
                .WithOne(d => d.Venta)
                .HasForeignKey(d => d.VentaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ventas - Venta 1:1 Factura
            modelBuilder.Entity<Factura>()
                .HasOne(f => f.Venta)
                .WithOne()
                .HasForeignKey<Factura>(f => f.VentaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ventas - DetalleVenta N:1 Variante (referencia suave)
            // No se crea FK dura porque la tabla variantes pertenece a Inventario
            // y puede ejecutarse en cualquier orden. Cuando se consoliden esquemas
            // se puede endurecer como FK.

            // Configurar nombres de tablas explícitamente (por si acaso)
            modelBuilder.Entity<Producto>().ToTable("productos");
            modelBuilder.Entity<Variante>().ToTable("variantes");
            modelBuilder.Entity<MovimientoStock>().ToTable("movimientos_stock");
            modelBuilder.Entity<Venta>().ToTable("ventas");
            modelBuilder.Entity<DetalleVenta>().ToTable("detalle_venta");
            modelBuilder.Entity<Factura>().ToTable("facturas");
            modelBuilder.Entity<ContadorFactura>().ToTable("contador_factura");

            // Configurar precisión decimal para campos monetarios
            modelBuilder.Entity<Producto>()
                .Property(p => p.PrecioBase)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Variante>()
                .Property(v => v.Sobreprecio)
                .HasPrecision(12, 2);

            modelBuilder.Entity<MovimientoStock>()
                .Property(m => m.Confianza)
                .HasPrecision(5, 4);

            modelBuilder.Entity<Venta>()
                .Property(v => v.Subtotal)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Venta>()
                .Property(v => v.Impuesto)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Venta>()
                .Property(v => v.Total)
                .HasPrecision(12, 2);

            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.PrecioUnitario)
                .HasPrecision(12, 2);

            modelBuilder.Entity<DetalleVenta>()
                .Property(d => d.Subtotal)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Factura>()
                .Property(f => f.Subtotal)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Factura>()
                .Property(f => f.Impuesto)
                .HasPrecision(12, 2);

            modelBuilder.Entity<Factura>()
                .Property(f => f.Total)
                .HasPrecision(12, 2);
        }
    }
}
