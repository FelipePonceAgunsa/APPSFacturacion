import { HttpErrorResponse } from '@angular/common/http';

/**
 * Mensaje legible de un error de la API. El jwtInterceptor deja en error.error.message el `mensaje`
 * del esquema Error del OpenAPI ({ codigo, mensaje, detalle }) o un texto por código HTTP.
 */
export function mensajeDeError(error: unknown): string {
  return error instanceof HttpErrorResponse ? (error.error?.message ?? error.message) : 'Error inesperado';
}
