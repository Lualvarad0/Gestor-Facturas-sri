import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { CATEGORIAS, categoria } from '../core/categorias';
import { CategoriaGasto } from '../core/models';

/** Punto de color + nombre de la categoría (el color nunca va solo). */
@Component({
  selector: 'app-categoria-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-2 text-sm">
      <span class="size-2.5 shrink-0 rounded-full" [style.background]="info().color" aria-hidden="true"></span>
      <span [class.text-muted-fg]="valor() === 'SinCategoria'">{{ info().nombre }}</span>
    </span>
  `,
})
export class CategoriaBadge {
  readonly valor = input.required<CategoriaGasto>();
  protected readonly info = computed(() => categoria(this.valor()));
}

/** Selector nativo de categoría con el punto de color al lado. */
@Component({
  selector: 'app-categoria-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="relative inline-flex w-full items-center">
      <span class="pointer-events-none absolute left-3 size-2.5 rounded-full" [style.background]="info().color" aria-hidden="true"></span>
      <select
        class="input appearance-none pl-8 pr-8"
        [class.text-muted-fg]="valor() === 'SinCategoria'"
        [disabled]="deshabilitado()"
        [attr.aria-label]="etiqueta()"
        (change)="cambio.emit($any($event.target).value)"
      >
        @for (c of categorias; track c.valor) {
          <option [value]="c.valor" [selected]="c.valor === valor()">{{ c.nombre }}</option>
        }
      </select>
      <svg class="pointer-events-none absolute right-3 size-4 text-muted-fg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="m6 9 6 6 6-6" /></svg>
    </span>
  `,
})
export class CategoriaSelect {
  readonly valor = input.required<CategoriaGasto>();
  readonly deshabilitado = input(false);
  readonly etiqueta = input('Categoría de gasto');
  readonly cambio = output<CategoriaGasto>();

  protected readonly categorias = CATEGORIAS;
  protected readonly info = computed(() => categoria(this.valor()));
}
