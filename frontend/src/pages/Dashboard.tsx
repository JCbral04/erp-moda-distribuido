import React, { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  TrendingUp,
  DollarSign,
  Percent,
  Shirt,
  AlertTriangle,
  Receipt,
  ArrowUpRight,
  RefreshCw,
  ShoppingBag,
} from 'lucide-react';
import { colors, radius } from '../design/tokens';
import { ventasApi, productosApi } from '../api/endpoints';
import type { VentasResumen, VentaResponse, ProductoResponse } from '../api/types';
import {
  idleState,
  loadingState,
  settledState,
  isPending,
  type ServiceState,
} from '../dashboard/loadState';
import {
  construirDatosGraficaVentas,
  contarVariantesStockBajo,
  esStockBajo,
  esVentaConfirmada,
} from '../dashboard/metrics';
import { SalesAreaChart } from '../components/charts/SalesAreaChart';
import { SparkChart } from '../components/charts/SparkChart';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Skeleton } from '../components/ui/Skeleton';
import { EmptyState } from '../components/ui/EmptyState';
import { Button } from '../components/ui/Button';

interface ServiceFailure {
  label: string;
  message: string;
}

export const Dashboard: React.FC = () => {
  // Un estado por servicio: los fallos se hacen visibles y no se disfrazan de
  // respuestas vacías ni de métricas en cero.
  const [resumenState, setResumenState] = useState<ServiceState<VentasResumen>>(idleState);
  const [ventasState, setVentasState] = useState<ServiceState<VentaResponse[]>>(idleState);
  const [productosState, setProductosState] = useState<ServiceState<ProductoResponse[]>>(idleState);
  const [isInitialLoad, setIsInitialLoad] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<Date>(() => new Date());
  const requestIdRef = useRef(0);

  const fetchData = useCallback(async (fromUserAction = false) => {
    const requestId = ++requestIdRef.current;
    if (fromUserAction) setIsRefreshing(true);
    setResumenState(loadingState);
    setVentasState(loadingState);
    setProductosState(loadingState);

    const [resumenResult, ventasResult, productosResult] = await Promise.allSettled([
      ventasApi.getResumen(),
      ventasApi.getAll(),
      productosApi.getAll(),
    ]);

    // Descarta respuestas tardías de una consulta anterior (reintento/desmontaje).
    if (requestId !== requestIdRef.current) return;

    setResumenState((prev) => settledState(prev, resumenResult));
    setVentasState((prev) => settledState(prev, ventasResult));
    setProductosState((prev) => settledState(prev, productosResult));
    setIsInitialLoad(false);
    setIsRefreshing(false);
    if ([resumenResult, ventasResult, productosResult].some((r) => r.status === 'fulfilled')) {
      setLastUpdated(new Date());
    }
  }, []);

  useEffect(() => {
    void fetchData();
    return () => {
      // Invalida respuestas pendientes al desmontar o en el segundo mount de StrictMode.
      requestIdRef.current += 1;
    };
  }, [fetchData]);

  const resumen = resumenState.data;
  const ventas = useMemo(() => ventasState.data ?? [], [ventasState.data]);
  const productos = useMemo(() => productosState.data ?? [], [productosState.data]);

  const failures: ServiceFailure[] = [];
  if (resumenState.status === 'error') {
    failures.push({ label: 'Resumen de ventas', message: resumenState.error ?? 'Sin detalle' });
  }
  if (ventasState.status === 'error') {
    failures.push({ label: 'Ventas', message: ventasState.error ?? 'Sin detalle' });
  }
  if (productosState.status === 'error') {
    failures.push({ label: 'Productos', message: productosState.error ?? 'Sin detalle' });
  }
  const resumenUnavailable = resumenState.status === 'error';
  const ventasUnavailable = ventasState.status === 'error';
  const productosUnavailable = productosState.status === 'error';

  // Contextual Greeting based on lastUpdated time
  const currentHour = lastUpdated.getHours();
  const greeting =
    currentHour < 12 ? 'Buenos días' : currentHour < 19 ? 'Buenas tardes' : 'Buenas noches';

  const formattedDate = useMemo(() => {
    return new Intl.DateTimeFormat('es-ES', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    }).format(lastUpdated);
  }, [lastUpdated]);

  const displayDate = formattedDate.charAt(0).toUpperCase() + formattedDate.slice(1);

  // Computed Metrics (el resumen del backend ya filtra solo ventas confirmadas)
  const totalIngresos = resumen?.totalVentasConfirmadas ?? 0;
  const cantidadVentas = resumen?.cantidadVentas ?? 0;
  const ticketPromedio =
    resumen && resumen.ventasConfirmadas > 0
      ? resumen.totalVentasConfirmadas / resumen.ventasConfirmadas
      : 0;

  // Alerta de stock bajo según HU-002: estrictamente menos de 5 unidades.
  const stockCriticoCount = useMemo(() => contarVariantesStockBajo(productos), [productos]);

  // Facturación de la gráfica: únicamente ventas confirmadas.
  const salesChartData = useMemo(() => construirDatosGraficaVentas(ventas), [ventas]);

  // Mini sparkline data if historical points exist
  const sparkData = useMemo(() => {
    if (salesChartData.length < 2) return undefined;
    return salesChartData.map((d) => ({ value: d.total }));
  }, [salesChartData]);

  // Relative Time Formatter
  const getRelativeTime = useCallback(
    (dateStr: string) => {
      try {
        const d = new Date(dateStr);
        const diffSecs = Math.floor((lastUpdated.getTime() - d.getTime()) / 1000);
        const rtf = new Intl.RelativeTimeFormat('es', { numeric: 'auto' });
        if (Math.abs(diffSecs) < 60) return 'hace un momento';
        if (Math.abs(diffSecs) < 3600) return rtf.format(-Math.floor(diffSecs / 60), 'minute');
        if (Math.abs(diffSecs) < 86400) return rtf.format(-Math.floor(diffSecs / 3600), 'hour');
        return rtf.format(-Math.floor(diffSecs / 86400), 'day');
      } catch {
        return dateStr;
      }
    },
    [lastUpdated],
  );

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        width: '100%',
      }}
    >
      {/* 8.1 Contextual Greeting */}
      <section
        style={{
          display: 'flex',
          flexWrap: 'wrap',
          alignItems: 'flex-end',
          justifyContent: 'space-between',
          gap: '16px',
          borderBottom: `1px solid ${colors.border.subtle}`,
          paddingBottom: '20px',
        }}
        aria-label="Encabezado del Dashboard"
      >
        <div>
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              marginBottom: '4px',
            }}
          >
            <span
              style={{
                fontSize: '11px',
                fontWeight: 600,
                textTransform: 'uppercase',
                letterSpacing: '0.08em',
                color: colors.accent.champagne,
              }}
            >
              Atelier Haute Couture · Fashion Intelligence
            </span>
            <span style={{ color: colors.border.strong }} aria-hidden="true">
              ·
            </span>
            <span
              style={{
                fontSize: '12px',
                color: colors.text.tertiary,
              }}
            >
              {displayDate}
            </span>
          </div>

          <h1
            style={{
              fontFamily: '"Instrument Serif", Georgia, serif',
              fontSize: '32px',
              fontWeight: 400,
              color: colors.text.primary,
              lineHeight: 1.2,
              margin: 0,
            }}
            className="text-balance"
          >
            {greeting}, Alexandre
          </h1>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <span
            style={{
              fontSize: '11px',
              color: colors.text.tertiary,
              fontFamily: '"Geist Mono", monospace',
              fontVariantNumeric: 'tabular-nums',
            }}
          >
            Act: {lastUpdated.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' })}
          </span>

          <Button
            variant="secondary"
            size="sm"
            onClick={() => void fetchData(true)}
            isLoading={isRefreshing || isInitialLoad}
            aria-label="Actualizar datos del dashboard"
            leftIcon={<RefreshCw size={13} />}
          >
            Sincronizar
          </Button>
        </div>
      </section>

      {failures.length > 0 && (
        <div
          role="alert"
          style={{
            display: 'flex',
            gap: '12px',
            alignItems: 'flex-start',
            padding: '14px 16px',
            backgroundColor: 'rgba(224, 82, 82, 0.08)',
            border: `1px solid ${colors.accent.danger}`,
            borderRadius: radius.md,
            color: colors.text.primary,
          }}
        >
          <AlertTriangle size={18} style={{ color: colors.accent.danger, flexShrink: 0 }} />
          <div style={{ fontSize: '13px' }}>
            <p style={{ fontWeight: 600, marginBottom: '4px' }}>
              No se pudo actualizar parte del dashboard
            </p>
            <ul style={{ margin: '0 0 4px 0', paddingLeft: '18px' }}>
              {failures.map((failure) => (
                <li key={failure.label}>
                  <strong>{failure.label}:</strong> {failure.message}
                </li>
              ))}
            </ul>
            <span style={{ color: colors.text.secondary }}>
              Los indicadores de los servicios que no respondieron muestran «N/D». Se conservan
              los datos de los servicios que sí respondieron. Puede reintentar con «Sincronizar».
            </span>
          </div>
        </div>
      )}

      {/* 8.2 KPI Cards — Staggered Entry with auto-fill (minmax(200px, 1fr)) */}
      <section
        aria-label="Indicadores Clave de Desempeño"
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fill, minmax(210px, 1fr))',
          gap: '16px',
        }}
      >
        {/* KPI 1: Ventas del Día */}
        <div className="animate-stagger-1">
          <Card hoverable style={{ padding: '18px 20px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '10px',
              }}
            >
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  color: colors.text.tertiary,
                }}
              >
                Ventas del Período
              </span>
              <Receipt size={16} style={{ color: colors.text.secondary }} aria-hidden="true" />
            </div>

            {isPending(resumenState) ? (
              <Skeleton width="90px" height="34px" />
            ) : (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  justifyContent: 'space-between',
                  gap: '8px',
                }}
              >
                <span
                  style={{
                    fontFamily: '"Geist Mono", monospace',
                    fontSize: '28px',
                    fontWeight: 600,
                    color: colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {resumenUnavailable ? 'N/D' : cantidadVentas}
                </span>

                {!resumenUnavailable && resumen && resumen.ventasConfirmadas > 0 && (
                  <span
                    style={{
                      fontSize: '11px',
                      fontWeight: 600,
                      color: colors.accent.success,
                      backgroundColor: 'rgba(82, 192, 122, 0.1)',
                      padding: '2px 6px',
                      borderRadius: radius.sm,
                      fontFamily: '"Geist Mono", monospace',
                    }}
                  >
                    {resumen.ventasConfirmadas} conf.
                  </span>
                )}
              </div>
            )}
          </Card>
        </div>

        {/* KPI 2: Ingresos Totales */}
        <div className="animate-stagger-2">
          <Card hoverable style={{ padding: '18px 20px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '10px',
              }}
            >
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  color: colors.text.tertiary,
                }}
              >
                Ingresos Facturados
              </span>
              <DollarSign size={16} style={{ color: colors.text.secondary }} aria-hidden="true" />
            </div>

            {isPending(resumenState) ? (
              <Skeleton width="130px" height="34px" />
            ) : (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  justifyContent: 'space-between',
                  gap: '8px',
                }}
              >
                <span
                  style={{
                    fontFamily: '"Geist Mono", monospace',
                    fontSize: '28px',
                    fontWeight: 600,
                    color: colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {resumenUnavailable ? 'N/D' : `$${totalIngresos.toLocaleString('es-CO')}`}
                </span>

                {!resumenUnavailable && sparkData && (
                  <SparkChart data={sparkData} color={colors.accent.champagne} />
                )}
              </div>
            )}
          </Card>
        </div>

        {/* KPI 3: Ticket Promedio */}
        <div className="animate-stagger-3">
          <Card hoverable style={{ padding: '18px 20px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '10px',
              }}
            >
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  color: colors.text.tertiary,
                }}
              >
                Ticket Promedio
              </span>
              <Percent size={16} style={{ color: colors.text.secondary }} aria-hidden="true" />
            </div>

            {isPending(resumenState) ? (
              <Skeleton width="110px" height="34px" />
            ) : (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  justifyContent: 'space-between',
                }}
              >
                <span
                  style={{
                    fontFamily: '"Geist Mono", monospace',
                    fontSize: '28px',
                    fontWeight: 600,
                    color: colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {resumenUnavailable
                    ? 'N/D'
                    : `$${Math.round(ticketPromedio).toLocaleString('es-CO')}`}
                </span>

                <span
                  style={{
                    fontSize: '11px',
                    color: colors.text.tertiary,
                    fontFamily: '"Geist Mono", monospace',
                  }}
                >
                  por orden
                </span>
              </div>
            )}
          </Card>
        </div>

        {/* KPI 4: Productos Disponibles */}
        <div className="animate-stagger-4">
          <Card hoverable style={{ padding: '18px 20px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '10px',
              }}
            >
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  color: colors.text.tertiary,
                }}
              >
                Productos Activos
              </span>
              <Shirt size={16} style={{ color: colors.text.secondary }} aria-hidden="true" />
            </div>

            {isPending(productosState) ? (
              <Skeleton width="70px" height="34px" />
            ) : (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  justifyContent: 'space-between',
                }}
              >
                <span
                  style={{
                    fontFamily: '"Geist Mono", monospace',
                    fontSize: '28px',
                    fontWeight: 600,
                    color: colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {productosUnavailable ? 'N/D' : productos.filter((p) => p.activo).length}
                </span>

                <span
                  style={{
                    fontSize: '11px',
                    color: colors.text.tertiary,
                  }}
                >
                  {productosUnavailable ? 'sin datos' : `${productos.length} registrados`}
                </span>
              </div>
            )}
          </Card>
        </div>

        {/* KPI 5: Alertas de Stock */}
        <div className="animate-stagger-5">
          <Card hoverable style={{ padding: '18px 20px' }}>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                marginBottom: '10px',
              }}
            >
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: '0.08em',
                  color: colors.text.tertiary,
                }}
              >
                Alertas de Stock
              </span>
              <AlertTriangle
                size={16}
                style={{
                  color:
                    !productosUnavailable && stockCriticoCount > 0
                      ? colors.accent.warning
                      : colors.text.secondary,
                }}
                aria-hidden="true"
              />
            </div>

            {isPending(productosState) ? (
              <Skeleton width="70px" height="34px" />
            ) : (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'baseline',
                  justifyContent: 'space-between',
                }}
              >
                <span
                  style={{
                    fontFamily: '"Geist Mono", monospace',
                    fontSize: '28px',
                    fontWeight: 600,
                    color:
                      !productosUnavailable && stockCriticoCount > 0
                        ? colors.accent.warning
                        : colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {productosUnavailable ? 'N/D' : stockCriticoCount}
                </span>

                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 500,
                    color: productosUnavailable
                      ? colors.text.tertiary
                      : stockCriticoCount > 0
                        ? colors.accent.warning
                        : colors.accent.success,
                    backgroundColor: productosUnavailable
                      ? 'rgba(255, 255, 255, 0.04)'
                      : stockCriticoCount > 0
                        ? 'rgba(212, 160, 53, 0.1)'
                        : 'rgba(82, 192, 122, 0.1)',
                    padding: '2px 6px',
                    borderRadius: radius.sm,
                    fontFamily: '"Geist Mono", monospace',
                  }}
                >
                  {productosUnavailable
                    ? 'Sin datos'
                    : stockCriticoCount > 0
                      ? 'Crítico (<5)'
                      : 'Óptimo'}
                </span>
              </div>
            )}
          </Card>
        </div>
      </section>

      {/* 8.3 Área de Analítica: Asimétrica 60% Gráfica / 40% Lista Secundaria */}
      <section
        aria-label="Analítica Comercial y Catálogo"
        className="animate-stagger-analytics"
        style={{
          display: 'grid',
          gridTemplateColumns: 'minmax(0, 1.5fr) minmax(0, 1fr)',
          gap: '20px',
        }}
      >
        {/* Gráfica Principal: Evolución de Ventas (60%) */}
        <Card variant="surface">
          <CardHeader>
            <div>
              <CardTitle>Evolución de Ventas</CardTitle>
              <p style={{ fontSize: '12px', color: colors.text.secondary, marginTop: '2px' }}>
                Comportamiento de facturación por período registrado
              </p>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
              <TrendingUp size={15} style={{ color: colors.accent.champagne }} aria-hidden="true" />
              <span
                style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  color: colors.accent.champagne,
                  fontFamily: '"Geist Mono", monospace',
                }}
              >
                Atelier Series
              </span>
            </div>
          </CardHeader>

          <CardContent>
            {ventasUnavailable ? (
              <EmptyState
                title="Ventas no disponibles"
                description={ventasState.error ?? 'No se pudo consultar el servicio de ventas.'}
                actionLabel="Reintentar"
                onAction={() => void fetchData(true)}
              />
            ) : (
              <SalesAreaChart
                data={salesChartData}
                isLoading={isPending(ventasState)}
                height={290}
              />
            )}
          </CardContent>
        </Card>

        {/* Lista Secundaria: Productos Destacados en Catálogo (40%) */}
        <Card variant="surface">
          <CardHeader>
            <div>
              <CardTitle>Catálogo Destacado</CardTitle>
              <p style={{ fontSize: '12px', color: colors.text.secondary, marginTop: '2px' }}>
                Prendas registradas con variantes activas
              </p>
            </div>
            <ShoppingBag size={15} style={{ color: colors.text.secondary }} aria-hidden="true" />
          </CardHeader>

          <CardContent>
            {productosUnavailable ? (
              <EmptyState
                title="Catálogo no disponible"
                description={productosState.error ?? 'No se pudo consultar el servicio de productos.'}
                actionLabel="Reintentar"
                onAction={() => void fetchData(true)}
              />
            ) : isPending(productosState) ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                {[1, 2, 3, 4].map((i) => (
                  <div
                    key={i}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'space-between',
                      padding: '10px 0',
                    }}
                  >
                    <Skeleton width="140px" height="18px" />
                    <Skeleton width="60px" height="18px" />
                  </div>
                ))}
              </div>
            ) : productos.length === 0 ? (
              <EmptyState
                title="Sin productos registrados"
                description="Aún no hay prendas cargadas en el catálogo de inventario."
              />
            ) : (
              <div
                style={{
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '8px',
                  maxHeight: '290px',
                  overflowY: 'auto',
                }}
              >
                {productos.slice(0, 6).map((prod) => {
                  const totalStock = (prod.variantes || []).reduce((sum, v) => sum + v.stock, 0);
                  return (
                    <div
                      key={prod.id}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '10px 12px',
                        backgroundColor: colors.bg.raised,
                        borderRadius: radius.md,
                        border: `1px solid ${colors.border.subtle}`,
                      }}
                    >
                      <div style={{ minWidth: 0, paddingRight: '12px' }}>
                        <p
                          style={{
                            fontSize: '13px',
                            fontWeight: 500,
                            color: colors.text.primary,
                            whiteSpace: 'nowrap',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                          }}
                        >
                          {prod.nombre}
                        </p>
                        <p
                          style={{
                            fontSize: '11px',
                            color: colors.text.tertiary,
                            textTransform: 'uppercase',
                            letterSpacing: '0.04em',
                            marginTop: '2px',
                          }}
                        >
                          {prod.categoria || 'Colección'} · {prod.variantes?.length || 0} variantes
                        </p>
                      </div>

                      <div style={{ textAlign: 'right', flexShrink: 0 }}>
                        <p
                          style={{
                            fontSize: '13px',
                            fontWeight: 600,
                            color: colors.text.primary,
                            fontFamily: '"Geist Mono", monospace',
                          }}
                          className="tabular-nums"
                        >
                          ${prod.precioBase?.toLocaleString('es-CO')}
                        </p>
                        <p
                          style={{
                            fontSize: '11px',
                            color: esStockBajo(totalStock)
                              ? colors.accent.warning
                              : colors.accent.success,
                            fontFamily: '"Geist Mono", monospace',
                            marginTop: '2px',
                          }}
                          className="tabular-nums"
                        >
                          {totalStock} en stock
                        </p>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>
      </section>

      {/* 8.4 Actividad Reciente */}
      <section aria-label="Registro de Actividad Reciente" className="animate-stagger-activity">
        <Card variant="surface">
          <CardHeader>
            <div>
              <CardTitle>Actividad Reciente</CardTitle>
              <p style={{ fontSize: '12px', color: colors.text.secondary, marginTop: '2px' }}>
                Registro cronológico de transacciones y estados de venta
              </p>
            </div>
            <ArrowUpRight size={16} style={{ color: colors.text.secondary }} aria-hidden="true" />
          </CardHeader>

          <CardContent>
            {ventasUnavailable ? (
              <EmptyState
                title="Actividad no disponible"
                description={ventasState.error ?? 'No se pudo consultar el servicio de ventas.'}
                actionLabel="Reintentar"
                onAction={() => void fetchData(true)}
              />
            ) : isPending(ventasState) ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                {[1, 2, 3].map((i) => (
                  <div
                    key={i}
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      gap: '12px',
                      padding: '12px',
                    }}
                  >
                    <Skeleton width="32px" height="32px" variant="circle" />
                    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: '6px' }}>
                      <Skeleton width="40%" height="16px" />
                      <Skeleton width="20%" height="12px" />
                    </div>
                    <Skeleton width="80px" height="20px" />
                  </div>
                ))}
              </div>
            ) : ventas.length === 0 ? (
              <EmptyState
                title="Sin actividad comercial reciente"
                description="Las ventas registradas en el terminal o tienda se desplegarán aquí en tiempo real."
              />
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                {ventas.slice(0, 5).map((v) => {
                  // La actividad reciente muestra todos los estados; la facturación
                  // (gráfica y KPIs) solo usa ventas confirmadas.
                  const isConfirmada = esVentaConfirmada(v);
                  const isAnulada = v.estado === 'Anulada';

                  return (
                    <div
                      key={v.id}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '12px 14px',
                        backgroundColor: colors.bg.raised,
                        borderRadius: radius.md,
                        border: `1px solid ${colors.border.subtle}`,
                        gap: '12px',
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', minWidth: 0 }}>
                        <div
                          style={{
                            width: '32px',
                            height: '32px',
                            borderRadius: radius.md,
                            backgroundColor: colors.bg.surface,
                            border: `1px solid ${colors.border.default}`,
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            color: isConfirmada
                              ? colors.accent.success
                              : isAnulada
                                ? colors.accent.danger
                                : colors.accent.warning,
                            flexShrink: 0,
                          }}
                          aria-hidden="true"
                        >
                          <Receipt size={16} />
                        </div>

                        <div style={{ minWidth: 0 }}>
                          <p
                            style={{
                              fontSize: '13px',
                              fontWeight: 500,
                              color: colors.text.primary,
                            }}
                          >
                            Venta #{v.id} · {v.metodoPago || 'Efectivo'}
                          </p>
                          <p
                            style={{
                              fontSize: '11px',
                              color: colors.text.tertiary,
                              marginTop: '2px',
                            }}
                          >
                            {getRelativeTime(v.fecha)} · {v.detalles?.length || 0} prendas
                          </p>
                        </div>
                      </div>

                      <div style={{ textAlign: 'right', flexShrink: 0 }}>
                        <p
                          style={{
                            fontSize: '14px',
                            fontWeight: 600,
                            color: colors.text.primary,
                            fontFamily: '"Geist Mono", monospace',
                          }}
                          className="tabular-nums"
                        >
                          ${v.total?.toLocaleString('es-CO')}
                        </p>
                        <span
                          style={{
                            display: 'inline-block',
                            fontSize: '10px',
                            fontWeight: 600,
                            textTransform: 'uppercase',
                            letterSpacing: '0.04em',
                            padding: '1px 6px',
                            borderRadius: radius.sm,
                            backgroundColor: isConfirmada
                              ? 'rgba(82, 192, 122, 0.1)'
                              : isAnulada
                                ? 'rgba(224, 82, 82, 0.1)'
                                : 'rgba(212, 160, 53, 0.1)',
                            color: isConfirmada
                              ? colors.accent.success
                              : isAnulada
                                ? colors.accent.danger
                                : colors.accent.warning,
                            marginTop: '2px',
                          }}
                        >
                          {v.estado || 'Pendiente'}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </CardContent>
        </Card>
      </section>
    </div>
  );
};

export default Dashboard;
