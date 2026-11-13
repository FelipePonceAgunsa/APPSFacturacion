import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CatalogoService } from '../../api/api/catalogo.service';
import {
  ArticuloSapDto, CatalogoResumenDto, KitSapDto, Moneda, PrecioListaDto, ResultadoImportacionDto,
  TipoCambioDto, TipoImportacion, VinculoServicioArticuloDto,
} from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';

/** Partes del formulario de importación (OpenAPI: POST /catalogo/importaciones). */
type Parte = 'archivo' | 'articulos' | 'listasPrecios' | 'kits' | 'tipoCambio';


/**
 * T3 Catálogo SAP (ADR-017): versión vigente, artículos, precios, kits, tipo de cambio, vínculos
 * servicio ↔ artículo e importación de los archivos A1 a A4 con confirmación humana.
 */
@Component({
  selector: 'app-catalogo',
  templateUrl: './catalogo.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    DatePipe, DecimalPipe, FormsModule, MatTabsModule, MatTableModule, MatFormFieldModule, MatInputModule,
    MatSelectModule, MatButtonModule, MatIconModule, MatCardModule, MatProgressBarModule, HasAccessDirective,
  ],
})
export class CatalogoComponent implements OnInit {
  private readonly api = inject(CatalogoService);
  private readonly toastr = inject(ToastrService);

  readonly Access = Access;
  readonly tiposImportacion: TipoImportacion[] = ['COMPLETO', 'ARTICULOS', 'LISTAS_PRECIOS', 'KITS', 'TIPO_CAMBIO'];
  readonly monedasTipoCambio: Moneda[] = ['USD', 'UF'];

  readonly cargando = signal(false);
  readonly resumen = signal<CatalogoResumenDto | null>(null);
  readonly sinCatalogo = signal(false);

  readonly busqueda = signal('');
  readonly articulos = signal<ArticuloSapDto[]>([]);

  readonly codigoLista = signal('LP-PUBLICA');
  readonly precios = signal<PrecioListaDto[]>([]);

  readonly ruta = signal('');
  readonly kits = signal<KitSapDto[]>([]);

  readonly tiposCambio = signal<TipoCambioDto[]>([]);
  readonly nuevoTipoCambio = { fecha: '', moneda: 'USD' as Moneda, valor: null as number | null };

  readonly vinculos = signal<VinculoServicioArticuloDto[]>([]);
  readonly seleccionVinculo = signal<Record<number, string>>({});
  readonly articulosActivos = computed(() => this.articulos().filter(a => a.activo));

  readonly tipoImportacion = signal<TipoImportacion>('COMPLETO');
  readonly archivos = signal<Partial<Record<Parte, File>>>({});
  readonly resultado = signal<ResultadoImportacionDto | null>(null);
  readonly partesCompleto: { parte: Parte; etiqueta: string }[] = [
    { parte: 'articulos', etiqueta: 'A1 Artículos' },
    { parte: 'listasPrecios', etiqueta: 'A2 Listas de precios' },
    { parte: 'kits', etiqueta: 'A3 Kits' },
    { parte: 'tipoCambio', etiqueta: 'A4 Tipo de cambio (opcional)' },
  ];

  readonly columnasArticulos = ['codigoArticulo', 'descripcion', 'unidadMedida', 'activo', 'grupoArticulo'];
  readonly columnasPrecios = ['codigoArticulo', 'precio', 'moneda', 'vigenteDesde', 'vigenteHasta'];
  readonly columnasTipoCambio = ['fecha', 'moneda', 'valor', 'fuente'];
  readonly columnasVinculos = ['tipoServicioId', 'descripcionServicio', 'articuloSap', 'estado', 'accion'];
  readonly columnasErrores = ['fila', 'columna', 'codigo', 'mensaje'];

  ngOnInit(): void {
    void this.recargar();
  }

  async recargar(): Promise<void> {
    this.cargando.set(true);
    try {
      await Promise.all([this.cargarResumen(), this.buscarArticulos(), this.cargarPrecios(), this.cargarKits(),
        this.cargarTiposCambio(), this.cargarVinculos()]);
    } finally {
      this.cargando.set(false);
    }
  }

  async cargarResumen(): Promise<void> {
    try {
      this.resumen.set(await firstValueFrom(this.api.catalogoResumen()));
      this.sinCatalogo.set(false);
    } catch (e) {
      this.resumen.set(null);
      this.sinCatalogo.set(e instanceof HttpErrorResponse && e.status === 404);
    }
  }

  async buscarArticulos(): Promise<void> {
    this.articulos.set(await this.intentar(this.api.catalogoArticulos(this.busqueda() || undefined), []));
  }

  async cargarPrecios(): Promise<void> {
    const lista = this.codigoLista().trim();
    if (!lista) return;
    try {
      this.precios.set(await firstValueFrom(this.api.catalogoPrecios(lista)));
    } catch {
      this.precios.set([]);
    }
  }

  async cargarKits(): Promise<void> {
    this.kits.set(await this.intentar(this.api.catalogoKits(this.ruta() || undefined), []));
  }

  async cargarTiposCambio(): Promise<void> {
    this.tiposCambio.set(await this.intentar(this.api.catalogoTipoCambio(), []));
  }

  async cargarVinculos(): Promise<void> {
    const vinculos = await this.intentar(this.api.catalogoVinculos(), []);
    this.vinculos.set(vinculos);
    this.seleccionVinculo.set(Object.fromEntries(vinculos.map(v => [v.tipoServicioId, v.articuloSap ?? ''])));
  }

  async registrarTipoCambio(): Promise<void> {
    const { fecha, moneda, valor } = this.nuevoTipoCambio;
    if (!fecha || !valor) {
      this.toastr.warning('Indica la fecha y el valor.');
      return;
    }
    try {
      await firstValueFrom(this.api.catalogoTipoCambioRegistrar({ fecha, moneda, valor }));
      this.toastr.success('Tipo de cambio registrado (fuente MANUAL).');
      this.nuevoTipoCambio.valor = null;
      await Promise.all([this.cargarTiposCambio(), this.cargarResumen()]);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  elegirArticulo(tipoServicioId: number, articulo: string): void {
    this.seleccionVinculo.update(s => ({ ...s, [tipoServicioId]: articulo }));
  }

  async fijarVinculo(v: VinculoServicioArticuloDto): Promise<void> {
    const articuloSap = this.seleccionVinculo()[v.tipoServicioId];
    if (!articuloSap) {
      this.toastr.warning('Elige un artículo SAP.');
      return;
    }
    try {
      await firstValueFrom(this.api.catalogoFijarVinculo(v.tipoServicioId, { articuloSap }));
      this.toastr.success(`Servicio ${v.tipoServicioId} vinculado a ${articuloSap}.`);
      await this.cargarVinculos();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  cambiarTipo(tipo: TipoImportacion): void {
    this.tipoImportacion.set(tipo);
    this.archivos.set({});
    this.resultado.set(null);
  }

  elegirArchivo(parte: Parte, evento: Event): void {
    const archivo = (evento.target as HTMLInputElement).files?.[0];
    this.archivos.update(a => ({ ...a, [parte]: archivo }));
  }

  async importar(): Promise<void> {
    const a = this.archivos();
    const tipo = this.tipoImportacion();
    this.cargando.set(true);
    try {
      this.resultado.set(await firstValueFrom(
        this.api.catalogoImportar(tipo, a.archivo, a.articulos, a.listasPrecios, a.kits, a.tipoCambio)));
      const r = this.resultado()!;
      if (r.estado === 'RECHAZADA') {
        this.toastr.warning('La importación tiene errores y quedó rechazada.');
      } else {
        this.toastr.info('Archivos validados. Revisa y confirma la importación.');
      }
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    } finally {
      this.cargando.set(false);
    }
  }

  async confirmarImportacion(): Promise<void> {
    const r = this.resultado();
    if (!r) return;
    this.cargando.set(true);
    try {
      const resumen = await firstValueFrom(this.api.catalogoConfirmarImportacion(r.importacionId));
      this.toastr.success(`Catálogo versión ${resumen.version} activo.`);
      this.resultado.set({ ...r, estado: 'CONFIRMADA' });
      this.archivos.set({});
      await this.recargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    } finally {
      this.cargando.set(false);
    }
  }

  private async intentar<T>(origen: Observable<T>, porDefecto: T): Promise<T> {
    try {
      return await firstValueFrom(origen);
    } catch {
      return porDefecto;
    }
  }
}
