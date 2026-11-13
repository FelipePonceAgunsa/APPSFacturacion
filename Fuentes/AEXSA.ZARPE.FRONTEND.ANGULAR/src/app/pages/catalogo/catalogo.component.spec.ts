import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { CatalogoComponent } from './catalogo.component';
import { CatalogoService } from '../../api/api/catalogo.service';
import { AuthService } from '../../services/auth.service';

describe('CatalogoComponent', () => {
  const api = {
    catalogoResumen: vi.fn(() => of({
      version: 3, fecha: '2026-11-13T12:00:00Z', origen: 'ARCHIVO', articulos: 12, listasPrecios: 2, kits: 2, tipoCambioAl: '2026-11-12',
    })),
    catalogoArticulos: vi.fn(() => of([
      { codigoArticulo: 'SRV-REC-001', descripcion: 'Recepción contenedor', unidadMedida: 'CTR', activo: true, grupoArticulo: 'SERVICIOS' },
    ])),
    catalogoPrecios: vi.fn(() => of([])),
    catalogoKits: vi.fn(() => of([])),
    catalogoTipoCambio: vi.fn(() => of([{ fecha: '2026-11-12', moneda: 'USD', valor: 812.4, fuente: 'ARCHIVO' }])),
    catalogoVinculos: vi.fn(() => of([
      { tipoServicioId: 42, descripcionServicio: 'Recepción', articuloSap: null, estado: 'SIN_VINCULO', origen: null, confirmadoPor: null },
    ])),
    catalogoFijarVinculo: vi.fn(() => of({})),
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CatalogoComponent],
      providers: [
        { provide: CatalogoService, useValue: api },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() } },
        { provide: AuthService, useValue: { hasAccess: () => true } },
      ],
    });
  });

  it('muestra la versión vigente del catálogo', async () => {
    const fixture = TestBed.createComponent(CatalogoComponent);
    fixture.detectChanges();
    await fixture.componentInstance.recargar();
    fixture.detectChanges();

    expect(fixture.componentInstance.resumen()?.version).toBe(3);
    expect(fixture.nativeElement.textContent).toContain('Versión 3');
    expect(fixture.componentInstance.vinculos()[0].estado).toBe('SIN_VINCULO');
  });

  it('confirma el vínculo elegido para un servicio', async () => {
    const fixture = TestBed.createComponent(CatalogoComponent);
    fixture.detectChanges();
    await fixture.componentInstance.recargar();
    const componente = fixture.componentInstance;

    componente.elegirArticulo(42, 'SRV-REC-001');
    await componente.fijarVinculo(componente.vinculos()[0]);

    expect(api.catalogoFijarVinculo).toHaveBeenCalledWith(42, { articuloSap: 'SRV-REC-001' });
  });
});
