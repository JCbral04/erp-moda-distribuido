import React from 'react';
import { FileText } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const OrdenesCompra: React.FC = () => {
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
        Órdenes de Compra
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<FileText size={24} />}
          title="Módulo de Órdenes de Compra (Etapa 2)"
          description="La emisión, seguimiento y recepción de materias primas e insumos de confección será implementada en la Etapa 2."
        />
      </Card>
    </div>
  );
};

export default OrdenesCompra;
