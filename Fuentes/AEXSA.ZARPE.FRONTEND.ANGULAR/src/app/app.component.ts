import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { AuthService } from './services/auth.service';
import { SessionManagerService } from './services/session-manager.service';
import { SideMenuComponent } from './components/side-menu/side-menu.component';
import { RouterOutlet } from '@angular/router';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrl: './app.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        SideMenuComponent,
        RouterOutlet,
    ]
})
export class AppComponent {
  title = 'ZARPE - FACTURACIÓN';

  readonly authService = inject(AuthService);

  // Inyectar para que el effect() interno arranque el monitoreo de inactividad
  private readonly sessionManager = inject(SessionManagerService);
}
