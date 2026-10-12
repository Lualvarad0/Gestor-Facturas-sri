import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideCircleAlert, LucideLoader, LucidePlus, LucideTrash2 } from '@lucide/angular';
import { cargar } from '../../core/carga';
import { AvisosService, PeriodoService, mensajeDeError } from '../../core/estado';
import { PresupuestoService } from '../../core/presupuesto.service';
import { ErrorCarga } from '../../shared/controles';
import { DineroPipe } from '../../shared/formato';
import { BarraApilada, Segmento } from '../../shared/barra-apilada';
import { Titulo } from '../../shared/marca';
import { Medidor, colorRubro } from '../../shared/medidor';

type RubroForm = FormGroup<{ id: FormControl<number | null>; nombre: FormControl<string>; limite: FormControl<number | null> }>;

/** Rubros frecuentes que se pueden agregar con un clic. */
const SUGERENCIAS = ['Vivienda', 'Alimentación', 'Servicios básicos', 'Transporte', 'Salud', 'Educación', 'Entretenimiento', 'Ropa'];

/**
 * Ingreso aproximado del mes y límite de cada rubro propio.
 * Con ?inicio=1 es el paso que sigue al registro: se puede guardar o dejar para después.
 */
@Component({
  selector: 'app-presupuesto',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, Titulo, ErrorCarga, DineroPipe, BarraApilada, Medidor, LucidePlus, LucideTrash2, LucideLoader, LucideCircleAlert],
  templateUrl: './presupuesto.html',
})
export class PresupuestoPagina {
  private readonly servicio = inject(PresupuestoService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly periodo = inject(PeriodoService);
  protected readonly inicio = inject(ActivatedRoute).snapshot.queryParamMap.get('inicio') === '1';

  private readonly recargas = signal(0);
  protected readonly carga = cargar(
    () => ({ anio: this.periodo.anio(), mes: this.periodo.mes(), recarga: this.recargas() }),
    ({ anio, mes }) => this.servicio.obtener(anio, mes),
  );
  protected readonly guardando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly enviado = signal(false);

  protected readonly form = this.fb.group({
    ingresoMensual: this.fb.control<number | null>(null, [Validators.min(0), Validators.max(10_000_000)]),
    rubros: this.fb.array<RubroForm>([]),
  });

  private readonly valores = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly totalLimites = computed(() =>
    (this.valores().rubros ?? []).reduce((suma, r) => suma + (Number(r?.limite) || 0), 0));
  protected readonly ingreso = computed(() => Number(this.valores().ingresoMensual) || 0);
  /** Positivo: dinero sin asignar. Negativo: los límites superan el ingreso. */
  protected readonly disponible = computed(() => this.ingreso() - this.totalLimites());
  protected readonly sugerencias = computed(() => {
    const usados = new Set((this.valores().rubros ?? []).map((r) => (r?.nombre ?? '').trim().toLowerCase()));
    return SUGERENCIAS.filter((s) => !usados.has(s.toLowerCase()));
  });
  protected readonly duplicados = computed(() => {
    const nombres = (this.valores().rubros ?? []).map((r) => (r?.nombre ?? '').trim().toLowerCase()).filter(Boolean);
    return new Set(nombres.filter((n, i) => nombres.indexOf(n) !== i));
  });
  /** Gasto del mes por id de rubro (de la última carga). */
  protected readonly gastado = computed(() => new Map((this.carga().datos?.rubros ?? []).map((r) => [r.id, r])));

  protected readonly rubroResaltado = signal<string | null>(null);
  /** Límites del formulario como partes del ingreso; del 8.º rubro en adelante se agrupan en "Otros rubros". */
  protected readonly reparto = computed<Segmento[]>(() => {
    const rubros = (this.valores().rubros ?? []).map((r, i) => ({
      clave: String(i), nombre: (r?.nombre ?? '').trim() || `Rubro ${i + 1}`, valor: Math.max(0, Number(r?.limite) || 0), color: colorRubro(i),
    }));
    const otros = rubros.slice(7).reduce((t, r) => t + r.valor, 0);
    const visibles = rubros.slice(0, 7);
    return otros > 0 ? [...visibles, { clave: 'otros', nombre: 'Otros rubros', valor: otros, color: colorRubro(7) }] : visibles;
  });
  /** Posición del ingreso en la barra cuando los límites lo superan. */
  protected readonly marcadorIngreso = computed(() =>
    this.ingreso() > 0 && this.totalLimites() > this.ingreso() ? (this.ingreso() / this.totalLimites()) * 100 : null);

  constructor() {
    // Rellena el formulario con lo guardado, salvo que el usuario esté editando.
    effect(() => {
      const p = this.carga().datos;
      if (!p || this.form.dirty) return;
      this.form.controls.rubros.clear({ emitEvent: false });
      for (const r of p.rubros) this.form.controls.rubros.push(this.crearRubro(r.id, r.nombre, r.limite), { emitEvent: false });
      this.form.reset({ ingresoMensual: p.ingresoMensual, rubros: p.rubros.map((r) => ({ id: r.id, nombre: r.nombre, limite: r.limite })) });
    });
  }

  private crearRubro(id: number | null, nombre: string, limite: number | null): RubroForm {
    return this.fb.group({
      id: this.fb.control<number | null>(id),
      nombre: [nombre, [Validators.required, Validators.minLength(2), Validators.maxLength(40), Validators.pattern(/\S/)]],
      limite: this.fb.control<number | null>(limite, [Validators.required, Validators.min(0), Validators.max(1_000_000)]),
    });
  }

  protected agregar(nombre = ''): void {
    this.form.controls.rubros.push(this.crearRubro(null, nombre, null));
    this.form.markAsDirty();
  }

  protected quitar(i: number): void {
    this.form.controls.rubros.removeAt(i);
    this.form.markAsDirty();
  }

  protected readonly colorRubro = colorRubro;

  protected invalido(i: number, campo: 'nombre' | 'limite'): boolean {
    const control = this.form.controls.rubros.at(i).controls[campo];
    return control.invalid && (control.touched || this.enviado());
  }

  protected guardar(): void {
    this.enviado.set(true);
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.duplicados().size > 0) {
      this.error.set(this.duplicados().size > 0 ? 'Hay rubros con el mismo nombre.' : 'Revisa los campos marcados.');
      return;
    }
    const v = this.form.getRawValue();
    this.guardando.set(true);
    this.servicio.guardar({
      ingresoMensual: v.ingresoMensual === null || String(v.ingresoMensual) === '' ? null : Number(v.ingresoMensual),
      rubros: v.rubros.map((r) => ({ id: r.id, nombre: r.nombre.trim(), limite: Number(r.limite) })),
    }).subscribe({
      next: () => {
        this.guardando.set(false);
        this.enviado.set(false);
        this.avisos.exito('Presupuesto guardado.');
        if (this.inicio) {
          void this.router.navigateByUrl('/importar');
          return;
        }
        this.form.markAsPristine();
        this.recargas.update((n) => n + 1);
      },
      error: (e) => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(e));
      },
    });
  }
}
