/** Error lanzado por el cliente HTTP cuando el backend responde con un estado no OK. */
export class ApiError extends Error {
  status: number;
  statusText: string;
  data: unknown;

  constructor(status: number, statusText: string, data: unknown) {
    super(`API Error ${status}: ${statusText}`);
    this.name = 'ApiError';
    this.status = status;
    this.statusText = statusText;
    this.data = data;
  }
}

/** Convierte cualquier fallo de red/HTTP en un mensaje mostrable al usuario. */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return `Error ${error.status} (${error.statusText}) al consultar la API`;
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return 'Error de comunicación desconocido';
}
