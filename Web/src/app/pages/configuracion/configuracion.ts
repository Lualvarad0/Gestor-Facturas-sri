import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { Titulo } from '../../shared/marca';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { LucideCircleAlert, LucideLoader } from '@lucide/angular';
import { AvisosService, PreferenciasService, mensajeDeError } from '../../core/estado';
import { RegimenTributario } from '../../core/models';
import { PerfilService, REGIMENES, cedulaValida, rucValido, soloDigitos } from '../../core/perfil.service';
import { Segmentado } from '../../shared/controles';
import { haceAnios, mayorDeEdad, nombreValido } from '../../shared/validadores';

@Component({
  selector: 'app-configuracion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Titulo, ReactiveFormsModule, LucideLoader, LucideCircleAlert, Segmentado],
  templateUrl: './configuracion.html',
})
export class Configuracion {
  protected readonly preferencias = inject(PreferenciasService);
  protected readonly perfilService = inject(PerfilService);
  private readonly avisos = inject(AvisosService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly regimenes = REGIMENES;
  protected readonly temas = [
    { valor: 'light', etiqueta: 'Claro' },
    { valor: 'dark', etiqueta: 'Oscuro' },
  ] as const;

  protected readonly guardando = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly fechaMaxima = haceAnios(18);
  protected readonly fechaMinima = haceAnios(120);

  protected readonly form = this.fb.group({
    nombres: ['', nombreValido],
    apellidos: ['', nombreValido],
    fechaNacimiento: ['', mayorDeEdad],
    ruc: [''],
    cedula: [''],
    razonSocial: [''],
    regimen: ['' as RegimenTributario | ''],
    cargasFamiliares: [0],
    enfermedadCatastrofica: [false],
  });

  private readonly valores = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly rucInvalido = computed(() => {
    const ruc = soloDigitos(this.valores().ruc ?? '');
    return ruc.length > 0 && !rucValido(ruc);
  });
  protected readonly cedulaInvalida = computed(() => {
    const cedula = soloDigitos(this.valores().cedula ?? '');
    return cedula.length > 0 && !cedulaValida(cedula);
  });

  constructor() {
    // Rellena el formulario cuando llega el perfil (y tras guardarlo).
    effect(() => {
      const p = this.perfilService.perfil();
      if (!p) return;
      this.form.reset({
        nombres: p.nombres,
        apellidos: p.apellidos,
        fechaNacimiento: p.fechaNacimiento ?? '',
        ruc: p.ruc ?? '',
        cedula: p.cedula ?? '',
        razonSocial: p.razonSocial ?? '',
        regimen: p.regimen ?? '',
        cargasFamiliares: p.cargasFamiliares,
        enfermedadCatastrofica: p.enfermedadCatastrofica,
      });
    });
  }

  /** Al escribir un RUC de persona natural, propone la cédula (sus 10 primeros dígitos) si falta o es inválida. */
  protected completarCedula(): void {
    const ruc = soloDigitos(this.form.controls.ruc.value);
    const cedula = soloDigitos(this.form.controls.cedula.value);
    if (ruc.length === 13 && Number(ruc[2]) < 6 && (!cedula || !cedulaValida(cedula))) {
      this.form.controls.cedula.setValue(ruc.slice(0, 10));
    }
  }

  protected guardar(): void {
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.controls.nombres.invalid || this.form.controls.apellidos.invalid || this.form.controls.fechaNacimiento.invalid) {
      this.error.set('Revisa tus datos personales antes de guardar.');
      return;
    }
    if (this.rucInvalido() || this.cedulaInvalida()) {
      this.error.set('Revisa la cédula y el RUC antes de guardar.');
      return;
    }
    const v = this.form.getRawValue();
    const rucAnterior = this.perfilService.perfil()?.ruc ?? null;

    this.guardando.set(true);
    this.perfilService.guardar({
      nombres: v.nombres.trim(),
      apellidos: v.apellidos.trim(),
      fechaNacimiento: v.fechaNacimiento || null,
      ruc: soloDigitos(v.ruc) || null,
      cedula: soloDigitos(v.cedula) || null,
      razonSocial: v.razonSocial.trim() || null,
      regimen: v.regimen || null,
      cargasFamiliares: Number(v.cargasFamiliares),
      enfermedadCatastrofica: v.enfermedadCatastrofica,
    }).subscribe({
      next: (r) => {
        this.guardando.set(false);
        const cambioRuc = r.perfil.ruc !== rucAnterior && r.comprobantesEmitidos >= 0;
        this.avisos.exito(cambioRuc
          ? `Perfil guardado. ${r.comprobantesRecibidos} ${r.comprobantesRecibidos === 1 ? 'comprobante cuenta' : 'comprobantes cuentan'} como gasto.`
          : 'Perfil guardado.');
      },
      error: (e) => {
        this.guardando.set(false);
        this.error.set(mensajeDeError(e));
      },
    });
  }
}
