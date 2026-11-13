import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { SessionService } from '../api/api/session.service';
import { UsuarioActualResponse } from '../api/model/models';
import { SKIP_AUTH_INTERCEPT } from './jwt-interceptor.service';
import { MenuItem, MENU_ITEMS } from '../helpers/menu.constants';
import { cognitoConfigurado } from '../helpers/oidc.config';
import { environment } from '../../environments/environment';

/**
 * Sesión del usuario. La identidad la resuelve Cognito (ADR-016); los roles y accesos del
 * Satélite los entrega la API en GET /api/Session/Me, que es la única fuente de verdad.
 */
@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly oidc = inject(OidcSecurityService);
  private readonly sessionService = inject(SessionService);
  private readonly router = inject(Router);

  /**
   * Modo abierto de desarrollo (environment.modoAbierto): no pasa por Cognito; la API (Auth:ModoAbierto,
   * solo Development) autentica cada request como el usuario local Administrador.
   */
  readonly modoAbierto = environment.modoAbierto;

  /** El ambiente puede iniciar sesión: tiene el user pool de Cognito o está en modo abierto. */
  readonly configurado = cognitoConfigurado || this.modoAbierto;

  /** Autenticado en Cognito. */
  readonly autenticado = signal(false);

  /** Usuario con roles y accesos del Satélite (null si no cargó o no tiene rol). */
  readonly usuario = signal<UsuarioActualResponse | null>(null);

  /** Autenticado en Cognito, pero sin rol del Satélite (la API respondió 403). */
  readonly sinRolSatelite = signal(false);

  /** Sesión utilizable: autenticado y con usuario del Satélite cargado. */
  readonly isLoggedIn = computed(() => this.autenticado() && this.usuario() !== null);

  /** Se ejecuta antes de la primera navegación (provideAppInitializer). Nunca rechaza. */
  async init(): Promise<void> {
    if (this.modoAbierto) {
      this.autenticado.set(true);
      await this.cargarUsuario();
      return;
    }
    if (!this.configurado) return;

    try {
      const result = await firstValueFrom(this.oidc.checkAuth());
      this.autenticado.set(result.isAuthenticated);
      if (result.isAuthenticated) {
        await this.cargarUsuario();
      }
    } catch (error) {
      console.error('No se pudo verificar la sesión con Cognito', error);
      this.limpiarEstado();
    }
  }

  /** Redirige al inicio de sesión de Cognito (Entra ID). */
  login(): void {
    if (this.modoAbierto) {
      this.autenticado.set(true);
      void this.cargarUsuario().then(() => {
        if (this.isLoggedIn()) {
          this.router.navigate([this.getDefaultRoute()]);
        }
      });
      return;
    }
    if (!this.configurado) return;
    this.oidc.authorize();
  }

  /** Cierra la sesión local y en Cognito. */
  cierreSesion(): void {
    if (this.modoAbierto) {
      this.limpiarEstado();
      this.router.navigate(['/']);
      return;
    }
    this.oidc.logoffLocal();
    this.limpiarEstado();

    const { domain, clientId } = environment.cognito;
    if (domain) {
      // Cognito no publica end_session_endpoint: se usa el endpoint /logout del Hosted UI
      const logoutUri = encodeURIComponent(window.location.origin);
      window.location.href = `${domain.replace(/\/$/, '')}/logout?client_id=${clientId}&logout_uri=${logoutUri}`;
      return;
    }
    this.router.navigate(['/']);
  }

  hasAccess(access: string[] | undefined): boolean {
    if (!access || access.length === 0) return true;

    const accesos = this.usuario()?.access ?? [];
    return access.some(a =>
      accesos.some(userAccess => userAccess.toLowerCase().endsWith(a.toLowerCase()))
    );
  }

  /**
   * Home según accesos: la ruta del primer ítem de menú visible para el
   * usuario (misma regla de visibilidad del side-menu), o /inicio.
   */
  getDefaultRoute(): string {
    const visible = (item: MenuItem): boolean =>
      this.hasAccess(item.access) &&
      !(item.excludeAccess?.length && this.hasAccess(item.excludeAccess));

    const findRoute = (items: MenuItem[]): string | null => {
      for (const item of items) {
        if (!visible(item)) continue;
        if (item.route) return item.route;
        if (item.children) {
          const childRoute = findRoute(item.children);
          if (childRoute) return childRoute;
        }
      }
      return null;
    };

    return findRoute(MENU_ITEMS) ?? '/inicio';
  }

  private async cargarUsuario(): Promise<void> {
    const context = new HttpContext().set(SKIP_AUTH_INTERCEPT, true);
    try {
      const usuario = await firstValueFrom(this.sessionService.sessionMe('body', false, { context }));
      this.usuario.set(usuario);
      this.sinRolSatelite.set(false);
    } catch (error) {
      this.usuario.set(null);
      this.sinRolSatelite.set(error instanceof HttpErrorResponse && error.status === 403);
      if (!this.sinRolSatelite()) {
        console.error('No se pudo cargar el usuario del Satélite', error);
      }
    }
  }

  private limpiarEstado(): void {
    this.autenticado.set(false);
    this.usuario.set(null);
    this.sinRolSatelite.set(false);
  }
}
