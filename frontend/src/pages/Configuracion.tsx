import React from 'react';
import { Settings } from 'lucide-react';
import { EmptyState } from '../components/ui/EmptyState';
import { Card } from '../components/ui/Card';

export const Configuracion: React.FC = () => {
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
        Configuración del Atelier
      </h1>
      <Card variant="surface">
        <EmptyState
          icon={<Settings size={24} />}
          title="Ajustes y Parámetros del Sistema (Etapa 2)"
          description="Ajustes de tienda, pasarelas de pago, perfiles de usuario y parámetros fiscales del atelier serán implementados en la Etapa 2."
        />
      </Card>
    </div>
  );
};

export default Configuracion;
