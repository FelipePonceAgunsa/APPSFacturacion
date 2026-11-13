import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { VersionConvenioDto } from '../../api/model/models';
import { diferenciasVersion } from './convenios-util';

export interface ConfirmarVersionData {
  borrador: VersionConvenioDto;
  vigente: VersionConvenioDto | null;
}

/** C4 Confirmar versión: resumen y diferencias con la vigente. La confirmación es humana (ADR-007). */
@Component({
  selector: 'app-confirmar-version',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Confirmar versión {{ data.borrador.numero }}</h2>
    <mat-dialog-content>
      <p>
        Vigencia {{ data.borrador.vigenciaDesde }} a {{ data.borrador.vigenciaHasta ?? 'sin término' }} ·
        TC mínimo {{ data.borrador.tcMinimo ?? '—' }} · días libres {{ data.borrador.diasLibres ?? '—' }} ·
        {{ data.borrador.servicios.length }} servicios
      </p>
      <h3>{{ data.vigente ? 'Diferencias con la versión vigente ' + data.vigente.numero : 'Resumen' }}</h3>
      <ul>
        @for (d of diferencias; track d) {
          <li>{{ d }}</li>
        }
      </ul>
      @if (data.vigente) {
        <p class="texto-secundario">Al confirmar, la versión {{ data.vigente.numero }} pasa a REEMPLAZADA y se conserva en el histórico.</p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="false">Cancelar</button>
      <button mat-flat-button type="button" [mat-dialog-close]="true">Confirmar y dejar vigente</button>
    </mat-dialog-actions>
  `,
})
export class ConfirmarVersionDialog {
  readonly data = inject<ConfirmarVersionData>(MAT_DIALOG_DATA);
  readonly diferencias = diferenciasVersion(this.data.borrador, this.data.vigente);
}
