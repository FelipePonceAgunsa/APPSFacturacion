import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatDialog } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { ConveniosService } from '../../api/api/convenios.service';
import { ConvenioDetalleDto, VersionConvenioDto } from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { ConfirmarVersionData, ConfirmarVersionDialog } from './confirmar-version.dialog';

/** C3 Detalle y versiones del convenio. Una versión VIGENTE no se edita: se crea una nueva. */
@Component({
  selector: 'app-convenio-detalle',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DatePipe, DecimalPipe, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatCardModule, HasAccessDirective],
  template: `
    <div class="pagina">
      @if (convenio(); as c) {
        <h2>Convenio {{ c.rut }} · {{ c.razonSocial }}</h2>
        <p class="texto-secundario">Comercial: {{ c.comercial ?? '—' }} · creado el {{ c.creadoEn | date: 'dd-MM-yyyy HH:mm' }}</p>
        <div class="fila-acciones">
          <a mat-button routerLink="/convenios"><mat-icon>arrow_back</mat-icon>Convenios</a>
          <a *hasAccess="[Access.Convenios_Edit]" mat-stroked-button [routerLink]="['/convenios', c.convenioId, 'nueva-version']">
            <mat-icon>add</mat-icon>Nueva versión
          </a>
        </div>
        @for (v of c.versiones; track v.versionId) {
          <mat-card appearance="outlined" class="section-card">
            <mat-card-header>
              <mat-card-title>Versión {{ v.numero }} · {{ v.estado }}</mat-card-title>
              <mat-card-subtitle>
                Origen {{ v.origen ?? '—' }} · vigencia {{ v.vigenciaDesde }} a {{ v.vigenciaHasta ?? 'sin término' }} ·
                TC mínimo {{ v.tcMinimo ?? '—' }} · días libres {{ v.diasLibres ?? '—' }}
                @if (v.confirmadoPor) { · confirmada por {{ v.confirmadoPor }} el {{ v.confirmadoEn | date: 'dd-MM-yyyy HH:mm' }} }
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <table mat-table [dataSource]="v.servicios" class="tabla-completa">
                <ng-container matColumnDef="servicio"><th mat-header-cell *matHeaderCellDef>Servicio</th><td mat-cell *matCellDef="let s">{{ s.tipoServicioId }} · {{ s.descripcionServicio }}</td></ng-container>
                <ng-container matColumnDef="unidad"><th mat-header-cell *matHeaderCellDef>Unidad</th><td mat-cell *matCellDef="let s">{{ s.unidad }}</td></ng-container>
                <ng-container matColumnDef="tarifa"><th mat-header-cell *matHeaderCellDef class="dato-numero">Tarifa</th><td mat-cell *matCellDef="let s" class="dato-numero">{{ s.tarifa | number: '1.0-4' }}</td></ng-container>
                <ng-container matColumnDef="moneda"><th mat-header-cell *matHeaderCellDef>Moneda</th><td mat-cell *matCellDef="let s">{{ s.moneda }}</td></ng-container>
                <tr mat-header-row *matHeaderRowDef="columnas"></tr>
                <tr mat-row *matRowDef="let row; columns: columnas"></tr>
              </table>
            </mat-card-content>
            @if (v.estado === 'BORRADOR') {
              <mat-card-actions>
                <button *hasAccess="[Access.Convenios_Confirmar]" mat-flat-button type="button" (click)="confirmar(v)">
                  <mat-icon>verified</mat-icon>Revisar y confirmar
                </button>
              </mat-card-actions>
            }
          </mat-card>
        }
      }
    </div>
  `,
})
export class ConvenioDetalleComponent implements OnInit {
  private readonly api = inject(ConveniosService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);
  private readonly convenioId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly Access = Access;
  readonly columnas = ['servicio', 'unidad', 'tarifa', 'moneda'];
  readonly convenio = signal<ConvenioDetalleDto | null>(null);
  readonly vigente = computed(() => this.convenio()?.versiones.find(v => v.estado === 'VIGENTE') ?? null);

  ngOnInit(): Promise<void> {
    return this.cargar();
  }

  async cargar(): Promise<void> {
    try {
      this.convenio.set(await firstValueFrom(this.api.conveniosObtener(this.convenioId)));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  async confirmar(borrador: VersionConvenioDto): Promise<void> {
    const data: ConfirmarVersionData = { borrador, vigente: this.vigente() };
    const aceptado = await firstValueFrom(this.dialog.open(ConfirmarVersionDialog, { data, width: '560px' }).afterClosed());
    if (!aceptado) return;
    try {
      const v = await firstValueFrom(this.api.conveniosConfirmarVersion(this.convenioId, borrador.versionId));
      this.toastr.success(`Versión ${v.numero} VIGENTE.`);
      await this.cargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
