import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LucideCircleAlert, LucideCircleCheck } from '@lucide/angular';
import { compararClaves } from './password';

/** Indica en vivo si la confirmación coincide con la contraseña y dónde empiezan a diferir. */
@Component({
  selector: 'app-coincidencia-claves',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideCircleCheck, LucideCircleAlert],
  template: `
    <p class="mt-1.5 flex min-h-5 items-center gap-1.5 text-xs" aria-live="polite"
      [class.text-success]="r().estado === 'coinciden'"
      [class.text-muted-fg]="r().estado === 'incompleta'"
      [class.text-danger]="r().estado === 'distinta'">
      @switch (r().estado) {
        @case ('coinciden') { <svg lucideCircleCheck class="size-3.5" aria-hidden="true"></svg> Las contraseñas coinciden. }
        @case ('incompleta') { Vas bien: {{ r().faltan === 1 ? 'falta 1 carácter' : 'faltan ' + r().faltan + ' caracteres' }}. }
        @case ('distinta') { <svg lucideCircleAlert class="size-3.5" aria-hidden="true"></svg> No coinciden desde el carácter {{ r().posicion }}. }
      }
    </p>
  `,
})
export class CoincidenciaClaves {
  readonly clave = input.required<string>();
  readonly confirmacion = input.required<string>();
  protected readonly r = computed(() => compararClaves(this.clave(), this.confirmacion()));
}
