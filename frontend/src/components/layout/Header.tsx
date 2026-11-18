import React, { useState, useEffect, useRef } from 'react';
import { useLocation, Link } from 'react-router-dom';
import {
  Menu,
  Search,
  Bell,
  ChevronRight,
  User,
  Settings,
  LogOut,
  X,
} from 'lucide-react';
import { colors, radius, motion, shadow } from '../../design/tokens';

export interface HeaderProps {
  onToggleSidebar?: () => void;
  notificationCount?: number;
}

const ROUTE_LABELS: Record<string, string> = {
  dashboard: 'Dashboard General',
  inventario: 'Inventario & Existencias',
  productos: 'Catálogo de Productos',
  ventas: 'Registro de Ventas',
  proveedores: 'Directorio de Proveedores',
  ordenes: 'Órdenes de Compra',
  configuracion: 'Ajustes del Sistema',
};

export const Header: React.FC<HeaderProps> = ({
  onToggleSidebar,
  notificationCount = 0,
}) => {
  const location = useLocation();
  const [isSearchOpen, setIsSearchOpen] = useState(false);
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const searchInputRef = useRef<HTMLInputElement>(null);

  // Keyboard shortcut Cmd+K or Ctrl+K
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setIsSearchOpen((prev) => !prev);
      } else if (e.key === 'Escape') {
        setIsSearchOpen(false);
        setIsUserMenuOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  // Focus search input when modal opens
  useEffect(() => {
    if (isSearchOpen) {
      setTimeout(() => {
        searchInputRef.current?.focus();
      }, 50);
    }
  }, [isSearchOpen]);

  // Click outside to close user dropdown
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (userMenuRef.current && !userMenuRef.current.contains(e.target as Node)) {
        setIsUserMenuOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Compute breadcrumbs
  const pathParts = location.pathname.split('/').filter(Boolean);
  const currentSegment = pathParts[0] || 'dashboard';
  const currentLabel = ROUTE_LABELS[currentSegment] || 'Inicio';

  return (
    <>
      <header
        style={{
          height: '56px',
          minHeight: '56px',
          position: 'sticky',
          top: 0,
          zIndex: 100,
          backgroundColor: 'rgba(17, 19, 23, 0.85)',
          backdropFilter: 'blur(12px)',
          WebkitBackdropFilter: 'blur(12px)',
          borderBottom: `1px solid ${colors.border.subtle}`,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 20px',
          gap: '16px',
        }}
      >
        {/* Left: Sidebar Toggle + Dynamic Breadcrumb */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', minWidth: 0 }}>
          <button
            type="button"
            onClick={onToggleSidebar}
            aria-label="Abrir o cerrar barra lateral"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: '32px',
              height: '32px',
              backgroundColor: 'transparent',
              border: `1px solid ${colors.border.subtle}`,
              borderRadius: radius.md,
              color: colors.text.secondary,
              cursor: 'pointer',
              transition: `background-color ${motion.duration.fast}ms ${motion.easing.standard}, color ${motion.duration.fast}ms ${motion.easing.standard}`,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.04)';
              e.currentTarget.style.color = colors.text.primary;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = 'transparent';
              e.currentTarget.style.color = colors.text.secondary;
            }}
          >
            <Menu size={16} />
          </button>

          {/* Breadcrumb nav */}
          <nav aria-label="Migas de pan" style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
            <Link
              to="/dashboard"
              style={{
                fontSize: '13px',
                color: colors.text.tertiary,
                textDecoration: 'none',
                fontWeight: 400,
                transition: `color ${motion.duration.fast}ms`,
              }}
              onMouseEnter={(e) => (e.currentTarget.style.color = colors.text.primary)}
              onMouseLeave={(e) => (e.currentTarget.style.color = colors.text.tertiary)}
            >
              Atelier
            </Link>
            <ChevronRight size={12} style={{ color: colors.text.tertiary }} aria-hidden="true" />
            <span
              style={{
                fontSize: '13px',
                fontWeight: 600,
                color: colors.text.primary,
                letterSpacing: '0.01em',
              }}
            >
              {currentLabel}
            </span>
          </nav>
        </div>

        {/* Flex Spacer */}
        <div style={{ flex: 1 }} />

        {/* Right Section: Search Trigger, Notifications, User Menu */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          {/* Global Search Trigger */}
          <button
            type="button"
            onClick={() => setIsSearchOpen(true)}
            aria-label="Buscar en la plataforma (⌘K)"
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              height: '34px',
              backgroundColor: colors.bg.surface,
              border: `1px solid ${colors.border.default}`,
              borderRadius: radius.md,
              padding: '0 10px',
              color: colors.text.secondary,
              fontSize: '12px',
              cursor: 'pointer',
              transition: `border-color ${motion.duration.fast}ms ${motion.easing.standard}, background-color ${motion.duration.fast}ms`,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.borderColor = colors.border.strong;
              e.currentTarget.style.backgroundColor = colors.bg.raised;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.borderColor = colors.border.default;
              e.currentTarget.style.backgroundColor = colors.bg.surface;
            }}
          >
            <Search size={14} style={{ color: colors.text.tertiary }} />
            <span style={{ display: 'inline-block' }}>Buscar…</span>
            <kbd
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                padding: '2px 5px',
                fontSize: '10px',
                fontWeight: 500,
                fontFamily: '"Geist Mono", monospace',
                color: colors.text.tertiary,
                backgroundColor: colors.bg.raised,
                border: `1px solid ${colors.border.subtle}`,
                borderRadius: radius.sm,
                marginLeft: '8px',
              }}
            >
              ⌘K
            </kbd>
          </button>

          {/* Notifications Trigger */}
          <button
            type="button"
            aria-label={`Notificaciones ${notificationCount > 0 ? `(${notificationCount} nuevas)` : ''}`}
            style={{
              position: 'relative',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: '34px',
              height: '34px',
              backgroundColor: 'transparent',
              border: `1px solid ${colors.border.subtle}`,
              borderRadius: radius.md,
              color: colors.text.secondary,
              cursor: 'pointer',
              transition: `background-color ${motion.duration.fast}ms, color ${motion.duration.fast}ms`,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.04)';
              e.currentTarget.style.color = colors.text.primary;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = 'transparent';
              e.currentTarget.style.color = colors.text.secondary;
            }}
          >
            <Bell size={16} />
            {notificationCount > 0 && (
              <span
                style={{
                  position: 'absolute',
                  top: '6px',
                  right: '6px',
                  width: '7px',
                  height: '7px',
                  borderRadius: '50%',
                  backgroundColor: colors.accent.champagne,
                }}
                aria-hidden="true"
              />
            )}
          </button>

          {/* User Profile Dropdown */}
          <div ref={userMenuRef} style={{ position: 'relative' }}>
            <button
              type="button"
              onClick={() => setIsUserMenuOpen((prev) => !prev)}
              aria-label="Menú de usuario"
              aria-expanded={isUserMenuOpen}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                padding: '3px 8px 3px 4px',
                backgroundColor: 'transparent',
                border: `1px solid ${colors.border.subtle}`,
                borderRadius: radius.pill,
                cursor: 'pointer',
                transition: `border-color ${motion.duration.fast}ms`,
              }}
              onMouseEnter={(e) => (e.currentTarget.style.borderColor = colors.border.strong)}
              onMouseLeave={(e) => (e.currentTarget.style.borderColor = colors.border.subtle)}
            >
              <div
                style={{
                  width: '26px',
                  height: '26px',
                  borderRadius: '50%',
                  backgroundColor: colors.bg.raised,
                  border: `1px solid ${colors.accent.champagne2}`,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  color: colors.accent.champagne,
                  fontFamily: '"Geist Mono", monospace',
                  fontSize: '11px',
                  fontWeight: 600,
                }}
              >
                AD
              </div>
              <span
                style={{
                  fontSize: '12px',
                  fontWeight: 500,
                  color: colors.text.primary,
                  display: 'inline-block',
                }}
              >
                Atelier
              </span>
            </button>

            {/* Dropdown Menu */}
            {isUserMenuOpen && (
              <div
                style={{
                  position: 'absolute',
                  right: 0,
                  top: 'calc(100% + 8px)',
                  width: '200px',
                  backgroundColor: colors.bg.overlay,
                  border: `1px solid ${colors.border.strong}`,
                  borderRadius: radius.lg,
                  boxShadow: shadow.lg,
                  padding: '6px',
                  zIndex: 200,
                  display: 'flex',
                  flexDirection: 'column',
                  gap: '2px',
                }}
              >
                <div
                  style={{
                    padding: '8px 10px',
                    borderBottom: `1px solid ${colors.border.subtle}`,
                    marginBottom: '4px',
                  }}
                >
                  <p style={{ fontSize: '12px', fontWeight: 600, color: colors.text.primary }}>
                    Alexandre Dubois
                  </p>
                  <p style={{ fontSize: '11px', color: colors.text.tertiary }}>
                    admin@ateliermoda.com
                  </p>
                </div>

                <Link
                  to="/configuracion"
                  onClick={() => setIsUserMenuOpen(false)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 10px',
                    fontSize: '13px',
                    color: colors.text.primary,
                    textDecoration: 'none',
                    borderRadius: radius.sm,
                    transition: `background-color ${motion.duration.fast}ms`,
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = colors.bg.raised)}
                  onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                >
                  <User size={14} style={{ color: colors.text.secondary }} />
                  <span>Perfil de Atelier</span>
                </Link>

                <Link
                  to="/configuracion"
                  onClick={() => setIsUserMenuOpen(false)}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 10px',
                    fontSize: '13px',
                    color: colors.text.primary,
                    textDecoration: 'none',
                    borderRadius: radius.sm,
                    transition: `background-color ${motion.duration.fast}ms`,
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = colors.bg.raised)}
                  onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                >
                  <Settings size={14} style={{ color: colors.text.secondary }} />
                  <span>Configuración</span>
                </Link>

                <div style={{ height: '1px', backgroundColor: colors.border.subtle, margin: '4px 0' }} />

                <button
                  type="button"
                  onClick={() => {
                    setIsUserMenuOpen(false);
                    // Non-destructive auth placeholder
                  }}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                    padding: '8px 10px',
                    fontSize: '13px',
                    color: colors.accent.danger,
                    backgroundColor: 'transparent',
                    border: 'none',
                    width: '100%',
                    textAlign: 'left',
                    borderRadius: radius.sm,
                    cursor: 'pointer',
                    transition: `background-color ${motion.duration.fast}ms`,
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = 'rgba(224, 82, 82, 0.08)')}
                  onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                >
                  <LogOut size={14} />
                  <span>Cerrar sesión</span>
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* Global Search Modal (Próximamente placeholder per section 7) */}
      {isSearchOpen && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Búsqueda global"
          style={{
            position: 'fixed',
            inset: 0,
            zIndex: 300,
            backgroundColor: 'rgba(9, 10, 12, 0.8)',
            backdropFilter: 'blur(6px)',
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'center',
            paddingTop: '80px',
          }}
          onClick={(e) => {
            if (e.target === e.currentTarget) setIsSearchOpen(false);
          }}
        >
          <div
            style={{
              width: '100%',
              maxWidth: '540px',
              backgroundColor: colors.bg.overlay,
              border: `1px solid ${colors.border.strong}`,
              borderRadius: radius.xl,
              boxShadow: shadow.lg,
              overflow: 'hidden',
              margin: '0 16px',
            }}
          >
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                padding: '14px 18px',
                borderBottom: `1px solid ${colors.border.subtle}`,
                gap: '12px',
              }}
            >
              <Search size={18} style={{ color: colors.text.secondary }} />
              <input
                ref={searchInputRef}
                type="search"
                placeholder="Buscar productos, variantes, clientes o facturas…"
                style={{
                  flex: 1,
                  backgroundColor: 'transparent',
                  border: 'none',
                  outline: 'none',
                  color: colors.text.primary,
                  fontSize: '15px',
                  fontFamily: '"Geist", sans-serif',
                }}
              />
              <button
                type="button"
                onClick={() => setIsSearchOpen(false)}
                aria-label="Cerrar búsqueda"
                style={{
                  background: 'transparent',
                  border: 'none',
                  color: colors.text.tertiary,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  padding: '4px',
                }}
              >
                <X size={18} />
              </button>
            </div>

            <div
              style={{
                padding: '32px 24px',
                textAlign: 'center',
                color: colors.text.secondary,
              }}
            >
              <p
                style={{
                  fontFamily: '"Instrument Serif", Georgia, serif',
                  fontSize: '20px',
                  color: colors.accent.champagne,
                  marginBottom: '8px',
                }}
              >
                Motor de Búsqueda Fashion Intelligence
              </p>
              <p style={{ fontSize: '13px', color: colors.text.secondary, maxWidth: '360px', margin: '0 auto' }}>
                Próximamente: indexación rápida de SKUs de prendas, clientes VIP y órdenes de compra con atajos de teclado.
              </p>
              <div
                style={{
                  marginTop: '16px',
                  fontSize: '11px',
                  color: colors.text.tertiary,
                  fontFamily: '"Geist Mono", monospace',
                }}
              >
                Presiona ESC para salir
              </div>
            </div>
          </div>
        </div>
      )}
    </>
  );
};
