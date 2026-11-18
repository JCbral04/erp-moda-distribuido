import { api } from './client';
import type {
  ProductoResponse,
  VentaResponse,
  VentasResumen,
  ProveedorResponse,
  OrdenCompraResponse,
} from './types';

export const productosApi = {
  getAll: () => api.get<ProductoResponse[]>('/productos'),
  getById: (id: number) => api.get<ProductoResponse>(`/productos/${id}`),
};

export const ventasApi = {
  getAll: (params?: { estado?: string; fecha?: string }) =>
    api.get<VentaResponse[]>('/ventas', params as Record<string, string>),
  getResumen: () => api.get<VentasResumen>('/ventas/resumen'),
  getById: (id: number) => api.get<VentaResponse>(`/ventas/${id}`),
};

export const proveedoresApi = {
  getAll: () => api.get<ProveedorResponse[]>('/proveedores'),
  getById: (id: number) => api.get<ProveedorResponse>(`/proveedores/${id}`),
};

export const ordenesCompraApi = {
  getAll: () => api.get<OrdenCompraResponse[]>('/ordenescompra'),
  getById: (id: number) => api.get<OrdenCompraResponse>(`/ordenescompra/${id}`),
};
