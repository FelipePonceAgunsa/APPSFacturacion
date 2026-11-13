using Satelite.Aplicacion.Comun;

namespace Satelite.Infraestructura.Archivos;

/// <summary>
/// Adaptador local de <see cref="IAlmacenArchivos"/> (stub de S3 para desarrollo). Guarda bajo
/// <c>Almacen:RutaLocal</c>, por defecto <c>&lt;ContentRoot&gt;/App_Data/archivos</c>. Las URIs son <c>local://ruta</c>.
/// </summary>
internal sealed class AlmacenArchivosLocal(string raiz) : IAlmacenArchivos
{
    private readonly string _raiz = Path.GetFullPath(raiz);

    public async Task<string> GuardarAsync(string ruta, Stream contenido, CancellationToken ct)
    {
        var destino = Resolver(ruta);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using var archivo = File.Create(destino);
        await contenido.CopyToAsync(archivo, ct);
        return UriDe(ruta);
    }

    public Task<Stream> AbrirAsync(string ruta, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(Resolver(ruta)));

    public string UriDe(string ruta) => $"local://{ruta}";

    /// <summary>Evita salir de la carpeta raíz con rutas como <c>../</c>.</summary>
    private string Resolver(string ruta)
    {
        var completa = Path.GetFullPath(Path.Combine(_raiz, ruta.Replace('/', Path.DirectorySeparatorChar)));
        if (!completa.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Ruta fuera del almacén: {ruta}", nameof(ruta));
        }
        return completa;
    }
}
