import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OfaService } from '../../api/api/ofa.service';
import { OperacionDto, PendienteDto } from '../../api/model/models';
import { AuthService } from '../../services/auth.service';
import { OperacionesBandejaComponent } from './operaciones-bandeja.component';
import { OperacionDetalleComponent } from './operacion-detalle.component';
import { armarSolicitud, formularioVacio, opcionesDecision } from './operaciones-util';
const pendiente = (tipo: PendienteDto['tipo'], extra: Partial<PendienteDto> = {}): PendienteDto => ({
  pendienteId: `p-${tipo}`, tipo, bloqueaValidacion: true, exigeDecision: false, detalle: null, detectadoEn: '2026-10-26T14:30:00Z',
  resueltoEn: null, resueltoPor: null, resolucion: null, decision: null, motivo: null, ...extra,
});
const operacion = (extra: Partial<OperacionDto> = {}): OperacionDto => ({
  operacionId: 'op1', entidad: 'VISADO_VIRTUAL', operacionIdAexsa: [98933], rut: '78.333.333-3', razonSocial: 'Cliente de ejemplo',
  conConvenio: false, facturable: true, tipoCarga: 'CONTENEDOR', sentido: 'IMPORTACION', tipoRetiro: null, estadoOperacion: 'ACTIVA',
  terminal: { id: 1, nombre: 'STI DE EJEMPLO' }, grupoTerminal: 'STI', pendientes: ['TIPO_RETIRO'],
  pendientesDetalle: [pendiente('TIPO_RETIRO')], din: [6123456789], cantidadDus: null, bls: [{ id: 184520, numero: 'BL-0001' }],
  papeletas: [5512], consignatario: { rut: '78.333.333-3', razonSocial: 'Cliente de ejemplo' }, clienteFacturador: null,
  convenio: null, contenedores: [], versionEvento: 1, rutaClasificada: null, momentoFacturacion: 'EN_VISACION',
  visadoAprobadoEn: '2026-10-26T14:30:00Z', ingeridoEn: '2026-10-26T14:31:00Z', actualizadoEn: '2026-10-26T14:31:00Z',
  servicios: [{ tipoServicioId: 48, descripcion: 'Almacenaje', cantidad: null, unidad: 'CTR_DIA', contenedor: null, finalizadoEn: null, origen: 'AEXSA' }],
  ofaId: null, estadoOfa: null, ...extra,
});
describe('Operaciones: utilidades', () => {
  it('arma el PATCH solo con lo informado', () => {
    const f = formularioVacio();
    f.motivo = '  Confirmado con el agente ';
    f.tipoRetiro = 'INDIRECTO';
    f.cantidades = { 48: 15, 42: null };
    f.decisiones = { INSPECCION_RECHAZADA: 'CONTINUAR' };
    expect(armarSolicitud(f)).toEqual({
      motivo: 'Confirmado con el agente', tipoRetiro: 'INDIRECTO', sentido: null, cantidadDus: null, fechaDespacho: null,
      contenedor: null, diasAforo: null, cantidadesServicio: [{ tipoServicioId: 48, cantidad: 15 }],
      decisiones: [{ tipo: 'INSPECCION_RECHAZADA', valor: 'CONTINUAR' }],
    });
  });
  it('ofrece los convenios candidatos como decisión', () => {
    const p = pendiente('VARIOS_CONVENIOS_VIGENTES', {
      exigeDecision: true,
      detalle: { candidatos: [{ convenioId: 'abcdef12-0000', numero: 1, creadoEn: '2026-01-02T00:00:00Z' }] },
    });
    expect(opcionesDecision(p)).toEqual([{ valor: 'abcdef12-0000', texto: 'Convenio abcdef12 · versión 1 · creado 2026-01-02' }]);
    expect(opcionesDecision(pendiente('DOCUMENTO_CON_VARIAS_SOLICITUDES')).map(o => o.valor)).toEqual(['FACTURAR', 'NO_FACTURAR']);
  });
});
describe('Operaciones: pantallas', () => {
  const api = {
    operacionesListar: vi.fn(() => of([{
      operacionId: 'op1', entidad: 'VISADO_VIRTUAL', operacionIdAexsa: [98933], rut: '78.333.333-3', razonSocial: 'Cliente de ejemplo',
      conConvenio: true, facturable: true, estadoOperacion: 'ACTIVA', tipoCarga: 'CONTENEDOR', tipoRetiro: null, rutaClasificada: null,
      visadoAprobadoEn: '2026-10-26T14:30:00Z', versionEvento: 1, pendientes: ['TIPO_RETIRO'], ofaId: null, estadoOfa: null,
    }])),
    operacionesObtener: vi.fn(() => of(operacion())),
    operacionesCompletar: vi.fn(() => of(operacion({ tipoRetiro: 'INDIRECTO', pendientes: [], pendientesDetalle: [] }))),
  };
  const ofa = { ofaCalcular: vi.fn(() => of({ ofaId: 'ofa1' })) };
  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: OperacionesService, useValue: api },
        { provide: OfaService, useValue: ofa },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: AuthService, useValue: { hasAccess: () => true } },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'op1' }) } } },
      ],
    });
  });
  it('F1 muestra las operaciones con sus pendientes', async () => {
    const fixture = TestBed.createComponent(OperacionesBandejaComponent);
    await fixture.componentInstance.buscar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('98933');
    expect(fixture.nativeElement.textContent).toContain('Falta el tipo de retiro');
  });
  it('F2 muestra "Sin convenio", completa el tipo de retiro con motivo y calcula la OFA', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    const detalle = fixture.componentInstance;
    await detalle.cargar();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sin convenio para el RUT 78.333.333-3');
    expect(fixture.nativeElement.textContent).toContain('Cantidad de 48 Almacenaje');
    detalle.form.motivo = 'Confirmado';
    detalle.form.tipoRetiro = 'INDIRECTO';
    await detalle.completar();
    expect(api.operacionesCompletar).toHaveBeenCalledWith('op1', expect.objectContaining({ motivo: 'Confirmado', tipoRetiro: 'INDIRECTO' }));
    expect(detalle.op()!.tipoRetiro).toBe('INDIRECTO');
    await detalle.calcularOfa();
    expect(navigate).toHaveBeenCalledWith(['/ofa', 'ofa1']);
  });
});
