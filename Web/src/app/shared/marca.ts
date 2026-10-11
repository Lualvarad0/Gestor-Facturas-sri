import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** Nombre comercial: cambiarlo aquí cambia el logo en toda la app. */
export const NOMBRE_APP = 'Nudo';

/**
 * Logo de Nudo: círculo carbón con un khipu (cordón principal, tres hilos colgantes y nudos),
 * el registro de cuerdas de los Andes: cada nudo es un registro.
 */
@Component({
  selector: 'app-marca',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="inline-flex items-center gap-2">
      <svg [attr.width]="tamano()" [attr.height]="tamano()" viewBox="0 0 38 38" aria-hidden="true" class="shrink-0">
        <circle cx="19" cy="19" r="19" fill="#252b31" />
        <path d="M8.5 12.6 Q19 9.6 29.5 12.6" stroke="#fff" stroke-width="1.8" fill="none" stroke-linecap="round" />
        <path d="M13 12 V25 M19 11.2 V29.5 M25 12 V22.5" stroke="#fff" stroke-width="1.4" fill="none" stroke-linecap="round" />
        <circle cx="13" cy="19" r="1.9" fill="#fff" />
        <circle cx="19" cy="16" r="1.5" fill="#fff" />
        <circle cx="19" cy="23" r="2.6" fill="#6467f1" />
        <circle cx="25" cy="17.5" r="1.9" fill="#fff" />
      </svg>
      @if (conNombre()) {
        <span class="font-body leading-none font-semibold tracking-tight text-fg" [style.font-size.px]="tamano() * 0.72">{{ nombre }}</span>
      }
    </span>
  `,
})
export class Marca {
  readonly tamano = input(28);
  readonly conNombre = input(true);
  protected readonly nombre = NOMBRE_APP;
}

export const AUTORIA = { nombre: 'Cornerstone Develop', url: 'https://portafolio-hwaq.vercel.app/' };

/** "Diseño y desarrollo: Cornerstone Develop", con enlace al portafolio. */
@Component({
  selector: 'app-credito',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="text-xs text-muted-fg">
      Diseño y desarrollo:
      <a [href]="autoria.url" target="_blank" rel="noopener" class="font-medium text-fg underline-offset-2 hover:underline">{{ autoria.nombre }}</a>
    </p>
  `,
})
export class Credito {
  protected readonly autoria = AUTORIA;
}

/** Titular de página en Instrument Serif; la segunda parte va en itálica. */
@Component({
  selector: 'app-titulo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div class="min-w-0">
        <h1 class="titular text-[clamp(34px,4.6vw,48px)] text-fg">
          {{ linea1() }} <em class="italic">{{ linea2() }}</em>
        </h1>
        @if (subtitulo()) {
          <p class="mt-2 max-w-2xl text-sm leading-relaxed text-muted-fg">{{ subtitulo() }}</p>
        }
      </div>
      <ng-content />
    </div>
  `,
})
export class Titulo {
  readonly linea1 = input.required<string>();
  readonly linea2 = input.required<string>();
  readonly subtitulo = input('');
}

const MONEDA = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

/** Monto destacado: los centavos van más pequeños y en gris ($8.450,32 → $8.450 ,32). */
@Component({
  selector: 'app-monto',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="cifra" [class]="clase()">{{ partes().entero }}<span class="text-[0.55em] font-medium text-muted-fg">{{ partes().decimales }}</span></span>`,
})
export class Monto {
  readonly valor = input<number | null | undefined>(0);
  readonly clase = input('text-[28px] leading-none');

  protected readonly partes = computed(() => {
    const texto = MONEDA.format(this.valor() ?? 0);
    const coma = texto.lastIndexOf(',');
    return coma < 0 ? { entero: texto, decimales: '' } : { entero: texto.slice(0, coma), decimales: texto.slice(coma) };
  });
}
