import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ConveniosService } from '../../api/api/convenios.service';
import { ConvenioResumenDto, EstadoVersionConvenio } from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { diasHasta } from './convenios-util';

/** C1 Convenios: lista por cliente con la versión vigente y el vencimiento (RN-021). */
@Component({
  selector: 'app-convenios-lista',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    FormsModule, RouterLink, MatTableModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule,
    MatIconModule, HasAccessDirective,
  ],
  template: `
    <div class="pagina">
      <h2>Convenios</h2>
      <div class="fila-acciones">
        <mat-form-field appearance="outline">
          <mat-label>RUT del cliente</mat-label>
          <input matInput [(ngModel)]="rut" (keyup.enter)="buscar()" placeholder="76.111.111-6" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Estado de alguna versión</mat-label>
          <mat-select [(ngModel)]="estado">
            <mat-option [value]="null">Todos</mat-option>
            @for (e of estados; track e) {
              <mat-option [value]="e">{{ e }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
        <button mat-flat-button type="button" (click)="buscar()"><mat-icon>search</mat-icon>Buscar</button>
        <a *hasAccess="[Access.Convenios_Edit]" mat-stroked-button routerLink="/convenios/nuevo"><mat-icon>add</mat-icon>Nuevo convenio</a>
      </div>
      <table mat-table [dataSource]="convenios()" class="tabla-completa">
        <ng-container matColumnDef="rut"><th mat-header-cell *matHeaderCellDef>RUT</th><td mat-cell *matCellDef="let c">{{ c.rut }}</td></ng-container>
        <ng-container matColumnDef="razonSocial"><th mat-header-cell *matHeaderCellDef>Cliente</th><td mat-cell *matCellDef="let c">{{ c.razonSocial }}</td></ng-container>
        <ng-container matColumnDef="comercial"><th mat-header-cell *matHeaderCellDef>Comercial</th><td mat-cell *matCellDef="let c">{{ c.comercial ?? '—' }}</td></ng-container>
        <ng-container matColumnDef="versionVigente"><th mat-header-cell *matHeaderCellDef>Versión vigente</th><td mat-cell *matCellDef="let c">{{ c.versionVigente ?? 'Sin vigente' }}</td></ng-container>
        <ng-container matColumnDef="vigencia"><th mat-header-cell *matHeaderCellDef>Vigencia</th><td mat-cell *matCellDef="let c">@if (c.vigenciaDesde) { {{ c.vigenciaDesde }} a {{ c.vigenciaHasta ?? 'sin término' }} } @else { — }</td></ng-container>
        <ng-container matColumnDef="vence">
          <th mat-header-cell *matHeaderCellDef>Vence</th>
          <td mat-cell *matCellDef="let c" [class.texto-error]="(venceEn(c) ?? 999) <= 30">{{ textoVence(c) }}</td>
        </ng-container>
        <ng-container matColumnDef="ultima"><th mat-header-cell *matHeaderCellDef>Última versión</th><td mat-cell *matCellDef="let c">{{ c.ultimaVersion }} · {{ c.estadoUltimaVersion }}</td></ng-container>
        <ng-container matColumnDef="accion"><th mat-header-cell *matHeaderCellDef></th><td mat-cell *matCellDef="let c"><a mat-button [routerLink]="['/convenios', c.convenioId]">Ver</a></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columnas"></tr>
        <tr mat-row *matRowDef="let row; columns: columnas"></tr>
      </table>
      @if (!convenios().length) {
        <p class="texto-secundario">No hay convenios con esos filtros.</p>
      }
    </div>
  `,
})
export class ConveniosListaComponent implements OnInit {
  private readonly api = inject(ConveniosService);
  private readonly toastr = inject(ToastrService);

  readonly Access = Access;
  readonly estados: EstadoVersionConvenio[] = ['BORRADOR', 'VIGENTE', 'VENCIDA', 'REEMPLAZADA'];
  readonly columnas = ['rut', 'razonSocial', 'comercial', 'versionVigente', 'vigencia', 'vence', 'ultima', 'accion'];
  readonly convenios = signal<ConvenioResumenDto[]>([]);
  rut = '';
  estado: EstadoVersionConvenio | null = null;

  ngOnInit(): Promise<void> {
    return this.buscar();
  }

  async buscar(): Promise<void> {
    try {
      this.convenios.set(await firstValueFrom(this.api.conveniosListar(this.rut.trim() || undefined, this.estado ?? undefined)));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  venceEn(c: ConvenioResumenDto): number | null {
    return c.versionVigente ? diasHasta(c.vigenciaHasta) : null;
  }

  textoVence(c: ConvenioResumenDto): string {
    const dias = this.venceEn(c);
    if (dias === null) return '—';
    return dias < 0 ? `Venció hace ${-dias} días` : `En ${dias} días`;
  }
}
