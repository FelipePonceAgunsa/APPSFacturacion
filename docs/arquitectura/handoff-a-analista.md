# Handoff Arquitecto → Analista de Negocio

Fecha: 2026-10-26
De: Arquitecto de Solución (`arquitecto-satelite`)
Para: Analista de Negocio (levantamiento)
Propósito: devolver al analista las **decisiones de diseño (TO-BE)** que impactan su documentación del AS-IS/TO-BE, y señalar lo que le corresponde **confirmar o cerrar con negocio**. Es el espejo del `handoff-arquitecto.md` que entregó el analista.

> División de responsabilidad: el analista define el **QUÉ** (negocio, AS-IS). El arquitecto define el **CÓMO** (TO-BE técnico). Este handoff **no cambia el AS-IS** (lo que pasa hoy sigue siendo verdad); informa decisiones de diseño que el analista debe reflejar en su TO-BE y marca lo que necesita cierre de negocio.

---

## 1. Decisión que más impacta tu levantamiento: flujo de convenios (Paso 2)

**Decisión tomada (ADR-012, Opción B, confirmada por el usuario 2026-10-26):** el convenio pasa a **cargarse directamente en el Satélite** por el comercial; **SharePoint deja de ser el flujo de trabajo** y queda como archivo histórico de solo lectura.

### Qué NO cambia (tu AS-IS sigue válido)
- Hoy los comerciales trabajan en SharePoint, con versionamiento por nombre de archivo y sin estado vigente/vencido confiable. Eso que levantaste (RN-043, BR-001, BR-011, BR-019) **sigue siendo el AS-IS correcto**. No lo toques.

### Qué sí debes reflejar en el TO-BE del Paso 2 (`paso-02-gestionar-convenios.md`)
1. **Sección 3 (TO-BE):** precisar que la fuente estructurada de convenios se alimenta por **carga directa del comercial en el Satélite** (ya no "se apoya en SharePoint"). SharePoint queda como histórico.
2. **Sección 9 (actores):** la responsabilidad futura del **Comercial** pasa de "custodia el convenio en SharePoint" a "**carga y mantiene el convenio vigente directamente en el Satélite**" (lo lee la IA, lo confirma una persona).
3. **Sección 7 (excepciones) / 10 (brechas):** las brechas BR-001, BR-011, BR-019 quedan **resueltas por diseño** en el TO-BE (fuente única, versión y vigencia explícitas, autoría auditable). Mantenlas en el AS-IS, pero anota que el TO-BE las cierra vía ADR-012.
4. **Migración inicial (nuevo):** agregar como actividad de arranque que los **convenios vigentes de SharePoint se cargan una vez** al Satélite (carga en lote asistida por IA + confirmación humana). Después, SharePoint no vuelve a ser origen.

### Resto del flujo de convenios (ya alineado, solo para tu referencia)
- El convenio vive en el Satélite, versionado, con extracción IA + **confirmación humana** antes de quedar vigente (RN-071, ADR-007).
- La IA **nunca fija un monto**; solo propone. El comercial/analista confirma.
- Multi-convenio por BL: el Satélite propone el **más antiguo** y permite override registrado (RN-018).

---

## 2. Lo que te corresponde confirmar o cerrar con negocio

Estas son de tu cancha (negocio); nuestra decisión de diseño las destapa pero no las resuelve:

| Tema | Qué falta | Referencia |
|------|-----------|------------|
| **Migración de convenios de SharePoint** | Confirmar con comercial el **universo real** a migrar (cuántos convenios vigentes, de qué clientes) y quién valida cada carga inicial. Hoy la cifra es ~167 clientes / 539 archivos, pero no todos vigentes. | BR-001, BR-011 |
| **Gestión del cambio del comercial** | El comercial deja de usar SharePoint y pasa a cargar en el Satélite. Confirmar con negocio el acompañamiento/capacitación y quién lidera el cambio. | ADR-012 (consecuencias) |
| **STI vs DPW (abierta del levantamiento)** | Sigue siendo la única regla de **monto** de negocio sin confirmar. No depende de convenios, pero está pendiente contigo/ Francisco. | PQ-006 / BR-007 |
| **Mecánica fina del DUS** | Confirmar cantidad de DUS por operación (relevante si expo entra en 1ª etapa). | BR-030 |

---

## 3. Aclaración importante: los convenios NO van a SAP

Para evitar un malentendido frecuente:
- **De SAP solo se leen las tarifas públicas / kits spot** (clientes **sin** convenio) y el maestro de artículos (ADR-003).
- **Los convenios NO están ni estarán en SAP.** Viven en el Satélite (ADR-012). SharePoint es el origen histórico, no SAP.
- Por eso, en tu documentación, "tarifa pública = SAP" y "convenio = Satélite" son fuentes **distintas**; conviene que el Paso 2 y el Paso 3 lo dejen explícito para no confundir a quien lo lea.

---

## 4. Dónde está el detalle (por si lo necesitas)
Repositorio de arquitectura (`docs/arquitectura/`):
- `adr/ADR-012-flujo-convenios-carga-directa.md` — la decisión completa con alternativas.
- `adr/ADR-007-convenios-versionados-ia-asistida.md` — convenio versionado + IA + confirmación.
- `adr/ADR-003-lectura-tarifas-sap.md` — qué sí se lee de SAP (tarifas públicas, no convenios).
- `modelo-datos.md` — tablas `convenio`, `version_convenio`, `servicio_convenio`, con `origen` y `documento_s3`.

---

## 5. Resumen de acciones para el analista
1. Actualizar el TO-BE del `paso-02-gestionar-convenios.md` (secciones 3, 7, 9, 10) según §1 de este handoff.
2. Agregar la actividad de **migración inicial** de convenios.
3. Marcar BR-001/BR-011/BR-019 como "resueltas por diseño en TO-BE (ADR-012)", sin borrarlas del AS-IS.
4. Cerrar con negocio los puntos de §2 (universo de migración, gestión del cambio, STI/DPW, DUS).
5. Dejar explícito en Paso 2 y Paso 3 que convenio (Satélite) y tarifa pública (SAP) son fuentes distintas.
