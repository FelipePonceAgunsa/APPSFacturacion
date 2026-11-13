import { ServicioConvenioDto, VersionConvenioDto } from '../../api/model/models';

/** Fecha de hoy como AAAA-MM-DD (hora local), el mismo formato de las vigencias. */
export function hoyIso(hoy = new Date()): string {
  const m = String(hoy.getMonth() + 1).padStart(2, '0');
  const d = String(hoy.getDate()).padStart(2, '0');
  return `${hoy.getFullYear()}-${m}-${d}`;
}

/** Días de calendario desde hoy hasta una fecha AAAA-MM-DD (negativo si ya pasó); null sin fecha. */
export function diasHasta(fecha: string | null | undefined, hoy = new Date()): number | null {
  if (!fecha) return null;
  const [a, m, d] = fecha.split('-').map(Number);
  const destino = Date.UTC(a, m - 1, d);
  const origen = Date.UTC(hoy.getFullYear(), hoy.getMonth(), hoy.getDate());
  return Math.round((destino - origen) / 86_400_000);
}

const texto = (s: ServicioConvenioDto) => `${s.tarifa} ${s.moneda}`;

/**
 * Diferencias de una versión en BORRADOR contra la VIGENTE (pantalla C4).
 * Sin versión vigente, la lista dice que es la primera versión.
 */
export function diferenciasVersion(borrador: VersionConvenioDto, vigente: VersionConvenioDto | null): string[] {
  if (!vigente) {
    return ['Primera versión vigente del convenio.'];
  }
  const cambios: string[] = [];
  const campo = (nombre: string, antes: unknown, despues: unknown) => {
    if ((antes ?? null) !== (despues ?? null)) {
      cambios.push(`${nombre}: ${antes ?? '—'} → ${despues ?? '—'}`);
    }
  };
  campo('Vigencia desde', vigente.vigenciaDesde, borrador.vigenciaDesde);
  campo('Vigencia hasta', vigente.vigenciaHasta, borrador.vigenciaHasta);
  campo('TC mínimo', vigente.tcMinimo, borrador.tcMinimo);
  campo('Días libres', vigente.diasLibres, borrador.diasLibres);

  const antes = new Map(vigente.servicios.map(s => [s.tipoServicioId, s]));
  const despues = new Map(borrador.servicios.map(s => [s.tipoServicioId, s]));
  const nombre = (s: ServicioConvenioDto) => `${s.tipoServicioId} ${s.descripcionServicio ?? ''}`.trim();
  for (const [id, s] of despues) {
    const previo = antes.get(id);
    if (!previo) {
      cambios.push(`Servicio nuevo ${nombre(s)}: ${texto(s)}`);
    } else if (previo.tarifa !== s.tarifa || previo.moneda !== s.moneda) {
      cambios.push(`Servicio ${nombre(s)}: ${texto(previo)} → ${texto(s)}`);
    }
  }
  for (const [id, s] of antes) {
    if (!despues.has(id)) {
      cambios.push(`Servicio quitado ${nombre(s)} (usará la tarifa pública como excepción)`);
    }
  }
  return cambios.length ? cambios : ['Sin diferencias con la versión vigente.'];
}
