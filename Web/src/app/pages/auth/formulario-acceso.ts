import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { LucideCircleCheck, LucideEye, LucideEyeOff, LucideLoader } from '@lucide/angular';
import { AuthService } from '../../core/auth';
import { mensajeDeError } from '../../core/estado';
import { CoincidenciaClaves } from '../../shared/coincidencia-claves';
import { contieneDatosPersonales, matchingPasswords, securePassword } from '../../shared/password';
import { haceAnios, mayorDeEdad, nombreValido } from '../../shared/validadores';

export type ModoAcceso = 'login' | 'registro';

function sinDatosPersonales(form: AbstractControl): ValidationErrors | null {
  const v = form.value as { nombres: string; apellidos: string; email: string; fechaNacimiento: string; password: string };
  return v.password && contieneDatosPersonales(v.password, v) ? { datosPersonales: true } : null;
}

/** Formulario de inicio de sesión o de registro; la pantalla de acceso muestra uno de cada lado. */
@Component({
  selector: 'app-formulario-acceso',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, NgTemplateOutlet, LucideLoader, LucideEye, LucideEyeOff, LucideCircleCheck, CoincidenciaClaves],
  template: `
    <h1 class="titular text-[clamp(34px,5vw,44px)]">
      {{ registro() ? 'Crea tu' : 'Inicia' }} <em class="italic">{{ registro() ? 'cuenta' : 'sesión' }}</em>
    </h1>
    <p class="mt-2 text-sm leading-relaxed text-muted-fg">
      {{ registro() ? 'Gratis para empezar. Sin pedirte tu clave del SRI.' : 'Ordena los gastos del mes con tus facturas del SRI.' }}
    </p>

    @if (error()) {
      <div class="aviso-error mt-5" role="alert">{{ error() }}</div>
    }

    <form class="mt-6 space-y-4" [formGroup]="form" (ngSubmit)="enviar()" novalidate>
      @if (registro()) {
        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label class="field-label" [attr.for]="id('nombres')">Nombres</label>
            <input [id]="id('nombres')" class="input min-h-11" formControlName="nombres" autocomplete="given-name" maxlength="60"
              [attr.aria-invalid]="invalido('nombres')" [attr.aria-describedby]="id('nombres-error')" />
            @if (invalido('nombres')) {
              <p [id]="id('nombres-error')" class="field-error">Escribe tus nombres (solo letras, mínimo 2).</p>
            }
          </div>
          <div>
            <label class="field-label" [attr.for]="id('apellidos')">Apellidos</label>
            <input [id]="id('apellidos')" class="input min-h-11" formControlName="apellidos" autocomplete="family-name" maxlength="60"
              [attr.aria-invalid]="invalido('apellidos')" [attr.aria-describedby]="id('apellidos-error')" />
            @if (invalido('apellidos')) {
              <p [id]="id('apellidos-error')" class="field-error">Escribe tus apellidos (solo letras, mínimo 2).</p>
            }
          </div>
        </div>
        <div>
          <label class="field-label" [attr.for]="id('nacimiento')">Fecha de nacimiento</label>
          <input [id]="id('nacimiento')" type="date" class="input min-h-11" formControlName="fechaNacimiento" autocomplete="bday"
            [min]="fechaMinima" [max]="fechaMaxima"
            [attr.aria-invalid]="invalido('fechaNacimiento')" [attr.aria-describedby]="id('nacimiento-error')" />
          @if (invalido('fechaNacimiento')) {
            <p [id]="id('nacimiento-error')" class="field-error">Debes ser mayor de 18 años. Revisa la fecha.</p>
          }
        </div>
      }

      <div>
        <label class="field-label" [attr.for]="id('email')">Correo electrónico</label>
        <input [id]="id('email')" type="email" class="input min-h-11" formControlName="email" autocomplete="email" inputmode="email"
          [attr.aria-invalid]="invalido('email')" [attr.aria-describedby]="id('email-error')" />
        @if (invalido('email')) {
          <p [id]="id('email-error')" class="field-error">Escribe un correo válido.</p>
        }
      </div>

      <div>
        <label class="field-label" [attr.for]="id('password')">Contraseña</label>
        <div class="relative">
          <input [id]="id('password')" class="input min-h-11 pr-12" formControlName="password"
            [type]="mostrarClave() ? 'text' : 'password'"
            [attr.autocomplete]="registro() ? 'new-password' : 'current-password'"
            [attr.aria-invalid]="invalido('password') || (registro() && datosPersonales())"
            [attr.aria-describedby]="id('password-error') + ' ' + id('password-fuerza')" />
          <ng-container *ngTemplateOutlet="ojo" />
        </div>
        @if (invalido('password')) {
          <p [id]="id('password-error')" class="field-error">{{ registro() ? 'Usa entre 15 y 128 caracteres y evita contraseñas comunes.' : 'Escribe tu contraseña.' }}</p>
        } @else if (registro() && datosPersonales() && (form.controls.password.touched || enviado())) {
          <p [id]="id('password-error')" class="field-error">No uses tus nombres, apellidos, correo ni fecha de nacimiento.</p>
        }

        @if (registro()) {
          <div [id]="id('password-fuerza')" class="mt-3" aria-live="polite">
            <div class="flex gap-1.5" aria-hidden="true">
              @for (r of requisitos(); track r.texto; let i = $index) {
                <span class="h-1.5 flex-1 rounded-full transition-colors duration-300"
                  [class]="i >= fuerza() ? 'bg-muted' : fuerza() <= 2 ? 'bg-danger' : fuerza() < requisitos().length ? 'bg-warning' : 'bg-success'"></span>
              }
            </div>
            <p class="mt-2 text-xs text-muted-fg">Requisitos: <span class="font-medium text-fg">{{ fuerza() }}/{{ requisitos().length }}</span>. Usa una frase única que no hayas usado en otra cuenta.</p>
            <ul class="mt-1.5 grid grid-cols-2 gap-1 text-xs">
              @for (r of requisitos(); track r.texto) {
                <li class="flex items-center gap-1.5" [class.text-success]="r.ok" [class.text-muted-fg]="!r.ok">
                  <svg lucideCircleCheck class="size-3.5 shrink-0" aria-hidden="true"></svg> {{ r.texto }}
                </li>
              }
            </ul>
          </div>
        }
      </div>

      @if (registro()) {
        <div>
          <label class="field-label" [attr.for]="id('confirmacion')">Confirma tu contraseña</label>
          <div class="relative">
            <input [id]="id('confirmacion')" class="input min-h-11 pr-12" formControlName="confirmacion" autocomplete="new-password"
              [type]="mostrarClave() ? 'text' : 'password'"
              [attr.aria-invalid]="form.hasError('passwordMismatch') && enviado()" [attr.aria-describedby]="id('coincidencia')" />
            <ng-container *ngTemplateOutlet="ojo" />
          </div>
          <app-coincidencia-claves [id]="id('coincidencia')" [clave]="valores().password ?? ''" [confirmacion]="valores().confirmacion ?? ''" />
          @if (form.hasError('passwordMismatch') && enviado() && !valores().confirmacion) {
            <p class="field-error" role="alert">Confirma tu contraseña.</p>
          }
        </div>
      } @else {
        <a routerLink="/recuperar" class="block text-right text-sm text-primary hover:underline">Olvidé mi contraseña</a>
      }
      <button type="submit" class="btn btn-primary min-h-11 w-full" [disabled]="enviando()">
        @if (enviando()) {
          <svg lucideLoader class="size-4 animate-spin" aria-hidden="true"></svg>
        }
        {{ registro() ? 'Crear cuenta' : 'Ingresar' }}
      </button>
    </form>

    <!-- Un solo botón de ojo por campo; muestra u oculta ambas contraseñas para compararlas -->
    <ng-template #ojo>
      <button type="button"
        class="absolute inset-y-0 right-0 grid w-11 cursor-pointer place-items-center rounded-r-lg text-muted-fg transition-colors hover:text-fg"
        [attr.aria-label]="(mostrarClave() ? 'Ocultar ' : 'Mostrar ') + (registro() ? 'contraseñas' : 'contraseña')"
        [attr.aria-pressed]="mostrarClave()"
        (click)="mostrarClave.set(!mostrarClave())">
        @if (mostrarClave()) {
          <svg lucideEyeOff class="size-[18px]" aria-hidden="true"></svg>
        } @else {
          <svg lucideEye class="size-[18px]" aria-hidden="true"></svg>
        }
      </button>
    </ng-template>

    <!-- En pantallas grandes el cambio de modo lo hace el panel deslizante -->
    <p class="mt-5 text-sm text-muted-fg sm:hidden">
      @if (registro()) {
        ¿Ya tienes cuenta? <a routerLink="/login" class="font-medium text-accent hover:underline">Inicia sesión</a>
      } @else {
        ¿Aún no tienes cuenta? <a routerLink="/registro" class="font-medium text-accent hover:underline">Regístrate gratis</a>
      }
    </p>
  `,
})
export class FormularioAcceso implements OnInit {
  readonly modo = input.required<ModoAcceso>();
  /** Ruta a la que volver después de iniciar sesión (query param ?volver= del guard). */
  readonly volver = input<string | undefined>();

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly registro = computed(() => this.modo() === 'registro');
  protected readonly enviando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly enviado = signal(false);
  /** El usuario puede ver lo que escribió en ambas contraseñas para compararlas. */
  protected readonly mostrarClave = signal(false);
  protected readonly fechaMaxima = haceAnios(18);
  protected readonly fechaMinima = haceAnios(120);

  protected readonly form = this.fb.group({
    nombres: [''],
    apellidos: [''],
    fechaNacimiento: [''],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
    confirmacion: [''],
  });

  protected readonly valores = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly datosPersonales = computed(() => {
    const v = this.valores();
    return !!v.password && contieneDatosPersonales(v.password, v);
  });

  /** Requisitos visibles al registrarse; la fuerza es cuántos se cumplen. */
  protected readonly requisitos = computed(() => {
    const c = this.valores().password ?? '';
    return [
      { texto: '15 caracteres o más', ok: c.length >= 15 },
      { texto: 'Hasta 128 caracteres', ok: c.length > 0 && c.length <= 128 },
      { texto: 'Frase no vacía', ok: c.trim().length > 0 },
      { texto: 'Evita claves comunes', ok: c.length > 0 && !['123456789012345', 'passwordpassword', 'contraseñacontraseña'].includes(c.toLowerCase()) },
      { texto: 'Sin tus datos personales', ok: c.length > 0 && !this.datosPersonales() },
    ];
  });
  protected readonly fuerza = computed(() => this.requisitos().filter((r) => r.ok).length);

  ngOnInit(): void {
    if (this.registro()) {
      this.form.controls.nombres.addValidators(nombreValido);
      this.form.controls.apellidos.addValidators(nombreValido);
      this.form.controls.fechaNacimiento.addValidators([Validators.required, mayorDeEdad]);
      this.form.controls.password.addValidators(securePassword);
      this.form.controls.confirmacion.addValidators(Validators.required);
      this.form.addValidators([matchingPasswords, sinDatosPersonales]);
      for (const control of Object.values(this.form.controls)) control.updateValueAndValidity({ emitEvent: false });
      this.form.updateValueAndValidity();
    }
  }

  /** Ids únicos: los dos formularios están en la página a la vez. */
  protected id(campo: string): string {
    return `${this.modo()}-${campo}`;
  }

  protected invalido(campo: 'nombres' | 'apellidos' | 'fechaNacimiento' | 'email' | 'password'): boolean {
    const control = this.form.controls[campo];
    return control.invalid && (control.touched || this.enviado());
  }

  protected enviar(): void {
    if (this.enviando()) return;
    this.enviado.set(true);
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { nombres, apellidos, fechaNacimiento, email, password } = this.form.getRawValue();
    const peticion = this.registro()
      ? this.auth.registrar({ nombres: nombres.trim(), apellidos: apellidos.trim(), fechaNacimiento, email: email.trim(), password })
      : this.auth.login(email.trim(), password);

    this.enviando.set(true);
    peticion.subscribe({
      next: () => {
        const destino = this.volver()?.startsWith('/') ? this.volver()! : '/resumen';
        // Tras registrarse, primero el presupuesto (se puede saltar) y luego importar facturas.
        void this.router.navigateByUrl(this.registro() ? '/presupuesto?inicio=1' : destino);
      },
      error: (e) => {
        this.enviando.set(false);
        this.error.set(mensajeDeError(e));
      },
    });
  }
}
