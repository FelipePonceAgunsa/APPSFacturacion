import { Component, ChangeDetectionStrategy, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { OfaService } from '../../api/api/ofa.service';
import { CatalogoService } from '../../api/api/catalogo.service';
import { AgregarLineaRequest, LineaOfaDto, Moneda, OfaDto, TipoServicioDto } from '../../api/model/models';
import { HasAccessDirective } from '../../helpers/has-access.directive';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { NOMBRE_REGLA, textoDetalle } from './memoria-calculo';
import { explicarLinea } from './explicar-linea';
import { formatoClp, formatoNumero, fuenteLinea, problemaLinea, unidad } from './operaciones-vista';

/**
 * Lo que corresponde cobrar (maqueta, pantalla 2): líneas con cantidad, tarifa, de dónde sale la tarifa y monto;
 * "¿Por qué?" en lenguaje común, "Ajustar" con motivo (F4), "+ Agregar un servicio que falta" y el total.
 * Todo ocurre en la misma página: la persona no sale a otra pantalla.
 */
@Component({
  selector: 'app-calculo-ofa',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [FormsModule, MatButtonModule, MatIconModule, HasAccessDirective],
  template: `
    @let o = ofa();
    <section class="zp-tarjeta" aria-labelledby="t-calculo">
      <h2 id="t-calculo">Lo que corresponde cobrar</h2>
      <p class="zp-sub" style="margin-bottom:10px">
        @for (a of o.asignaciones; track a.asignacionId) { Se cobra a {{ a.razonSocial ?? a.destinatarioRut }} ({{ a.destinatarioRut }}) · }
        cálculo versión {{ o.versionCalculo }} · catálogo SAP versión {{ o.versionCatalogo ?? '—' }}
      </p>
      <table class="zp-tabla">
        <thead>
          <tr>
            <th>Servicio</th><th class="num">Cantidad</th><th class="num">Tarifa</th><th>Tarifa según</th><th class="num">Monto (CLP)</th>
            <th><span class="zp-sr">Acciones</span></th>
          </tr>
        </thead>
        <tbody>
          @for (l of o.lineas; track l.lineaId) {
            <tr>
              <td>
                {{ l.descripcionServicio ?? l.tipoServicioId }}
                @if (l.articuloSap) { <small>Artículo SAP {{ l.articuloSap }}</small> }
                @if (problema(l); as p) { <span class="zp-chip falta">{{ p }}</span> }
              </td>
              <td class="num">{{ numero(l.cantidad) }}<small>{{ unidad(l.unidad) }}</small></td>
              <td class="num">{{ tarifa(l) }}</td>
              <td><span class="zp-chip" [class]="fuente(l).clase">{{ fuente(l).texto }}</span></td>
              <td class="num"><strong>{{ clp(l.montoClp) }}</strong></td>
              <td>
                <button mat-button type="button" class="zp-link" [attr.aria-expanded]="abiertas().has(l.lineaId)" (click)="alternarPorque(l.lineaId)">¿Por qué?</button>
                @if (editable()) {
                  <button *hasAccess="[Access.Ofa_AjusteManual]" mat-button type="button" class="zp-link" (click)="abrirAjuste(l)">Ajustar</button>
                }
              </td>
            </tr>
            @if (abiertas().has(l.lineaId)) {
              <tr class="zp-fila-extra"><td colspan="6">
                <div class="zp-porque">
                  <p>{{ explicar(l) }}</p>
                  <details>
                    <summary>Detalle técnico de la memoria de cálculo</summary>
                    <ol>
                      @for (r of l.memoria.reglasAplicadas; track $index) {
                        <li><strong>{{ nombre(r.regla) }}</strong> v{{ r.version }} — {{ detalle(r) }}</li>
                      }
                    </ol>
                    <small>Convenio {{ l.memoria.versionConvenio ? 'versión ' + l.memoria.versionConvenio : 'no aplica' }} · catálogo versión {{ l.memoria.versionTarifaPublica ?? '—' }} · {{ l.cantidadMemorias }} registro(s)</small>
                  </details>
                </div>
              </td></tr>
            }
            @if (ajustando() === l.lineaId) {
              <tr class="zp-fila-extra"><td colspan="6">
                <div class="zp-ajuste">
                  <div class="zp-campo"><label [for]="'am-' + l.lineaId">Nuevo monto (CLP)</label>
                    <input [id]="'am-' + l.lineaId" type="number" min="0" [(ngModel)]="ajMonto" /></div>
                  <div class="zp-campo"><label [for]="'ma-' + l.lineaId">Motivo (obligatorio)</label>
                    <input [id]="'ma-' + l.lineaId" style="min-width:300px" [(ngModel)]="ajMotivo" placeholder="Ej.: descuento acordado con el cliente" /></div>
                  <button mat-flat-button type="button" [disabled]="!ajusteValido()" (click)="guardarAjuste(l)">Guardar ajuste</button>
                  <button mat-button type="button" (click)="ajustando.set(null)">Cancelar</button>
                </div>
                <p class="zp-nota">Queda registrado quién, cuándo y el valor anterior. Si recalculas, los ajustes de monto hay que repetirlos.</p>
              </td></tr>
            }
          } @empty {
            <tr><td colspan="6">La OFA no tiene líneas.</td></tr>
          }
        </tbody>
      </table>

      @if (editable()) {
        <div style="padding:10px 0 0">
          <button *hasAccess="[Access.Ofa_AjusteManual]" mat-button type="button" class="zp-link" (click)="abrirAgregar()"><mat-icon>add</mat-icon>Agregar un servicio que falta</button>
        </div>
        @if (agregando()) {
          <div class="zp-panel">
            <p class="zp-nota" style="margin:0 0 8px">Para algo que el sistema no sabe y tú sí. Se suma al total y queda registrado quién lo agregó y por qué.</p>
            <div class="zp-ajuste">
              <div class="zp-campo"><label for="ag-serv">Servicio</label>
                <select id="ag-serv" [(ngModel)]="agTipo">
                  <option [ngValue]="null">Elegir…</option>
                  @for (t of tipos(); track t.tipoServicioId) { <option [ngValue]="t.tipoServicioId">{{ t.descripcion }} ({{ unidad(t.unidad) }})</option> }
                </select></div>
              <div class="zp-campo"><label for="ag-cant">Cantidad</label><input id="ag-cant" type="number" min="0" style="width:90px" [(ngModel)]="agCantidad" /></div>
              <div class="zp-campo"><label for="ag-tarifa">Tarifa (opcional)</label><input id="ag-tarifa" type="number" min="0" style="width:130px" [(ngModel)]="agTarifa" />
                <small>Vacía: la del convenio o la pública.</small></div>
              <div class="zp-campo"><label for="ag-moneda">Moneda</label>
                <select id="ag-moneda" [disabled]="agTarifa === null || agTarifa === ''" [(ngModel)]="agMoneda">
                  <option value="CLP">CLP</option><option value="USD">USD</option><option value="UF">UF</option></select></div>
              <div class="zp-campo"><label for="ag-motivo">Motivo (obligatorio)</label>
                <input id="ag-motivo" style="min-width:260px" [(ngModel)]="agMotivo" placeholder="Ej.: lo pidió el cliente por correo" /></div>
              <button mat-flat-button type="button" [disabled]="!agregarValido()" (click)="guardarAgregar()">Agregar</button>
              <button mat-button type="button" (click)="agregando.set(false)">Cancelar</button>
            </div>
          </div>
        }
      }

      <div class="zp-total"><span>Total a cobrar (neto)</span><span>{{ clp(o.totalClp) }}</span></div>
      @if (editable()) {
        <div class="zp-acciones">
          <button mat-button type="button" class="zp-link" (click)="recalcular()"><mat-icon>refresh</mat-icon>Recalcular con los datos actuales</button>
          <span class="zp-nota" style="margin:0">Los servicios agregados a mano se conservan; los ajustes de monto hay que repetirlos.</span>
        </div>
      }
    </section>
  `,
})
export class CalculoOfaComponent {
  private readonly api = inject(OfaService);
  private readonly catalogo = inject(CatalogoService);
  private readonly toastr = inject(ToastrService);
  readonly ofa = input.required<OfaDto>();
  readonly cambio = output<void>();
  readonly Access = Access;
  readonly numero = formatoNumero;
  readonly clp = formatoClp;
  readonly unidad = unidad;
  readonly fuente = fuenteLinea;
  readonly problema = problemaLinea;
  readonly explicar = explicarLinea;
  readonly detalle = textoDetalle;
  readonly editable = computed(() => ['BORRADOR', 'VALIDADA'].includes(this.ofa().estado));
  readonly abiertas = signal<ReadonlySet<string>>(new Set());
  readonly ajustando = signal<string | null>(null);
  readonly agregando = signal(false);
  readonly tipos = signal<TipoServicioDto[]>([]);
  ajMonto: number | null = null;
  ajMotivo = '';
  agTipo: number | null = null;
  agCantidad: number | null = 1;
  agTarifa: number | string | null = null;
  agMoneda: Moneda = 'CLP';
  agMotivo = '';
  nombre(regla: string): string {
    return NOMBRE_REGLA[regla] ?? regla;
  }
  tarifa(l: LineaOfaDto): string {
    return l.moneda === 'CLP' ? formatoClp(l.tarifa) : `${l.moneda} ${formatoNumero(l.tarifa, 4)}`;
  }
  alternarPorque(id: string): void {
    const nuevo = new Set(this.abiertas());
    if (!nuevo.delete(id)) nuevo.add(id);
    this.abiertas.set(nuevo);
  }
  abrirAjuste(l: LineaOfaDto): void {
    this.ajMonto = l.montoClp;
    this.ajMotivo = '';
    this.ajustando.set(l.lineaId);
  }
  ajusteValido(): boolean {
    return this.ajMonto !== null && String(this.ajMonto) !== '' && Number(this.ajMonto) >= 0 && this.ajMotivo.trim().length > 0;
  }
  async guardarAjuste(l: LineaOfaDto): Promise<void> {
    try {
      await firstValueFrom(this.api.ofaAjustarLinea(this.ofa().ofaId, l.lineaId, { nuevoMonto: Number(this.ajMonto), motivo: this.ajMotivo.trim() }));
      this.toastr.success('Monto ajustado. Quedó registrado quién, cuándo y por qué.');
      this.ajustando.set(null);
      this.cambio.emit();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async abrirAgregar(): Promise<void> {
    this.agTipo = null;
    this.agCantidad = 1;
    this.agTarifa = null;
    this.agMoneda = 'CLP';
    this.agMotivo = '';
    this.agregando.set(true);
    if (!this.tipos().length) {
      try {
        this.tipos.set((await firstValueFrom(this.catalogo.tiposServicioListar())).filter(t => t.habilitado));
      } catch (e) {
        this.toastr.error(mensajeDeError(e));
      }
    }
  }
  agregarValido(): boolean {
    return this.agTipo !== null && (this.agCantidad ?? 0) > 0 && this.agMotivo.trim().length > 0;
  }
  solicitudAgregar(): AgregarLineaRequest {
    const conTarifa = this.agTarifa !== null && String(this.agTarifa) !== '';
    return {
      tipoServicioId: this.agTipo!,
      cantidad: Number(this.agCantidad),
      motivo: this.agMotivo.trim(),
      tarifa: conTarifa ? Number(this.agTarifa) : null,
      moneda: conTarifa ? this.agMoneda : null,
    };
  }
  async guardarAgregar(): Promise<void> {
    try {
      const linea = await firstValueFrom(this.api.ofaAgregarLinea(this.ofa().ofaId, this.solicitudAgregar()));
      this.toastr.success(`Servicio agregado: ${formatoClp(linea.montoClp)}.`);
      this.agregando.set(false);
      this.cambio.emit();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  async recalcular(): Promise<void> {
    try {
      const o = await firstValueFrom(this.api.ofaCalcular(this.ofa().operacionId));
      this.toastr.success(`Recalculada: versión ${o.versionCalculo}. Los ajustes de monto se deben repetir.`);
      this.ajustando.set(null);
      this.cambio.emit();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
