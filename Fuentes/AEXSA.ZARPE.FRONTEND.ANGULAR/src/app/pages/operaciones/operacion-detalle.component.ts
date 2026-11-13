import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OfaService } from '../../api/api/ofa.service';
import { OfaDto, OperacionDto } from '../../api/model/models';
import { AuthService } from '../../services/auth.service';
import { Access } from '../../helpers/access.constants';
import { mensajeDeError } from '../../helpers/errores';
import { FormularioCompletar, armarSolicitud, formularioVacio, hayCambios, opcionesDecision, textoIds } from './operaciones-util';
import {
  fechaCorta, fechaHora, formatoNumero, textoConvenio, textoDiasAlmacenaje, textoGrupo, textoMomento, textoResultado, textoRetiro,
  textoRuta, textoSentido, unidad,
} from './operaciones-vista';
import { CalculoOfaComponent } from './calculo-ofa.component';
import { ComoFacturoComponent } from './como-facturo.component';

/**
 * F2 "Operación" (maqueta, pantalla 2): arriba lo que llegó de AEXSA, lo que falta se completa ahí mismo y,
 * debajo, el resultado: lo que corresponde cobrar y cómo facturar. Una sola pantalla, sin pasos intermedios.
 */
@Component({
  selector: 'app-operacion-detalle',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DatePipe, NgTemplateOutlet, FormsModule, RouterLink, MatButtonModule, MatIconModule, CalculoOfaComponent, ComoFacturoComponent],
  template: `
    <div class="zp-pagina">
      @if (op(); as o) {
        <a mat-button routerLink="/operaciones" class="zp-volver"><mat-icon>arrow_back</mat-icon>Volver a Por facturar</a>
        <h1>Operación {{ textoIds(o.operacionIdAexsa) }} · {{ o.razonSocial ?? o.rut }}</h1>
        <p class="zp-sub" style="margin-bottom:8px">RUT {{ o.rut }} · aprobada para facturar el {{ o.visadoAprobadoEn | date: 'dd-MM-yyyy' }}</p>
        <p class="zp-convenio" [class.zp-convenio--si]="!!o.convenio">{{ convenio(o) }}</p>

        @if (o.estadoOperacion === 'ANULADA') {
          <div class="zp-aviso zp-aviso--error" role="alert"><strong>Operación anulada en AEXSA</strong>No se calcula ni se factura.</div>
        } @else if (!o.facturable) {
          <div class="zp-aviso zp-aviso--error" role="alert"><strong>Esta operación no se factura</strong>Se marcó como no facturable.</div>
        }

        @if (abiertos().length) {
          <div class="zp-aviso" role="alert">
            <strong>{{ tituloAviso() }}</strong>
            <span>{{ textoAviso() }}</span>
            @for (p of abiertos(); track p.pendienteId) {
              <div class="zp-aviso-item">
                @if (!puedeEditar() || sinCampo(p.tipo)) { <span class="zp-aviso-tipo">{{ etiqueta(p.tipo) }}</span> }
                @if (puedeEditar()) {
                  @switch (p.tipo) {
                    @case ('TIPO_RETIRO') {
                      <div class="zp-campo"><label for="av-retiro">Tipo de retiro</label>
                        <select id="av-retiro" [(ngModel)]="aviso.tipoRetiro"><option [ngValue]="null">Elegir…</option><option value="DIRECTO">Directo</option><option value="INDIRECTO">Indirecto</option></select></div>
                    }
                    @case ('SENTIDO') {
                      <div class="zp-campo"><label for="av-sentido">Sentido</label>
                        <select id="av-sentido" [(ngModel)]="aviso.sentido"><option [ngValue]="null">Elegir…</option><option value="IMPORTACION">Importación</option><option value="EXPORTACION">Exportación</option></select></div>
                    }
                    @case ('CANTIDAD_DUS') {
                      <div class="zp-campo"><label for="av-dus">Cantidad de DUS</label><input id="av-dus" type="number" min="0" [(ngModel)]="aviso.cantidadDus" /></div>
                    }
                    @case ('CANTIDAD_SERVICIO') {
                      <div class="zp-fila-campos">
                        @for (s of sinCantidad(); track s.tipoServicioId) {
                          <div class="zp-campo"><label [for]="'av-c' + s.tipoServicioId">Cantidad de {{ s.descripcion }}</label>
                            <input [id]="'av-c' + s.tipoServicioId" type="number" min="0" [(ngModel)]="aviso.cantidades[s.tipoServicioId]" /></div>
                        }
                      </div>
                    }
                    @case ('DIAS_AFORO') {
                      <div class="zp-fila-campos">
                        @if (o.contenedores.length > 1) { <ng-container *ngTemplateOutlet="selContenedor" /> }
                        <div class="zp-campo"><label for="av-aforo">Días bajo aforo</label><input id="av-aforo" type="number" min="0" [(ngModel)]="aviso.diasAforo" /></div>
                      </div>
                    }
                    @case ('FECHA_DESPACHO') {
                      <div class="zp-fila-campos">
                        @if (o.contenedores.length > 1) { <ng-container *ngTemplateOutlet="selContenedor" /> }
                        <div class="zp-campo"><label for="av-desp">Fecha de despacho</label><input id="av-desp" type="datetime-local" [(ngModel)]="aviso.fechaDespacho" /></div>
                      </div>
                    }
                    @case ('TERMINAL_SIN_MAPEAR') {
                      @if (o.terminal; as t) {
                        <span>Asigna el grupo del terminal «{{ t.nombre }}» en <a routerLink="/configuracion/terminales">Configuración → Terminales</a> y se vuelve a calcular.</span>
                      } @else {
                        <span>AEXSA no informó el terminal. Se completa cuando AEXSA envíe una versión nueva de la operación con el terminal.</span>
                      }
                    }
                    @case ('INSPECCION_NO_FINALIZADA') {
                      <span>Espera a que AEXSA informe el término de la inspección. No se puede validar antes.</span>
                    }
                    @default {
                      <div class="zp-campo"><label [for]="'av-d-' + p.pendienteId">{{ etiqueta(p.tipo) }}</label>
                        <select [id]="'av-d-' + p.pendienteId" [(ngModel)]="aviso.decisiones[p.tipo]"><option [ngValue]="null">Elegir…</option>
                          @for (d of opciones(p); track d.valor) { <option [ngValue]="d.valor">{{ d.texto }}</option> }</select></div>
                    }
                  }
                }
              </div>
            }
            @if (puedeEditar() && hayCambios(aviso)) {
              <div class="zp-ajuste" style="margin-top:12px">
                <div class="zp-campo"><label for="av-motivo">Motivo (obligatorio)</label>
                  <input id="av-motivo" style="min-width:320px" [(ngModel)]="aviso.motivo" placeholder="Ej.: confirmado con el agente de aduana" /></div>
                <button mat-flat-button type="button" [disabled]="!aviso.motivo.trim()" (click)="guardar(aviso, 'aviso')">Guardar y calcular</button>
              </div>
              <p class="zp-nota">Queda registrado quién, cuándo y por qué.</p>
            }
          </div>
        }

        <section class="zp-tarjeta" aria-labelledby="t-datos">
          <div style="display:flex; justify-content:space-between; align-items:center; gap:12px">
            <h2 id="t-datos" style="margin:0">Datos de la operación</h2>
            @if (puedeCorregir()) {
              <button mat-button type="button" class="zp-link" (click)="abrirCorreccion()">Corregir datos</button>
            }
          </div>
          <dl class="zp-grid-op" style="margin-top:14px">
            <div><dt>Documento</dt><dd>{{ documentos(o) }}</dd></div>
            <div><dt>Contenedor</dt><dd>{{ contenedores(o) }}</dd></div>
            <div><dt>Terminal</dt><dd>{{ o.terminal ? o.terminal.nombre + ' (' + grupo[o.grupoTerminal] + ')' : 'Sin terminal' }}</dd></div>
            <div><dt>Retiro</dt><dd>{{ retiro(o.tipoRetiro) }}
              @if (o.tipoRetiro && puedeCorregir()) { <button type="button" class="zp-btn-link" (click)="abrirCorreccion(true)">Cambiar</button> }</dd></div>
            <div><dt>Recepción</dt><dd>{{ primeraFecha(o, 'fechaRecepcion') }}</dd></div>
            <div><dt>Retiro del contenedor</dt><dd>{{ primeraFecha(o, 'fechaRetiro') }}</dd></div>
            <div><dt>Días de almacenaje</dt><dd>{{ dias(o) }}</dd></div>
            <div><dt>Sentido</dt><dd>{{ sentido(o.sentido) }}</dd></div>
            <div><dt>Ruta</dt><dd>{{ ruta(o.rutaClasificada) }}</dd></div>
            <div><dt>Cuándo se factura</dt><dd>{{ momento(o.momentoFacturacion) }}</dd></div>
            <div><dt>DIN</dt><dd>{{ o.din.join(', ') || '—' }}</dd></div>
            <div><dt>Papeletas</dt><dd>{{ o.papeletas.join(', ') || '—' }}</dd></div>
            <div><dt>Consignatario</dt><dd>{{ o.consignatario?.razonSocial ?? o.consignatario?.rut ?? '—' }}<small>{{ o.consignatario?.rut }}</small></dd></div>
            <div><dt>Cliente facturador</dt><dd>@if (o.clienteFacturador; as c) { {{ c.razonSocial ?? c.rut }}<small>{{ c.rut }} · {{ c.tipo }}</small> } @else { El consignatario<small>AEXSA no indicó otro</small> }</dd></div>
          </dl>
        </section>

        @if (corrigiendo()) {
          <section class="zp-tarjeta zp-tarjeta--aviso" aria-labelledby="t-corr">
            <h2 id="t-corr">Corregir datos de la operación</h2>
            <p class="zp-nota" style="margin:0 0 10px">Úsalo si el dato venía mal de AEXSA o se equivocó alguien. Se vuelve a calcular. Los servicios agregados a mano se conservan; los ajustes de monto hay que repetirlos. Una factura ya enviada a SAP no se recalcula.</p>
            <div class="zp-fila-campos">
              <div class="zp-campo"><label for="co-retiro">Tipo de retiro</label>
                <select id="co-retiro" [(ngModel)]="corr.tipoRetiro"><option [ngValue]="null">Sin cambio</option><option value="DIRECTO">Directo</option><option value="INDIRECTO">Indirecto</option></select></div>
              <div class="zp-campo"><label for="co-sentido">Sentido</label>
                <select id="co-sentido" [(ngModel)]="corr.sentido"><option [ngValue]="null">Sin cambio</option><option value="IMPORTACION">Importación</option><option value="EXPORTACION">Exportación</option></select></div>
              <div class="zp-campo"><label for="co-dus">Cantidad de DUS</label><input id="co-dus" type="number" min="0" style="width:130px" [(ngModel)]="corr.cantidadDus" /></div>
            </div>
            @if (o.contenedores.length) {
              <div class="zp-fila-campos">
                <div class="zp-campo"><label for="co-cont">Contenedor</label>
                  <select id="co-cont" [(ngModel)]="corr.contenedor"><option [ngValue]="null">Todos</option>
                    @for (c of o.contenedores; track c.numero) { <option [ngValue]="c.numero">{{ c.numero }}</option> }</select></div>
                <div class="zp-campo"><label for="co-desp">Fecha de despacho</label><input id="co-desp" type="datetime-local" [(ngModel)]="corr.fechaDespacho" /></div>
                <div class="zp-campo"><label for="co-aforo">Días bajo aforo</label><input id="co-aforo" type="number" min="0" style="width:130px" [(ngModel)]="corr.diasAforo" /></div>
              </div>
            }
            <div class="zp-ajuste">
              <div class="zp-campo"><label for="co-motivo">Motivo (obligatorio)</label>
                <input id="co-motivo" style="min-width:320px" [(ngModel)]="corr.motivo" placeholder="Ej.: el cliente retiró con camión propio" /></div>
              <button mat-flat-button type="button" [disabled]="!hayCambios(corr) || !corr.motivo.trim()" (click)="guardar(corr, 'corr')">Guardar y recalcular</button>
              <button mat-button type="button" (click)="corrigiendo.set(false)">Cancelar</button>
            </div>
          </section>
        }

        @if (ofa(); as f) {
          <app-calculo-ofa [ofa]="f" (cambio)="cargar()" />
          <app-como-facturo [ofa]="f" (cambio)="cargar()" />
        } @else if (mensajeCalculo(); as m) {
          <section class="zp-tarjeta zp-tarjeta--aviso" aria-labelledby="t-nocalc">
            <h2 id="t-nocalc">Todavía no hay cálculo</h2>
            <p style="margin:0">{{ m }}</p>
          </section>
        } @else if (!o.pendientes.includes('TIPO_RETIRO') && o.facturable && o.estadoOperacion === 'ACTIVA') {
          <section class="zp-tarjeta" aria-labelledby="t-nocalc2">
            <h2 id="t-nocalc2">Todavía no hay cálculo</h2>
            <p class="zp-nota" style="margin:0">Se calcula al abrir la operación si tienes permiso para la prefactura.</p>
          </section>
        }

        <details class="zp-tarjeta zp-detalles">
          <summary>Ver lo que informó AEXSA (contenedores, inspecciones y servicios)</summary>
          @if (o.contenedores.length) {
            <table class="zp-tabla">
              <thead><tr><th>Contenedor</th><th>Recepción</th><th>Retiro</th><th>Despacho</th><th class="num">Días almacenaje</th><th class="num">Bajo aforo</th><th>Inspecciones</th></tr></thead>
              <tbody>
                @for (c of o.contenedores; track c.numero) {
                  <tr>
                    <td>{{ c.numero }}@if (c.esReefer) { <small>reefer</small> }</td>
                    <td>{{ fecha(c.fechaRecepcion) }}</td><td>{{ fecha(c.fechaRetiro) }}</td><td>{{ fecha(c.fechaDespacho) }}</td>
                    <td class="num">{{ c.diasAlmacenaje ?? '—' }}</td><td class="num">{{ c.diasBajoAforo ?? '—' }}</td>
                    <td>@for (i of c.inspecciones; track $index) { <div>{{ i.tipo }}: {{ i.resultado ? resultado[i.resultado] : 'sin resultado' }}<small>{{ fecha(i.asignadaEn) }} → {{ fecha(i.finalizadaEn) }}</small></div> } @empty { — }</td>
                  </tr>
                }
              </tbody>
            </table>
          }
          <table class="zp-tabla">
            <thead><tr><th>Servicio prestado</th><th class="num">Cantidad</th><th>Contenedor</th><th>Origen</th></tr></thead>
            <tbody>
              @for (s of o.servicios; track $index) {
                <tr>
                  <td>{{ s.descripcion ?? s.tipoServicioId }}</td>
                  <td class="num" [class.texto-error]="s.cantidad === null">{{ s.cantidad === null ? 'pendiente' : numero(s.cantidad) + ' ' + unidad(s.unidad) }}</td>
                  <td>{{ s.contenedor ?? '—' }}</td><td>{{ s.origen ?? '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
          @for (p of resueltos(); track p.pendienteId) {
            <p class="zp-nota">{{ etiqueta(p.tipo) }} — resuelto ({{ p.resolucion }}) por {{ p.resueltoPor }}{{ p.motivo ? ': ' + p.motivo : '' }}</p>
          }
          <p class="zp-nota">Versión {{ o.versionEvento }} de AEXSA · recibida el {{ fechaHora(o.actualizadoEn) }} · {{ o.entidad }}</p>
        </details>

        <p class="zp-sub">El cálculo es automático y siempre da el mismo resultado con los mismos datos. Solo una persona puede ajustar un monto, y debe indicar el motivo.</p>
      }
    </div>
    <ng-template #selContenedor>
      <div class="zp-campo"><label for="av-cont">Contenedor</label>
        <select id="av-cont" [(ngModel)]="aviso.contenedor"><option [ngValue]="null">Todos</option>
          @for (c of op()!.contenedores; track c.numero) { <option [ngValue]="c.numero">{{ c.numero }}</option> }</select></div>
    </ng-template>
  `,
})
export class OperacionDetalleComponent implements OnInit {
  private readonly api = inject(OperacionesService);
  private readonly ofas = inject(OfaService);
  private readonly auth = inject(AuthService);
  private readonly toastr = inject(ToastrService);
  private readonly operacionId = inject(ActivatedRoute).snapshot.paramMap.get('id')!;
  readonly Access = Access;
  readonly textoIds = textoIds;
  readonly opciones = opcionesDecision;
  readonly hayCambios = hayCambios;
  readonly numero = formatoNumero;
  readonly unidad = unidad;
  readonly grupo = textoGrupo;
  readonly resultado = textoResultado;
  readonly retiro = textoRetiro;
  readonly sentido = textoSentido;
  readonly ruta = textoRuta;
  readonly momento = textoMomento;
  readonly convenio = textoConvenio;
  readonly dias = textoDiasAlmacenaje;
  readonly fecha = fechaCorta;
  readonly fechaHora = fechaHora;
  readonly op = signal<OperacionDto | null>(null);
  readonly ofa = signal<OfaDto | null>(null);
  readonly mensajeCalculo = signal<string | null>(null);
  readonly corrigiendo = signal(false);
  aviso: FormularioCompletar = formularioVacio();
  corr: FormularioCompletar = formularioVacio();
  readonly abiertos = computed(() => this.op()?.pendientesDetalle.filter(p => !p.resueltoEn) ?? []);
  readonly resueltos = computed(() => this.op()?.pendientesDetalle.filter(p => !!p.resueltoEn) ?? []);
  readonly sinCantidad = computed(() => this.op()?.servicios.filter(s => s.cantidad === null) ?? []);
  ngOnInit(): Promise<void> {
    return this.cargar();
  }
  puedeEditar(): boolean {
    const o = this.op();
    return !!o && o.estadoOperacion === 'ACTIVA' && this.auth.hasAccess([Access.Operaciones_Edit]);
  }
  /** Corregir solo se ofrece mientras la OFA se puede recalcular (BORRADOR, VALIDADA o sin calcular). */
  puedeCorregir(): boolean {
    const estado = this.ofa()?.estado;
    return this.puedeEditar() && (!estado || estado === 'BORRADOR' || estado === 'VALIDADA');
  }
  etiqueta(tipo: string): string {
    return TEXTO_ETIQUETA[tipo] ?? tipo;
  }
  /** Pendientes que no tienen un campo para completar: solo se explican. */
  sinCampo(tipo: string): boolean {
    return tipo === 'TERMINAL_SIN_MAPEAR' || tipo === 'INSPECCION_NO_FINALIZADA';
  }
  tituloAviso(): string {
    const n = this.abiertos().length;
    return this.abiertos().some(p => p.tipo === 'TIPO_RETIRO')
      ? 'Todavía no se puede calcular: falta el tipo de retiro'
      : `Falta resolver ${n} cosa${n > 1 ? 's' : ''} antes de validar`;
  }
  textoAviso(): string {
    return this.abiertos().some(p => p.tipo === 'TIPO_RETIRO')
      ? 'AEXSA no informó si el retiro es directo o indirecto, y el cobro cambia según eso. Elígelo aquí y el cálculo aparece.'
      : 'Complétalo aquí mismo. Si ya hay un cálculo, se actualiza solo.';
  }
  documentos(o: OperacionDto): string {
    return o.bls.map(b => b.numero).join(', ') || '—';
  }
  contenedores(o: OperacionDto): string {
    return o.contenedores.map(c => c.numero).join(', ') || '—';
  }
  primeraFecha(o: OperacionDto, campo: 'fechaRecepcion' | 'fechaRetiro'): string {
    const c = o.contenedores.find(x => x[campo]);
    return c ? fechaCorta(c[campo]) : '—';
  }
  abrirCorreccion(soloRetiro = false): void {
    this.corr = formularioVacio();
    const actual = this.op()?.tipoRetiro;
    if (soloRetiro && actual) this.corr.tipoRetiro = actual === 'DIRECTO' ? 'INDIRECTO' : 'DIRECTO';
    this.corrigiendo.set(true);
  }
  async cargar(): Promise<void> {
    try {
      const o = await firstValueFrom(this.api.operacionesObtener(this.operacionId));
      this.op.set(o);
      this.ofa.set(o.ofaId ? await firstValueFrom(this.ofas.ofaObtener(o.ofaId)) : null);
      await this.calcularSiCorresponde();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
  /** Sin OFA y con lo necesario, el cálculo aparece solo (maqueta: no hay paso "armar la OFA"). */
  private async calcularSiCorresponde(): Promise<void> {
    const o = this.op()!;
    this.mensajeCalculo.set(null);
    if (o.ofaId || !o.facturable || o.estadoOperacion !== 'ACTIVA' || o.pendientes.includes('TIPO_RETIRO') || !this.auth.hasAccess([Access.Ofa])) return;
    try {
      this.ofa.set(await firstValueFrom(this.ofas.ofaCalcular(this.operacionId)));
      this.op.set(await firstValueFrom(this.api.operacionesObtener(this.operacionId)));
    } catch (e) {
      this.mensajeCalculo.set(mensajeDeError(e));
    }
  }
  async guardar(form: FormularioCompletar, cual: 'aviso' | 'corr'): Promise<void> {
    try {
      await firstValueFrom(this.api.operacionesCompletar(this.operacionId, armarSolicitud(form)));
      if (cual === 'aviso') this.aviso = formularioVacio();
      else {
        this.corr = formularioVacio();
        this.corrigiendo.set(false);
      }
      this.toastr.success('Datos guardados. Quedó registrado quién, cuándo y por qué.');
      await this.cargar();
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}

const TEXTO_ETIQUETA: Record<string, string> = {
  TIPO_RETIRO: 'Falta el tipo de retiro',
  SENTIDO: 'Falta el sentido (importación o exportación)',
  TERMINAL_SIN_MAPEAR: 'El terminal no tiene grupo asignado',
  CANTIDAD_DUS: 'Falta la cantidad de DUS',
  CANTIDAD_SERVICIO: 'Hay servicios sin cantidad',
  DIAS_AFORO: 'Faltan los días bajo aforo',
  FECHA_DESPACHO: 'Falta la fecha de despacho',
  VARIOS_CONVENIOS_VIGENTES: 'Hay varios convenios vigentes: elige cuál aplica',
  DOCUMENTO_CON_VARIAS_SOLICITUDES: 'El documento tiene otra solicitud activa: decide si se factura esta',
  INSPECCION_NO_FINALIZADA: 'Una inspección no ha terminado',
  INSPECCION_RECHAZADA: 'Una inspección fue rechazada: decide cómo seguir',
};
