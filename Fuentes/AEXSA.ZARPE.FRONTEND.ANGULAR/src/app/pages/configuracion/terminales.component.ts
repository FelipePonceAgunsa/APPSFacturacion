import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ToastrService } from 'ngx-toastr';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { ConfiguracionService } from '../../api/api/configuracion.service';
import { GrupoTerminal, TerminalMapaDto } from '../../api/model/models';
import { mensajeDeError } from '../../helpers/errores';

/** T4 Terminales (RN-079): el Administrador asocia cada terminal de AEXSA con STI, DP World u otro. */
@Component({
  selector: 'app-terminales',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [MatTableModule, MatSelectModule],
  template: `
    <div class="pagina">
      <h2>Terminales</h2>
      <p class="texto-secundario">El grupo define la regla del retiro directo sin convenio (refacturación STI o DP World).</p>
      <table mat-table [dataSource]="terminales()" class="tabla-completa">
        <ng-container matColumnDef="terminalId"><th mat-header-cell *matHeaderCellDef>Id AEXSA</th><td mat-cell *matCellDef="let t">{{ t.terminalId }}</td></ng-container>
        <ng-container matColumnDef="nombre"><th mat-header-cell *matHeaderCellDef>Nombre</th><td mat-cell *matCellDef="let t">{{ t.nombre }}</td></ng-container>
        <ng-container matColumnDef="grupo">
          <th mat-header-cell *matHeaderCellDef>Grupo</th>
          <td mat-cell *matCellDef="let t">
            <mat-select [value]="t.grupo === 'SIN_MAPEAR' ? null : t.grupo" placeholder="SIN MAPEAR"
              (selectionChange)="asignar(t, $event.value)" aria-label="Grupo del terminal">
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
export class TerminalesComponent implements OnInit {
  private readonly api = inject(ConfiguracionService);
  private readonly toastr = inject(ToastrService);

  readonly grupos: GrupoTerminal[] = ['STI', 'DP_WORLD', 'OTRO'];
  readonly columnas = ['terminalId', 'nombre', 'grupo', 'operacionesPendientes'];
  readonly terminales = signal<TerminalMapaDto[]>([]);

  async ngOnInit(): Promise<void> {
    try {
      this.terminales.set(await firstValueFrom(this.api.configuracionTerminales()));
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }

  async asignar(terminal: TerminalMapaDto, grupo: GrupoTerminal): Promise<void> {
    try {
      const actualizado = await firstValueFrom(this.api.configuracionAsignarTerminal(terminal.terminalId, { grupo }));
      this.terminales.update(lista => lista.map(t => (t.terminalId === actualizado.terminalId ? actualizado : t)));
      this.toastr.success(`${terminal.nombre}: ${grupo}`);
    } catch (e) {
      this.toastr.error(mensajeDeError(e));
    }
  }
}
