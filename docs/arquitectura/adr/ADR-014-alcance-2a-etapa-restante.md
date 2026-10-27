# ADR-014 — Alcance restante de 2ª etapa (diseño anticipado)

Estado: **Aceptado como diseño anticipado** · Construcción diferida a **2ª etapa**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

> Propósito: cerrar el diseño anticipado de las piezas de 2ª etapa que faltaban tras ADR-013 (carga suelta/letra tarifaria). Igual que ese ADR, se diseñan **ahora** para verificar que **no obligan a rediseñar la 1ª etapa**. Son tres piezas acotadas; se agrupan aquí.

## Contexto
Fuera del alcance de la 1ª etapa (REQ §1.3): además de la carga suelta (ADR-013), quedan **expo por DUS a volumen** (RN-047, BR-030), **licitaciones grandes** (PQ-024) y la **gestión contable de notas de crédito y devoluciones** (RN-069, RN-034). En 1ª etapa las notas de crédito y devoluciones **se registran, no se calculan**.

---

## Pieza 1 — Exportación por DUS a volumen (RN-047, BR-030)

**Qué es:** en expo la unidad de cobro es el DUS; se cobra por cantidad de DUS presentados. Gran parte de la expo es carga suelta (va con ADR-013), pero el **driver DUS** aplica a cualquier operación de exportación.

**Decisión:** el DUS como driver de cobro **ya está modelado en 1ª etapa** (ADR-011: `operacion.cantidad_dus`, `UnidadCobro.DUS`, Regla 2 del motor). La 2ª etapa no agrega modelo nuevo; agrega:
1. La **captura confiable de la cantidad de DUS por operación** (hoy AEXSA solo tiene `REFDUS` aduanero, sin cantidad — BR-030). Fuente a definir con negocio/AEXSA.
2. El **volumen** de expo suelta (ligado a ADR-013).

**No bloquea la 1ª etapa:** el modelo y el motor ya soportan DUS. Solo falta el dato de cantidad (pendiente de negocio BR-030), que no cambia el esquema.

---

## Pieza 2 — Licitaciones grandes (PQ-024)

**Qué es:** usan el **mismo formato de convenio** (carta Word), pero con un análisis de negociación por volumen/costos que hoy es manual. No requieren tratamiento estructural distinto (PQ-024 resuelta en reunión 2).

**Decisión:** una licitación se modela como un **convenio** (ADR-007/012) — misma entidad `convenio`/`version_convenio`/`servicio_convenio`. Lo propio de la licitación es:
1. Una **marca/tipo** en el convenio (`tipo = LICITACION`) para distinguirla en reportes.
2. El **análisis de negociación** (volumen, costos) se adjunta como documento de respaldo (S3), **ingreso manual**; no se calcula en el Satélite.

**No bloquea la 1ª etapa:** reutiliza el modelo de convenios tal cual; solo añade un atributo `tipo` (aditivo).

---

## Pieza 3 — Notas de crédito y devoluciones: gestión contable (RN-069, RN-034)

**Qué es:** en 1ª etapa, cuando SAP emite una nota de crédito (DTE 61/112) o hay saldo a favor, el Satélite **lo registra** (asocia a la operación, refleja el saldo) pero **no calcula** la gestión contable. La devolución la autoriza Jefatura de Administración y la ejecuta Tesorería de AGUNSA, a quien efectuó el pago (RN-069).

**Decisión (2ª etapa):** ampliar de "registrar" a "**gestionar**":
1. **Flujo de devolución:** estado del saldo a favor (detectado → consultado al cliente → autorizado → devuelto), con quién autoriza y quién ejecuta (RN-069).
2. **Devolución a quien pagó**, no necesariamente al cliente de la factura (RN-069) — se apoya en `pago.pagador_rut` que **ya existe** en el modelo (ADR-010).
3. **Asociación nota de crédito ↔ operación/factura original** — `factura` ya admite varias por operación y el folio une la cadena (ADR-010).

**No bloquea la 1ª etapa:** el modelo de datos ya guarda `pago.pagador_rut`, el tipo de DTE (incluye 61/112) y la asociación por folio. La 2ª etapa agrega el **flujo de estados de devolución**, que es aditivo (una entidad de proceso), sin tocar lo existente.

---

## Validación global: la 1ª etapa no se rediseña
| Pieza 2ª etapa | ¿Qué agrega? | ¿Toca la 1ª etapa? |
|----------------|--------------|--------------------|
| Carga suelta / letra tarifaria (ADR-013) | Tablas `letra_tarifaria`, `servicio_letra`, `bl`; tercera fuente de tarifa | No — aditivo |
| Expo DUS a volumen | Captura de `cantidad_dus` (dato, no esquema) | No — ya modelado |
| Licitaciones | Atributo `tipo=LICITACION` en convenio | No — aditivo |
| Notas de crédito / devoluciones | Flujo de estados de devolución | No — aditivo; `pagador_rut` ya existe |

**Conclusión:** todo el alcance de 2ª etapa se incorpora **por extensión aditiva** sobre el diseño de 1ª etapa. No hay rediseño. El diseño de 1ª etapa queda validado como base escalable para la 2ª.

## Alternativas consideradas
1. **No anticipar el diseño de 2ª etapa.** Descartada (criterio del usuario 2026-10-26): es barato validar en papel ahora que la 1ª etapa no se cierra; caro descubrirlo con código construido.
2. **Modelar licitaciones como entidad separada del convenio.** Descartada: PQ-024 confirma que usan el mismo formato; duplicar sería innecesario.
3. **Construir el flujo de devoluciones en 1ª etapa.** Descartada por alcance (REQ §1.3): en 1ª etapa se registra, no se calcula.

## Reglas / requerimientos relacionados
RN-034, RN-047, RN-069, BR-030, PQ-024. Reutiliza ADR-007, ADR-010, ADR-011, ADR-012, ADR-013. Fuera de alcance 1ª etapa (REQ §1.3).
