import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatDialog } from '@angular/material/dialog';
import { ConveniosService } from '../../api/api/convenios.service';
import { CatalogoService } from '../../api/api/catalogo.service';
import { VersionConvenioDto } from '../../api/model/models';
import { AuthService } from '../../services/auth.service';
import { ConveniosListaComponent } from './convenios-lista.component';
import { ConvenioFormComponent } from './convenio-form.component';
import { ConvenioDetalleComponent } from './convenio-detalle.component';
import { ConfirmarVersionDialog } from './confirmar-version.dialog';
import { diasHasta, diferenciasVersion } from './convenios-util';

const version = (numero: number, estado: VersionConvenioDto['estado'], tarifa: number): VersionConvenioDto => ({
  versionId: `v${numero}`, convenioId: 'c1', numero, estado, vigenciaDesde: '2026-01-01', vigenciaHasta: '2026-12-31',
  tcMinimo: 800, diasLibres: 10, origen: 'MANUAL', confianzaIa: null, confirmadoPor: null, confirmadoEn: null,
  creadoEn: '2026-11-13T12:00:00Z',
  servicios: [{ tipoServicioId: 42, tarifa, moneda: 'CLP', unidad: 'CTR', descripcionServicio: 'Recepción' }],
});

describe('Convenios: utilidades', () => {
  it('calcula los días que faltan para el vencimiento', () => {
    expect(diasHasta('2026-12-31', new Date(2026, 11, 1))).toBe(30);
    expect(diasHasta(null)).toBeNull();
  });

  it('lista las diferencias del borrador contra la vigente', () => {
    const cambios = diferenciasVersion(version(2, 'BORRADOR', 16000), version(1, 'VIGENTE', 15000));
    expect(cambios).toEqual(['Servicio 42 Recepción: 15000 CLP → 16000 CLP']);
    expect(diferenciasVersion(version(1, 'BORRADOR', 1), null)).toEqual(['Primera versión vigente del convenio.']);
  });
});

describe('Convenios: pantallas', () => {
  const convenios = {
    conveniosListar: vi.fn(() => of([{
      convenioId: 'c1', rut: '76.111.111-6', razonSocial: 'Cliente de ejemplo', comercial: null, versionVigente: 1,
      vigenciaDesde: '2026-01-01', vigenciaHasta: '2026-12-31', ultimaVersion: 2, estadoUltimaVersion: 'BORRADOR',
    }])),
    conveniosCrear: vi.fn(() => of(version(1, 'BORRADOR', 15000))),
    conveniosObtener: vi.fn(() => of({
      convenioId: 'c1', rut: '76.111.111-6', razonSocial: 'Cliente de ejemplo', comercial: null, creadoEn: '2026-11-13T12:00:00Z',
      versiones: [version(2, 'BORRADOR', 16000), version(1, 'VIGENTE', 15000)],
    })),
    conveniosConfirmarVersion: vi.fn(() => of(version(2, 'VIGENTE', 16000))),
  };
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of(true) })) };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: ConveniosService, useValue: convenios },
        { provide: CatalogoService, useValue: { tiposServicioListar: () => of([
          { tipoServicioId: 42, codigo: 'REC', descripcion: 'Recepción', unidad: 'CTR', habilitado: true },
        ]) } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), warning: vi.fn() } },
        { provide: AuthService, useValue: { hasAccess: () => true } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'c1' }) } } },
      ],
    });
  });

  it('C1 muestra los convenios con su versión vigente', async () => {
    const fixture = TestBed.createComponent(ConveniosListaComponent);
    await fixture.componentInstance.buscar();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('76.111.111-6');
    expect(fixture.nativeElement.textContent).toContain('2 · BORRADOR');
  });

  it('C2 guarda un convenio nuevo en borrador con sus servicios', async () => {
    TestBed.overrideProvider(ActivatedRoute, { useValue: { snapshot: { paramMap: convertToParamMap({}) } } });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(ConvenioFormComponent);
    const form = fixture.componentInstance;
    await form.ngOnInit();
    form.rut = '76.111.111-6';
    form.vigenciaDesde = '2026-01-01';
    form.tcMinimo = 800;
    form.filas.set([{ tipoServicioId: 42, tarifa: 15000, moneda: 'CLP' }]);

    await form.guardar();

    expect(convenios.conveniosCrear).toHaveBeenCalledWith(expect.objectContaining({
      rut: '76.111.111-6', vigenciaDesde: '2026-01-01', tcMinimo: 800,
      servicios: [{ tipoServicioId: 42, tarifa: 15000, moneda: 'CLP' }],
    }));
    expect(navigate).toHaveBeenCalledWith(['/convenios', 'c1']);
  });

  it('C3/C4 confirma un borrador después del diálogo', async () => {
    TestBed.overrideProvider(MatDialog, { useValue: dialog });
    const fixture = TestBed.createComponent(ConvenioDetalleComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Versión 2 · BORRADOR');

    await fixture.componentInstance.confirmar(fixture.componentInstance.convenio()!.versiones[0]);

    expect(dialog.open).toHaveBeenCalledWith(ConfirmarVersionDialog, expect.anything());
    expect(convenios.conveniosConfirmarVersion).toHaveBeenCalledWith('c1', 'v2');
  });
});
