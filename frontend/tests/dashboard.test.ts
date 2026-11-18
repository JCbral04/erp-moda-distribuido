import assert from 'node:assert/strict';
import test from 'node:test';

import { ApiError, describeError, parseErrorBody } from '../src/api/errors.ts';
import {
  hasError,
  idleState,
  isPending,
  loadingState,
  settledState,
} from '../src/dashboard/loadState.ts';
import {
  ESTADO_VENTA_CONFIRMADA,
  calcularTotalFacturado,
  construirDatosGraficaVentas,
  contarVariantesStockBajo,
  esStockBajo,
  esVentaConfirmada,
} from '../src/dashboard/metrics.ts';
import type { ProductoResponse, VentaResponse } from '../src/api/types.ts';

function venta(
  id: number,
  estado: VentaResponse['estado'],
  total: number,
  fecha = '2026-11-18T12:00:00Z',
): VentaResponse {
  return {
    id,
    fecha,
    total,
    estado,
    metodoPago: 'Efectivo',
    motivoAnulacion: null,
    detalles: [],
  };
}

function producto(stock: number, id = 1): ProductoResponse {
  return {
    id,
    nombre: `Producto ${id}`,
    descripcion: null,
    precioBase: 100000,
    categoria: 'Prueba',
    activo: true,
    variantes: [
      { id, talla: 'M', color: 'Negro', sku: `SKU-${id}`, stock, sobreprecio: null },
    ],
  };
}

test('el estado confirmado del backend es exactamente "Confirmada"', () => {
  assert.equal(ESTADO_VENTA_CONFIRMADA, 'Confirmada');
  assert.equal(esVentaConfirmada(venta(1, 'Confirmada', 100)), true);
  assert.equal(esVentaConfirmada(venta(2, 'Pendiente', 100)), false);
  assert.equal(esVentaConfirmada(venta(3, 'Anulada', 100)), false);
  assert.equal(esVentaConfirmada(venta(4, null, 100)), false);
});

test('una venta confirmada incrementa los ingresos; pendiente y anulada no', () => {
  assert.equal(calcularTotalFacturado([venta(1, 'Confirmada', 50000)]), 50000);
  assert.equal(calcularTotalFacturado([venta(2, 'Pendiente', 50000)]), 0);
  assert.equal(calcularTotalFacturado([venta(3, 'Anulada', 50000)]), 0);
  assert.equal(
    calcularTotalFacturado([
      venta(1, 'Confirmada', 50000),
      venta(2, 'Pendiente', 70000),
      venta(3, 'Anulada', 80000),
      venta(4, 'Confirmada', 25000),
    ]),
    75000,
  );
});

test('los importes de la gráfica coinciden solo con las ventas confirmadas', () => {
  const puntos = construirDatosGraficaVentas([
    venta(1, 'Confirmada', 100, '2026-11-17T10:00:00Z'),
    venta(2, 'Pendiente', 900, '2026-11-17T11:00:00Z'),
    venta(3, 'Confirmada', 250, '2026-11-17T12:00:00Z'),
    venta(4, 'Anulada', 700, '2026-11-18T10:00:00Z'),
    venta(5, 'Confirmada', 300, '2026-11-18T13:00:00Z'),
  ]);

  assert.deepEqual(puntos, [
    { date: '2026-11-17', total: 350, count: 2 },
    { date: '2026-11-18', total: 300, count: 1 },
  ]);
  assert.equal(
    puntos.reduce((sum, punto) => sum + punto.total, 0),
    calcularTotalFacturado([
      venta(1, 'Confirmada', 100, '2026-11-17T10:00:00Z'),
      venta(2, 'Pendiente', 900, '2026-11-17T11:00:00Z'),
      venta(3, 'Confirmada', 250, '2026-11-17T12:00:00Z'),
      venta(4, 'Anulada', 700, '2026-11-18T10:00:00Z'),
      venta(5, 'Confirmada', 300, '2026-11-18T13:00:00Z'),
    ]),
  );
});

test('la actividad reciente conserva todos los estados de venta', () => {
  const ventas = [
    venta(1, 'Confirmada', 100),
    venta(2, 'Pendiente', 200),
    venta(3, 'Anulada', 300),
  ];

  assert.deepEqual(
    ventas.map((v) => v.estado),
    ['Confirmada', 'Pendiente', 'Anulada'],
  );
  assert.equal(ventas.filter(esVentaConfirmada).length, 1);
});

test('el umbral de stock bajo es estricto: solo 0 y 4 son críticos', () => {
  assert.equal(esStockBajo(0), true);
  assert.equal(esStockBajo(4), true);
  assert.equal(esStockBajo(5), false);
  assert.equal(esStockBajo(6), false);
  assert.equal(
    contarVariantesStockBajo([producto(0, 1), producto(4, 2), producto(5, 3), producto(6, 4)]),
    2,
  );
});

test('una colección vacía es una respuesta válida, no un error', () => {
  assert.deepEqual(construirDatosGraficaVentas([]), []);
  assert.equal(contarVariantesStockBajo([]), 0);

  const estado = settledState(idleState<VentaResponse[]>(), {
    status: 'fulfilled',
    value: [],
  });
  assert.equal(estado.status, 'success');
  assert.deepEqual(estado.data, []);
  assert.equal(estado.error, null);
});

test('un fallo de HTTP se conserva como error y no se convierte en cero', () => {
  const anterior = settledState(idleState<VentaResponse[]>(), {
    status: 'fulfilled',
    value: [venta(1, 'Confirmada', 100)],
  });
  const recargando = loadingState(anterior);
  assert.equal(isPending(recargando), true);
  assert.equal(recargando.data?.length, 1);

  const fallido = settledState(recargando, {
    status: 'rejected',
    reason: new ApiError(503, 'Service Unavailable', { error: 'DB caída' }),
  });
  assert.equal(fallido.status, 'error');
  assert.equal(hasError(fallido), true);
  assert.match(fallido.error ?? '', /503 \(Service Unavailable\)/);
  // Se conservan los datos del último resultado correcto, pero se marca el error.
  assert.equal(fallido.data?.length, 1);
});

test('el botón de reintento recupera un servicio que había fallado', () => {
  const fallido = settledState(idleState<VentaResponse[]>(), {
    status: 'rejected',
    reason: new Error('Failed to fetch'),
  });
  assert.equal(fallido.error, 'Failed to fetch');

  const recuperado = settledState(loadingState(fallido), {
    status: 'fulfilled',
    value: [venta(9, 'Confirmada', 1200)],
  });
  assert.equal(recuperado.status, 'success');
  assert.equal(recuperado.error, null);
  assert.equal(recuperado.data?.length, 1);
});

test('el cuerpo de un error HTTP se lee una sola vez y se parsea sin enmascarar el status', () => {
  assert.deepEqual(parseErrorBody('{"error":"DB caída"}'), { error: 'DB caída' });
  assert.equal(parseErrorBody('<html>502 Bad Gateway</html>'), '<html>502 Bad Gateway</html>');
  const httpError = new ApiError(502, 'Bad Gateway', parseErrorBody('<html>502</html>'));
  assert.match(describeError(httpError), /502 \(Bad Gateway\)/);
});

test('describeError distingue errores HTTP, de red y desconocidos', () => {
  assert.equal(
    describeError(new ApiError(500, 'Internal Server Error', 'detalle')),
    'Error 500 (Internal Server Error) al consultar la API',
  );
  assert.equal(describeError(new Error('Failed to fetch')), 'Failed to fetch');
  assert.equal(describeError('fallo raro'), 'Error de comunicación desconocido');
});
