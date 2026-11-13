import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { CatalogoService } from '../../api/api/catalogo.service';
import { ConveniosService } from '../../api/api/convenios.service';
import { ConvenioDetalleDto, OrigenVersionConvenio, TipoServicioDto, VersionConvenioDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';

/** Fila editable de la tabla de servicios. */
interface FilaServicio {
  tipoServicioId: number | null;
  tarifa: number | null;
  moneda: 'CLP' | 'USD';
}

/**
 * C2 Ingresar convenio (ADR-012): formulario simple para un convenio nuevo o una versión nueva.
 * La unidad de cada servicio sale del catálogo y no se digita (RN-036). La versión nace en BORRADOR.
 */
@Component({
  selector: 'app-convenio-form',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [FormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, MatCardModule],
  template: `
    <div class="pagina">
      <h2>{{ convenioId ? 'Nueva versión del convenio' : 'Nuevo convenio' }}</h2>
      @if (convenio(); as c) {
        <p>{{ c.rut }} · {{ c.razonSocial }}</p>
      }
      <form #f="ngForm" (ngSubmit)="guardar()">
        <div class="fila-acciones">
          @if (!convenioId) {
            <mat-form-field appearance="outline">
              <mat-label>RUT del cliente</mat-label>
              <input matInput name="rut" [(ngModel)]="rut" required placeholder="76.111.111-6" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Razón social</mat-label>
              <input matInput name="razonSocial" [(ngModel)]="razonSocial" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Comercial (opcional)</mat-label>
              <input matInput name="comercial" [(ngModel)]="comercial" />
            </mat-form-field>
          } @else {
            <mat-form-field appearance="outline">
              <mat-label>Origen</mat-label>
              <mat-select name="origen" [(ngModel)]="origen">
                <mat-option value="MANUAL">Manual</mat-option>
                <mat-option value="COMPLEMENTO_CORREO">Complemento por correo</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Motivo del cambio</mat-label>
              <input matInput name="motivo" [(ngModel)]="motivo" />
            </mat-form-field>
          }
        </div>
        <div class="fila-acciones">
          <mat-form-field appearance="outline">
            <mat-label>Vigencia desde</mat-label>
            <input matInput type="date" name="vigenciaDesde" [(ngModel)]="vigenciaDesde" required />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Vigencia hasta</mat-label>
            <input matInput type="date" name="vigenciaHasta" [(ngModel)]="vigenciaHasta" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>TC mínimo (CLP por USD)</mat-label>
            <input matInput type="number" min="0" step="0.01" name="tcMinimo" [(ngModel)]="tcMinimo" />
            @if (hayUsd() && !tcMinimo) {
              <mat-hint>Hay tarifas en USD: sin TC mínimo se usa el del día sin piso</mat-hint>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Días libres de almacenaje</mat-label>
            <input matInput type="number" min="0" step="1" name="diasLibres" [(ngModel)]="diasLibres" />
          </mat-form-field>
        </div>

        <h3>Servicios</h3>
        <p class="texto-secundario">Los servicios que no se ingresen se cobran con la tarifa pública y quedan marcados como excepción.</p>
        @for (fila of filas(); track $index; let i = $index) {
          <div class="fila-acciones">
            <mat-form-field appearance="outline">
              <mat-label>Servicio</mat-label>
              <mat-select [name]="'servicio' + i" [(ngModel)]="fila.tipoServicioId" (selectionChange)="refrescar()" required>
                @for (t of tipos(); track t.tipoServicioId) {
                  <mat-option [value]="t.tipoServicioId">{{ t.tipoServicioId }} · {{ t.descripcion }}</mat-option>
                }
              </mat-select>
            </mat-form-field>
            <span class="texto-secundario">Unidad: {{ unidadDe(fila.tipoServicioId) }}</span>
            <mat-form-field appearance="outline">
              <mat-label>Tarifa</mat-label>
              <input matInput type="number" min="0" step="0.0001" [name]="'tarifa' + i" [(ngModel)]="fila.tarifa" required />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>Moneda</mat-label>
              <mat-select [name]="'moneda' + i" [(ngModel)]="fila.moneda" (selectionChange)="refrescar()">
                <mat-option value="CLP">CLP</mat-option>
                <mat-option value="USD">USD</mat-option>
              </mat-select>
            </mat-form-field>
            <button mat-icon-button type="button" (click)="quitar(i)" aria-label="Quitar servicio"><mat-icon>delete</mat-icon></button>
          </div>
        }
        <div class="fila-acciones">
          <button mat-stroked-button type="button" (click)="agregar()"><mat-icon>add</mat-icon>Agregar servicio</button>
        </div>
        <div class="fila-acciones">
          <button mat-flat-button type="submit" [disabled]="guardando() || !f.valid || !filas().length"><mat-icon>save</mat-icon>Guardar en borrador</button>
          <a mat-button [routerLink]="convenioId ? ['/convenios', convenioId] : ['/convenios']">Cancelar</a>
        </div>
      </form>
    </div>
  `,
})
export class ConvenioFormComponent implements OnInit {
  private readonly convenios = inject(ConveniosService);
  private readonly catalogo = inject(CatalogoService);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);

  /** Con :id en la ruta el formulario crea una versión nueva de ese convenio. */
  readonly convenioId: string | null = inject(ActivatedRoute).snapshot.paramMap.get('id');

  readonly tipos = signal<TipoServicioDto[]>([]);
  readonly convenio = signal<ConvenioDetalleDto | null>(null);
  readonly filas = signal<FilaServicio[]>([{ tipoServicioId: null, tarifa: null, moneda: 'CLP' }]);
  readonly guardando = signal(false);
  private readonly version = signal(0);
  readonly hayUsd = computed(() => {
    this.version();
    return this.filas().some(f => f.moneda === 'USD');
  });

  rut = '';
  razonSocial = '';
  comercial = '';
  origen: OrigenVersionConvenio = 'MANUAL';
  motivo = '';
  vigenciaDesde = '';
  vigenciaHasta = '';
  tcMinimo: number | null = null;
  diasLibres: number | null = null;

  async ngOnInit(): Promise<void> {
    try {
      this.tipos.set((await firstValueFrom(this.catalogo.tiposServicioListar())).filter(t => t.habilitado));
      if (this.convenioId) {
        const convenio = await firstValueFrom(this.convenios.conveniosObtener(this.convenioId));
        this.convenio.set(convenio);
        // La versión nueva parte de la vigente (o de la más nueva) para cambiar solo lo necesario.
        const base = convenio.versiones.find(v => v.estado === 'VIGENTE') ?? convenio.versiones[0];
        if (base) this.cargarDesde(base);
      }
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  unidadDe(tipoServicioId: number | null): string {
    return this.tipos().find(t => t.tipoServicioId === tipoServicioId)?.unidad ?? '—';
  }

  agregar(): void {
    this.filas.update(f => [...f, { tipoServicioId: null, tarifa: null, moneda: 'CLP' }]);
  }

  quitar(indice: number): void {
    this.filas.update(f => f.filter((_, i) => i !== indice));
  }

  /** Las filas se editan en el lugar con ngModel: esto recalcula lo que depende de ellas. */
  refrescar(): void {
    this.version.update(v => v + 1);
  }

  async guardar(): Promise<void> {
    const servicios = this.filas()
      .filter(f => f.tipoServicioId !== null && f.tarifa !== null)
      .map(f => ({ tipoServicioId: f.tipoServicioId!, tarifa: Number(f.tarifa), moneda: f.moneda }));
    const comun = {
      vigenciaDesde: this.vigenciaDesde,
      vigenciaHasta: this.vigenciaHasta || null,
      tcMinimo: this.tcMinimo || null,
      diasLibres: this.diasLibres ?? null,
      servicios,
    };
    this.guardando.set(true);
    try {
      const version: VersionConvenioDto = this.convenioId
        ? await firstValueFrom(this.convenios.conveniosCrearVersion(this.convenioId, {
            ...comun, origen: this.origen, motivo: this.motivo.trim() || null,
          }))
        : await firstValueFrom(this.convenios.conveniosCrear({
            ...comun, rut: this.rut.trim(), razonSocial: this.razonSocial.trim() || null, comercial: this.comercial.trim() || null,
          }));
      this.toastr.success(`Versión ${version.numero} guardada en BORRADOR. Falta confirmarla.`);
      await this.router.navigate(['/convenios', version.convenioId]);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    } finally {
      this.guardando.set(false);
    }
  }

  private cargarDesde(v: VersionConvenioDto): void {
    this.vigenciaDesde = v.vigenciaDesde;
    this.vigenciaHasta = v.vigenciaHasta ?? '';
    this.tcMinimo = v.tcMinimo;
    this.diasLibres = v.diasLibres;
    this.filas.set(v.servicios.map(s => ({ tipoServicioId: s.tipoServicioId, tarifa: s.tarifa, moneda: s.moneda === 'USD' ? 'USD' : 'CLP' })));
  }
}
