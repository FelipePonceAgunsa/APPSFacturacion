import { LineaOfaDto, ReglaAplicadaDto } from '../../api/model/models';
/** Nombre legible de cada regla de la memoria de cálculo (motor-tarifas-detalle.md §1). */
export const NOMBRE_REGLA: Record<string, string> = {
  R0_COMPOSICION_DIRECTO: 'Regla 0 · composición del directo sin convenio',
  R1_RESOLUCION_TARIFA: 'Regla 1 · tarifa (convenio → pública)',
  R2_CANTIDAD: 'Regla 2 · cantidad por unidad',
  R3_CONVERSION_MONEDA: 'Regla 3 · conversión de moneda',
  R4_ALMACENAJE: 'Regla 4 · almacenaje (días libres y bajo aforo)',
  R4_DIAS_AFORO: 'Días bajo aforo desde la inspección [supuesto por confirmar]',
  R5_RECARGO: 'Regla 5 · recargo',
  R7_AJUSTE_MANUAL: 'Regla 7 · ajuste manual de monto',
  AGREGADO_MANUAL: 'Ítem agregado a mano',
  COPIA_RECALCULO: 'Copiado de la versión de cálculo anterior',
};
/** Datos de una regla como "clave: valor", sin los vacíos. */
export function textoDetalle(r: ReglaAplicadaDto): string {
  return Object.entries(r.detalle)
    .filter(([, v]) => v !== '')
    .map(([k, v]) => `${k}: ${v}`)
    .join(' · ');
}
/** Lo que impide validar una línea, o null. */
export function problemaLinea(l: LineaOfaDto): string | null {
  if (l.sinTarifa) return 'Sin tarifa';
  if (l.sinTipoCambio) return 'Sin tipo de cambio';
  if (!l.articuloSap) return 'Sin artículo SAP';
  return null;
}
