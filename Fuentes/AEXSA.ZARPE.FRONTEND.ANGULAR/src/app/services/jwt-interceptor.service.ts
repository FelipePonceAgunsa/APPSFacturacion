import { HttpHandlerFn, HttpRequest, HttpEvent, HttpErrorResponse, HttpContextToken } from '@angular/common/http';
import { Observable, catchError, throwError } from 'rxjs';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

/** Marca requests que no deben disparar logout automático en 401 (ej: carga inicial del usuario). */
export const SKIP_AUTH_INTERCEPT = new HttpContextToken<boolean>(() => false);

const HTTP_MESSAGES: Record<number, string> = {
  403: 'No tienes permisos para realizar esta acción.',
  404: 'El recurso solicitado no existe.',
  500: 'Error interno del servidor.',
};

/**
 * El access token lo adjunta authInterceptor() de angular-auth-oidc-client (secureRoutes).
 * Este interceptor solo maneja el rechazo del servidor: 401 cierra la sesión y los errores
 * se traducen a un mensaje legible en error.error.message.
 */
export function jwtInterceptor(req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.context.get(SKIP_AUTH_INTERCEPT)) {
        authService.cierreSesion();
      }

      // La API responde con el esquema Error del OpenAPI ({ codigo, mensaje, detalle }).
      const serverMessage = error.error?.mensaje ?? error.error?.message;
      const friendlyMessage = serverMessage || HTTP_MESSAGES[error.status];

      if (friendlyMessage) {
        error = new HttpErrorResponse({
          ...error,
          error: { ...error.error, message: friendlyMessage },
        });
      }

      return throwError(() => error);
    })
  );
}
