import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from './auth.service';
import { SessionService } from '../api/api/session.service';

describe('AuthService', () => {
  const usuario = {
    sub: 'modo-abierto',
    userName: 'admin.local',
    email: 'admin.local@zarpe.local',
    roles: ['Administrador'],
    access: ['convenios', 'configuracion'],
  };
  let sessionMe: ReturnType<typeof vi.fn>;
  let checkAuth: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    sessionMe = vi.fn(() => of(usuario));
    checkAuth = vi.fn(() => of({ isAuthenticated: false }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionService, useValue: { sessionMe } },
        { provide: OidcSecurityService, useValue: { checkAuth, authorize: vi.fn(), logoffLocal: vi.fn() } },
      ],
    });
  });

  it('en modo abierto carga el usuario de la API sin pasar por Cognito', async () => {
    const service = TestBed.inject(AuthService);
    (service as { modoAbierto: boolean }).modoAbierto = true;

    await service.init();

    expect(checkAuth).not.toHaveBeenCalled();
    expect(sessionMe).toHaveBeenCalledTimes(1);
    expect(service.isLoggedIn()).toBe(true);
    expect(service.usuario()?.roles).toEqual(['Administrador']);
    expect(service.hasAccess(['configuracion'])).toBe(true);
    expect(service.hasAccess(['ofa_ajusteManual'])).toBe(false);
  });

  it('sin modo abierto y sin Cognito configurado no inicia sesión', async () => {
    const service = TestBed.inject(AuthService);

    await service.init();

    expect(service.modoAbierto).toBe(false);
    expect(sessionMe).not.toHaveBeenCalled();
    expect(service.isLoggedIn()).toBe(false);
  });
});
