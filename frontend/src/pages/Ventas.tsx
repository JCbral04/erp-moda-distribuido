import React from 'react';
import { Receipt } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const Ventas: React.FC = () => {
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
        Registro de Ventas
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<Receipt size={24} />}
          title="Módulo de Ventas (Etapa 2)"
          description="El flujo de caja, punto de venta (POS), confirmación y anulación de transacciones será implementado en la Etapa 2."
        />
      </Card>
    </div>
  );
};

export default Ventas;
