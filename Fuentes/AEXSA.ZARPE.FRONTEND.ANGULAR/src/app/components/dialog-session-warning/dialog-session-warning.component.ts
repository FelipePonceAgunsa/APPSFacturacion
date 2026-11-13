import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogTitle, MatDialogContent, MatDialogActions } from '@angular/material/dialog';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatProgressBar } from '@angular/material/progress-bar';
import { WritableSignal } from '@angular/core';

const COUNTDOWN_TOTAL = 120;

interface DialogData {
  countdownSeconds: WritableSignal<number>;
}

@Component({
    selector: 'app-dialog-session-warning',
    templateUrl: './dialog-session-warning.component.html',
    styleUrl: './dialog-session-warning.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        MatDialogTitle,
        MatDialogContent,
        MatDialogActions,
        MatButton,
        MatIcon,
        MatProgressBar,
    ]
})
export class DialogSessionWarningComponent {
  private readonly dialogRef = inject(MatDialogRef<DialogSessionWarningComponent>);
  private readonly data: DialogData = inject(MAT_DIALOG_DATA);

  readonly countdownSeconds = this.data.countdownSeconds;

  readonly displayTime = computed(() => {
    const total = this.countdownSeconds();
    const min = Math.floor(total / 60);
    const sec = total % 60;
    return `${min}:${sec.toString().padStart(2, '0')}`;
  });

  readonly progressValue = computed(() =>
    (this.countdownSeconds() / COUNTDOWN_TOTAL) * 100
  );

  continueSession(): void {
    this.dialogRef.close(true);
  }
}
