import {
  GrupoTerminal, LineaOfaDto, MomentoFacturacion, OperacionDto, OperacionResumenDto, ResultadoInspeccion, Sentido, TipoPendiente,
  TipoRetiro, UnidadCobro,
} from '../../api/model/models';

/**
 * Textos y formatos de las pantallas "Por facturar" y "Operación" (maqueta ux/maqueta-satelite-simple.html).
 * Todo es presentación: ninguna regla de negocio vive aquí.
 */

/** Número con formato chileno (miles con punto, decimales con coma), sin ceros de relleno. */
export function formatoNumero(n: number | null | undefined, maxDecimales = 2): string {
  return n === null || n === undefined ? '—' : Number(n).toLocaleString('es-CL', { maximumFractionDigits: maxDecimales });
}
/** Monto en pesos: `$ 14.000`. */
export function formatoClp(n: number | null | undefined): string {
  return n === null || n === undefined ? '—' : `$ ${formatoNumero(n)}`;
}
export function fechaHora(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}
export function fechaCorta(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleDateString('es-CL', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** Lo que falta, dicho en pocas palabras (para el estado de la bandeja). */
export const ETIQUETA_PENDIENTE: Record<TipoPendiente, string> = {
  TIPO_RETIRO: 'tipo de retiro',
  SENTIDO: 'sentido',
  TERMINAL_SIN_MAPEAR: 'terminal sin asignar',
  CANTIDAD_DUS: 'cantidad de DUS',
  CANTIDAD_SERVICIO: 'cantidad de un servicio',
  DIAS_AFORO: 'días bajo aforo',
  FECHA_DESPACHO: 'fecha de despacho',
  VARIOS_CONVENIOS_VIGENTES: 'elegir convenio',
  DOCUMENTO_CON_VARIAS_SOLICITUDES: 'documento repetido',
  INSPECCION_NO_FINALIZADA: 'inspección sin terminar',
  INSPECCION_RECHAZADA: 'inspección rechazada',
};
/** Pendientes que no se completan con un dato sino con una decisión de la persona. */
export const TIPOS_DECISION: ReadonlySet<TipoPendiente> = new Set<TipoPendiente>([
  'VARIOS_CONVENIOS_VIGENTES', 'DOCUMENTO_CON_VARIAS_SOLICITUDES', 'INSPECCION_RECHAZADA',
]);

export type ClaseChip = 'ok' | 'falta' | 'neutro' | 'excepcion';
export interface Chip {
  clase: ClaseChip;
  texto: string;
}

/** Estado de una operación en la bandeja. Siempre lleva texto: el color solo refuerza. */
export function estadoOperacion(o: Pick<OperacionResumenDto, 'estadoOperacion' | 'facturable' | 'pendientes' | 'ofaId' | 'estadoOfa'>): Chip {
  if (o.estadoOperacion === 'ANULADA') return { clase: 'excepcion', texto: 'Anulada' };
  if (!o.facturable) return { clase: 'excepcion', texto: 'No facturable' };
  if (o.pendientes.length) {
    const primero = o.pendientes[0];
    const prefijo = TIPOS_DECISION.has(primero) ? 'Revisar' : 'Falta';
    const mas = o.pendientes.length > 1 ? ` (+${o.pendientes.length - 1})` : '';
    return { clase: 'falta', texto: `${prefijo}: ${ETIQUETA_PENDIENTE[primero]}${mas}` };
  }
  if (o.ofaId) return { clase: 'ok', texto: o.estadoOfa === 'VALIDADA' ? 'Validada' : 'Calculada' };
  return { clase: 'neutro', texto: 'Sin calcular' };
}

export const textoRetiro = (r: TipoRetiro | null): string => (r === 'DIRECTO' ? 'Directo' : r === 'INDIRECTO' ? 'Indirecto' : 'Sin informar');
export const textoSentido = (s: Sentido | null): string => (s === 'IMPORTACION' ? 'Importación' : s === 'EXPORTACION' ? 'Exportación' : 'Sin informar');
export const textoGrupo: Record<GrupoTerminal, string> = { STI: 'STI', DP_WORLD: 'DP World', OTRO: 'Otro', SIN_MAPEAR: 'sin asignar' };
export const textoMomento = (m: MomentoFacturacion | null): string =>
  m === 'EN_VISACION' ? 'En la visación' : m === 'POST_DESPACHO' ? 'Después del despacho' : '—';

/** `CONTENEDOR-INDIRECTO-CON_CONVENIO` → `Contenedor · indirecto · con convenio`. */
export function textoRuta(ruta: string | null): string {
  if (!ruta) return 'Sin clasificar';
  const partes = ruta.split('-').map(p => p.replace(/_/g, ' ').toLowerCase());
  return partes.map((p, i) => (i === 0 ? p.charAt(0).toUpperCase() + p.slice(1) : p)).join(' · ');
}

export const textoResultado: Record<ResultadoInspeccion, string> = {
  APROBADA: 'Aprobada', RECHAZADA_PARCIAL: 'Rechazada parcial', RECHAZADA_TOTAL: 'Rechazada total', VISADO_LIBERADO: 'Visado liberado',
};

/** Unidad de cobro en palabras, para acompañar a la cantidad. */
export const textoUnidad: Record<UnidadCobro, string> = {
  BL: 'BL', DUS: 'DUS', CTR: 'contenedor', CTR_DIA: 'días', TON: 'ton', M3: 'm³', CAMION: 'camión', PALLET: 'pallet', UNIDAD: 'unidad',
};
export const unidad = (u: UnidadCobro | null | undefined): string => (u ? (textoUnidad[u] ?? u) : '');

/** De dónde sale la tarifa de una línea ("Tarifa según"). */
export function fuenteLinea(l: LineaOfaDto): Chip {
  if (l.sinTarifa) return { clase: 'falta', texto: 'Sin tarifa' };
  if (l.memoria.ajusteManual) return { clase: 'neutro', texto: 'Ajuste manual' };
  if (l.origen === 'AGREGADA_MANUAL') return { clase: 'neutro', texto: 'Agregado a mano' };
  if (l.fuenteTarifa === 'CONVENIO') return { clase: 'neutro', texto: 'Convenio' };
  if (l.fuenteTarifa === 'REGLA_0_DP_WORLD') return { clase: 'neutro', texto: 'Regla DP World (en cero)' };
  if (l.fuenteTarifa === 'MANUAL') return { clase: 'neutro', texto: 'Tarifa indicada' };
  return l.esExcepcion ? { clase: 'excepcion', texto: 'Pública (excepción)' } : { clase: 'neutro', texto: 'Pública' };
}

/** Lo que impide validar una línea, o null. */
export function problemaLinea(l: LineaOfaDto): string | null {
  if (l.sinTarifa) return 'Sin tarifa';
  if (l.sinTipoCambio) return 'Sin tipo de cambio';
  if (!l.articuloSap) return 'Sin artículo SAP';
  return null;
}

/** Convenio de la operación, con las frases que fija la tarea 3.1a. */
export function textoConvenio(o: OperacionDto): string {
  const c = o.convenio;
  return c
    ? `Convenio encontrado: versión ${c.numero} (vigencia ${c.vigenciaDesde} a ${c.vigenciaHasta ?? 'sin término'})`
    : `Sin convenio para el RUT ${o.rut ?? '—'}: se cobra con tarifa pública.`;
}

/** Días de almacenaje de todos los contenedores, con los que van bajo aforo. */
export function textoDiasAlmacenaje(o: OperacionDto): string {
  const con = o.contenedores.filter(c => c.diasAlmacenaje !== null);
  if (!con.length) return '—';
  const total = con.reduce((s, c) => s + (c.diasAlmacenaje ?? 0), 0);
  const aforo = o.contenedores.reduce((s, c) => s + (c.diasBajoAforo ?? 0), 0);
  return aforo > 0 ? `${total} (${aforo} bajo aforo)` : `${total}`;
}
