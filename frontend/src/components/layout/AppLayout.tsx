import React, { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { Header } from './Header';
import { colors } from '../../design/tokens';

export const AppLayout: React.FC = () => {
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const [isCollapsed, setIsCollapsed] = useState<boolean>(() => {
    try {
      const saved = localStorage.getItem('erp_sidebar_collapsed');
      return saved ? JSON.parse(saved) : false;
    } catch {
      return false;
    }
  });

  const handleToggleCollapse = () => {
    setIsCollapsed((prev) => {
      const next = !prev;
      try {
        localStorage.setItem('erp_sidebar_collapsed', JSON.stringify(next));
      } catch {
        // ignore
      }
      return next;
    });
  };

  const handleToggleMobile = () => {
    setIsMobileOpen((prev) => !prev);
  };

  return (
    <div
      style={{
        display: 'flex',
        minHeight: '100vh',
        backgroundColor: colors.bg.base,
        color: colors.text.primary,
        position: 'relative',
      }}
    >
      {/* Skip Link for WCAG Keyboard Navigation */}
      <a href="#main" className="sr-only focus:not-sr-only">
        Saltar al contenido principal
      </a>

      {/* Main Sidebar */}
      <Sidebar
        isCollapsed={isCollapsed}
        onToggleCollapse={handleToggleCollapse}
        isMobileOpen={isMobileOpen}
        onMobileClose={() => setIsMobileOpen(false)}
      />

      {/* Content Area */}
      <div
        style={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          minWidth: 0,
          backgroundColor: colors.bg.base,
        }}
      >
        <Header onToggleSidebar={handleToggleMobile} />

        <main
          id="main"
          style={{
            flex: 1,
            padding: '28px',
            maxWidth: '1600px',
            width: '100%',
            margin: '0 auto',
            outline: 'none',
          }}
          tabIndex={-1}
        >
          <Outlet />
        </main>
      </div>
    </div>
  );
};
