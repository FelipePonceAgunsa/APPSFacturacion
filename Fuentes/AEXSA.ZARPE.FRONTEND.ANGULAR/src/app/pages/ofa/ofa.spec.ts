import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatDialog } from '@angular/material/dialog';
import { OfaService } from '../../api/api/ofa.service';
import { LineaOfaDto, OfaDto } from '../../api/model/models';
import { AuthService } from '../../services/auth.service';
import { OfaDetalleComponent } from './ofa-detalle.component';
import { OfaDigitarComponent } from './ofa-digitar.component';
import { AjusteLineaDialog } from './ajuste-linea.dialog';
import { problemaLinea, textoDetalle } from './ofa-util';
const linea = (extra: Partial<LineaOfaDto> = {}): LineaOfaDto => ({
  lineaId: 'l1', asignacionId: 'a1', tipoServicioId: 42, descripcionServicio: 'Recepción', articuloSap: 'SRV-REC-001',
  cantidad: 1, unidad: 'CTR', tarifa: 15000, moneda: 'CLP', montoClp: 15000, esExcepcion: false, origen: 'CALCULADA',
  enKitSap: null, versionCalculo: 1, agregadaPor: null, agregadaEn: null, motivoAgregada: null, lineaOrigenId: null,
  fuenteTarifa: 'CONVENIO', listaPrecios: null, sinTarifa: false, sinTipoCambio: false, cantidadMemorias: 1,
  memoria: {
    memoriaId: 'm1', versionConvenioId: 'v1', versionConvenio: 1, versionTarifaPublica: 1, tipoCambioAplicado: null,
    reglaConfigurableId: null, ajusteManual: null, calculadoEn: '2026-11-13T12:00:00Z',
    reglasAplicadas: [{ regla: 'R1_RESOLUCION_TARIFA', version: 1, detalle: { fuente: 'CONVENIO', tarifa: '15000', lista: '' } }],
  },
  ...extra,
});
const ofa = (extra: Partial<OfaDto> = {}): OfaDto => ({
  ofaId: 'ofa1', operacionId: 'op1', operacionIdAexsa: [98933], tipo: 'PRINCIPAL', estado: 'BORRADOR', versionCalculo: 1,
  fechaCalculo: '2026-11-13T12:00:00Z',
  asignaciones: [{ asignacionId: 'a1', destinatarioRut: '76.111.111-6', razonSocial: 'Importadora', rol: 'CONSIGNATARIO', origenInstruccion: 'DEFECTO_CONSIGNATARIO' }],
  lineas: [linea()], totalClp: 15000, kitSugerido: null, versionCatalogo: 1, envioSapHabilitado: false, ...extra,
});
describe('OFA: utilidades', () => {
  it('muestra los datos de una regla sin los vacíos', () => {
    expect(textoDetalle(linea().memoria.reglasAplicadas[0])).toBe('fuente: CONVENIO · tarifa: 15000');
  });
  it('marca lo que impide validar una línea', () => {
    expect(problemaLinea(linea())).toBeNull();
    expect(problemaLinea(linea({ articuloSap: null }))).toBe('Sin artículo SAP');
    expect(problemaLinea(linea({ sinTarifa: true }))).toBe('Sin tarifa');
  });
});
describe('OFA: pantallas', () => {
  const api = {
    ofaObtener: vi.fn(() => of(ofa())),
    ofaAjustarLinea: vi.fn(() => of(linea({ montoClp: 10000, cantidadMemorias: 2 }))),
    ofaValidar: vi.fn(() => of({ valida: false, faltantes: ['Pendiente TIPO_RETIRO: complete el dato en la operación.'], estado: 'BORRADOR' })),
  };
  const dialog = { open: vi.fn(() => ({ afterClosed: () => of({ nuevoMonto: 10000, motivo: 'Descuento' }) })) };
  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: OfaService, useValue: api },
        { provide: MatDialog, useValue: dialog },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: AuthService, useValue: { hasAccess: () => true } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'ofa1' }) } } },
      ],
    });
  });
  it('F3 muestra las líneas y la memoria, y F4 ajusta un monto con motivo', async () => {
    const fixture = TestBed.createComponent(OfaDetalleComponent);
    const detalle = fixture.componentInstance;
    await detalle.cargar();
    detalle.seleccionada.set(detalle.ofa()!.lineas[0]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('SRV-REC-001');
    expect(fixture.nativeElement.textContent).toContain('Regla 1 · tarifa');
    await detalle.ajustar(detalle.ofa()!.lineas[0]);
    expect(dialog.open).toHaveBeenCalledWith(AjusteLineaDialog, expect.objectContaining({ data: expect.objectContaining({ lineaId: 'l1' }) }));
    expect(api.ofaAjustarLinea).toHaveBeenCalledWith('ofa1', 'l1', { nuevoMonto: 10000, motivo: 'Descuento' });
  });
  it('F6 lista lo que falta y deja "Enviar a SAP" deshabilitado', async () => {
    const fixture = TestBed.createComponent(OfaDigitarComponent);
    await fixture.componentInstance.cargar();
    await fixture.componentInstance.validar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Pendiente TIPO_RETIRO');
    const enviar = [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) => b.textContent?.includes('Enviar a SAP')) as HTMLButtonElement;
    expect(enviar.disabled).toBe(true);
  });
  it('F6 muestra la vista para digitar cuando la OFA está VALIDADA', async () => {
    api.ofaObtener.mockReturnValue(of(ofa({ estado: 'VALIDADA' })));
    const fixture = TestBed.createComponent(OfaDigitarComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Lista para digitar en SAP');
    expect(fixture.nativeElement.textContent).toContain('76.111.111-6');
  });
});
