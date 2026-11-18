import React from 'react';
import { Truck } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const Proveedores: React.FC = () => {
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
        Directorio de Proveedores
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<Truck size={24} />}
          title="Módulo de Proveedores (Etapa 2)"
          description="La administración de casas textiles, talleres de maquila, plazos de pago y condiciones comerciales será implementada en la Etapa 2."
        />
      </Card>
    </div>
  );
};

export default Proveedores;
