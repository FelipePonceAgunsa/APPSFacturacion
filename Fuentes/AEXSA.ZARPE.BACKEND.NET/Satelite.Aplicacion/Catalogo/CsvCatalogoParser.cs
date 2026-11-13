using System.Globalization;
using System.Text;
using Satelite.Dominio.Comun;

namespace Satelite.Aplicacion.Catalogo;

public sealed record FilaArticulo(int Fila, string Codigo, string Descripcion, string UnidadMedida, bool Activo, string? Grupo);

public sealed record FilaPrecio(
    int Fila, string CodigoLista, string NombreLista, string CodigoArticulo, decimal Precio, Moneda Moneda,
    DateOnly VigenteDesde, DateOnly? VigenteHasta);

public sealed record FilaKit(int Fila, string CodigoKit, string NombreKit, string CodigoArticulo, decimal Cantidad, string? Ruta, bool Activo);

public sealed record FilaTipoCambio(int Fila, DateOnly Fecha, Moneda Moneda, decimal Valor);

/// <summary>Filas válidas de un archivo, cuántas se leyeron y los errores de formato.</summary>
public sealed class LecturaArchivo<T>
{
    public List<T> Filas { get; } = [];
    public int FilasLeidas { get; set; }
    public List<ErrorImportacionDto> Errores { get; } = [];
}

/// <summary>
/// Lector de los archivos A1 a A4 (docs/desarrollo/formato-archivos-catalogo-sap.md): CSV UTF-8 separado por
/// punto y coma, encabezado exacto en minúsculas, punto decimal y fechas AAAA-MM-DD.
/// Solo valida el formato de cada fila (código <c>FORMATO</c> o <c>MONEDA_INVALIDA</c>). Los duplicados y las
/// referencias entre archivos los valida <see cref="ImportacionCatalogoService"/>.
/// La fila informada es la línea del archivo (el encabezado es la fila 1).
/// </summary>
public static class CsvCatalogoParser
{
    public const string Formato = "FORMATO";
    public const string MonedaInvalida = "MONEDA_INVALIDA";

    public static readonly string[] EncabezadoA1 = ["codigo_articulo", "descripcion", "unidad_medida", "activo", "grupo_articulo"];
    public static readonly string[] EncabezadoA2 = ["codigo_lista", "nombre_lista", "codigo_articulo", "precio", "moneda", "vigente_desde", "vigente_hasta"];
    public static readonly string[] EncabezadoA3 = ["codigo_kit", "nombre_kit", "codigo_articulo", "cantidad", "ruta", "activo"];
    public static readonly string[] EncabezadoA4 = ["fecha", "moneda", "valor"];

    public static LecturaArchivo<FilaArticulo> LeerArticulos(Stream contenido) =>
        Leer(contenido, "A1", EncabezadoA1, c =>
        {
            var codigo = c.Texto("codigo_articulo", 40);
            var descripcion = c.Texto("descripcion");
            var unidad = c.Texto("unidad_medida", 20);
            var activo = c.SiNo("activo");
            var grupo = c.Texto("grupo_articulo", 60, obligatoria: false);
            return c.ConErrores ? null : new FilaArticulo(c.Fila, codigo!, descripcion!, unidad!, activo!.Value, grupo);
        });

    public static LecturaArchivo<FilaPrecio> LeerPrecios(Stream contenido) =>
        Leer(contenido, "A2", EncabezadoA2, c =>
        {
            var lista = c.Texto("codigo_lista", 30);
            var nombre = c.Texto("nombre_lista");
            var articulo = c.Texto("codigo_articulo", 40);
            var precio = c.Decimal("precio", minimoExclusivo: null);
            var moneda = c.Moneda("moneda", Dominio.Comun.Moneda.CLP, Dominio.Comun.Moneda.USD, Dominio.Comun.Moneda.UF);
            var desde = c.Fecha("vigente_desde");
            var hasta = c.Fecha("vigente_hasta", obligatoria: false);
            if (desde is not null && hasta is not null && hasta < desde)
            {
                c.Error("vigente_hasta", Formato, "vigente_hasta es anterior a vigente_desde");
            }
            return c.ConErrores ? null : new FilaPrecio(c.Fila, lista!, nombre!, articulo!, precio!.Value, moneda!.Value, desde!.Value, hasta);
        });

    public static LecturaArchivo<FilaKit> LeerKits(Stream contenido) =>
        Leer(contenido, "A3", EncabezadoA3, c =>
        {
            var kit = c.Texto("codigo_kit", 40);
            var nombre = c.Texto("nombre_kit");
            var articulo = c.Texto("codigo_articulo", 40);
            var cantidad = c.Decimal("cantidad", minimoExclusivo: 0m);
            var ruta = c.Texto("ruta", obligatoria: false);
            var activo = c.SiNo("activo");
            return c.ConErrores ? null : new FilaKit(c.Fila, kit!, nombre!, articulo!, cantidad!.Value, ruta, activo!.Value);
        });

    public static LecturaArchivo<FilaTipoCambio> LeerTiposCambio(Stream contenido) =>
        Leer(contenido, "A4", EncabezadoA4, c =>
        {
            var fecha = c.Fecha("fecha");
            var moneda = c.Moneda("moneda", Dominio.Comun.Moneda.USD, Dominio.Comun.Moneda.UF);
            var valor = c.Decimal("valor", minimoExclusivo: 0m);
            return c.ConErrores ? null : new FilaTipoCambio(c.Fila, fecha!.Value, moneda!.Value, valor!.Value);
        });

    private static LecturaArchivo<T> Leer<T>(Stream contenido, string archivo, string[] encabezado, Func<Celdas, T?> convertir)
        where T : class
    {
        var resultado = new LecturaArchivo<T>();
        List<string> lineas;
        try
        {
            using var lector = new StreamReader(contenido, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
            lineas = lector.ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        }
        catch (DecoderFallbackException)
        {
            resultado.Errores.Add(new ErrorImportacionDto(null, null, Formato, $"[{archivo}] El archivo no está en UTF-8."));
            return resultado;
        }

        var primera = lineas.Count > 0 ? lineas[0].TrimStart('\uFEFF') : string.Empty;
        var columnas = primera.Split(';').Select(x => x.Trim()).ToArray();
        if (!columnas.SequenceEqual(encabezado, StringComparer.Ordinal))
        {
            resultado.Errores.Add(new ErrorImportacionDto(1, null, Formato,
                $"[{archivo}] Encabezado inválido. Se esperaba: {string.Join(';', encabezado)}"));
            return resultado;
        }

        for (var i = 1; i < lineas.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i]))
            {
                continue;
            }

            resultado.FilasLeidas++;
            var fila = i + 1;
            var valores = lineas[i].Split(';');
            if (valores.Length != encabezado.Length)
            {
                resultado.Errores.Add(new ErrorImportacionDto(fila, null, Formato,
                    $"[{archivo}] Se esperaban {encabezado.Length} columnas y vienen {valores.Length}."));
                continue;
            }

            var celdas = new Celdas(archivo, fila, encabezado, valores, resultado.Errores);
            if (convertir(celdas) is { } convertida)
            {
                resultado.Filas.Add(convertida);
            }
        }

        return resultado;
    }

    /// <summary>Lectura tipada de las celdas de una fila; cada problema queda como error con fila y columna.</summary>
    private sealed class Celdas(string archivo, int fila, string[] encabezado, string[] valores, List<ErrorImportacionDto> errores)
    {
        private readonly int _erroresAlInicio = errores.Count;

        public int Fila => fila;
        public bool ConErrores => errores.Count > _erroresAlInicio;

        public void Error(string columna, string codigo, string mensaje) =>
            errores.Add(new ErrorImportacionDto(fila, columna, codigo, $"[{archivo}] {mensaje}"));

        private string Valor(string columna) => valores[Array.IndexOf(encabezado, columna)].Trim();

        public string? Texto(string columna, int? largoMaximo = null, bool obligatoria = true)
        {
            var valor = Valor(columna);
            if (valor.Length == 0)
            {
                if (obligatoria)
                {
                    Error(columna, Formato, $"{columna} es obligatoria");
                }
                return null;
            }
            if (largoMaximo is { } max && valor.Length > max)
            {
                Error(columna, Formato, $"{columna} supera {max} caracteres");
                return null;
            }
            return valor;
        }

        public decimal? Decimal(string columna, decimal? minimoExclusivo)
        {
            var valor = Texto(columna);
            if (valor is null)
            {
                return null;
            }
            if (!decimal.TryParse(valor, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var numero))
            {
                Error(columna, Formato, $"{columna} no es un decimal con punto: '{valor}'");
                return null;
            }
            var minimo = minimoExclusivo ?? -1m;
            if (numero < 0 || (minimoExclusivo is not null && numero <= minimo))
            {
                Error(columna, Formato, minimoExclusivo is null ? $"{columna} no puede ser negativo" : $"{columna} debe ser mayor que {minimo}");
                return null;
            }
            return numero;
        }

        public DateOnly? Fecha(string columna, bool obligatoria = true)
        {
            var valor = Texto(columna, obligatoria: obligatoria);
            if (valor is null)
            {
                return null;
            }
            if (!DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                Error(columna, Formato, $"{columna} no es una fecha AAAA-MM-DD: '{valor}'");
                return null;
            }
            return fecha;
        }

        public bool? SiNo(string columna)
        {
            var valor = Texto(columna);
            switch (valor)
            {
                case null: return null;
                case "S": return true;
                case "N": return false;
                default:
                    Error(columna, Formato, $"{columna} debe ser S o N: '{valor}'");
                    return null;
            }
        }

        public Moneda? Moneda(string columna, params Moneda[] permitidas)
        {
            var valor = Texto(columna);
            if (valor is null)
            {
                return null;
            }
            if (!Enum.TryParse<Moneda>(valor, ignoreCase: false, out var moneda) || !permitidas.Contains(moneda) || valor != moneda.ToString())
            {
                Error(columna, MonedaInvalida, $"Moneda '{valor}' no permitida. Valores: {string.Join(", ", permitidas)}");
                return null;
            }
            return moneda;
        }
    }
}
