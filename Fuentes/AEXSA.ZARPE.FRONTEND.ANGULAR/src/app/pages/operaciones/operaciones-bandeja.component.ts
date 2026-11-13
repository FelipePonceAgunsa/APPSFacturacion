import { Component, ChangeDetectionStrategy, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { OperacionesService } from '../../api/api/operaciones.service';
import { OperacionResumenDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';
import { textoIds } from './operaciones-util';
import { estadoOperacion, textoRetiro } from './operaciones-vista';

export type FiltroBandeja = 'todas' | 'falta' | 'ok';

/**
 * F1 "Por facturar": lo que llegó de AEXSA y qué tiene que mirar la persona (maqueta, pantalla 1).
 * Filtros rápidos y buscador; un clic (o Enter) abre la operación. El estado siempre lleva texto.
 */
@Component({
  selector: 'app-operaciones-bandeja',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DatePipe],
  template: `
    <div class="zp-pagina">
      <h1>Por facturar</h1>
      <p class="zp-sub">Operaciones que llegaron de AEXSA SYS. Abre una para ver <strong>cuánto cobrar</strong>. La factura se emite en SAP, con los valores que calcula esta pantalla.</p>
      <div class="zp-filtros" role="group" aria-label="Filtros">
        <button type="button" class="zp-pill" [attr.aria-pressed]="filtro() === 'todas'" (click)="filtro.set('todas')">Todas</button>
        <button type="button" class="zp-pill" [attr.aria-pressed]="filtro() === 'falta'" (click)="filtro.set('falta')">Con dato pendiente</button>
        <button type="button" class="zp-pill" [attr.aria-pressed]="filtro() === 'ok'" (click)="filtro.set('ok')">Calculadas</button>
        <input type="search" aria-label="Buscar por cliente u operación" placeholder="Buscar cliente u operación"
               [value]="busqueda()" (input)="busqueda.set($any($event.target).value)" />
      </div>
      <div class="zp-tarjeta">
        <table class="zp-tabla">
          <thead><tr><th>Operación</th><th>Cliente</th><th>Convenio</th><th>Retiro</th><th>Aprobada el</th><th>Estado</th></tr></thead>
          <tbody>
            @for (o of visibles(); track o.operacionId) {
              <tr class="zp-fila-click" tabindex="0" (click)="abrir(o)" (keydown.enter)="abrir(o)">
                <td>{{ textoIds(o.operacionIdAexsa) }}</td>
                <td>{{ o.razonSocial ?? o.rut }}<small>{{ o.rut }}</small></td>
                <td>{{ o.conConvenio ? 'Sí' : 'No' }}</td>
                <td>{{ retiro(o) }}</td>
                <td>{{ o.visadoAprobadoEn | date: 'dd-MM-yyyy' }}</td>
                <td><span class="zp-chip" [class]="estado(o).clase">{{ estado(o).texto }}</span></td>
              </tr>
            } @empty {
              <tr><td colspan="6">{{ cargado() ? 'No hay operaciones con esos filtros.' : 'Cargando…' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
    </div>
  `,
})
export class OperacionesBandejaComponent implements OnInit {
  private readonly api = inject(OperacionesService);
  private readonly toastr = inject(ToastrService);
  private readonly router = inject(Router);
  readonly textoIds = textoIds;
  readonly estado = estadoOperacion;
  readonly operaciones = signal<OperacionResumenDto[]>([]);
  readonly cargado = signal(false);
  readonly filtro = signal<FiltroBandeja>('todas');
  readonly busqueda = signal('');
  readonly visibles = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    return this.operaciones().filter(o => {
      if (this.filtro() !== 'todas' && estadoOperacion(o).clase !== this.filtro()) return false;
      return !q || `${textoIds(o.operacionIdAexsa)} ${o.razonSocial ?? ''} ${o.rut ?? ''}`.toLowerCase().includes(q);
    });
  });
  ngOnInit(): Promise<void> {
    return this.cargar();
  }
  retiro(o: OperacionResumenDto): string {
    return o.tipoRetiro ? textoRetiro(o.tipoRetiro) : '—';
  }
  abrir(o: OperacionResumenDto): void {
    void this.router.navigate(['/operaciones', o.operacionId]);
  }
  async cargar(): Promise<void> {
    try {
      this.operaciones.set(await firstValueFrom(this.api.operacionesListar()));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    } finally {
      this.cargado.set(true);
    }
  }
}
