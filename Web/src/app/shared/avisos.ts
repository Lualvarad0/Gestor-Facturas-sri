import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LucideCircleAlert, LucideCircleCheck, LucideX } from '@lucide/angular';
import { AvisosService } from '../core/estado';

@Component({
  selector: 'app-avisos',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideCircleCheck, LucideCircleAlert, LucideX],
  template: `
    <div class="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-end gap-2 sm:left-auto sm:w-96" aria-live="polite">
      @for (a of avisos.avisos(); track a.id) {
        <div
          class="pointer-events-auto flex w-full items-start gap-3 rounded-xl border p-4 text-sm shadow-lg"
          [class]="a.tipo === 'exito' ? 'border-success/30 bg-success-soft text-fg' : 'border-danger/30 bg-danger-soft text-fg'"
          [attr.role]="a.tipo === 'error' ? 'alert' : 'status'"
        >
          @if (a.tipo === 'exito') {
            <svg lucideCircleCheck class="mt-0.5 size-5 shrink-0 text-success" aria-hidden="true"></svg>
          } @else {
            <svg lucideCircleAlert class="mt-0.5 size-5 shrink-0 text-danger" aria-hidden="true"></svg>
          }
          <p class="flex-1">{{ a.texto }}</p>
          <button type="button" class="-m-1 rounded p-1 text-muted-fg hover:text-fg" aria-label="Cerrar aviso" (click)="avisos.cerrar(a.id)">
            <svg lucideX class="size-4" aria-hidden="true"></svg>
          </button>
        </div>
      }
    </div>
  `,
})
export class Avisos {
  protected readonly avisos = inject(AvisosService);
}
