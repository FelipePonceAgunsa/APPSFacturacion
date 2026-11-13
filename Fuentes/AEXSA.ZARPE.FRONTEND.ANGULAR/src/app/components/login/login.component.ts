import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { Router } from '@angular/router';
import { MatProgressBar } from '@angular/material/progress-bar';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { AuthService } from '../../services/auth.service';
import { AppVersionService } from '../../services/app-version.service';

/**
 * Pantalla de ingreso. No pide usuario ni contraseña: redirige al inicio de sesión
 * corporativo (Cognito federado con Entra ID, ADR-016).
 */
@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrl: './login.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        MatProgressBar,
        MatButton,
        MatIcon,
    ]
})
export class LoginComponent {
  readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly appVersion = inject(AppVersionService).version;

  vbCargando = signal(false);

  readonly innerOrbit = this.buildOrbitItems(18, [
    { src: './assets/img/report_white.png', alt: 'REPORT' },
    { src: './assets/img/ttp_white.png',    alt: 'TTP' },
    { src: './assets/img/pcf_white.png',    alt: 'PCF' },
    { src: './assets/img/tpv_white.png',    alt: 'TPV' },
  ]);

  readonly outerOrbit = this.buildOrbitItems(30, [
    { src: './assets/img/agenor_white.png',       alt: 'AGENOR' },
    { src: './assets/img/serlog_white.png',       alt: 'SERLOG' },
    { src: './assets/img/solmin_white.png',       alt: 'SOLMIN' },
    { src: './assets/img/tesco_white.png',        alt: 'TESCO' },
    { src: './assets/img/linkprojects_white.png', alt: 'LINKPROJECTS' },
    { src: './assets/img/abx_white.png',          alt: 'ABX' },
    { src: './assets/img/airsec_white.png',       alt: 'AIRSEC' },
    { src: './assets/img/kar_white.png',          alt: 'KAR LOGISTICS' },
    { src: './assets/img/aexa_white.png',         alt: 'AEXA' },
    { src: './assets/img/act_white.png',          alt: 'ACT' },
  ]);

  private buildOrbitItems(cycleDuration: number, items: { src: string; alt: string }[]) {
    const spacing = cycleDuration / items.length;
    return items.map((item, i) => ({ ...item, delay: `${-(i * spacing)}s` }));
  }

  constructor() {
    if (this.authService.isLoggedIn()) {
      this.router.navigate([this.authService.getDefaultRoute()]);
    }
  }

  login(): void {
    this.vbCargando.set(true);
    this.authService.login();
  }

  logout(): void {
    this.authService.cierreSesion();
  }
}
