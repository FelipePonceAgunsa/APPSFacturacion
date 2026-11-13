import { Component, ChangeDetectionStrategy, computed, inject } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle, MatCardSubtitle } from '@angular/material/card';
import { AuthService } from '../../services/auth.service';

/** Pantalla de inicio. Punto de partida para los módulos de la etapa 1. */
@Component({
  selector: 'app-inicio',
  imports: [MatCard, MatCardHeader, MatCardTitle, MatCardSubtitle, MatCardContent],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <mat-card class="section-card" appearance="outlined">
      <mat-card-header>
        <mat-card-title>Bienvenido a ZARPE - FACTURACIÓN</mat-card-title>
        <mat-card-subtitle>{{ nombre() }}</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <p>Roles asignados:</p>
        <ul>
          @for (rol of roles(); track rol) {
            <li>{{ rol }}</li>
          }
        </ul>
      </mat-card-content>
    </mat-card>
  `,
})
export class InicioComponent {
  private readonly authService = inject(AuthService);

  readonly nombre = computed(() => {
    const usuario = this.authService.usuario();
    return usuario?.email || usuario?.userName || '';
  });

  readonly roles = computed(() => this.authService.usuario()?.roles ?? []);
}
