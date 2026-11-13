import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { TerminalesComponent } from './terminales.component';
import { TiposInspeccionComponent } from './tipos-inspeccion.component';
import { ConfiguracionService } from '../../api/api/configuracion.service';

describe('Configuración: terminales y tipos de inspección', () => {
  const api = {
    configuracionTerminales: vi.fn(() => of([
      { terminalId: 1, nombre: 'STI DE EJEMPLO', grupo: 'STI', operacionesPendientes: 0 },
      { terminalId: 3, nombre: 'TERMINAL NUEVO DE EJEMPLO', grupo: 'SIN_MAPEAR', operacionesPendientes: 2 },
    ])),
    configuracionAsignarTerminal: vi.fn(() => of({ terminalId: 3, nombre: 'TERMINAL NUEVO DE EJEMPLO', grupo: 'DP_WORLD', operacionesPendientes: 0 })),
    configuracionTiposInspeccion: vi.fn(() => of([
      { codigo: 'ADU-FIS', descripcion: 'Aforo físico de aduana', grupo: 'AFORO_ADUANA', operacionesPendientes: 0 },
    ])),
    configuracionAsignarTipoInspeccion: vi.fn(),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: ConfiguracionService, useValue: api },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn() } },
      ],
    });
  });

  it('lista los terminales y asigna el grupo de uno sin mapear', async () => {
    const fixture = TestBed.createComponent(TerminalesComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('TERMINAL NUEVO DE EJEMPLO');

    const componente = fixture.componentInstance;
    await componente.asignar(componente.terminales()[1], 'DP_WORLD');

    expect(api.configuracionAsignarTerminal).toHaveBeenCalledWith(3, { grupo: 'DP_WORLD' });
    expect(componente.terminales()[1].grupo).toBe('DP_WORLD');
  });

  it('lista los tipos de inspección con su grupo', async () => {
    const fixture = TestBed.createComponent(TiposInspeccionComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('ADU-FIS');
    expect(fixture.componentInstance.tipos()[0].grupo).toBe('AFORO_ADUANA');
  });
});
