import { CompletarOperacionRequest, PendienteDto, TipoPendiente, TipoRetiro, Sentido } from '../../api/model/models';
/** Texto de cada pendiente para la bandeja y el detalle (tarea 3.1a). */
export const TEXTO_PENDIENTE: Record<TipoPendiente, string> = {
  TIPO_RETIRO: 'Falta el tipo de retiro',
  SENTIDO: 'Falta el sentido',
  TERMINAL_SIN_MAPEAR: 'Terminal sin mapear',
  CANTIDAD_DUS: 'Falta la cantidad de DUS',
  CANTIDAD_SERVICIO: 'Servicio sin cantidad',
  DIAS_AFORO: 'Faltan los días bajo aforo',
  FECHA_DESPACHO: 'Falta la fecha de despacho',
  VARIOS_CONVENIOS_VIGENTES: 'Varios convenios vigentes',
  DOCUMENTO_CON_VARIAS_SOLICITUDES: 'Documento con varias solicitudes',
  INSPECCION_NO_FINALIZADA: 'Inspección no finalizada',
  INSPECCION_RECHAZADA: 'Inspección rechazada',
};
export interface OpcionDecision {
  valor: string;
  texto: string;
}
/** Opciones de decisión de un pendiente que la exige (PATCH decisiones[]). */
export function opcionesDecision(p: PendienteDto): OpcionDecision[] {
  switch (p.tipo) {
    case 'VARIOS_CONVENIOS_VIGENTES': {
      const detalle = (p.detalle ?? {}) as { candidatos?: { convenioId: string; numero: number; creadoEn?: string }[] };
      return (detalle.candidatos ?? []).map(c => ({
        valor: c.convenioId,
        texto: `Convenio ${c.convenioId.slice(0, 8)} · versión ${c.numero}${c.creadoEn ? ' · creado ' + String(c.creadoEn).slice(0, 10) : ''}`,
      }));
    }
    case 'DOCUMENTO_CON_VARIAS_SOLICITUDES':
      return [{ valor: 'FACTURAR', texto: 'Facturar esta operación' }, { valor: 'NO_FACTURAR', texto: 'No facturar esta operación' }];
    case 'INSPECCION_RECHAZADA':
      return [{ valor: 'CONTINUAR', texto: 'Continuar: el rechazo no cambia el cobro' }, { valor: 'NO_FACTURAR', texto: 'No facturar esta operación' }];
    default:
      return [];
  }
}
/** Datos del formulario "Completar datos" (F2). Un campo vacío no cambia. */
export interface FormularioCompletar {
  motivo: string;
  tipoRetiro: TipoRetiro | null;
  sentido: Sentido | null;
  cantidadDus: number | null;
  contenedor: string | null;
  fechaDespacho: string | null;
  diasAforo: number | null;
  cantidades: Record<number, number | null>;
  decisiones: Partial<Record<TipoPendiente, string>>;
}
export function formularioVacio(): FormularioCompletar {
  return {
    motivo: '', tipoRetiro: null, sentido: null, cantidadDus: null, contenedor: null, fechaDespacho: null,
    diasAforo: null, cantidades: {}, decisiones: {},
  };
}
/** Arma el cuerpo del PATCH /operaciones/{id} solo con lo informado. La fecha local se envía como ISO (UTC). */
export function armarSolicitud(f: FormularioCompletar): CompletarOperacionRequest {
  const vacio = (v: unknown) => v === null || v === undefined || v === '';
  const cantidades = Object.entries(f.cantidades)
    .filter(([, c]) => !vacio(c))
    .map(([id, c]) => ({ tipoServicioId: Number(id), cantidad: Number(c) }));
  const decisiones = Object.entries(f.decisiones)
    .filter(([, v]) => !vacio(v))
    .map(([tipo, valor]) => ({ tipo: tipo as TipoPendiente, valor: valor as string }));
  return {
    motivo: f.motivo.trim(),
    tipoRetiro: f.tipoRetiro,
    sentido: f.sentido,
    cantidadDus: vacio(f.cantidadDus) ? null : Number(f.cantidadDus),
    fechaDespacho: vacio(f.fechaDespacho) ? null : new Date(f.fechaDespacho as string).toISOString(),
    contenedor: vacio(f.contenedor) ? null : f.contenedor,
    diasAforo: vacio(f.diasAforo) ? null : Number(f.diasAforo),
    cantidadesServicio: cantidades.length ? cantidades : null,
    decisiones: decisiones.length ? decisiones : null,
  };
}
/** ¿La persona informó algo además del motivo? Evita guardar un PATCH vacío. */
export function hayCambios(f: FormularioCompletar): boolean {
  const s = armarSolicitud(f);
  return !!(s.tipoRetiro || s.sentido || s.cantidadDus !== null || s.fechaDespacho || s.diasAforo !== null || s.cantidadesServicio || s.decisiones);
}
/** IDs de AEXSA como texto (visado virtual: uno; presencial: varios). */
export function textoIds(ids: number[] | null | undefined): string {
  return (ids ?? []).join(', ');
}
