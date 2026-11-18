import React, { useState, useEffect, useCallback, useMemo } from 'react';
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
import { SalesAreaChart } from '../components/charts/SalesAreaChart';
import { SparkChart } from '../components/charts/SparkChart';
import { Card, CardHeader, CardTitle, CardContent } from '../components/ui/Card';
import { Skeleton } from '../components/ui/Skeleton';
import { EmptyState } from '../components/ui/EmptyState';
import { Button } from '../components/ui/Button';

export const Dashboard: React.FC = () => {
  const [resumen, setResumen] = useState<VentasResumen | null>(null);
  const [ventas, setVentas] = useState<VentaResponse[]>([]);
  const [productos, setProductos] = useState<ProductoResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date>(() => new Date());

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [resumenData, ventasData, productosData] = await Promise.all([
        ventasApi.getResumen().catch(() => null),
        ventasApi.getAll().catch(() => []),
        productosApi.getAll().catch(() => []),
      ]);
      setResumen(resumenData);
      setVentas(ventasData || []);
      setProductos(productosData || []);
      setLastUpdated(new Date());
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Error al conectar con los servicios');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let ignore = false;
    const load = async () => {
      try {
        const [resumenData, ventasData, productosData] = await Promise.all([
          ventasApi.getResumen().catch(() => null),
          ventasApi.getAll().catch(() => []),
          productosApi.getAll().catch(() => []),
        ]);
        if (!ignore) {
          setResumen(resumenData);
          setVentas(ventasData || []);
          setProductos(productosData || []);
          setLastUpdated(new Date());
          setLoading(false);
        }
      } catch (err) {
        if (!ignore) {
          setError(err instanceof Error ? err.message : 'Error al conectar con los servicios');
          setLoading(false);
        }
      }
    };

    void load();
    return () => {
      ignore = true;
    };
  }, []);

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

  // Computed Metrics
  const totalIngresos = resumen?.totalVentasConfirmadas ?? 0;
  const cantidadVentas = resumen?.cantidadVentas ?? ventas.length;
  const ticketPromedio =
    resumen && resumen.ventasConfirmadas > 0
      ? resumen.totalVentasConfirmadas / resumen.ventasConfirmadas
      : 0;

  // Stock Alerts count: variants with stock <= 5
  const stockCriticoCount = useMemo(() => {
    return productos.reduce((acc, prod) => {
      const lowVariants = (prod.variantes || []).filter((v) => v.stock <= 5);
      return acc + lowVariants.length;
    }, 0);
  }, [productos]);

  // Aggregated Sales Data for Recharts AreaChart
  const salesChartData = useMemo(() => {
    if (!ventas || ventas.length === 0) return [];

    const grouped = new Map<string, { total: number; count: number }>();
    ventas.forEach((v) => {
      const dateKey = v.fecha ? v.fecha.split('T')[0] : 'Hoy';
      const existing = grouped.get(dateKey) || { total: 0, count: 0 };
      grouped.set(dateKey, {
        total: existing.total + (v.total || 0),
        count: existing.count + 1,
      });
    });

    return Array.from(grouped.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([date, data]) => ({
        date,
        total: data.total,
        count: data.count,
      }));
  }, [ventas]);

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
              Fashion Intelligence · ERP Moda
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
            {greeting}, Administrador
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
            onClick={fetchData}
            isLoading={loading}
            aria-label="Actualizar datos del dashboard"
            leftIcon={<RefreshCw size={13} />}
          >
            Sincronizar
          </Button>
        </div>
      </section>

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

            {loading ? (
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
                  {cantidadVentas}
                </span>

                {resumen && resumen.ventasConfirmadas > 0 && (
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

            {loading ? (
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
                  ${totalIngresos.toLocaleString('es-CO')}
                </span>

                {sparkData && <SparkChart data={sparkData} color={colors.accent.champagne} />}
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

            {loading ? (
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
                  ${Math.round(ticketPromedio).toLocaleString('es-CO')}
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

            {loading ? (
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
                  {productos.filter((p) => p.activo).length}
                </span>

                <span
                  style={{
                    fontSize: '11px',
                    color: colors.text.tertiary,
                  }}
                >
                  {productos.length} registrados
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
                  color: stockCriticoCount > 0 ? colors.accent.warning : colors.text.secondary,
                }}
                aria-hidden="true"
              />
            </div>

            {loading ? (
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
                    color: stockCriticoCount > 0 ? colors.accent.warning : colors.text.primary,
                    lineHeight: 1,
                  }}
                  className="tabular-nums"
                >
                  {stockCriticoCount}
                </span>

                <span
                  style={{
                    fontSize: '11px',
                    fontWeight: 500,
                    color: stockCriticoCount > 0 ? colors.accent.warning : colors.accent.success,
                    backgroundColor:
                      stockCriticoCount > 0
                        ? 'rgba(212, 160, 53, 0.1)'
                        : 'rgba(82, 192, 122, 0.1)',
                    padding: '2px 6px',
                    borderRadius: radius.sm,
                    fontFamily: '"Geist Mono", monospace',
                  }}
                >
                  {stockCriticoCount > 0 ? 'Crítico (≤5)' : 'Óptimo'}
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
                Tendencia Comercial
              </span>
            </div>
          </CardHeader>

          <CardContent>
            {error ? (
              <EmptyState
                title="Conexión no disponible"
                description={error}
                actionLabel="Reintentar"
                onAction={fetchData}
              />
            ) : (
              <SalesAreaChart data={salesChartData} isLoading={loading} height={290} />
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
            {loading ? (
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
                            color: totalStock <= 5 ? colors.accent.warning : colors.accent.success,
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
            {loading ? (
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
                  const isConfirmada = v.estado === 'Confirmada';
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
