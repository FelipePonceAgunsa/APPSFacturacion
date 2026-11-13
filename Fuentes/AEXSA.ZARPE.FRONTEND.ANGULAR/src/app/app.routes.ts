import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { InicioComponent } from './components/inicio/inicio.component';
import { ForbiddenComponent } from './components/forbidden/forbidden.component';
import { authGuard } from './helpers/auth.guard';

/**
 * Rutas. Para un módulo nuevo:
 * { path: 'convenios', component: ConveniosComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } },
 */
export const routes: Routes = [
  { path: '', component: LoginComponent },
  { path: 'inicio', component: InicioComponent, canActivate: [authGuard] },
  { path: 'forbidden', component: ForbiddenComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
