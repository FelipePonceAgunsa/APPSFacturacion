import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';

@Component({
    selector: 'app-dialog-confirm-logout',
    templateUrl: './dialog-confirm-logout.component.html',
    styleUrl: './dialog-confirm-logout.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        MatDialogTitle,
        MatDialogContent,
        MatDialogActions,
        MatButton,
    ]
})
export class DialogConfirmLogoutComponent {
  constructor(private dialogRef: MatDialogRef<DialogConfirmLogoutComponent>) {}

  cancel(): void {
    this.dialogRef.close(false);
  }

  confirm(): void {
    this.dialogRef.close(true);
  }
}
