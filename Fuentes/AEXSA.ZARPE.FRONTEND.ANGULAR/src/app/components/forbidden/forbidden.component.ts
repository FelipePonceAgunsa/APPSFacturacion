import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [MatButton, MatIcon, RouterLink],
  template: `
    <div class="forbidden-container">
      <mat-icon class="forbidden-icon">lock</mat-icon>
      <h1>403</h1>
      <h2>Acceso denegado</h2>
      <p>No tienes permisos para acceder a esta página.</p>
      <a mat-raised-button color="primary" routerLink="/inicio">
        <mat-icon>arrow_back</mat-icon>
        Volver al inicio
      </a>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [`
    .forbidden-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      text-align: center;
      padding: 2rem;
    }
    .forbidden-icon {
      font-size: 64px;
      width: 64px;
      height: 64px;
      color: #f44336;
      margin-bottom: 1rem;
    }
    h1 {
      font-size: 4rem;
      margin: 0;
      color: #f44336;
    }
    h2 {
      margin: 0.5rem 0;
      color: rgba(0, 0, 0, 0.7);
    }
    p {
      color: rgba(0, 0, 0, 0.54);
      margin-bottom: 2rem;
    }
  `],
})
export class ForbiddenComponent {}
