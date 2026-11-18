import React from 'react';
import { Boxes } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const Inventario: React.FC = () => {
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
        Inventario & Stock
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<Boxes size={24} />}
          title="Módulo de Inventario (Etapa 2)"
          description="El kardex de movimientos, control de existencias en almacén y alertas de reposición será implementado en la Etapa 2."
        />
      </Card>
    </div>
  );
};

export default Inventario;
