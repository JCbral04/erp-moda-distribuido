import React from 'react';
import { colors, radius, motion } from '../../design/tokens';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { Button } from './Button';

export interface Column<T> {
  key: string;
  header: string;
  render?: (item: T) => React.ReactNode;
  align?: 'left' | 'center' | 'right';
  isNumeric?: boolean;
  width?: string | number;
}

export interface TableProps<T> {
  columns: Column<T>[];
  data: T[];
  keyExtractor: (item: T) => string | number;
  emptyMessage?: string;
  currentPage?: number;
  totalItems?: number;
  pageSize?: number;
  onPageChange?: (page: number) => void;
  className?: string;
  onRowClick?: (item: T) => void;
}

export function Table<T>({
  columns,
  data,
  keyExtractor,
  emptyMessage = 'No hay registros disponibles.',
  currentPage = 1,
  totalItems,
  pageSize = 10,
  onPageChange,
  className = '',
  onRowClick,
}: TableProps<T>) {
  const effectiveTotal = totalItems !== undefined ? totalItems : data.length;
  const totalPages = Math.ceil(effectiveTotal / pageSize);
  const startItem = (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(currentPage * pageSize, effectiveTotal);

  return (
    <div
      className={`fi-table-container ${className}`}
      style={{
        width: '100%',
        backgroundColor: colors.bg.surface,
        border: `1px solid ${colors.border.subtle}`,
        borderRadius: radius.lg,
        overflow: 'hidden',
      }}
    >
      <div style={{ overflowX: 'auto', width: '100%' }}>
        <table
          style={{
            width: '100%',
            borderCollapse: 'collapse',
            textAlign: 'left',
            fontSize: '13px',
          }}
        >
          <thead>
            <tr
              style={{
                backgroundColor: colors.bg.raised,
                borderBottom: `1px solid ${colors.border.subtle}`,
              }}
            >
              {columns.map((col) => (
                <th
                  key={col.key}
                  style={{
                    padding: '12px 16px',
                    fontSize: '11px',
                    fontWeight: 600,
                    textTransform: 'uppercase',
                    letterSpacing: '0.06em',
                    color: colors.text.tertiary,
                    textAlign: col.align || (col.isNumeric ? 'right' : 'left'),
                    width: col.width,
                    whiteSpace: 'nowrap',
                  }}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.length === 0 ? (
              <tr>
                <td
                  colSpan={columns.length}
                  style={{
                    padding: '36px 16px',
                    textAlign: 'center',
                    color: colors.text.secondary,
                    fontSize: '13px',
                  }}
                >
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              data.map((item) => (
                <tr
                  key={keyExtractor(item)}
                  onClick={() => onRowClick?.(item)}
                  style={{
                    borderBottom: `1px solid ${colors.border.subtle}`,
                    cursor: onRowClick ? 'pointer' : 'default',
                    transition: `background-color ${motion.duration.fast}ms ${motion.easing.standard}`,
                  }}
                  onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = colors.bg.raised;
                  }}
                  onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = 'transparent';
                  }}
                >
                  {columns.map((col) => (
                    <td
                      key={col.key}
                      style={{
                        padding: '12px 16px',
                        color: colors.text.primary,
                        textAlign: col.align || (col.isNumeric ? 'right' : 'left'),
                        fontFamily: col.isNumeric ? '"Geist Mono", monospace' : '"Geist", sans-serif',
                        fontVariantNumeric: col.isNumeric ? 'tabular-nums' : undefined,
                        whiteSpace: 'nowrap',
                      }}
                    >
                      {col.render ? col.render(item) : String((item as Record<string, unknown>)[col.key] ?? '')}
                    </td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {onPageChange && effectiveTotal > 0 && (
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 16px',
            borderTop: `1px solid ${colors.border.subtle}`,
            fontSize: '12px',
            color: colors.text.secondary,
          }}
        >
          <span style={{ fontVariantNumeric: 'tabular-nums' }}>
            Mostrando {startItem}–{endItem} de {effectiveTotal}
          </span>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Button
              variant="ghost"
              size="sm"
              disabled={currentPage <= 1}
              onClick={() => onPageChange(currentPage - 1)}
              aria-label="Página anterior"
              leftIcon={<ChevronLeft size={16} />}
            >
              Anterior
            </Button>
            <span style={{ fontVariantNumeric: 'tabular-nums', padding: '0 6px', color: colors.text.primary }}>
              {currentPage} / {Math.max(1, totalPages)}
            </span>
            <Button
              variant="ghost"
              size="sm"
              disabled={currentPage >= totalPages}
              onClick={() => onPageChange(currentPage + 1)}
              aria-label="Página siguiente"
              rightIcon={<ChevronRight size={16} />}
            >
              Siguiente
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
