using ErpModa.Api.Inventario.Models;
using ErpModa.Api.Proveedores.Models;
using ErpModa.Api.Proveedores.OrdenesCompra.Models;
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

        // Proveedores
        public DbSet<Proveedor> Proveedores { get; set; }
        public DbSet<OrdenCompra> OrdenesCompra { get; set; }
        public DbSet<DetalleOrdenCompra> DetallesOrdenCompra { get; set; }

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
            // WithOne(m => m.Variante) referencia explícitamente la propiedad de navegación
            // declarada en MovimientoStock, evitando que EF Core cree una segunda relación
            // con columna sombra "VarianteId1".
            modelBuilder.Entity<Variante>()
                .HasMany(v => v.MovimientosStock)
                .WithOne(m => m.Variante)
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

            // Proveedores - Proveedor
            modelBuilder.Entity<Proveedor>(eb =>
            {
                eb.ToTable("proveedores");
                eb.HasKey(p => p.Id);
                eb.Property(p => p.Id).HasColumnName("id");
                eb.Property(p => p.RazonSocial).HasColumnName("razon_social").IsRequired().HasMaxLength(150);
                eb.Property(p => p.Nit).HasColumnName("nit").IsRequired().HasMaxLength(20);
                eb.Property(p => p.Contacto).HasColumnName("contacto").HasMaxLength(60);
                eb.Property(p => p.Email).HasColumnName("email").IsRequired().HasMaxLength(100);
                eb.Property(p => p.Telefono).HasColumnName("telefono").HasMaxLength(20);
                eb.Property(p => p.Direccion).HasColumnName("direccion").HasMaxLength(120);
                eb.Property(p => p.FormaPago).HasColumnName("forma_pago").HasMaxLength(50);
                eb.Property(p => p.TiempoEntrega).HasColumnName("tiempo_entrega");
                eb.Property(p => p.Estado).HasColumnName("estado").IsRequired();
            });

            // Proveedores - Orden de Compra 1:N DetalleOrdenCompra
            modelBuilder.Entity<OrdenCompra>(eb =>
            {
                eb.ToTable("ordenes_compra");
                eb.HasKey(o => o.Id);
                eb.Property(o => o.Id).HasColumnName("id");
                eb.Property(o => o.ProveedorId).HasColumnName("proveedor_id").IsRequired();
                eb.Property(o => o.Fecha).HasColumnName("fecha").IsRequired();
                eb.Property(o => o.Subtotal).HasColumnName("subtotal").IsRequired().HasPrecision(12, 2);
                eb.Property(o => o.Total).HasColumnName("total").IsRequired().HasPrecision(12, 2);
                eb.Property(o => o.Estado).HasColumnName("estado").IsRequired().HasMaxLength(20);
                eb.Property(o => o.Observaciones).HasColumnName("observaciones").HasMaxLength(500);
                eb.Property(o => o.CreadoEn).HasColumnName("creado_en").IsRequired();
                eb.Property(o => o.ActualizadoEn).HasColumnName("actualizado_en").IsRequired();

                eb.HasOne<Proveedor>()
                    .WithMany()
                    .HasForeignKey(o => o.ProveedorId)
                    .OnDelete(DeleteBehavior.Restrict);

                eb.HasMany(o => o.Detalles)
                    .WithOne()
                    .HasForeignKey(d => d.OrdenCompraId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DetalleOrdenCompra>(eb =>
            {
                eb.ToTable("detalles_orden_compra");
                eb.HasKey(d => d.Id);
                eb.Property(d => d.Id).HasColumnName("id");
                eb.Property(d => d.OrdenCompraId).HasColumnName("orden_compra_id").IsRequired();
                eb.Property(d => d.ProductoId).HasColumnName("producto_id").IsRequired(); // referencia suave a Inventario
                eb.Property(d => d.Cantidad).HasColumnName("cantidad").IsRequired();
                eb.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario").IsRequired().HasPrecision(12, 2);
                eb.Property(d => d.Subtotal).HasColumnName("subtotal").IsRequired().HasPrecision(12, 2);
            });

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
