import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { ConfiguracionService } from '../../api/api/configuracion.service';
import { GrupoInspeccion, TipoInspeccionMapaDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';

/**
 * Tipos de inspección de AEXSA ([E1-7], RN-052). Los días bajo una inspección AFORO_ADUANA no se cobran;
 * mientras un código esté SIN_MAPEAR, los días bajo aforo quedan como pendiente DIAS_AFORO.
 */
@Component({
  selector: 'app-tipos-inspeccion',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [MatTableModule, MatSelectModule],
  template: `
    <div class="pagina">
      <h2>Tipos de inspección</h2>
      <p class="texto-secundario">Los códigos nuevos llegan con los eventos de AEXSA como SIN MAPEAR.</p>
      <table mat-table [dataSource]="tipos()" class="tabla-completa">
        <ng-container matColumnDef="codigo"><th mat-header-cell *matHeaderCellDef>Código AEXSA</th><td mat-cell *matCellDef="let t">{{ t.codigo }}</td></ng-container>
        <ng-container matColumnDef="descripcion"><th mat-header-cell *matHeaderCellDef>Descripción</th><td mat-cell *matCellDef="let t">{{ t.descripcion }}</td></ng-container>
        <ng-container matColumnDef="grupo">
          <th mat-header-cell *matHeaderCellDef>Grupo</th>
          <td mat-cell *matCellDef="let t">
            <mat-select [value]="t.grupo === 'SIN_MAPEAR' ? null : t.grupo" placeholder="SIN MAPEAR"
              (selectionChange)="asignar(t, $event.value)" aria-label="Grupo del tipo de inspección">
              @for (g of grupos; track g) {
                <mat-option [value]="g">{{ g }}</mat-option>
              }
            </mat-select>
          </td>
        </ng-container>
        <ng-container matColumnDef="operacionesPendientes"><th mat-header-cell *matHeaderCellDef class="dato-numero">Operaciones esperando</th><td mat-cell *matCellDef="let t" class="dato-numero">{{ t.operacionesPendientes }}</td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columnas"></tr>
        <tr mat-row *matRowDef="let row; columns: columnas"></tr>
      </table>
    </div>
  `,
})
export class TiposInspeccionComponent implements OnInit {
  private readonly api = inject(ConfiguracionService);
  private readonly toastr = inject(ToastrService);

  readonly grupos: GrupoInspeccion[] = ['AFORO_ADUANA', 'SAG', 'OTRA'];
  readonly columnas = ['codigo', 'descripcion', 'grupo', 'operacionesPendientes'];
  readonly tipos = signal<TipoInspeccionMapaDto[]>([]);

  async ngOnInit(): Promise<void> {
    try {
      this.tipos.set(await firstValueFrom(this.api.configuracionTiposInspeccion()));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  async asignar(tipo: TipoInspeccionMapaDto, grupo: GrupoInspeccion): Promise<void> {
    try {
      const actualizado = await firstValueFrom(this.api.configuracionAsignarTipoInspeccion(tipo.codigo, { grupo }));
      this.tipos.update(lista => lista.map(t => (t.codigo === actualizado.codigo ? actualizado : t)));
      this.toastr.success(`${tipo.codigo}: ${grupo}`);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
