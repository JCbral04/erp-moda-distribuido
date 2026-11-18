import React from 'react';
import { ResponsiveContainer, LineChart, Line } from 'recharts';
import { colors } from '../../design/tokens';

export interface SparkChartProps {
  data: { value: number }[];
  color?: string;
  height?: number;
  width?: number | string;
}

export const SparkChart: React.FC<SparkChartProps> = ({
  data,
  color = colors.accent.champagne,
  height = 32,
  width = 80,
}) => {
  if (!data || data.length < 2) return null;

  return (
    <div style={{ width, height, overflow: 'hidden' }} aria-hidden="true">
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 2, right: 2, left: 2, bottom: 2 }}>
          <Line
            type="monotone"
            dataKey="value"
            stroke={color}
            strokeWidth={1.5}
            dot={false}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
};
