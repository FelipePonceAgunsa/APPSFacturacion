import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
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
import { MatChipsModule } from '@angular/material/chips';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OperacionResumenDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';
import { TEXTO_PENDIENTE, textoIds } from './operaciones-util';
/** F1 Bandeja de operaciones: lo que llegó de AEXSA, con el convenio encontrado y los pendientes. */
@Component({
  selector: 'app-operaciones-bandeja',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    DatePipe, FormsModule, RouterLink, MatTableModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatChipsModule,
  ],
  template: `
    <div class="pagina">
      <h2>Operaciones</h2>
      <div class="fila-acciones">
        <mat-form-field appearance="outline">
          <mat-label>RUT del cliente</mat-label>
          <input matInput [(ngModel)]="rut" (keyup.enter)="buscar()" placeholder="76.111.111-6" />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Facturable</mat-label>
          <mat-select [(ngModel)]="facturable">
            <mat-option [value]="null">Todas</mat-option>
            <mat-option [value]="true">Sí</mat-option>
            <mat-option [value]="false">No</mat-option>
          </mat-select>
        </mat-form-field>
        <button mat-flat-button type="button" (click)="buscar()"><mat-icon>search</mat-icon>Buscar</button>
      </div>
      <table mat-table [dataSource]="operaciones()" class="tabla-completa">
        <ng-container matColumnDef="ids"><th mat-header-cell *matHeaderCellDef>IDs AEXSA</th><td mat-cell *matCellDef="let o">{{ textoIds(o.operacionIdAexsa) }}</td></ng-container>
        <ng-container matColumnDef="visado"><th mat-header-cell *matHeaderCellDef>Visado</th><td mat-cell *matCellDef="let o">{{ o.visadoAprobadoEn | date: 'dd-MM-yyyy HH:mm' }}</td></ng-container>
        <ng-container matColumnDef="rut"><th mat-header-cell *matHeaderCellDef>Cliente</th><td mat-cell *matCellDef="let o">{{ o.rut }} · {{ o.razonSocial }}</td></ng-container>
        <ng-container matColumnDef="convenio"><th mat-header-cell *matHeaderCellDef>Convenio</th><td mat-cell *matCellDef="let o">{{ o.conConvenio ? 'Sí' : 'No' }}</td></ng-container>
        <ng-container matColumnDef="ruta"><th mat-header-cell *matHeaderCellDef>Ruta</th>
          <td mat-cell *matCellDef="let o">{{ o.rutaClasificada ?? 'Sin clasificar' }}@if (!o.facturable) { · <span class="texto-error">no facturable</span> }@if (o.estadoOperacion === 'ANULADA') { · <span class="texto-error">ANULADA</span> }</td></ng-container>
        <ng-container matColumnDef="pendientes"><th mat-header-cell *matHeaderCellDef>Pendientes</th>
          <td mat-cell *matCellDef="let o">
            <mat-chip-set>
              @for (p of o.pendientes; track p) { <mat-chip>{{ texto[p] }}</mat-chip> }
            </mat-chip-set>
            @if (!o.pendientes.length) { <span class="texto-secundario">—</span> }
          </td></ng-container>
        <ng-container matColumnDef="ofa"><th mat-header-cell *matHeaderCellDef>OFA</th><td mat-cell *matCellDef="let o">{{ o.estadoOfa ?? '—' }}</td></ng-container>
        <ng-container matColumnDef="accion"><th mat-header-cell *matHeaderCellDef></th><td mat-cell *matCellDef="let o"><a mat-button [routerLink]="['/operaciones', o.operacionId]">Ver</a></td></ng-container>
        <tr mat-header-row *matHeaderRowDef="columnas"></tr>
        <tr mat-row *matRowDef="let row; columns: columnas"></tr>
      </table>
      @if (!operaciones().length) {
        <p class="texto-secundario">No hay operaciones con esos filtros.</p>
      }
    </div>
  `,
})
export class OperacionesBandejaComponent implements OnInit {
  private readonly api = inject(OperacionesService);
  private readonly toastr = inject(ToastrService);
  readonly texto = TEXTO_PENDIENTE;
  readonly textoIds = textoIds;
  readonly columnas = ['ids', 'visado', 'rut', 'convenio', 'ruta', 'pendientes', 'ofa', 'accion'];
  readonly operaciones = signal<OperacionResumenDto[]>([]);
  rut = '';
  facturable: boolean | null = null;
  ngOnInit(): Promise<void> {
    return this.buscar();
  }
  async buscar(): Promise<void> {
    try {
      this.operaciones.set(await firstValueFrom(this.api.operacionesListar(this.facturable ?? undefined, this.rut.trim() || undefined)));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
