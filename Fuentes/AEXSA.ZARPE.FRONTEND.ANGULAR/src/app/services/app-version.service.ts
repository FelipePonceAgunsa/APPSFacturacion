import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

/**
 * Expone la versión desplegada (tag de la imagen Docker) leída de
 * assets/version.json. En desarrollo el archivo del repo dice "local";
 * el Dockerfile lo sobreescribe en build con el tag real.
 */
@Injectable({ providedIn: 'root' })
export class AppVersionService {
  private readonly http = inject(HttpClient);

  readonly version = signal('');

  constructor() {
    this.http.get<{ version: string }>('./assets/version.json').subscribe({
      next: data => this.version.set(data?.version || 'local'),
      error: () => this.version.set('local'),
    });
  }
}
