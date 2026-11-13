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
import { OfaService } from '../../api/api/ofa.service';
import { AgregarLineaRequest, AjusteManualRequest, LineaOfaDto, OfaDto } from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { AgregarLineaDialog } from './agregar-linea.dialog';
import { AjusteLineaDialog } from './ajuste-linea.dialog';
import { NOMBRE_REGLA, problemaLinea, textoDetalle } from './ofa-util';
/**
 * F3 OFA: líneas valorizadas con su memoria de cálculo (reglas, versión de convenio y de catálogo, tipo de cambio,
 * ajuste). Desde aquí se agrega un ítem a mano, se ajusta un monto (F4) y se recalcula.
 */
@Component({
  selector: 'app-ofa-detalle',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DatePipe, DecimalPipe, RouterLink, MatTableModule, MatButtonModule, MatIconModule, MatCardModule, HasAccessDirective],
  template: `
    <div class="pagina">
      @if (ofa(); as o) {
        <h2>OFA {{ o.operacionIdAexsa.join(', ') }} · {{ o.estado }}</h2>
        <p class="texto-secundario">Versión de cálculo {{ o.versionCalculo }} del {{ o.fechaCalculo | date: 'dd-MM-yyyy HH:mm' }} ·
          catálogo versión {{ o.versionCatalogo ?? '—' }} · kit sugerido {{ o.kitSugerido ? o.kitSugerido.codigoKit + ' ' + o.kitSugerido.nombre : 'sin kit para la ruta' }}</p>
        @for (a of o.asignaciones; track a.asignacionId) {
          <p>Se cobra a {{ a.destinatarioRut }} {{ a.razonSocial ?? '' }} ({{ a.rol }}, {{ a.origenInstruccion }})</p>
        }
        <div class="fila-acciones">
          <a mat-button [routerLink]="['/operaciones', o.operacionId]"><mat-icon>arrow_back</mat-icon>Operación</a>
          @if (editable()) {
            <button *hasAccess="[Access.Ofa_AjusteManual]" mat-stroked-button type="button" (click)="agregar()"><mat-icon>add</mat-icon>Agregar ítem</button>
            <button mat-stroked-button type="button" (click)="recalcular()"><mat-icon>refresh</mat-icon>Recalcular</button>
          }
          <a mat-flat-button [routerLink]="['/ofa', o.ofaId, 'digitar']"><mat-icon>fact_check</mat-icon>Validar y digitar en SAP</a>
        </div>
        <table mat-table [dataSource]="o.lineas" class="tabla-completa">
          <ng-container matColumnDef="servicio"><th mat-header-cell *matHeaderCellDef>Servicio</th><td mat-cell *matCellDef="let l">{{ l.tipoServicioId }} · {{ l.descripcionServicio }}</td></ng-container>
          <ng-container matColumnDef="articulo"><th mat-header-cell *matHeaderCellDef>Artículo SAP</th><td mat-cell *matCellDef="let l" [class.texto-error]="!l.articuloSap">{{ l.articuloSap ?? 'sin artículo SAP' }}</td></ng-container>
          <ng-container matColumnDef="cantidad"><th mat-header-cell *matHeaderCellDef class="dato-numero">Cantidad</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.cantidad | number: '1.0-4' }} {{ l.unidad }}</td></ng-container>
          <ng-container matColumnDef="tarifa"><th mat-header-cell *matHeaderCellDef class="dato-numero">Tarifa</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.tarifa | number: '1.0-4' }} {{ l.moneda }}</td></ng-container>
          <ng-container matColumnDef="monto"><th mat-header-cell *matHeaderCellDef class="dato-numero">Monto CLP</th><td mat-cell *matCellDef="let l" class="dato-numero">{{ l.montoClp | number: '1.0-4' }}</td></ng-container>
          <ng-container matColumnDef="marcas"><th mat-header-cell *matHeaderCellDef>Origen</th>
            <td mat-cell *matCellDef="let l">{{ l.origen }}{{ l.esExcepcion ? ' · excepción' : '' }}{{ l.enKitSap === null ? '' : (l.enKitSap ? ' · en kit' : ' · fuera del kit') }}
              @if (problema(l); as p) { · <span class="texto-error">{{ p }}</span> }</td></ng-container>
          <ng-container matColumnDef="acciones"><th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let l">
              <button mat-button type="button" (click)="seleccionada.set(l)">Memoria</button>
              @if (editable()) { <button *hasAccess="[Access.Ofa_AjusteManual]" mat-button type="button" (click)="ajustar(l)">Ajustar</button> }
            </td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columnas"></tr>
          <tr mat-row *matRowDef="let row; columns: columnas"></tr>
        </table>
        <p class="dato-numero"><strong>Total {{ o.totalClp | number: '1.0-4' }} CLP</strong></p>
        @if (seleccionada(); as l) {
          <mat-card appearance="outlined" class="section-card">
            <mat-card-header>
              <mat-card-title>Memoria de cálculo · {{ l.tipoServicioId }} {{ l.descripcionServicio }}</mat-card-title>
              <mat-card-subtitle>
                Convenio {{ l.memoria.versionConvenio ? 'versión ' + l.memoria.versionConvenio : 'no aplica' }} · catálogo versión {{ l.memoria.versionTarifaPublica ?? '—' }} ·
                TC {{ l.memoria.tipoCambioAplicado ?? '—' }} · tarifa {{ l.fuenteTarifa }} {{ l.listaPrecios ?? '' }} · {{ l.cantidadMemorias }} registro(s)
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              @if (l.origen === 'AGREGADA_MANUAL') { <p>Agregada por {{ l.agregadaPor }} el {{ l.agregadaEn | date: 'dd-MM-yyyy HH:mm' }}: {{ l.motivoAgregada }}</p> }
              @if (l.memoria.ajusteManual; as a) {
                <p>Ajuste de {{ a.usuario }} el {{ a.fecha | date: 'dd-MM-yyyy HH:mm' }}: {{ a.valorAnterior | number: '1.0-4' }} → {{ a.valorNuevo | number: '1.0-4' }} ({{ a.motivo }})</p>
              }
              <ol>
                @for (r of l.memoria.reglasAplicadas; track $index) {
                  <li><strong>{{ nombre(r.regla) }}</strong> v{{ r.version }} — {{ detalle(r) }}</li>
                }
              </ol>
            </mat-card-content>
          </mat-card>
        }
      }
    </div>
  `,
})
export class OfaDetalleComponent implements OnInit {
  private readonly api = inject(OfaService);
  private readonly toastr = inject(ToastrService);
  private readonly dialog = inject(MatDialog);
  private readonly ofaId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly Access = Access;
  readonly columnas = ['servicio', 'articulo', 'cantidad', 'tarifa', 'monto', 'marcas', 'acciones'];
  readonly ofa = signal<OfaDto | null>(null);
  readonly seleccionada = signal<LineaOfaDto | null>(null);
  readonly editable = computed(() => ['BORRADOR', 'VALIDADA'].includes(this.ofa()?.estado ?? ''));
  readonly problema = problemaLinea;
  readonly detalle = textoDetalle;
  ngOnInit(): Promise<void> {
    return this.cargar();
  }
  nombre(regla: string): string {
    return NOMBRE_REGLA[regla] ?? regla;
  }
  async cargar(): Promise<void> {
    try {
      this.ofa.set(await firstValueFrom(this.api.ofaObtener(this.ofaId)));
      const elegida = this.seleccionada();
      this.seleccionada.set(elegida ? (this.ofa()!.lineas.find(l => l.lineaId === elegida.lineaId) ?? null) : null);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async agregar(): Promise<void> {
    const solicitud = await firstValueFrom(this.dialog.open(AgregarLineaDialog, { width: '560px' }).afterClosed()) as AgregarLineaRequest | null;
    if (!solicitud) return;
    try {
      const linea = await firstValueFrom(this.api.ofaAgregarLinea(this.ofaId, solicitud));
      this.toastr.success(`Ítem agregado: ${linea.montoClp} CLP.`);
      this.seleccionada.set(linea);
      await this.cargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async ajustar(linea: LineaOfaDto): Promise<void> {
    const solicitud = await firstValueFrom(this.dialog.open(AjusteLineaDialog, { data: linea, width: '480px' }).afterClosed()) as AjusteManualRequest | null;
    if (!solicitud) return;
    try {
      const ajustada = await firstValueFrom(this.api.ofaAjustarLinea(this.ofaId, linea.lineaId, solicitud));
      this.toastr.success('Monto ajustado.');
      this.seleccionada.set(ajustada);
      await this.cargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async recalcular(): Promise<void> {
    try {
      const o = await firstValueFrom(this.api.ofaCalcular(this.ofa()!.operacionId));
      this.toastr.success(`Recalculada: versión ${o.versionCalculo}. Los ajustes de monto se deben repetir.`);
      this.seleccionada.set(null);
      this.ofa.set(o);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
