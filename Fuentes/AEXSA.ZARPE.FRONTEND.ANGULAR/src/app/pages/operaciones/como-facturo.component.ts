import { Component, ChangeDetectionStrategy, computed, inject, input, output, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { OfaService } from '../../api/api/ofa.service';
import { OfaDto, ResultadoValidacionDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';
import { formatoClp, formatoNumero, unidad } from './operaciones-vista';

/**
 * ¿Cómo facturo? (maqueta, pantalla 2): la persona revisa los montos, valida y emite la factura en SAP como hoy.
 * Al validar aparece la lista lista para digitar. "Enviar a SAP" se ve pero está apagado hasta la Fase B (tarea 3.3).
 */
@Component({
  selector: 'app-como-facturo',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [MatButtonModule, MatIconModule],
  template: `
    @let o = ofa();
    <section class="zp-tarjeta zp-tarjeta--acento" aria-labelledby="t-facturo">
      <h2 id="t-facturo">¿Cómo facturo?</h2>
      <ol class="zp-pasos">
        <li>Revisa los montos de arriba. Si algo no cuadra, abre «¿Por qué?» o usa «Ajustar».</li>
        <li>Valida el cálculo y entra a SAP a emitir la factura con estos valores, como hoy.</li>
      </ol>
      <div class="zp-acciones">
        @if (editable()) {
          <button mat-flat-button type="button" [disabled]="validando()" (click)="validar()"><mat-icon>fact_check</mat-icon>{{ o.estado === 'VALIDADA' ? 'Volver a validar' : 'Validar para digitar en SAP' }}</button>
        }
        <button mat-flat-button type="button" disabled style="opacity:.45;cursor:not-allowed"><mat-icon>send</mat-icon>Enviar a SAP</button>
        <span class="zp-chip neutro">Desactivado en esta etapa · se activa en la Fase B</span>
      </div>
      @if (resultado(); as r) {
        @if (!r.valida) {
          <div class="zp-aviso zp-aviso--error" role="alert" style="margin:14px 0 0">
            <strong>Todavía no se puede validar. Falta:</strong>
            <ul style="margin:6px 0 0; padding-left:20px">@for (f of r.faltantes; track f) { <li>{{ f }}</li> }</ul>
          </div>
        }
      }
      @if (o.estado === 'VALIDADA') {
        <div class="zp-ok-msg" role="status"><strong>Lista para digitar en SAP.</strong> El cálculo está validado.</div>
        <h3 style="margin:16px 0 4px; font-size:15px">Lista para digitar en SAP</h3>
        <p class="zp-nota" style="margin:0 0 8px">
          @for (a of o.asignaciones; track a.asignacionId) { Cliente {{ a.destinatarioRut }} {{ a.razonSocial ?? '' }} · }
          Kit sugerido: {{ o.kitSugerido ? o.kitSugerido.codigoKit + ' · ' + o.kitSugerido.nombre : 'sin kit para la ruta' }}
        </p>
        <table class="zp-tabla">
          <thead><tr><th>Artículo</th><th>Servicio</th><th class="num">Cantidad</th><th class="num">Precio</th><th>Moneda</th><th>Lista</th><th class="num">Monto (CLP)</th><th>En kit</th></tr></thead>
          <tbody>
            @for (l of o.lineas; track l.lineaId) {
              <tr>
                <td>{{ l.articuloSap }}</td>
                <td>{{ l.descripcionServicio }}</td>
                <td class="num">{{ numero(l.cantidad) }} {{ unidad(l.unidad) }}</td>
                <td class="num">{{ numero(l.tarifa, 4) }}</td>
                <td>{{ l.moneda }}</td>
                <td>{{ l.listaPrecios ?? l.fuenteTarifa }}</td>
                <td class="num">{{ clp(l.montoClp) }}</td>
                <td>{{ l.enKitSap === null ? '—' : (l.enKitSap ? 'Sí' : 'No') }}</td>
              </tr>
            }
          </tbody>
        </table>
        <div class="zp-total"><span>Total a digitar (neto)</span><span>{{ clp(o.totalClp) }}</span></div>
      }
      <p class="zp-nota">Por ahora el Satélite solo calcula y la factura se emite en SAP. Cuando se active el envío, este botón mandará la factura a SAP sin digitarla. Se prende con una configuración, sin cambiar la pantalla.</p>
    </section>
  `,
})
export class ComoFacturoComponent {
  private readonly api = inject(OfaService);
  private readonly toastr = inject(ToastrService);
  readonly ofa = input.required<OfaDto>();
  readonly cambio = output<void>();
  readonly numero = formatoNumero;
  readonly clp = formatoClp;
  readonly unidad = unidad;
  readonly editable = computed(() => ['BORRADOR', 'VALIDADA'].includes(this.ofa().estado));
  readonly resultado = signal<ResultadoValidacionDto | null>(null);
  readonly validando = signal(false);
  async validar(): Promise<void> {
    this.validando.set(true);
    try {
      this.resultado.set(await firstValueFrom(this.api.ofaValidar(this.ofa().ofaId)));
      this.cambio.emit();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    } finally {
      this.validando.set(false);
    }
  }
}
