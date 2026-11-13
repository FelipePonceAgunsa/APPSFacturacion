import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OfaService } from '../../api/api/ofa.service';
import { OperacionDto } from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { FormularioCompletar, TEXTO_PENDIENTE, armarSolicitud, formularioVacio, opcionesDecision, textoIds } from './operaciones-util';
/**
 * F2 Detalle de la operación: ruta, convenio encontrado (o no), contenedores, servicios y pendientes.
 * "Completar datos" envía el PATCH con motivo obligatorio; "Calcular OFA" arma la prefactura.
 */
@Component({
  selector: 'app-operacion-detalle',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [
    DatePipe, DecimalPipe, FormsModule, RouterLink, MatTableModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatCardModule, HasAccessDirective,
  ],
  template: `
    <div class="pagina">
      @if (op(); as o) {
        <h2>Operación {{ textoIds(o.operacionIdAexsa) }} · {{ o.entidad }}</h2>
        <div class="fila-acciones">
          <a mat-button routerLink="/operaciones"><mat-icon>arrow_back</mat-icon>Operaciones</a>
          @if (o.ofaId) {
            <a mat-flat-button [routerLink]="['/ofa', o.ofaId]"><mat-icon>receipt_long</mat-icon>Ver OFA ({{ o.estadoOfa }})</a>
          } @else if (o.facturable && o.estadoOperacion === 'ACTIVA') {
            <button *hasAccess="[Access.Ofa]" mat-flat-button type="button" (click)="calcularOfa()"><mat-icon>calculate</mat-icon>Calcular OFA</button>
          }
        </div>
        <p [class.texto-error]="!o.convenio">
          @if (o.convenio; as c) {
            Convenio encontrado: versión {{ c.numero }} (vigencia {{ c.vigenciaDesde }} a {{ c.vigenciaHasta ?? 'sin término' }})
          } @else {
            Sin convenio para el RUT {{ o.rut }}: se cobra con tarifa pública.
          }
        </p>
        <mat-card appearance="outlined" class="section-card">
          <mat-card-content>
            <p>Estado {{ o.estadoOperacion }} · versión AEXSA {{ o.versionEvento }} · visado {{ o.visadoAprobadoEn | date: 'dd-MM-yyyy HH:mm' }}
              · {{ o.facturable ? 'facturable' : 'no facturable' }}</p>
            <p>Ruta clasificada: {{ o.rutaClasificada ?? 'sin clasificar (falta el tipo de retiro)' }} · momento {{ o.momentoFacturacion ?? '—' }}</p>
            <p>Carga {{ o.tipoCarga ?? '—' }} · sentido {{ o.sentido ?? '—' }} · retiro {{ o.tipoRetiro ?? '—' }} ·
              terminal {{ o.terminal ? o.terminal.id + ' ' + o.terminal.nombre : 'sin terminal' }} ({{ o.grupoTerminal }})</p>
            <p>DIN {{ o.din.join(', ') || '—' }} · BL {{ bls(o) }} · papeletas {{ o.papeletas.join(', ') || '—' }} · DUS {{ o.cantidadDus ?? '—' }}</p>
            <p>Consignatario {{ o.consignatario?.rut }} {{ o.consignatario?.razonSocial }} ·
              facturador {{ o.clienteFacturador ? o.clienteFacturador.rut + ' ' + (o.clienteFacturador.razonSocial ?? '') + ' (' + o.clienteFacturador.tipo + ')' : 'no indicado (se usa el consignatario)' }}</p>
          </mat-card-content>
        </mat-card>
        <h3>Contenedores</h3>
        <table mat-table [dataSource]="o.contenedores" class="tabla-completa">
          <ng-container matColumnDef="numero"><th mat-header-cell *matHeaderCellDef>Contenedor</th><td mat-cell *matCellDef="let c">{{ c.numero }}@if (c.esReefer) { · reefer }</td></ng-container>
          <ng-container matColumnDef="fechas"><th mat-header-cell *matHeaderCellDef>Recepción / retiro / despacho</th>
            <td mat-cell *matCellDef="let c">{{ c.fechaRecepcion | date: 'dd-MM-yy' }} / {{ c.fechaRetiro | date: 'dd-MM-yy' }} / {{ c.fechaDespacho | date: 'dd-MM-yy' }}</td></ng-container>
          <ng-container matColumnDef="dias"><th mat-header-cell *matHeaderCellDef>Días almacenaje / bajo aforo</th><td mat-cell *matCellDef="let c">{{ c.diasAlmacenaje ?? '—' }} / {{ c.diasBajoAforo ?? '—' }}</td></ng-container>
          <ng-container matColumnDef="inspecciones"><th mat-header-cell *matHeaderCellDef>Inspecciones</th>
            <td mat-cell *matCellDef="let c">@for (i of c.inspecciones; track $index) { <div>{{ i.tipo }}: {{ i.resultado ?? 'sin resultado' }} ({{ i.asignadaEn | date: 'dd-MM' }} → {{ i.finalizadaEn | date: 'dd-MM' }})</div> }</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columnasContenedor"></tr>
          <tr mat-row *matRowDef="let row; columns: columnasContenedor"></tr>
        </table>
        <h3>Servicios prestados</h3>
        <table mat-table [dataSource]="o.servicios" class="tabla-completa">
          <ng-container matColumnDef="servicio"><th mat-header-cell *matHeaderCellDef>Servicio</th><td mat-cell *matCellDef="let s">{{ s.tipoServicioId }} · {{ s.descripcion }}</td></ng-container>
          <ng-container matColumnDef="cantidad"><th mat-header-cell *matHeaderCellDef class="dato-numero">Cantidad</th><td mat-cell *matCellDef="let s" class="dato-numero" [class.texto-error]="s.cantidad === null">{{ s.cantidad === null ? 'pendiente' : (s.cantidad | number: '1.0-4') }}</td></ng-container>
          <ng-container matColumnDef="unidad"><th mat-header-cell *matHeaderCellDef>Unidad</th><td mat-cell *matCellDef="let s">{{ s.unidad }}</td></ng-container>
          <ng-container matColumnDef="contenedor"><th mat-header-cell *matHeaderCellDef>Contenedor</th><td mat-cell *matCellDef="let s">{{ s.contenedor ?? '—' }}</td></ng-container>
          <ng-container matColumnDef="origen"><th mat-header-cell *matHeaderCellDef>Origen</th><td mat-cell *matCellDef="let s">{{ s.origen ?? '—' }}</td></ng-container>
          <tr mat-header-row *matHeaderRowDef="columnasServicio"></tr>
          <tr mat-row *matRowDef="let row; columns: columnasServicio"></tr>
        </table>
        <h3>Pendientes</h3>
        @for (p of o.pendientesDetalle; track p.pendienteId) {
          <p [class.texto-error]="!p.resueltoEn && p.bloqueaValidacion" [class.texto-secundario]="!!p.resueltoEn">
            {{ texto[p.tipo] }} — {{ p.resueltoEn ? 'resuelto (' + p.resolucion + ') por ' + p.resueltoPor + (p.motivo ? ': ' + p.motivo : '') : (p.bloqueaValidacion ? 'bloquea la validación' : 'informativo') }}
          </p>
        } @empty {
          <p class="texto-secundario">Sin pendientes.</p>
        }
        @if (o.estadoOperacion === 'ACTIVA') {
          <mat-card *hasAccess="[Access.Operaciones_Edit]" appearance="outlined" class="section-card">
            <mat-card-header><mat-card-title>Completar datos</mat-card-title>
              <mat-card-subtitle>Cada cambio exige motivo y queda en la auditoría. Si hay OFA en borrador o validada, se recalcula.</mat-card-subtitle></mat-card-header>
            <mat-card-content>
              <div class="fila-acciones">
                <mat-form-field appearance="outline"><mat-label>Tipo de retiro</mat-label>
                  <mat-select [(ngModel)]="form.tipoRetiro"><mat-option [value]="null">Sin cambio</mat-option><mat-option value="DIRECTO">DIRECTO</mat-option><mat-option value="INDIRECTO">INDIRECTO</mat-option></mat-select></mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Sentido</mat-label>
                  <mat-select [(ngModel)]="form.sentido"><mat-option [value]="null">Sin cambio</mat-option><mat-option value="IMPORTACION">IMPORTACION</mat-option><mat-option value="EXPORTACION">EXPORTACION</mat-option></mat-select></mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Cantidad de DUS</mat-label><input matInput type="number" min="0" [(ngModel)]="form.cantidadDus" /></mat-form-field>
              </div>
              @if (o.contenedores.length) {
                <div class="fila-acciones">
                  <mat-form-field appearance="outline"><mat-label>Contenedor</mat-label>
                    <mat-select [(ngModel)]="form.contenedor"><mat-option [value]="null">Todos</mat-option>
                      @for (c of o.contenedores; track c.numero) { <mat-option [value]="c.numero">{{ c.numero }}</mat-option> }</mat-select></mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Fecha de despacho</mat-label><input matInput type="datetime-local" [(ngModel)]="form.fechaDespacho" /></mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Días bajo aforo</mat-label><input matInput type="number" min="0" [(ngModel)]="form.diasAforo" /></mat-form-field>
                </div>
              }
              @for (s of sinCantidad(); track s.tipoServicioId) {
                <mat-form-field appearance="outline"><mat-label>Cantidad de {{ s.tipoServicioId }} {{ s.descripcion }}</mat-label>
                  <input matInput type="number" min="0" [(ngModel)]="form.cantidades[s.tipoServicioId]" /></mat-form-field>
              }
              @for (p of decisionesAbiertas(); track p.pendienteId) {
                <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Decisión: {{ texto[p.tipo] }}</mat-label>
                  <mat-select [(ngModel)]="form.decisiones[p.tipo]"><mat-option [value]="null">Sin decidir</mat-option>
                    @for (d of opciones(p); track d.valor) { <mat-option [value]="d.valor">{{ d.texto }}</mat-option> }</mat-select></mat-form-field>
              }
              <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Motivo (obligatorio)</mat-label><textarea matInput rows="2" [(ngModel)]="form.motivo"></textarea></mat-form-field>
            </mat-card-content>
            <mat-card-actions>
              <button mat-flat-button type="button" [disabled]="!form.motivo.trim()" (click)="completar()"><mat-icon>save</mat-icon>Guardar</button>
            </mat-card-actions>
          </mat-card>
        }
      }
    </div>
  `,
})
export class OperacionDetalleComponent implements OnInit {
  private readonly api = inject(OperacionesService);
  private readonly ofas = inject(OfaService);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  private readonly operacionId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly Access = Access;
  readonly texto = TEXTO_PENDIENTE;
  readonly textoIds = textoIds;
  readonly opciones = opcionesDecision;
  readonly columnasContenedor = ['numero', 'fechas', 'dias', 'inspecciones'];
  readonly columnasServicio = ['servicio', 'cantidad', 'unidad', 'contenedor', 'origen'];
  readonly op = signal<OperacionDto | null>(null);
  readonly sinCantidad = computed(() => this.op()?.servicios.filter(s => s.cantidad === null) ?? []);
  readonly decisionesAbiertas = computed(() => this.op()?.pendientesDetalle.filter(p => p.exigeDecision && !p.resueltoEn) ?? []);
  form: FormularioCompletar = formularioVacio();
  ngOnInit(): Promise<void> {
    return this.cargar();
  }
  async cargar(): Promise<void> {
    try {
      this.op.set(await firstValueFrom(this.api.operacionesObtener(this.operacionId)));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  bls(o: OperacionDto): string {
    return o.bls.map(b => `${b.numero} (${b.id})`).join(', ');
  }
  async completar(): Promise<void> {
    try {
      this.op.set(await firstValueFrom(this.api.operacionesCompletar(this.operacionId, armarSolicitud(this.form))));
      this.form = formularioVacio();
      this.toastr.success('Datos guardados.');
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async calcularOfa(): Promise<void> {
    try {
      const ofa = await firstValueFrom(this.ofas.ofaCalcular(this.operacionId));
      await this.router.navigate(['/ofa', ofa.ofaId]);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
