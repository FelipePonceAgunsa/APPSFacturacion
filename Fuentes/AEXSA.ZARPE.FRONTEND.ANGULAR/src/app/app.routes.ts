import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { InicioComponent } from './components/inicio/inicio.component';
import { ForbiddenComponent } from './components/forbidden/forbidden.component';
import { CatalogoComponent } from './pages/catalogo/catalogo.component';
import { TerminalesComponent } from './pages/configuracion/terminales.component';
import { TiposInspeccionComponent } from './pages/configuracion/tipos-inspeccion.component';
import { authGuard } from './helpers/auth.guard';
import { Access } from './helpers/access.constants';

/**
 * Rutas. Para un módulo nuevo:
 * { path: 'convenios', component: ConveniosComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } },
 */
export const routes: Routes = [
  { path: '', component: LoginComponent },
  { path: 'inicio', component: InicioComponent, canActivate: [authGuard] },
  { path: 'catalogo', component: CatalogoComponent, canActivate: [authGuard], data: { access: [Access.CatalogoSap] } },
  { path: 'configuracion/terminales', component: TerminalesComponent, canActivate: [authGuard], data: { access: [Access.Configuracion] } },
  { path: 'configuracion/tipos-inspeccion', component: TiposInspeccionComponent, canActivate: [authGuard], data: { access: [Access.Configuracion] } },
  { path: 'forbidden', component: ForbiddenComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
