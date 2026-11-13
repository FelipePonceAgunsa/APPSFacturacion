import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OfaService } from '../../api/api/ofa.service';
import { OfaDto, ResultadoValidacionDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';
/**
 * F6 en su forma de la Fase A: validar la OFA y, si está VALIDADA, mostrarla "lista para digitar en SAP"
 * (artículo, cantidad, precio, moneda, lista, kit sugerido y RUT del cliente). "Enviar a SAP" se ve pero está
 * deshabilitado hasta la Fase B (tarea 3.3).
 */
@Component({
  selector: 'app-ofa-digitar',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DecimalPipe, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatTooltipModule],
  template: `
    <div class="pagina">
      @if (ofa(); as o) {
        <h2>Validar y digitar en SAP · OFA {{ o.operacionIdAexsa.join(', ') }}</h2>
        <div class="fila-acciones">
          <a mat-button [routerLink]="['/ofa', o.ofaId]"><mat-icon>arrow_back</mat-icon>OFA</a>
          @if (o.estado === 'BORRADOR' || o.estado === 'VALIDADA') {
            <button mat-flat-button type="button" (click)="validar()"><mat-icon>fact_check</mat-icon>Validar</button>
          }
          <span matTooltip="Disponible en la Fase B">
            <button mat-stroked-button type="button" disabled><mat-icon>send</mat-icon>Enviar a SAP</button>
          </span>
        </div>
        @if (resultado(); as r) {
          @if (r.valida) {
            <p>La OFA está completa y quedó {{ r.estado }}.</p>
          } @else {
            <p class="texto-error">No se puede validar. Falta:</p>
            <ul>@for (f of r.faltantes; track f) { <li>{{ f }}</li> }</ul>
          }
        }
        @if (o.estado === 'VALIDADA') {
          <h3>Lista para digitar en SAP</h3>
          @for (a of o.asignaciones; track a.asignacionId) { <p>Cliente {{ a.destinatarioRut }} {{ a.razonSocial ?? '' }}</p> }
          <p>Kit sugerido: {{ o.kitSugerido ? o.kitSugerido.codigoKit + ' · ' + o.kitSugerido.nombre : 'sin kit para la ruta' }}</p>
          <table mat-table [dataSource]="o.lineas" class="tabla-completa">
            <ng-container matColumnDef="articulo"><th mat-header-cell *matHeaderCellDef>Artículo</th><td mat-cell *matCellDef="let l">{{ l.articuloSap }}</td></ng-container>
            <ng-container matColumnDef="descripcion"><th mat-header-cell *matHeaderCellDef>Servicio</th><td mat-cell *matCellDef="let l">{{ l.descripcionServicio }}</td></ng-container>
            <ng-container matColumnDef="cantidad"><th mat-header-cell *matHeaderCellDef class="dato-numero">Cantidad</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.cantidad | number: '1.0-4' }}</td></ng-container>
            <ng-container matColumnDef="precio"><th mat-header-cell *matHeaderCellDef class="dato-numero">Precio</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.tarifa | number: '1.0-4' }}</td></ng-container>
            <ng-container matColumnDef="moneda"><th mat-header-cell *matHeaderCellDef>Moneda</th><td mat-cell *matCellDef="let l">{{ l.moneda }}</td></ng-container>
            <ng-container matColumnDef="lista"><th mat-header-cell *matHeaderCellDef>Lista</th><td mat-cell *matCellDef="let l">{{ l.listaPrecios ?? l.fuenteTarifa }}</td></ng-container>
            <ng-container matColumnDef="monto"><th mat-header-cell *matHeaderCellDef class="dato-numero">Monto CLP</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.montoClp | number: '1.0-4' }}</td></ng-container>
            <ng-container matColumnDef="kit"><th mat-header-cell *matHeaderCellDef>En kit</th><td mat-cell *matCellDef="let l">{{ l.enKitSap === null ? '—' : (l.enKitSap ? 'Sí' : 'No') }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columnas"></tr>
            <tr mat-row *matRowDef="let row; columns: columnas"></tr>
          </table>
          <p class="dato-numero"><strong>Total {{ o.totalClp | number: '1.0-4' }} CLP</strong></p>
        }
      }
    </div>
  `,
})
export class OfaDigitarComponent implements OnInit {
  private readonly api = inject(OfaService);
  private readonly toastr = inject(ToastrService);
  private readonly ofaId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly columnas = ['articulo', 'descripcion', 'cantidad', 'precio', 'moneda', 'lista', 'monto', 'kit'];
  readonly ofa = signal<OfaDto | null>(null);
  readonly resultado = signal<ResultadoValidacionDto | null>(null);
  ngOnInit(): Promise<void> {
    return this.cargar();
  }
  async cargar(): Promise<void> {
    try {
      this.ofa.set(await firstValueFrom(this.api.ofaObtener(this.ofaId)));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async validar(): Promise<void> {
    try {
      this.resultado.set(await firstValueFrom(this.api.ofaValidar(this.ofaId)));
      await this.cargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
