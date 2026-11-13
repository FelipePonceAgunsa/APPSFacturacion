// Enumeraciones del dominio. Cada nombre es igual al valor del CHECK en la base
// (db/init/01-esquema.sql + 02-ajustes-etapa1.sql); se guardan como texto.
#pragma warning disable CA1707 // Los nombres reproducen los valores de la base (DP_WORLD, SIN_MAPEAR...)
namespace Satelite.Dominio.Comun;

public enum CondicionPago { CONTADO, CREDITO }

/// <summary>Unidad de cobro normalizada (BR-014, RN-048).</summary>
public enum UnidadCobro { BL, DUS, CTR, CTR_DIA, TON, M3, CAMION, PALLET, UNIDAD }

public enum EstadoVersionConvenio { BORRADOR, VIGENTE, VENCIDA, REEMPLAZADA }

public enum OrigenVersionConvenio { IA_EXTRAIDO, MANUAL, COMPLEMENTO_CORREO }

/// <summary>Moneda de un monto. Convenios: solo CLP o USD. Tipo de cambio: USD o UF.</summary>
public enum Moneda { CLP, USD, UF }

public enum TipoCarga { CONTENEDOR, BULTO }

public enum Sentido { IMPORTACION, EXPORTACION }

public enum TipoRetiro { DIRECTO, INDIRECTO }

/// <summary>Grupo del terminal resuelto con terminal_mapa (RN-079, ADR-015).</summary>
public enum GrupoTerminal { STI, DP_WORLD, OTRO, SIN_MAPEAR }

public enum EstadoOperacion { ACTIVA, ANULADA }

public enum TipoClienteFacturador { EMISOR_BL, CLIENTE, GENERADOR_NEGOCIO, CONSIGNATARIO }

public enum MomentoFacturacion { EN_VISACION, POST_DESPACHO }

/// <summary>Origen de la operación en AEXSA (contrato v3.0, [E1-6]).</summary>
public enum EntidadAexsa { VISADO_VIRTUAL, VISADO_PRESENCIAL }

public enum OrigenDato { AEXSA, LIQUIDACION_PUERTO }

public enum TipoEvento { VISADO_APROBADO, OPERACION_ACTUALIZADA }

public enum ResultadoEvento { APLICADO, DUPLICADO, RECHAZADO }

public enum TipoPendiente
{
    TIPO_RETIRO,
    SENTIDO,
    TERMINAL_SIN_MAPEAR,
    CANTIDAD_DUS,
    CANTIDAD_SERVICIO,
    DIAS_AFORO,
    FECHA_DESPACHO,
    VARIOS_CONVENIOS_VIGENTES,
    DOCUMENTO_CON_VARIAS_SOLICITUDES,
    INSPECCION_NO_FINALIZADA,
    INSPECCION_RECHAZADA,
}

public enum ResolucionPendiente { DATO_COMPLETADO, DECISION, YA_NO_APLICA }
/// <summary>Resultado de una inspección (contrato v3.0, nota 15). Se guarda en el jsonb contenedor_operacion.inspecciones.</summary>
public enum ResultadoInspeccion { APROBADA, RECHAZADA_PARCIAL, RECHAZADA_TOTAL, VISADO_LIBERADO }

/// <summary>Grupo de un tipo de inspección de AEXSA ([E1-7]). AFORO_ADUANA: sus días no se cobran (RN-052).</summary>
public enum GrupoInspeccion { AFORO_ADUANA, SAG, OTRA, SIN_MAPEAR }

public enum OrigenCatalogo { ARCHIVO, API }

public enum TipoImportacion { ARTICULOS, LISTAS_PRECIOS, KITS, TIPO_CAMBIO, SOCIOS_NEGOCIO, COMPLETO }

public enum EstadoImportacion { PENDIENTE_CONFIRMACION, RECHAZADA, CONFIRMADA }

public enum OrigenVinculo { SUGERENCIA, MANUAL }

/// <summary>Fuente del tipo de cambio ([E1-3]). Gana el valor de SAP (API o ARCHIVO).</summary>
public enum FuenteTipoCambio { API, ARCHIVO, MANUAL }

public enum TipoOfa { PRINCIPAL, COMPLEMENTARIA }

public enum EstadoOfa { BORRADOR, VALIDADA, ENVIADA, EMITIDA, EMITIDA_SIN_CONFIRMACION, CONCILIADA, INFORMADA_AEXSA }

public enum RolCobro { CONSIGNATARIO, FORWARDER, EMBARCADOR }

public enum OrigenLinea { CALCULADA, AGREGADA_MANUAL, REGLA_CONFIGURABLE }

public enum OrigenOmision { CRUCE_DETERMINISTICO, PROPUESTA_IA }

public enum DecisionOmision { INCLUIDA, DESCARTADA, COMPLEMENTARIA }

public enum TipoAlerta
{
    CONVENIO_POR_VENCER,
    ANULACION_CON_OFA_ENVIADA,
    COMPLEMENTARIA_TARDIA,
    EMITIDA_SIN_CONFIRMACION,
    RETORNO_AEXSA_FALLIDO,
    IMPAGO,
    CATALOGO_DESACTUALIZADO,
}

public enum EstadoAlerta { ABIERTA, ATENDIDA, DESCARTADA }

public enum EstadoPago { PAGADA, PENDIENTE, PARCIAL }

public enum EstadoOutbox { PENDIENTE, ENVIADO, ERROR, REPROCESAR }
