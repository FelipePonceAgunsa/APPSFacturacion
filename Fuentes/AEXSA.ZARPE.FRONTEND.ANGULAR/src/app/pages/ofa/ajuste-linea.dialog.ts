import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { AjusteManualRequest, LineaOfaDto } from '../../api/model/models';
/**
 * F4 Ajuste manual de monto (REQ-006, nivel 3). El monto lo fija la persona, con motivo obligatorio;
 * queda en la memoria de cálculo con usuario, fecha, valor anterior y valor nuevo. Se repite tras un recálculo.
 */
@Component({
  selector: 'app-ajuste-linea',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [DecimalPipe, FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Ajustar monto</h2>
    <mat-dialog-content>
      <p>{{ linea.tipoServicioId }} · {{ linea.descripcionServicio }} — monto actual {{ linea.montoClp | number: '1.0-4' }} CLP</p>
      <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Monto nuevo (CLP)</mat-label><input matInput type="number" min="0" [(ngModel)]="nuevoMonto" /></mat-form-field>
      <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Motivo (obligatorio)</mat-label><textarea matInput rows="2" [(ngModel)]="motivo"></textarea></mat-form-field>
      <p class="texto-secundario">Si la OFA se recalcula, los ajustes de monto no se copian a la versión nueva y hay que repetirlos.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="null">Cancelar</button>
      <button mat-flat-button type="button" [disabled]="!valido()" (click)="aceptar()">Ajustar</button>
    </mat-dialog-actions>
  `,
})
export class AjusteLineaDialog {
  readonly linea = inject<LineaOfaDto>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<AjusteLineaDialog, AjusteManualRequest | null>);
  nuevoMonto: number | null = this.linea.montoClp;
  motivo = '';
  valido(): boolean {
    return this.nuevoMonto !== null && String(this.nuevoMonto) !== '' && Number(this.nuevoMonto) >= 0 && this.motivo.trim().length > 0;
  }
  aceptar(): void {
    this.ref.close({ nuevoMonto: Number(this.nuevoMonto), motivo: this.motivo.trim() });
  }
}
