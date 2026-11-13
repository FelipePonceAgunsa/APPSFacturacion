import { Injectable, OnDestroy, effect, inject, signal } from '@angular/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { AuthService } from './auth.service';
import { DialogSessionWarningComponent } from '../components/dialog-session-warning/dialog-session-warning.component';

const INACTIVITY_TIMEOUT = 15 * 60 * 1000;   // 15 minutos
const COUNTDOWN_DURATION = 120;               // 2 minutos en segundos
const ACTIVITY_DEBOUNCE  = 500;               // ms

const ACTIVITY_EVENTS: string[] = ['click', 'keydown', 'mousemove', 'scroll', 'touchstart'];

type SessionState = 'active' | 'warning' | 'stopped';

/**
 * Cierre de sesión por inactividad (estándar AGUNSA). La renovación del token la hace
 * angular-auth-oidc-client con el refresh token de Cognito (silentRenew + useRefreshToken).
 */
@Injectable({ providedIn: 'root' })
export class SessionManagerService implements OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  /** Segundos restantes en el countdown de advertencia (leído por el dialog). */
  readonly countdownSeconds = signal(COUNTDOWN_DURATION);

  private state: SessionState = 'stopped';
  private timerId: ReturnType<typeof setTimeout> | null = null;
  private debounceId: ReturnType<typeof setTimeout> | null = null;
  private lastActivity = 0;
  private dialogRef: MatDialogRef<DialogSessionWarningComponent> | null = null;

  private readonly boundOnActivity = () => this.onActivity();

  constructor() {
    effect(() => {
      // En modo abierto de desarrollo no hay sesión de Cognito que cerrar por inactividad.
      if (this.authService.isLoggedIn() && !this.authService.modoAbierto) {
        this.start();
      } else {
        this.stop();
      }
    });
  }

  ngOnDestroy(): void {
    this.stop();
  }

  // ── Ciclo de vida ──

  private start(): void {
    if (this.state !== 'stopped') return;

    this.state = 'active';
    this.lastActivity = Date.now();
    this.addActivityListeners();
    this.scheduleNext();
  }

  private stop(): void {
    this.state = 'stopped';
    this.clearTimer();
    this.clearDebounce();
    this.removeActivityListeners();
    this.closeDialog();
  }

  // ── Timer único encadenado ──

  private scheduleNext(): void {
    this.clearTimer();
    if (this.state === 'stopped') return;

    if (this.state === 'warning') {
      this.timerId = setTimeout(() => this.tickCountdown(), 1000);
      return;
    }

    const msToInactivity = Math.max(1000, (this.lastActivity + INACTIVITY_TIMEOUT) - Date.now());
    this.timerId = setTimeout(() => this.tick(), msToInactivity);
  }

  private tick(): void {
    if (this.state !== 'active') return;

    if (Date.now() - this.lastActivity >= INACTIVITY_TIMEOUT) {
      this.enterWarningState();
      return;
    }

    this.scheduleNext();
  }

  // ── Estado de advertencia (countdown) ──

  private enterWarningState(): void {
    this.state = 'warning';
    this.countdownSeconds.set(COUNTDOWN_DURATION);
    this.removeActivityListeners();

    this.dialogRef = this.dialog.open(DialogSessionWarningComponent, {
      data: { countdownSeconds: this.countdownSeconds },
      disableClose: true,
      width: '420px',
      ariaLabel: 'Advertencia de expiración de sesión',
    });

    this.dialogRef.afterClosed().subscribe(continueSession => {
      this.dialogRef = null;
      if (continueSession && this.state === 'warning') {
        this.state = 'active';
        this.lastActivity = Date.now();
        this.addActivityListeners();
        this.scheduleNext();
      }
    });

    this.scheduleNext();
  }

  private tickCountdown(): void {
    if (this.state !== 'warning') return;

    const remaining = this.countdownSeconds() - 1;
    this.countdownSeconds.set(remaining);

    if (remaining <= 0) {
      this.closeDialog();
      this.authService.cierreSesion();
      return;
    }

    this.scheduleNext();
  }

  // ── Detección de actividad (debounced) ──

  private onActivity(): void {
    if (this.state !== 'active') return;
    if (this.debounceId) return;

    this.lastActivity = Date.now();

    this.debounceId = setTimeout(() => {
      this.debounceId = null;
    }, ACTIVITY_DEBOUNCE);
  }

  private addActivityListeners(): void {
    for (const event of ACTIVITY_EVENTS) {
      document.addEventListener(event, this.boundOnActivity, { passive: true });
    }
  }

  private removeActivityListeners(): void {
    for (const event of ACTIVITY_EVENTS) {
      document.removeEventListener(event, this.boundOnActivity);
    }
  }

  // ── Limpieza ──

  private clearTimer(): void {
    if (this.timerId !== null) {
      clearTimeout(this.timerId);
      this.timerId = null;
    }
  }

  private clearDebounce(): void {
    if (this.debounceId !== null) {
      clearTimeout(this.debounceId);
      this.debounceId = null;
    }
  }

  private closeDialog(): void {
    if (this.dialogRef) {
      this.dialogRef.close(false);
      this.dialogRef = null;
    }
  }
}
