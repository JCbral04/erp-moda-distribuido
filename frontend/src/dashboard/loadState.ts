import { describeError } from '../api/errors.ts';

export type RequestStatus = 'idle' | 'loading' | 'success' | 'error';

/** Estado independiente por servicio: permite conservar respuestas correctas
 * mientras se informa de las que fallaron, sin convertir un error en un cero. */
export interface ServiceState<T> {
  status: RequestStatus;
  data: T | null;
  error: string | null;
}

export const idleState = <T,>(): ServiceState<T> => ({
  status: 'idle',
  data: null,
  error: null,
});

/** Al recargar se mantiene la última respuesta correcta para evitar parpadeos. */
export const loadingState = <T,>(previous: ServiceState<T>): ServiceState<T> => ({
  status: 'loading',
  data: previous.data,
  error: null,
});

/** Aplica el resultado de Promise.allSettled sin ocultar el fallo. */
export function settledState<T>(
  previous: ServiceState<T>,
  result: PromiseSettledResult<T>,
): ServiceState<T> {
  if (result.status === 'fulfilled') {
    return { status: 'success', data: result.value, error: null };
  }
  return { status: 'error', data: previous.data, error: describeError(result.reason) };
}

/** Verdadero durante la carga inicial o mientras se espera una respuesta. */
export const isPending = <T,>(state: ServiceState<T>): boolean =>
  state.status === 'idle' || state.status === 'loading';

export const hasError = <T,>(state: ServiceState<T>): boolean => state.status === 'error';
