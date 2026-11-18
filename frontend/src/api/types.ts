// Types matching backend DTOs exactly (camelCase from System.Text.Json)

export interface ProductoResponse {
  id: number;
  nombre: string;
  descripcion: string | null;
  precioBase: number;
  categoria: string;
  activo: boolean;
  variantes: VarianteResponse[];
}

export interface VarianteResponse {
  id: number;
  talla: string;
  color: string;
  sku: string;
  stock: number;
  sobreprecio: number | null;
}

export interface VentaResponse {
  id: number;
  fecha: string;
  total: number;
  estado: 'Pendiente' | 'Confirmada' | 'Anulada' | null;
  metodoPago: 'Efectivo' | 'Tarjeta' | 'Transferencia' | null;
  motivoAnulacion: string | null;
  detalles: DetalleVentaResponse[];
}

export interface DetalleVentaResponse {
  id: number;
  varianteId: number;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

export interface VentasResumen {
  cantidadVentas: number;
  ventasConfirmadas: number;
  ventasAnuladas: number;
  totalVentasConfirmadas: number;
}

export interface ProveedorResponse {
  id: number;
  razonSocial: string;
  nit: string;
  contacto: string | null;
  email: string;
  telefono: string | null;
  direccion: string | null;
  formaPago: string | null;
  tiempoEntrega: number | null;
  estado: 'Activo' | 'Inactivo';
  version: number;
}

export interface OrdenCompraResponse {
  id: number;
  proveedorId: number;
  proveedorRazonSocial: string | null;
  fecha: string;
  estado: string;
  subtotal: number;
  total: number;
  observaciones: string | null;
  version: number;
  detalles: DetalleOrdenCompraResponse[];
}

export interface DetalleOrdenCompraResponse {
  id: number;
  productoId: number;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}
