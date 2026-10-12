import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LucideCircleAlert, LucideCircleCheck, LucideCircleX } from '@lucide/angular';
import { DineroPipe } from './formato';

export type EstadoRubro = 'al-dia' | 'cerca' | 'excedido';

/** Estado de un gasto frente a su límite: cerca desde el 80 %, excedido al superarlo. */
export function estadoRubro(gastado: number, limite: number): EstadoRubro {
  if (gastado > limite) return 'excedido';
  return limite > 0 && gastado / limite >= 0.8 ? 'cerca' : 'al-dia';
}

/** Color categórico de un rubro según su posición (orden fijo, nunca ciclado); del 8.º en adelante, neutro. */
export function colorRubro(indice: number): string {
  return indice < 7 ? `var(--cat-${indice + 1})` : 'var(--cat-neutral)';
}

const ESTADOS: Record<EstadoRubro, { texto: string; pildora: string; relleno: string; pista: string }> = {
  'al-dia': { texto: 'Al día', pildora: 'bg-success-soft text-success', relleno: 'bg-bar', pista: 'bg-bar-soft' },
  cerca: { texto: 'Cerca del límite', pildora: 'bg-warning-soft text-warning', relleno: 'bg-warning', pista: 'bg-warning-soft' },
  excedido: { texto: 'Excedido', pildora: 'bg-danger-soft text-danger', relleno: 'bg-danger', pista: 'bg-danger-soft' },
};

/**
 * Medidor de un rubro: lo gastado frente al límite del mes.
 * El relleno lleva el estado (índigo → ámbar → rojo) sobre una pista del mismo tono; el estado va además con ícono y texto.
 */
@Component({
  selector: 'app-medidor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, DineroPipe, LucideCircleCheck, LucideCircleAlert, LucideCircleX],
  host: { class: 'block' },
  template: `
    <!-- Nombre (o lo gastado, en la versión compacta) y estado; luego la barra; debajo, límite y saldo -->
    <div class="flex items-center justify-between gap-3">
      @if (nombre()) {
        <span class="flex min-w-0 items-center gap-2 text-sm font-medium">
          @if (color()) { <span class="size-2.5 shrink-0 rounded-[3px]" [style.background]="color()" aria-hidden="true"></span> }
          <span class="truncate">{{ nombre() }}</span>
        </span>
      } @else {
        <span class="text-sm font-semibold whitespace-nowrap">{{ gastado() | dinero }}</span>
      }
      <ng-container *ngTemplateOutlet="pildora" />
    </div>
    <div class="mt-2.5 h-2 overflow-hidden rounded-[4px]" [class]="estilo().pista" role="meter" aria-valuemin="0"
      [attr.aria-valuemax]="limite()" [attr.aria-valuenow]="gastado()"
      [attr.aria-label]="(nombre() || 'Gasto') + ': ' + (gastado() | dinero) + ' de ' + (limite() | dinero) + ', ' + estilo().texto">
      <div class="h-full rounded-[4px] transition-[width] duration-500" [class]="estilo().relleno" [style.width.%]="ancho()"></div>
    </div>
    <div class="mt-2 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5 text-xs text-muted-fg">
      <span class="whitespace-nowrap">
        @if (nombre()) { <span class="font-semibold text-fg">{{ gastado() | dinero }}</span>&nbsp;de&nbsp;{{ limite() | dinero }} }
        @else { de {{ limite() | dinero }} }
      </span>
      <span class="whitespace-nowrap" [class.text-danger]="restante() < 0">
        {{ restante() >= 0 ? 'Quedan' : 'Te pasaste por' }}&nbsp;{{ (restante() < 0 ? -restante() : restante()) | dinero }}
      </span>
    </div>

    <ng-template #pildora>
      <span class="pildora shrink-0" [class]="estilo().pildora">
        @switch (estado()) {
          @case ('al-dia') { <svg lucideCircleCheck class="size-3.5" aria-hidden="true"></svg> }
          @case ('cerca') { <svg lucideCircleAlert class="size-3.5" aria-hidden="true"></svg> }
          @case ('excedido') { <svg lucideCircleX class="size-3.5" aria-hidden="true"></svg> }
        }
        {{ estilo().texto }}
      </span>
    </ng-template>
  `,
})
export class Medidor {
  readonly gastado = input.required<number>();
  readonly limite = input.required<number>();
  /** Sin nombre es la versión compacta (debajo del campo del rubro). */
  readonly nombre = input('');
  readonly color = input<string | null>(null);

  protected readonly estado = computed(() => estadoRubro(this.gastado(), this.limite()));
  protected readonly estilo = computed(() => ESTADOS[this.estado()]);
  protected readonly restante = computed(() => this.limite() - this.gastado());
  protected readonly ancho = computed(() => {
    const l = this.limite();
    const g = Math.max(0, this.gastado());
    return l > 0 ? Math.min(100, (g / l) * 100) : g > 0 ? 100 : 0;
  });
}
