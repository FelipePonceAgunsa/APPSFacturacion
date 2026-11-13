import { Component, ChangeDetectionStrategy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { CatalogoService } from '../../api/api/catalogo.service';
import { AgregarLineaRequest, Moneda, TipoServicioDto } from '../../api/model/models';
/**
 * Agregar a mano un servicio que el cálculo no incluyó (tarea 2.2). Sin tarifa se usa la del convenio o la pública.
 * El motivo es obligatorio y queda en la memoria de cálculo.
 */
@Component({
  selector: 'app-agregar-linea',
  changeDetection: ChangeDetectionStrategy.Eager,
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Agregar ítem</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Servicio</mat-label>
        <mat-select [(ngModel)]="tipoServicioId">
          @for (t of tipos(); track t.tipoServicioId) { <mat-option [value]="t.tipoServicioId">{{ t.tipoServicioId }} · {{ t.descripcion }} ({{ t.unidad }})</mat-option> }
        </mat-select></mat-form-field>
      <div class="fila-acciones">
        <mat-form-field appearance="outline"><mat-label>Cantidad</mat-label><input matInput type="number" min="0" [(ngModel)]="cantidad" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Tarifa (opcional)</mat-label><input matInput type="number" min="0" [(ngModel)]="tarifa" /></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Moneda</mat-label>
          <mat-select [(ngModel)]="moneda" [disabled]="tarifa === null"><mat-option value="CLP">CLP</mat-option><mat-option value="USD">USD</mat-option><mat-option value="UF">UF</mat-option></mat-select></mat-form-field>
      </div>
      <mat-form-field appearance="outline" class="tabla-completa"><mat-label>Motivo (obligatorio)</mat-label><textarea matInput rows="2" [(ngModel)]="motivo"></textarea></mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" [mat-dialog-close]="null">Cancelar</button>
      <button mat-flat-button type="button" [disabled]="!valido()" (click)="aceptar()">Agregar</button>
    </mat-dialog-actions>
  `,
})
export class AgregarLineaDialog implements OnInit {
  private readonly catalogo = inject(CatalogoService);
  private readonly ref = inject(MatDialogRef<AgregarLineaDialog, AgregarLineaRequest | null>);
  readonly tipos = signal<TipoServicioDto[]>([]);
  tipoServicioId: number | null = null;
  cantidad: number | null = 1;
  tarifa: number | null = null;
  moneda: Moneda = 'CLP';
  motivo = '';
  async ngOnInit(): Promise<void> {
    this.tipos.set((await firstValueFrom(this.catalogo.tiposServicioListar())).filter(t => t.habilitado));
  }
  valido(): boolean {
    return this.tipoServicioId !== null && (this.cantidad ?? 0) > 0 && this.motivo.trim().length > 0;
  }
  solicitud(): AgregarLineaRequest {
    const conTarifa = this.tarifa !== null && String(this.tarifa) !== '';
    return {
      tipoServicioId: this.tipoServicioId!,
      cantidad: Number(this.cantidad),
      motivo: this.motivo.trim(),
      tarifa: conTarifa ? Number(this.tarifa) : null,
      moneda: conTarifa ? this.moneda : null,
    };
  }
  aceptar(): void {
    this.ref.close(this.solicitud());
  }
}
