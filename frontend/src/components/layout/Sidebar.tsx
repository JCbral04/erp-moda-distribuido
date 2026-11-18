import React, { useState, useEffect, useRef, useTransition } from 'react';
import { NavLink, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
  Boxes,
  Shirt,
  Receipt,
  Truck,
  FileText,
  Settings,
  ChevronLeft,
  ChevronRight,
  Sparkles,
} from 'lucide-react';
import { colors, radius, motion } from '../../design/tokens';

export interface SidebarProps {
  isMobileOpen?: boolean;
  onMobileClose?: () => void;
  isCollapsed?: boolean;
  onToggleCollapse?: () => void;
}

interface NavItemDef {
  label: string;
  path: string;
  icon: React.ComponentType<{ size?: number; className?: string; style?: React.CSSProperties }>;
  badge?: string | number;
}

interface NavGroupDef {
  title?: string;
  items: NavItemDef[];
}

const NAV_GROUPS: NavGroupDef[] = [
  {
    items: [
      {
        label: 'Dashboard',
        path: '/dashboard',
        icon: LayoutDashboard,
      },
    ],
  },
  {
    title: 'Catálogo & Stock',
    items: [
      {
        label: 'Inventario',
        path: '/inventario',
        icon: Boxes,
      },
      {
        label: 'Productos',
        path: '/productos',
        icon: Shirt,
      },
    ],
  },
  {
    title: 'Comercial & Compras',
    items: [
      {
        label: 'Ventas',
        path: '/ventas',
        icon: Receipt,
      },
      {
        label: 'Proveedores',
        path: '/proveedores',
        icon: Truck,
      },
      {
        label: 'Órdenes',
        path: '/ordenes',
        icon: FileText,
      },
    ],
  },
  {
    title: 'Sistema',
    items: [
      {
        label: 'Configuración',
        path: '/configuracion',
        icon: Settings,
      },
    ],
  },
];

export const Sidebar: React.FC<SidebarProps> = ({
  isMobileOpen = false,
  onMobileClose,
  isCollapsed: controlledCollapsed,
  onToggleCollapse,
}) => {
  const [, startTransition] = useTransition();
  const [internalCollapsed, setInternalCollapsed] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('erp_sidebar_collapsed');
      return saved ? JSON.parse(saved) : false;
    } catch {
      return false;
    }
  });

  const location = useLocation();
  const isCollapsed = controlledCollapsed !== undefined ? controlledCollapsed : internalCollapsed;

  const handleToggle = () => {
    if (onToggleCollapse) {
      onToggleCollapse();
    } else {
      startTransition(() => {
        setInternalCollapsed((prev) => {
          const next = !prev;
          try {
            localStorage.setItem('erp_sidebar_collapsed', JSON.stringify(next));
          } catch {
            // ignore localStorage error
          }
          return next;
        });
      });
    }
  };

  // Keyboard navigation across links with ArrowUp / ArrowDown
  const navRef = useRef<HTMLElement>(null);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
    const focusableLinks = navRef.current?.querySelectorAll<HTMLAnchorElement>('a.fi-sidebar-item');
    if (!focusableLinks || focusableLinks.length === 0) return;

    const list = Array.from(focusableLinks);
    const activeIndex = list.indexOf(document.activeElement as HTMLAnchorElement);

    if (e.key === 'ArrowDown') {
      e.preventDefault();
      const nextIndex = activeIndex < list.length - 1 ? activeIndex + 1 : 0;
      list[nextIndex]?.focus();
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      const prevIndex = activeIndex > 0 ? activeIndex - 1 : list.length - 1;
      list[prevIndex]?.focus();
    }
  };

  // Close mobile drawer on route change
  useEffect(() => {
    if (onMobileClose) {
      onMobileClose();
    }
  }, [location.pathname, onMobileClose]);

  const sidebarWidth = isCollapsed ? '56px' : '240px';

  return (
    <>
      {/* Mobile Backdrop Overlay */}
      {isMobileOpen && (
        <div
          onClick={onMobileClose}
          style={{
            position: 'fixed',
            inset: 0,
            backgroundColor: 'rgba(9, 10, 12, 0.75)',
            backdropFilter: 'blur(4px)',
            zIndex: 150,
          }}
          aria-hidden="true"
        />
      )}

      <aside
        id="sidebar"
        className={`fi-sidebar ${isCollapsed ? 'fi-sidebar-collapsed' : ''} ${isMobileOpen ? 'fi-sidebar-mobile-open' : ''}`}
        style={{
          width: sidebarWidth,
          minWidth: sidebarWidth,
          maxWidth: sidebarWidth,
          height: '100vh',
          position: 'sticky',
          top: 0,
          backgroundColor: colors.bg.surface,
          borderRight: `1px solid ${colors.border.subtle}`,
          display: 'flex',
          flexDirection: 'column',
          zIndex: 160,
          transition: `width 250ms ${motion.easing.standard}`,
          userSelect: 'none',
          overflow: 'hidden',
        }}
      >
        {/* Brand Header */}
        <div
          style={{
            height: '56px',
            minHeight: '56px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: isCollapsed ? 'center' : 'space-between',
            padding: isCollapsed ? '0 12px' : '0 16px',
            borderBottom: `1px solid ${colors.border.subtle}`,
            overflow: 'hidden',
          }}
        >
          {!isCollapsed && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', minWidth: 0 }}>
              <span
                style={{
                  color: colors.accent.champagne,
                  display: 'flex',
                  alignItems: 'center',
                }}
                aria-hidden="true"
              >
                <Sparkles size={16} />
              </span>
              <span
                style={{
                  fontFamily: '"Instrument Serif", Georgia, serif',
                  fontSize: '16px',
                  color: colors.accent.champagne,
                  letterSpacing: '0.02em',
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                Fashion Intelligence
              </span>
            </div>
          )}

          {isCollapsed && (
            <span
              style={{
                fontFamily: '"Instrument Serif", Georgia, serif',
                fontSize: '18px',
                color: colors.accent.champagne,
                fontWeight: 600,
              }}
              title="Fashion Intelligence"
            >
              FI
            </span>
          )}

          <button
            type="button"
            onClick={handleToggle}
            aria-label={isCollapsed ? 'Expandir barra lateral' : 'Colapsar barra lateral'}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              width: '28px',
              height: '28px',
              background: 'transparent',
              border: `1px solid ${colors.border.subtle}`,
              borderRadius: radius.sm,
              color: colors.text.secondary,
              cursor: 'pointer',
              transition: `background-color ${motion.duration.fast}ms ${motion.easing.standard}, color ${motion.duration.fast}ms ${motion.easing.standard}`,
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.05)';
              e.currentTarget.style.color = colors.text.primary;
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.backgroundColor = 'transparent';
              e.currentTarget.style.color = colors.text.secondary;
            }}
          >
            {isCollapsed ? <ChevronRight size={14} /> : <ChevronLeft size={14} />}
          </button>
        </div>

        {/* Navigation list */}
        <nav
          ref={navRef}
          aria-label="Navegación principal"
          onKeyDown={handleKeyDown}
          style={{
            flex: 1,
            overflowY: 'auto',
            overflowX: 'hidden',
            padding: isCollapsed ? '12px 6px' : '16px 8px',
            display: 'flex',
            flexDirection: 'column',
            gap: '12px',
          }}
        >
          {NAV_GROUPS.map((group, groupIndex) => (
            <div key={group.title || `group-${groupIndex}`} style={{ display: 'flex', flexDirection: 'column', gap: '2px' }}>
              {group.title && (
                <div
                  style={{
                    padding: isCollapsed ? '8px 0' : '6px 12px 4px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: isCollapsed ? 'center' : 'flex-start',
                    borderTop: groupIndex > 0 ? `1px solid ${colors.border.subtle}` : 'none',
                    marginTop: groupIndex > 0 ? '8px' : '0',
                  }}
                >
                  {!isCollapsed ? (
                    <span
                      style={{
                        fontSize: '10px',
                        fontWeight: 600,
                        textTransform: 'uppercase',
                        letterSpacing: '0.08em',
                        color: colors.text.tertiary,
                        userSelect: 'none',
                      }}
                    >
                      {group.title}
                    </span>
                  ) : (
                    <span
                      style={{
                        width: '16px',
                        height: '1px',
                        backgroundColor: colors.border.subtle,
                        display: 'block',
                      }}
                    />
                  )}
                </div>
              )}

              {group.items.map((item) => {
                const IconComponent = item.icon;
                const isActive =
                  location.pathname === item.path ||
                  (item.path !== '/dashboard' && location.pathname.startsWith(item.path));

                return (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    title={isCollapsed ? item.label : undefined}
                    aria-label={item.label}
                    className="fi-sidebar-item"
                    style={{
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: isCollapsed ? 'center' : 'flex-start',
                      height: '38px',
                      padding: isCollapsed ? '0' : '0 12px',
                      borderRadius: radius.md,
                      textDecoration: 'none',
                      color: isActive ? colors.text.primary : colors.text.secondary,
                      backgroundColor: isActive ? colors.bg.raised : 'transparent',
                      borderLeft: isActive ? `2px solid ${colors.accent.champagne}` : '2px solid transparent',
                      position: 'relative',
                      fontSize: '13px',
                      fontWeight: isActive ? 600 : 400,
                      gap: '12px',
                      transition: `background-color ${motion.duration.fast}ms ${motion.easing.standard}, color ${motion.duration.fast}ms ${motion.easing.standard}`,
                    }}
                    onMouseEnter={(e) => {
                      if (!isActive) {
                        e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.04)';
                        e.currentTarget.style.color = colors.text.primary;
                      }
                    }}
                    onMouseLeave={(e) => {
                      if (!isActive) {
                        e.currentTarget.style.backgroundColor = 'transparent';
                        e.currentTarget.style.color = colors.text.secondary;
                      }
                    }}
                  >
                    <span
                      style={{
                        display: 'inline-flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: isActive ? colors.accent.champagne : 'inherit',
                        flexShrink: 0,
                      }}
                    >
                      <IconComponent size={18} />
                    </span>

                    {!isCollapsed && (
                      <span
                        style={{
                          whiteSpace: 'nowrap',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          flex: 1,
                        }}
                      >
                        {item.label}
                      </span>
                    )}
                  </NavLink>
                );
              })}
            </div>
          ))}
        </nav>

        {/* User Profile Footer */}
        <div
          style={{
            padding: isCollapsed ? '12px 6px' : '12px 14px',
            borderTop: `1px solid ${colors.border.subtle}`,
            backgroundColor: colors.bg.surface,
            display: 'flex',
            alignItems: 'center',
            justifyContent: isCollapsed ? 'center' : 'flex-start',
            gap: '10px',
            overflow: 'hidden',
          }}
        >
          <div
            style={{
              width: '32px',
              height: '32px',
              borderRadius: '50%',
              backgroundColor: colors.bg.raised,
              border: `1px solid ${colors.border.strong}`,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              color: colors.accent.champagne,
              fontFamily: '"Geist Mono", monospace',
              fontSize: '12px',
              fontWeight: 600,
              flexShrink: 0,
            }}
            title="Alexandre Dubois — Director Creativo"
          >
            AD
          </div>

          {!isCollapsed && (
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                minWidth: 0,
                overflow: 'hidden',
              }}
            >
              <span
                style={{
                  fontSize: '13px',
                  fontWeight: 500,
                  color: colors.text.primary,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                Alexandre Dubois
              </span>
              <span
                style={{
                  fontSize: '11px',
                  color: colors.text.tertiary,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                }}
              >
                Atelier Director
              </span>
            </div>
          )}
        </div>
      </aside>
    </>
  );
};
