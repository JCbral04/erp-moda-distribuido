import React from 'react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
} from 'recharts';
import { colors, radius, shadow } from '../../design/tokens';

export interface SalesDataPoint {
  date: string;
  total: number;
  count: number;
}

export interface SalesAreaChartProps {
  data: SalesDataPoint[];
  isLoading?: boolean;
  height?: number;
}

export const SalesAreaChart: React.FC<SalesAreaChartProps> = ({
  data,
  isLoading = false,
  height = 280,
}) => {
  if (isLoading) {
    return (
      <div
        style={{
          height: `${height}px`,
          width: '100%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: 'rgba(255, 255, 255, 0.01)',
          borderRadius: radius.md,
          color: colors.text.tertiary,
          fontSize: '13px',
        }}
      >
        <span>Cargando analítica de ventas…</span>
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div
        style={{
          height: `${height}px`,
          width: '100%',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: 'rgba(255, 255, 255, 0.01)',
          borderRadius: radius.md,
          border: `1px dashed ${colors.border.subtle}`,
          padding: '24px',
          textAlign: 'center',
        }}
      >
        <span
          style={{
            color: colors.text.primary,
            fontSize: '14px',
            fontWeight: 500,
            marginBottom: '4px',
          }}
        >
          Sin datos de ventas registradas
        </span>
        <span style={{ color: colors.text.secondary, fontSize: '12px' }}>
          Conectando con el servicio de ventas para proyectar la evolución comercial…
        </span>
      </div>
    );
  }

  return (
    <div style={{ width: '100%', height: `${height}px` }}>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
          <defs>
            <linearGradient id="champagneGradient" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%" stopColor={colors.accent.champagne} stopOpacity={0.25} />
              <stop offset="95%" stopColor={colors.accent.champagne} stopOpacity={0.0} />
            </linearGradient>
          </defs>

          <CartesianGrid
            strokeDasharray="3 3"
            stroke="rgba(255, 255, 255, 0.04)"
            vertical={false}
          />

          <XAxis
            dataKey="date"
            stroke={colors.text.tertiary}
            fontSize={11}
            tickLine={false}
            axisLine={{ stroke: colors.border.subtle }}
            dy={8}
            fontFamily='"Geist Mono", monospace'
          />

          <YAxis
            stroke={colors.text.tertiary}
            fontSize={11}
            tickLine={false}
            axisLine={false}
            tickFormatter={(value) => `$${Number(value).toLocaleString('es-CO')}`}
            fontFamily='"Geist Mono", monospace'
          />

          <Tooltip
            content={({ active, payload, label }) => {
              if (active && payload && payload.length) {
                const item = payload[0].payload as SalesDataPoint;
                return (
                  <div
                    style={{
                      backgroundColor: colors.bg.overlay,
                      border: `1px solid ${colors.border.strong}`,
                      borderRadius: radius.md,
                      padding: '10px 14px',
                      boxShadow: shadow.md,
                    }}
                  >
                    <p
                      style={{
                        color: colors.text.tertiary,
                        fontSize: '11px',
                        textTransform: 'uppercase',
                        letterSpacing: '0.04em',
                        marginBottom: '4px',
                      }}
                    >
                      {label}
                    </p>
                    <p
                      style={{
                        color: colors.accent.champagne,
                        fontSize: '16px',
                        fontWeight: 600,
                        fontFamily: '"Geist Mono", monospace',
                        fontVariantNumeric: 'tabular-nums',
                        marginBottom: '2px',
                      }}
                    >
                      ${item.total.toLocaleString('es-CO')}
                    </p>
                    <p
                      style={{
                        color: colors.text.secondary,
                        fontSize: '12px',
                        fontFamily: '"Geist Mono", monospace',
                      }}
                    >
                      {item.count} {item.count === 1 ? 'transacción' : 'transacciones'}
                    </p>
                  </div>
                );
              }
              return null;
            }}
          />

          <Area
            type="monotone"
            dataKey="total"
            stroke={colors.accent.champagne}
            strokeWidth={2}
            fillOpacity={1}
            fill="url(#champagneGradient)"
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
};
