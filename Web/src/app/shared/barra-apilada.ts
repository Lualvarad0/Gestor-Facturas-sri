import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { DineroPipe, porcentaje } from './formato';

export interface Segmento {
  clave: string;
  nombre: string;
  valor: number;
  color: string;
}

/**
 * Barra horizontal de parte del total. Los segmentos se separan con 2px del color de la superficie;
 * lo que no cubren (si `base` supera la suma) queda como pista. `resaltado` se comparte con la leyenda.
 */
@Component({
  selector: 'app-barra-apilada',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DineroPipe],
  host: { class: 'relative block' },
  template: `
    <!-- Reparto con flex-grow: los 2px de separación no desbordan la barra -->
    <div class="flex h-3 w-full gap-[2px] overflow-hidden rounded-[4px]" role="img" [attr.aria-label]="etiqueta()">
      @for (s of partes(); track s.clave) {
        <div class="h-full min-w-[3px] transition-opacity duration-150"
          [style.flex]="s.valor + ' 1 0%'" [style.background]="s.color"
          [style.opacity]="resaltado() && resaltado() !== s.clave ? 0.3 : 1"
          (mouseenter)="resaltado.set(s.clave)" (mouseleave)="resaltado.set(null)"></div>
      }
      @if (resto() > 0) {
        <div class="h-full bg-muted" [style.flex]="resto() + ' 1 0%'"></div>
      } @else if (partes().length === 0) {
        <div class="h-full flex-1 bg-muted"></div>
      }
    </div>
    @if (marcador() !== null) {
      <span class="pointer-events-none absolute -top-1 -bottom-1 w-0.5 rounded-full bg-fg" [style.left.%]="marcador()" aria-hidden="true"></span>
    }
    @if (activo(); as s) {
      <div class="pointer-events-none absolute bottom-full z-10 mb-2 -translate-x-1/2 rounded-lg border border-border bg-card px-3 py-2 text-xs whitespace-nowrap shadow-dashboard"
        [style.left.%]="s.centro" role="status">
        <span class="flex items-center gap-1.5 font-medium"><span class="size-2 rounded-[2px]" [style.background]="s.color"></span>{{ s.nombre }}</span>
        <span class="mt-0.5 block text-muted-fg"><span class="money text-fg">{{ s.valor | dinero }}</span> · {{ s.porcentaje }}</span>
      </div>
    }
  `,
})
export class BarraApilada {
  readonly segmentos = input.required<Segmento[]>();
  /** Total contra el que se mide; por defecto, la suma de los segmentos. */
  readonly base = input<number | null>(null);
  /** Posición (en %) de una línea de referencia, p. ej. el ingreso cuando los límites lo superan. */
  readonly marcador = input<number | null>(null);
  readonly etiqueta = input('');
  readonly resaltado = model<string | null>(null);

  protected readonly partes = computed(() => {
    const visibles = this.segmentos().filter((s) => s.valor > 0);
    const suma = visibles.reduce((t, s) => t + s.valor, 0);
    const base = Math.max(this.base() ?? suma, suma) || 1;
    let inicio = 0;
    return visibles.map((s) => {
      const ancho = (s.valor / base) * 100;
      const parte = { ...s, ancho, centro: inicio + ancho / 2, porcentaje: porcentaje((s.valor / base) * 100) };
      inicio += ancho;
      return parte;
    });
  });
  /** Parte de la base que no cubren los segmentos. */
  protected readonly resto = computed(() => {
    const suma = this.segmentos().filter((s) => s.valor > 0).reduce((t, s) => t + s.valor, 0);
    return Math.max(0, (this.base() ?? suma) - suma);
  });
  protected readonly activo = computed(() => this.partes().find((p) => p.clave === this.resaltado()) ?? null);
}
