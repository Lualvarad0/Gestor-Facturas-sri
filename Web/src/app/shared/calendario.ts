import {
  AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, OnDestroy, effect, forwardRef, inject, input, signal, viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { LucideCalendar, LucideChevronLeft, LucideChevronRight } from '@lucide/angular';
import AirDatepicker, { AirDatepickerLocale } from 'air-datepicker';
import { PeriodoService } from '../core/estado';

/** Español de Ecuador; la semana empieza el lunes. (El locale del paquete es CommonJS y frena la optimización.) */
const LOCALE_ES: AirDatepickerLocale = {
  days: ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'],
  daysShort: ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'],
  daysMin: ['Do', 'Lu', 'Ma', 'Mi', 'Ju', 'Vi', 'Sá'],
  months: ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'],
  monthsShort: ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'],
  today: 'Hoy',
  clear: 'Borrar',
  dateFormat: 'dd/MM/yyyy',
  timeFormat: 'HH:mm',
  firstDay: 1,
};

// Flechas de Lucide (chevron-left / chevron-right) para la navegación del calendario.
const flecha = (d: string) =>
  `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="${d}"/></svg>`;
const OPCIONES_BASE = {
  locale: LOCALE_ES,
  prevHtml: flecha('m15 18-6-6 6-6'),
  nextHtml: flecha('m9 18 6-6-6-6'),
  navTitles: { days: 'MMMM <i>yyyy</i>', months: 'yyyy', years: 'yyyy1 – yyyy2' },
  keyboardNav: true,
  autoClose: true,
  position: 'bottom left' as const,
};

const enMovil = () => typeof matchMedia === 'function' && matchMedia('(max-width: 640px)').matches;

/** 'yyyy-MM-dd' → Date local (sin desfase de zona horaria). */
function aFecha(iso: string): Date | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso);
  if (!m) return null;
  const fecha = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  return fecha.getMonth() === Number(m[2]) - 1 ? fecha : null;
}

const dos = (n: number) => String(n).padStart(2, '0');
const aIso = (d: Date) => `${d.getFullYear()}-${dos(d.getMonth() + 1)}-${dos(d.getDate())}`;
const aTexto = (d: Date) => `${dos(d.getDate())}/${dos(d.getMonth() + 1)}/${d.getFullYear()}`;

/**
 * Campo de fecha con calendario desplegable. El valor del formulario es 'yyyy-MM-dd' ('' si está vacío).
 * También admite escribir dd/mm/aaaa; un texto incompleto queda como valor para que el validador lo rechace.
 */
@Component({
  selector: 'app-selector-fecha',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideCalendar],
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SelectorFecha), multi: true }],
  host: { class: 'relative block' },
  template: `
    <input #campo [id]="inputId()" class="input min-h-11 pr-11" inputmode="numeric" placeholder="dd/mm/aaaa" maxlength="10"
      [attr.autocomplete]="autocomplete()" [attr.aria-invalid]="invalido()" [attr.aria-describedby]="describedby()"
      [disabled]="deshabilitado()" (input)="alEscribir(campo.value)" (blur)="alTocar()" />
    <svg lucideCalendar class="pointer-events-none absolute top-1/2 right-3 size-[18px] -translate-y-1/2 text-muted-fg" aria-hidden="true"></svg>
  `,
})
export class SelectorFecha implements ControlValueAccessor, AfterViewInit, OnDestroy {
  readonly inputId = input.required<string>();
  /** 'yyyy-MM-dd' */
  readonly min = input<string>();
  readonly max = input<string>();
  readonly invalido = input(false);
  readonly describedby = input<string | null>(null);
  readonly autocomplete = input('off');
  /** Vista al abrir sin fecha: 'years' para fechas lejanas como el nacimiento. */
  readonly vistaInicial = input<'days' | 'years'>('days');

  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');
  protected readonly deshabilitado = signal(false);
  private calendario?: AirDatepicker<HTMLInputElement>;
  private valor = '';
  private alCambiar: (v: string) => void = () => {};
  private alTocado: () => void = () => {};

  ngAfterViewInit(): void {
    const seleccionada = aFecha(this.valor);
    const max = aFecha(this.max() ?? '');
    // "Hoy" solo si hoy es una fecha permitida (no lo es, p. ej., en la fecha de nacimiento).
    const hoyPermitido = !max || max >= new Date();
    this.calendario = new AirDatepicker(this.campo()!.nativeElement, {
      ...OPCIONES_BASE,
      isMobile: enMovil(),
      dateFormat: 'dd/MM/yyyy',
      buttons: hoyPermitido
        ? ['clear', { content: 'Hoy', className: 'adp-principal', onClick: (dp) => void dp.selectDate(new Date()) }]
        : ['clear'],
      minDate: aFecha(this.min() ?? '') ?? false,
      maxDate: max ?? false,
      selectedDates: seleccionada ? [seleccionada] : false,
      startDate: seleccionada ?? max ?? new Date(),
      view: seleccionada ? 'days' : this.vistaInicial(),
      onSelect: ({ date }) => {
        const fecha = Array.isArray(date) ? date[0] : date;
        this.valor = fecha ? aIso(fecha) : '';
        this.alCambiar(this.valor);
        this.alTocado();
      },
    });
    this.mostrar();
  }

  ngOnDestroy(): void {
    this.calendario?.destroy();
  }

  writeValue(valor: string | null): void {
    this.valor = valor ?? '';
    this.mostrar();
  }

  registerOnChange(fn: (v: string) => void): void {
    this.alCambiar = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.alTocado = fn;
  }

  setDisabledState(deshabilitado: boolean): void {
    this.deshabilitado.set(deshabilitado);
  }

  protected alEscribir(texto: string): void {
    const m = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(texto.trim());
    const fecha = m ? aFecha(`${m[3]}-${dos(Number(m[2]))}-${dos(Number(m[1]))}`) : null;
    this.valor = texto.trim() === '' ? '' : fecha ? aIso(fecha) : texto.trim();
    if (fecha) {
      void this.calendario?.selectDate(fecha, { silent: true });
      this.calendario?.setViewDate(fecha);
    } else if (!this.valor) {
      this.calendario?.clear({ silent: true });
    }
    this.alCambiar(this.valor);
  }

  protected alTocar(): void {
    this.alTocado();
  }

  /** Refleja el valor del formulario en el campo y en el calendario. */
  private mostrar(): void {
    const entrada = this.campo()?.nativeElement;
    if (!entrada) return;
    const fecha = aFecha(this.valor);
    if (fecha) {
      void this.calendario?.selectDate(fecha, { silent: true });
      this.calendario?.setViewDate(fecha);
      // Con una fecha guardada, el calendario abre en su mes (sin ella, en la vista inicial).
      if (!this.calendario?.visible) this.calendario?.setCurrentView('days', { silent: true });
      entrada.value = aTexto(fecha);
    } else {
      this.calendario?.clear({ silent: true });
      entrada.value = this.valor;
    }
  }
}

/** Selector del período (mes y año) de toda la app, con flechas para el mes anterior y el siguiente. */
@Component({
  selector: 'app-selector-periodo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideCalendar, LucideChevronLeft, LucideChevronRight],
  host: { class: 'flex items-center gap-1' },
  template: `
    <button type="button" class="btn btn-ghost min-h-10 px-2" aria-label="Mes anterior" (click)="mover(-1)">
      <svg lucideChevronLeft class="size-[18px]" aria-hidden="true"></svg>
    </button>
    <div class="relative">
      <svg lucideCalendar class="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-fg" aria-hidden="true"></svg>
      <input #campo readonly class="input w-[9.75rem] sm:w-[11.5rem] cursor-pointer pl-9 font-medium" aria-haspopup="dialog"
        [attr.aria-label]="'Período: ' + periodo.etiqueta() + '. Elegir otro mes'" />
    </div>
    <button type="button" class="btn btn-ghost min-h-10 px-2" aria-label="Mes siguiente" (click)="mover(1)">
      <svg lucideChevronRight class="size-[18px]" aria-hidden="true"></svg>
    </button>
  `,
})
export class SelectorPeriodo implements AfterViewInit, OnDestroy {
  protected readonly periodo = inject(PeriodoService);
  private readonly campo = viewChild<ElementRef<HTMLInputElement>>('campo');
  private calendario?: AirDatepicker<HTMLInputElement>;

  constructor() {
    // El período puede cambiar desde otras pantallas (p. ej. el gráfico mensual del panel).
    effect(() => {
      const fecha = new Date(this.periodo.anio(), this.periodo.mes() - 1, 1);
      const entrada = this.campo()?.nativeElement;
      if (entrada) entrada.value = this.periodo.etiqueta();
      if (this.calendario && this.calendario.selectedDates[0]?.getTime() !== fecha.getTime()) {
        void this.calendario.selectDate(fecha, { silent: true });
        this.calendario.setViewDate(fecha);
      }
    });
  }

  ngAfterViewInit(): void {
    const fecha = new Date(this.periodo.anio(), this.periodo.mes() - 1, 1);
    this.calendario = new AirDatepicker(this.campo()!.nativeElement, {
      ...OPCIONES_BASE,
      isMobile: enMovil(),
      view: 'months',
      minView: 'months',
      minDate: new Date(2000, 0, 1),
      maxDate: new Date(2100, 11, 1),
      selectedDates: [fecha],
      startDate: fecha,
      toggleSelected: false,
      buttons: [{
        content: 'Este mes',
        className: 'adp-principal',
        onClick: (dp) => {
          const hoy = new Date();
          void dp.selectDate(new Date(hoy.getFullYear(), hoy.getMonth(), 1));
        },
      }],
      dateFormat: (d: Date) => `${LOCALE_ES.months[d.getMonth()][0].toUpperCase()}${LOCALE_ES.months[d.getMonth()].slice(1)} ${d.getFullYear()}`,
      onSelect: ({ date }) => {
        const d = Array.isArray(date) ? date[0] : date;
        if (d) this.periodo.seleccionar(`${d.getFullYear()}-${dos(d.getMonth() + 1)}`);
        this.campo()!.nativeElement.value = this.periodo.etiqueta();
      },
    });
    this.campo()!.nativeElement.value = this.periodo.etiqueta();
  }

  ngOnDestroy(): void {
    this.calendario?.destroy();
  }

  protected mover(meses: number): void {
    const d = new Date(this.periodo.anio(), this.periodo.mes() - 1 + meses, 1);
    this.periodo.seleccionar(`${d.getFullYear()}-${dos(d.getMonth() + 1)}`);
  }
}
