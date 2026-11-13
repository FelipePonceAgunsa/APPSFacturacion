import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OfaService } from '../../api/api/ofa.service';
import { CatalogoService } from '../../api/api/catalogo.service';
import { LineaOfaDto, OfaDto, OperacionDto, OperacionResumenDto, PendienteDto, ReglaAplicadaDto } from '../../api/model/models';
import { AuthService } from '../../services/auth.service';
import { OperacionesBandejaComponent } from './operaciones-bandeja.component';
import { OperacionDetalleComponent } from './operacion-detalle.component';
import { armarSolicitud, formularioVacio, hayCambios, opcionesDecision } from './operaciones-util';
import { estadoOperacion, textoRuta } from './operaciones-vista';
import { explicarLinea } from './explicar-linea';

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
  servicios: [{ tipoServicioId: 48, descripcion: 'Almacenaje', cantidad: 2, unidad: 'CTR_DIA', contenedor: null, finalizadoEn: null, origen: 'AEXSA' }],
  ofaId: null, estadoOfa: null, ...extra,
});
const resumen = (extra: Partial<OperacionResumenDto> = {}): OperacionResumenDto => ({
  operacionId: 'op1', entidad: 'VISADO_VIRTUAL', operacionIdAexsa: [98933], rut: '78.333.333-3', razonSocial: 'Cliente de ejemplo',
  conConvenio: true, facturable: true, estadoOperacion: 'ACTIVA', tipoCarga: 'CONTENEDOR', tipoRetiro: null, rutaClasificada: null,
  visadoAprobadoEn: '2026-10-26T14:30:00Z', versionEvento: 1, pendientes: ['TIPO_RETIRO'], ofaId: null, estadoOfa: null, ...extra,
});
const regla = (r: string, detalle: Record<string, string>): ReglaAplicadaDto => ({ regla: r, version: 1, detalle });
const linea = (extra: Partial<LineaOfaDto> = {}): LineaOfaDto => ({
  lineaId: 'l1', asignacionId: 'a1', tipoServicioId: 42, descripcionServicio: 'Recepción', articuloSap: 'SRV-REC-001',
  cantidad: 1, unidad: 'CTR', tarifa: 15000, moneda: 'CLP', montoClp: 15000, esExcepcion: false, origen: 'CALCULADA',
  enKitSap: null, versionCalculo: 1, agregadaPor: null, agregadaEn: null, motivoAgregada: null, lineaOrigenId: null,
  fuenteTarifa: 'CONVENIO', listaPrecios: null, sinTarifa: false, sinTipoCambio: false, cantidadMemorias: 1,
  memoria: {
    memoriaId: 'm1', versionConvenioId: 'v1', versionConvenio: 1, versionTarifaPublica: 1, tipoCambioAplicado: null,
    reglaConfigurableId: null, ajusteManual: null, calculadoEn: '2026-11-13T12:00:00Z',
    reglasAplicadas: [
      regla('R1_RESOLUCION_TARIFA', { fuente: 'CONVENIO', tarifa: '15000', lista: '', versionConvenio: '1', esExcepcion: 'false' }),
      regla('R2_CANTIDAD', { driver: 'CANTIDAD_SERVICIO', cantidad: '1' }),
      regla('R3_CONVERSION_MONEDA', { moneda: 'CLP', pisoActivado: 'false', sinTipoCambio: 'false' }),
    ],
  },
  ...extra,
});
const ofa = (extra: Partial<OfaDto> = {}): OfaDto => ({
  ofaId: 'ofa1', operacionId: 'op1', operacionIdAexsa: [98933], tipo: 'PRINCIPAL', estado: 'BORRADOR', versionCalculo: 1,
  fechaCalculo: '2026-11-13T12:00:00Z',
  asignaciones: [{ asignacionId: 'a1', destinatarioRut: '76.111.111-6', razonSocial: 'Importadora', rol: 'CONSIGNATARIO', origenInstruccion: 'DEFECTO_CONSIGNATARIO' }],
  lineas: [linea()], totalClp: 15000, kitSugerido: null, versionCatalogo: 1, envioSapHabilitado: false, ...extra,
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
  it('detecta si el formulario trae algún cambio además del motivo', () => {
    const f = formularioVacio();
    f.motivo = 'Solo motivo';
    expect(hayCambios(f)).toBe(false);
    f.cantidadDus = 3;
    expect(hayCambios(f)).toBe(true);
  });
  it('ofrece los convenios candidatos como decisión', () => {
    const p = pendiente('VARIOS_CONVENIOS_VIGENTES', {
      exigeDecision: true,
      detalle: { candidatos: [{ convenioId: 'abcdef12-0000', numero: 1, creadoEn: '2026-01-02T00:00:00Z' }] },
    });
    expect(opcionesDecision(p)).toEqual([{ valor: 'abcdef12-0000', texto: 'Convenio abcdef12 · versión 1 · creado 2026-01-02' }]);
    expect(opcionesDecision(pendiente('DOCUMENTO_CON_VARIAS_SOLICITUDES')).map(o => o.valor)).toEqual(['FACTURAR', 'NO_FACTURAR']);
  });
  it('el estado de la bandeja siempre dice qué pasa con la operación', () => {
    expect(estadoOperacion(resumen())).toEqual({ clase: 'falta', texto: 'Falta: tipo de retiro' });
    expect(estadoOperacion(resumen({ pendientes: ['TIPO_RETIRO', 'SENTIDO'] })).texto).toBe('Falta: tipo de retiro (+1)');
    expect(estadoOperacion(resumen({ pendientes: ['INSPECCION_RECHAZADA'] })).texto).toBe('Revisar: inspección rechazada');
    expect(estadoOperacion(resumen({ pendientes: [] })).texto).toBe('Sin calcular');
    expect(estadoOperacion(resumen({ pendientes: [], ofaId: 'o', estadoOfa: 'BORRADOR' }))).toEqual({ clase: 'ok', texto: 'Calculada' });
    expect(estadoOperacion(resumen({ pendientes: [], ofaId: 'o', estadoOfa: 'VALIDADA' })).texto).toBe('Validada');
    expect(estadoOperacion(resumen({ facturable: false })).texto).toBe('No facturable');
    expect(estadoOperacion(resumen({ estadoOperacion: 'ANULADA' })).texto).toBe('Anulada');
  });
  it('escribe la ruta en palabras', () => {
    expect(textoRuta('CONTENEDOR-INDIRECTO-CON_CONVENIO')).toBe('Contenedor · indirecto · con convenio');
    expect(textoRuta(null)).toBe('Sin clasificar');
  });
});

describe('Operaciones: "¿Por qué?" en lenguaje común', () => {
  it('explica la tarifa del convenio en USD con el dólar mínimo', () => {
    const l = linea({
      tarifa: 20, moneda: 'USD', montoClp: 16000, unidad: 'BL',
      memoria: { ...linea().memoria, reglasAplicadas: [
        regla('R1_RESOLUCION_TARIFA', { fuente: 'CONVENIO', versionConvenio: '1', esExcepcion: 'false' }),
        regla('R2_CANTIDAD', { driver: 'CANTIDAD_SERVICIO' }),
        regla('R3_CONVERSION_MONEDA', { moneda: 'USD', tipoCambioDia: '700', tcMinimo: '800', tipoCambioAplicado: '800', pisoActivado: 'true', sinTipoCambio: 'false' }),
      ] },
    });
    const t = explicarLinea(l);
    expect(t).toContain('Tarifa del convenio vigente (versión 1)');
    expect(t).toContain('Dólar del día 700; el convenio fija un mínimo de 800, así que se usa 800');
    expect(t).toContain('1 × 20 USD × 800 = 16.000 CLP');
  });
  it('explica los días de almacenaje a cobrar', () => {
    const l = linea({
      cantidad: 3, unidad: 'CTR_DIA', tarifa: 8000, montoClp: 24000,
      memoria: { ...linea().memoria, reglasAplicadas: [
        regla('R1_RESOLUCION_TARIFA', { fuente: 'CONVENIO', versionConvenio: '1', esExcepcion: 'false' }),
        regla('R2_CANTIDAD', { driver: 'CANTIDAD_SERVICIO' }),
        regla('R4_ALMACENAJE', { diasCalculados: '15', diasLibres: '10', diasBajoAforo: '2', diasCobrados: '3', porContenedor: 'ABCD1234567:15-10-2=3' }),
      ] },
    });
    const t = explicarLinea(l);
    expect(t).toContain('15 días de almacenaje en total, menos 10 libres del convenio y menos 2 bajo aforo (no se cobran) = 3 días a cobrar.');
    expect(t).toContain('3 × 8.000 = 24.000 CLP');
  });
  it('marca la excepción cuando el convenio no tiene el servicio', () => {
    const l = linea({
      fuenteTarifa: 'PUBLICA', esExcepcion: true,
      memoria: { ...linea().memoria, reglasAplicadas: [regla('R1_RESOLUCION_TARIFA', { fuente: 'PUBLICA', lista: 'LP-PUBLICA', esExcepcion: 'true' })] },
    });
    expect(explicarLinea(l)).toContain('El convenio no tiene este servicio: se usó la tarifa pública vigente (lista LP-PUBLICA) y se marcó como excepción.');
  });
  it('cuenta quién ajustó el monto y por qué', () => {
    const l = linea({
      montoClp: 14000,
      memoria: { ...linea().memoria, ajusteManual: { usuario: 'admin.local', fecha: '2026-11-13T12:00:00Z', motivo: 'Descuento acordado', valorAnterior: 15000, valorNuevo: 14000 } },
    });
    const t = explicarLinea(l);
    expect(t).toContain('El cálculo daba 1 × 15.000 = 15.000 CLP.');
    expect(t).toContain('admin.local lo ajustó');
    expect(t).toContain('Motivo: Descuento acordado.');
  });
  it('explica la refacturación en cero de DP World', () => {
    const l = linea({
      tarifa: 0, montoClp: 0, fuenteTarifa: 'REGLA_0_DP_WORLD',
      memoria: { ...linea().memoria, reglasAplicadas: [
        regla('R0_COMPOSICION_DIRECTO', { concepto: 'REFACTURACION', terminal: 'DP_WORLD', refacturacionEnCero: 'true', serviciosDescartados: '' }),
        regla('R1_RESOLUCION_TARIFA', { fuente: 'REGLA_0_DP_WORLD' }),
      ] },
    });
    expect(explicarLinea(l)).toContain('Terminal del grupo DP World: la refacturación va en cero.');
  });
});

describe('Operaciones: pantallas', () => {
  const api = {
    operacionesListar: vi.fn(() => of([
      resumen(),
      resumen({ operacionId: 'op2', operacionIdAexsa: [98934], razonSocial: 'Otro cliente SA', rut: '76.111.111-6', pendientes: [], ofaId: 'ofa1', estadoOfa: 'BORRADOR', tipoRetiro: 'DIRECTO' }),
    ])),
    operacionesObtener: vi.fn(() => of(operacion())),
    operacionesCompletar: vi.fn(() => of(operacion({ tipoRetiro: 'INDIRECTO', pendientes: [], pendientesDetalle: [] }))),
  };
  const ofas = {
    ofaObtener: vi.fn(() => of(ofa())),
    ofaCalcular: vi.fn(() => of(ofa())),
    ofaAjustarLinea: vi.fn(() => of(linea({ montoClp: 10000 }))),
    ofaAgregarLinea: vi.fn(() => of(linea({ lineaId: 'l2' }))),
    ofaValidar: vi.fn(() => of({ valida: false, faltantes: ['Pendiente SENTIDO: complete el dato en la operación.'], estado: 'BORRADOR' })),
  };
  const catalogo = { tiposServicioListar: vi.fn(() => of([{ tipoServicioId: 44, descripcion: 'Movilización', unidad: 'CTR', habilitado: true }])) };
  const ruta = (id: string) => ({ snapshot: { paramMap: convertToParamMap({ id }) } });
  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: OperacionesService, useValue: api },
        { provide: OfaService, useValue: ofas },
        { provide: CatalogoService, useValue: catalogo },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn() } },
        { provide: AuthService, useValue: { hasAccess: () => true } },
        { provide: ActivatedRoute, useValue: ruta('op1') },
      ],
    });
  });
  const texto = (f: { nativeElement: HTMLElement }) => f.nativeElement.textContent ?? '';

  it('Por facturar muestra cada operación con un estado escrito', async () => {
    const fixture = TestBed.createComponent(OperacionesBandejaComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Por facturar');
    expect(texto(fixture)).toContain('98933');
    expect(texto(fixture)).toContain('Falta: tipo de retiro');
    expect(texto(fixture)).toContain('Calculada');
  });
  it('Por facturar filtra por "Con dato pendiente" y por texto', async () => {
    const fixture = TestBed.createComponent(OperacionesBandejaComponent);
    await fixture.componentInstance.cargar();
    fixture.componentInstance.filtro.set('falta');
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Cliente de ejemplo');
    expect(texto(fixture)).not.toContain('Otro cliente SA');
    fixture.componentInstance.filtro.set('todas');
    fixture.componentInstance.busqueda.set('otro');
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Otro cliente SA');
    expect(texto(fixture)).not.toContain('Cliente de ejemplo');
  });
  it('Por facturar abre la operación con un clic', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(OperacionesBandejaComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('tr.zp-fila-click') as HTMLElement).click();
    expect(navigate).toHaveBeenCalledWith(['/operaciones', 'op1']);
  });

  it('Operación sin tipo de retiro: avisa qué falta, lo completa con motivo y no calcula todavía', async () => {
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    const detalle = fixture.componentInstance;
    await detalle.cargar();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Sin convenio para el RUT 78.333.333-3');
    expect(texto(fixture)).toContain('Todavía no se puede calcular: falta el tipo de retiro');
    expect(texto(fixture)).not.toContain('Lo que corresponde cobrar');
    expect(ofas.ofaCalcular).not.toHaveBeenCalled();
    detalle.aviso.tipoRetiro = 'INDIRECTO';
    detalle.aviso.motivo = 'Confirmado';
    await detalle.guardar(detalle.aviso, 'aviso');
    expect(api.operacionesCompletar).toHaveBeenCalledWith('op1', expect.objectContaining({ motivo: 'Confirmado', tipoRetiro: 'INDIRECTO' }));
  });
  it('Operación completa y sin cálculo: el cálculo aparece solo', async () => {
    api.operacionesObtener
      .mockReturnValueOnce(of(operacion({ pendientes: [], pendientesDetalle: [], tipoRetiro: 'INDIRECTO' })))
      .mockReturnValueOnce(of(operacion({ pendientes: [], pendientesDetalle: [], tipoRetiro: 'INDIRECTO', ofaId: 'ofa1', estadoOfa: 'BORRADOR' })));
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(ofas.ofaCalcular).toHaveBeenCalledWith('op1');
    expect(texto(fixture)).toContain('Lo que corresponde cobrar');
    expect(texto(fixture)).toContain('$ 15.000');
  });
  it('Operación con cálculo: muestra el total, explica una línea y ajusta un monto con motivo', async () => {
    api.operacionesObtener.mockReturnValue(of(operacion({ pendientes: [], pendientesDetalle: [], tipoRetiro: 'INDIRECTO', ofaId: 'ofa1', estadoOfa: 'BORRADOR' })));
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Total a cobrar (neto)');
    expect(texto(fixture)).toContain('SRV-REC-001');
    const boton = (t: string) => [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) => b.textContent?.includes(t)) as HTMLButtonElement;
    boton('¿Por qué?').click();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Tarifa del convenio vigente (versión 1)');
    boton('Ajustar').click();
    fixture.detectChanges();
    const calculo = fixture.debugElement.children[0].query(e => e.name === 'app-calculo-ofa').componentInstance;
    calculo.ajMonto = 10000;
    calculo.ajMotivo = 'Descuento';
    await calculo.guardarAjuste(calculo.ofa().lineas[0]);
    expect(ofas.ofaAjustarLinea).toHaveBeenCalledWith('ofa1', 'l1', { nuevoMonto: 10000, motivo: 'Descuento' });
  });
  it('¿Cómo facturo? lista lo que falta para validar y deja "Enviar a SAP" apagado', async () => {
    api.operacionesObtener.mockReturnValue(of(operacion({ pendientes: [], pendientesDetalle: [], tipoRetiro: 'INDIRECTO', ofaId: 'ofa1', estadoOfa: 'BORRADOR' })));
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    const como = fixture.debugElement.children[0].query(e => e.name === 'app-como-facturo').componentInstance;
    await como.validar();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Pendiente SENTIDO');
    const enviar = [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLButtonElement) => b.textContent?.includes('Enviar a SAP')) as HTMLButtonElement;
    expect(enviar.disabled).toBe(true);
    expect(texto(fixture)).toContain('Desactivado en esta etapa');
  });
  it('¿Cómo facturo? muestra la lista para digitar cuando la OFA está validada', async () => {
    api.operacionesObtener.mockReturnValue(of(operacion({ pendientes: [], pendientesDetalle: [], tipoRetiro: 'INDIRECTO', ofaId: 'ofa1', estadoOfa: 'VALIDADA' })));
    ofas.ofaObtener.mockReturnValue(of(ofa({ estado: 'VALIDADA' })));
    const fixture = TestBed.createComponent(OperacionDetalleComponent);
    await fixture.componentInstance.cargar();
    fixture.detectChanges();
    expect(texto(fixture)).toContain('Lista para digitar en SAP');
    expect(texto(fixture)).toContain('76.111.111-6');
  });
});
