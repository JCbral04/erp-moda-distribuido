import type { ProductoResponse, VentaResponse } from '../api/types';

/** Valor exacto del enum del backend: EstadoVenta.Confirmada.ToString(). */
export const ESTADO_VENTA_CONFIRMADA = 'Confirmada';

/** Criterio HU-002 de docs/requerimientos/analisis-dashboard.md: stock < 5 unidades. */
export const UMBRAL_STOCK_BAJO = 5;

export interface SalesChartPoint {
  date: string;
  total: number;
  count: number;
}

export const esVentaConfirmada = (venta: VentaResponse): boolean =>
  venta.estado === ESTADO_VENTA_CONFIRMADA;

/** Facturación efectiva: solo ventas confirmadas (pendientes y anuladas excluidas). */
export const calcularTotalFacturado = (ventas: VentaResponse[]): number =>
  ventas.reduce(
    (total, venta) => total + (esVentaConfirmada(venta) ? (venta.total || 0) : 0),
    0,
  );

/** Evolución de facturación por día; base de la gráfica y de la tendencia (sparkline). */
export function construirDatosGraficaVentas(ventas: VentaResponse[]): SalesChartPoint[] {
  const grouped = new Map<string, SalesChartPoint>();

  ventas.filter(esVentaConfirmada).forEach((venta) => {
    const dateKey = venta.fecha ? venta.fecha.split('T')[0] : 'Hoy';
    const existing = grouped.get(dateKey) ?? { date: dateKey, total: 0, count: 0 };
    grouped.set(dateKey, {
      date: dateKey,
      total: existing.total + (venta.total || 0),
      count: existing.count + 1,
    });
  });

  return Array.from(grouped.values()).sort((a, b) => a.date.localeCompare(b.date));
}

export const esStockBajo = (stock: number): boolean => stock < UMBRAL_STOCK_BAJO;

export function contarVariantesStockBajo(productos: ProductoResponse[]): number {
  return productos.reduce(
    (total, producto) =>
      total + (producto.variantes || []).filter((variante) => esStockBajo(variante.stock)).length,
    0,
  );
}
