import { LineaOfaDto } from '../../api/model/models';
import { fechaHora, formatoNumero, unidad } from './operaciones-vista';

/**
 * "¿Por qué?" de una línea, en lenguaje común (maqueta, decisión 2). Se arma solo con la memoria de cálculo
 * que guardó el motor, así que explica exactamente lo que se hizo y sirve para comparar con SAP.
 * Si el motor agrega una regla nueva que aquí no se conoce, la línea sigue mostrando el detalle técnico.
 */
type Datos = Record<string, string>;

const n = (v: string | undefined): number | null => (v === undefined || v === '' ? null : Number(v));
const f = (v: number | null | undefined): string => formatoNumero(v, 2);

export function explicarLinea(l: LineaOfaDto): string {
  const reglas = new Map<string, Datos>(l.memoria.reglasAplicadas.map(r => [r.regla, r.detalle as Datos]));
  const frases: string[] = [];

  const manual = reglas.get('AGREGADO_MANUAL');
  if (manual) {
    frases.push(`Agregado por ${manual['usuario']} el ${fechaHora(manual['fecha'])}. Motivo: ${manual['motivo']}. No viene del convenio ni de AEXSA.`);
  }

  const r0 = reglas.get('R0_COMPOSICION_DIRECTO');
  if (r0) frases.push(fraseDirecto(r0));

  const r1 = reglas.get('R1_RESOLUCION_TARIFA');
  if (r1) {
    const frase = fraseTarifa(r1, !!r0);
    if (frase) frases.push(frase);
  }

  const r4 = reglas.get('R4_ALMACENAJE');
  const r2 = reglas.get('R2_CANTIDAD');
  if (r4) frases.push(fraseAlmacenaje(r4));
  else if (r2) frases.push(fraseCantidad(r2, l));

  const r3 = reglas.get('R3_CONVERSION_MONEDA');
  if (r3 && l.moneda !== 'CLP') frases.push(fraseMoneda(r3, l));

  const r5 = reglas.get('R5_RECARGO');
  if (r5) frases.push(`Recargo ${r5['tipo']} de ${r5['porcentaje']}% sobre ${f(n(r5['base']))}: ${f(n(r5['recargo']))} CLP.`);

  const ajuste = l.memoria.ajusteManual;
  const base = ajuste ? ajuste.valorAnterior : l.montoClp;
  if (!r5) frases.push(`${ajuste ? 'El cálculo daba' : 'Cuenta:'} ${cuenta(l, r3, base)}.`);
  if (ajuste) {
    frases.push(`${ajuste.usuario} lo ajustó el ${fechaHora(ajuste.fecha)} a ${f(ajuste.valorNuevo)} CLP. Motivo: ${ajuste.motivo}.`);
  }
  return frases.join(' ');
}

function fraseDirecto(r0: Datos): string {
  const intro = 'Retiro directo sin convenio: se cobran tres conceptos (visación, security fee y refacturación).';
  if (r0['concepto'] !== 'REFACTURACION') {
    const descartados = (r0['serviciosDescartados'] ?? '') !== '' && r0['concepto'] === 'VISACION' ? ' Los demás servicios prestados no se cobran en un directo.' : '';
    return intro + descartados;
  }
  if (r0['refacturacionEnCero'] === 'true') return `${intro} Terminal del grupo DP World: la refacturación va en cero.`;
  return r0['terminal'] === 'STI'
    ? `${intro} Terminal del grupo STI: la refacturación se cobra con valor.`
    : `${intro} Terminal del grupo ${r0['terminal']}: la refacturación se cobra con valor, como en STI.`;
}

function fraseTarifa(r1: Datos, hayRegla0: boolean): string | null {
  switch (r1['fuente']) {
    case 'CONVENIO':
      return `Tarifa del convenio vigente (versión ${r1['versionConvenio']}).`;
    case 'PUBLICA':
      return r1['esExcepcion'] === 'true'
        ? `El convenio no tiene este servicio: se usó la tarifa pública vigente (lista ${r1['lista']}) y se marcó como excepción.`
        : `Cliente sin convenio: se usa la tarifa pública (lista ${r1['lista']}).`;
    case 'MANUAL':
      return 'Tarifa indicada por la persona al agregar el servicio.';
    case 'SIN_TARIFA':
      return 'No hay tarifa para este servicio, ni en el convenio ni en la lista pública. Ajusta el monto o carga la tarifa en el catálogo.';
    default:
      return hayRegla0 ? null : `Tarifa según ${r1['fuente']}.`;
  }
}

function fraseCantidad(r2: Datos, l: LineaOfaDto): string {
  switch (r2['driver']) {
    case 'CANTIDAD_DUS':
      return `Se cobra por DUS: ${f(n(r2['cantidadDus']))}.`;
    case 'MAYOR_M3_TON':
      return `Se cobra por el mayor entre ${f(n(r2['metrosCubicos']))} m³ y ${f(n(r2['toneladas']))} toneladas: ${f(n(r2['cantidad']))}.`;
    default:
      return `Cantidad: ${f(l.cantidad)} ${unidad(l.unidad)}.`.replace(/ \.$/, '.');
  }
}

function fraseAlmacenaje(r4: Datos): string {
  const porContenedor = (r4['porContenedor'] ?? '').split(';').filter(x => x !== '');
  if (porContenedor.length > 1) {
    const detalle = porContenedor.map(p => {
      const [num, resto] = p.split(':');
      const [calc, libres, aforo] = resto.split('=')[0].split('-');
      return `${num}: ${calc} días − ${libres} libres − ${aforo} bajo aforo`;
    });
    return `Almacenaje por contenedor (${detalle.join(' · ')}). En total ${r4['diasCobrados']} días a cobrar.`;
  }
  const libres = n(r4['diasLibres']) ?? 0;
  const aforo = n(r4['diasBajoAforo']) ?? 0;
  const menos = [libres > 0 ? `${libres} libres del convenio` : '', aforo > 0 ? `${aforo} bajo aforo (no se cobran)` : ''].filter(x => x !== '');
  return `${r4['diasCalculados']} días de almacenaje en total${menos.length ? ', menos ' + menos.join(' y menos ') : ''} = ${r4['diasCobrados']} días a cobrar.`;
}

function fraseMoneda(r3: Datos, l: LineaOfaDto): string {
  if (r3['sinTipoCambio'] === 'true') {
    return `La tarifa está en ${l.moneda} y no hay tipo de cambio para la fecha de la operación: el monto queda en 0 hasta que se cargue.`;
  }
  const nombre = l.moneda === 'UF' ? 'UF' : 'Dólar';
  const dia = f(n(r3['tipoCambioDia']));
  const aplicado = f(n(r3['tipoCambioAplicado']));
  return r3['pisoActivado'] === 'true'
    ? `Tarifa en ${l.moneda}. ${nombre} del día ${dia}; el convenio fija un mínimo de ${f(n(r3['tcMinimo']))}, así que se usa ${aplicado}.`
    : `Tarifa en ${l.moneda}. ${nombre} del día ${dia}: se usa ${aplicado}.`;
}

/** La cuenta en una línea: `1 × 40 USD × 940 = 37.600 CLP`. */
function cuenta(l: LineaOfaDto, r3: Datos | undefined, monto: number): string {
  const tc = l.moneda !== 'CLP' ? n(r3?.['tipoCambioAplicado']) : null;
  const tarifa = l.moneda === 'CLP' ? f(l.tarifa) : `${f(l.tarifa)} ${l.moneda}`;
  return `${f(l.cantidad)} × ${tarifa}${tc ? ` × ${f(tc)}` : ''} = ${f(monto)} CLP`;
}
