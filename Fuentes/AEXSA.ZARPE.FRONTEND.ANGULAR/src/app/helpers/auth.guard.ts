import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

/**
 * Guard de rutas: exige sesión con rol del Satélite y, si la ruta declara
 * data.access, al menos uno de esos accesos. La API vuelve a validar cada request.
 */
export const authGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isLoggedIn()) {
    return router.createUrlTree(['/']);
  }

  const access: string[] | undefined = route.data?.['access'];

  if (!authService.hasAccess(access)) {
    return router.createUrlTree(['/forbidden']);
  }

  return true;
};
