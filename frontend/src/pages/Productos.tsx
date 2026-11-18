import React from 'react';
import { Tag } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const Productos: React.FC = () => {
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <h1
        style={{
          fontFamily: '"Instrument Serif", Georgia, serif',
          fontSize: '32px',
          fontWeight: 400,
          margin: 0,
        }}
      >
        Catálogo de Productos
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<Tag size={24} />}
          title="Módulo de Productos (Etapa 2)"
          description="La gestión integral de prendas, variantes por talla/color y matriz de precios será implementada en la Etapa 2 una vez validada la identidad visual."
        />
      </Card>
    </div>
  );
};

export default Productos;
