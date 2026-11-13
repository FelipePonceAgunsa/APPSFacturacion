import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login.component';
import { InicioComponent } from './components/inicio/inicio.component';
import { ForbiddenComponent } from './components/forbidden/forbidden.component';
import { CatalogoComponent } from './pages/catalogo/catalogo.component';
import { ConveniosListaComponent } from './pages/convenios/convenios-lista.component';
import { ConvenioFormComponent } from './pages/convenios/convenio-form.component';
import { ConvenioDetalleComponent } from './pages/convenios/convenio-detalle.component';
import { TerminalesComponent } from './pages/configuracion/terminales.component';
import { TiposInspeccionComponent } from './pages/configuracion/tipos-inspeccion.component';
import { OperacionesBandejaComponent } from './pages/operaciones/operaciones-bandeja.component';
import { OperacionDetalleComponent } from './pages/operaciones/operacion-detalle.component';
import { OfaDetalleComponent } from './pages/ofa/ofa-detalle.component';
import { OfaDigitarComponent } from './pages/ofa/ofa-digitar.component';
import { authGuard } from './helpers/auth.guard';
import { Access } from './helpers/access.constants';

/**
 * Rutas. Para un módulo nuevo:
 * { path: 'convenios', component: ConveniosComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } },
 */
export const routes: Routes = [
  { path: '', component: LoginComponent },
  { path: 'inicio', component: InicioComponent, canActivate: [authGuard] },
  { path: 'convenios', component: ConveniosListaComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } },
  { path: 'convenios/nuevo', component: ConvenioFormComponent, canActivate: [authGuard], data: { access: [Access.Convenios_Edit] } },
  { path: 'convenios/:id', component: ConvenioDetalleComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } },
  { path: 'convenios/:id/nueva-version', component: ConvenioFormComponent, canActivate: [authGuard], data: { access: [Access.Convenios_Edit] } },
  { path: 'operaciones', component: OperacionesBandejaComponent, canActivate: [authGuard], data: { access: [Access.Operaciones] } },
  { path: 'operaciones/:id', component: OperacionDetalleComponent, canActivate: [authGuard], data: { access: [Access.Operaciones] } },
  { path: 'ofa/:id', component: OfaDetalleComponent, canActivate: [authGuard], data: { access: [Access.Ofa] } },
  { path: 'ofa/:id/digitar', component: OfaDigitarComponent, canActivate: [authGuard], data: { access: [Access.Ofa] } },
  { path: 'catalogo', component: CatalogoComponent, canActivate: [authGuard], data: { access: [Access.CatalogoSap] } },
  { path: 'configuracion/terminales', component: TerminalesComponent, canActivate: [authGuard], data: { access: [Access.Configuracion] } },
  { path: 'configuracion/tipos-inspeccion', component: TiposInspeccionComponent, canActivate: [authGuard], data: { access: [Access.Configuracion] } },
  { path: 'forbidden', component: ForbiddenComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' },
];
