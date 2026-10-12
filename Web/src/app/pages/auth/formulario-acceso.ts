import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { LucideCircleCheck, LucideEye, LucideEyeOff, LucideLoader } from '@lucide/angular';
import { AuthService } from '../../core/auth';
import { mensajeDeError } from '../../core/estado';
import { matchingPasswords, securePassword } from '../../shared/password';

export type ModoAcceso = 'login' | 'registro';

/** Formulario de inicio de sesión o de registro; la pantalla de acceso muestra uno de cada lado. */
@Component({
  selector: 'app-formulario-acceso',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, LucideLoader, LucideEye, LucideEyeOff, LucideCircleCheck],
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
        <div>
          <label class="field-label" [attr.for]="id('nombre')">Nombre</label>
          <input [id]="id('nombre')" class="input min-h-11" formControlName="nombre" autocomplete="name"
            [attr.aria-invalid]="invalido('nombre')" [attr.aria-describedby]="id('nombre-error')" />
          @if (invalido('nombre')) {
            <p [id]="id('nombre-error')" class="field-error">Escribe tu nombre (mínimo 2 caracteres).</p>
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
            [attr.aria-invalid]="invalido('password')"
            [attr.aria-describedby]="id('password-error') + ' ' + id('password-fuerza')" />
          <button type="button"
            class="absolute inset-y-0 right-0 grid w-11 cursor-pointer place-items-center rounded-r-lg text-muted-fg transition-colors hover:text-fg"
            [attr.aria-label]="mostrarClave() ? 'Ocultar contraseña' : 'Mostrar contraseña'"
            [attr.aria-pressed]="mostrarClave()"
            (click)="mostrarClave.set(!mostrarClave())">
            @if (mostrarClave()) {
              <svg lucideEyeOff class="size-[18px]" aria-hidden="true"></svg>
            } @else {
              <svg lucideEye class="size-[18px]" aria-hidden="true"></svg>
            }
          </button>
        </div>
        @if (invalido('password')) {
          <p [id]="id('password-error')" class="field-error">{{ registro() ? 'Usa entre 15 y 128 caracteres y evita contraseñas comunes.' : 'Escribe tu contraseña.' }}</p>
        }

        @if (registro()) {
          <div [id]="id('password-fuerza')" class="mt-3" aria-live="polite">
            <div class="flex gap-1.5" aria-hidden="true">
              @for (i of [1, 2, 3, 4]; track i) {
                <span class="h-1.5 flex-1 rounded-full transition-colors duration-300"
                  [class]="i > fuerza() ? 'bg-muted' : fuerza() <= 1 ? 'bg-danger' : fuerza() === 2 ? 'bg-warning' : 'bg-success'"></span>
              }
            </div>
            <p class="mt-2 text-xs text-muted-fg">Requisitos: <span class="font-medium text-fg">{{ fuerza() }}/4</span>. Usa una frase única que no hayas usado en otra cuenta.</p>
            <ul class="mt-1.5 grid grid-cols-2 gap-1 text-xs">
              @for (r of requisitos(); track r.texto) {
                <li class="flex items-center gap-1.5" [class.text-success]="r.ok" [class.text-muted-fg]="!r.ok">
                  <svg lucideCircleCheck class="size-3.5" aria-hidden="true"></svg> {{ r.texto }}
                </li>
              }
            </ul>
          </div>
        }
      </div>

      @if (registro()) {
        <div><label class="field-label" [attr.for]="id('confirmacion')">Confirma tu contraseña</label><input [id]="id('confirmacion')" type="password" class="input min-h-11" formControlName="confirmacion" autocomplete="new-password" [attr.aria-invalid]="form.hasError('passwordMismatch') && enviado()" />
        @if (form.hasError('passwordMismatch') && enviado()) { <p class="field-error" role="alert">Las contraseñas no coinciden.</p> }
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
  /** El usuario puede ver lo que escribió en la contraseña. */
  protected readonly mostrarClave = signal(false);

  protected readonly form = this.fb.group({
    nombre: [''],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
    confirmacion: [''],
  });

  private readonly clave = toSignal(this.form.controls.password.valueChanges.pipe(map((v) => v ?? '')), { initialValue: '' });

  /** Requisitos visibles al registrarse; la fuerza es cuántos se cumplen (0–4). */
  protected readonly requisitos = computed(() => {
    const c = this.clave();
    return [
      { texto: '15 caracteres o más', ok: c.length >= 15 },
      { texto: 'Hasta 128 caracteres', ok: c.length > 0 && c.length <= 128 },
      { texto: 'Frase no vacía', ok: c.trim().length > 0 },
      { texto: 'Evita claves comunes', ok: c.length > 0 && !['123456789012345', 'passwordpassword', 'contraseñacontraseña'].includes(c.toLowerCase()) },
    ];
  });
  protected readonly fuerza = computed(() => this.requisitos().filter((r) => r.ok).length);

  ngOnInit(): void {
    if (this.registro()) {
      this.form.controls.nombre.addValidators([Validators.required, Validators.minLength(2), Validators.maxLength(100), Validators.pattern(/\S/)]);
      this.form.controls.password.addValidators(securePassword);
      this.form.controls.confirmacion.addValidators(Validators.required);
      this.form.addValidators(matchingPasswords);
      this.form.controls.confirmacion.updateValueAndValidity();
      this.form.controls.nombre.updateValueAndValidity();
      this.form.controls.password.updateValueAndValidity();
    }
  }

  /** Ids únicos: los dos formularios están en la página a la vez. */
  protected id(campo: string): string {
    return `${this.modo()}-${campo}`;
  }

  protected invalido(campo: 'nombre' | 'email' | 'password'): boolean {
    const control = this.form.controls[campo];
    return control.invalid && (control.touched || this.enviado());
  }

  protected enviar(): void {
    if (this.enviando()) return;
    this.enviado.set(true);
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { nombre, email, password } = this.form.getRawValue();
    const peticion = this.registro()
      ? this.auth.registrar(nombre.trim(), email.trim(), password)
      : this.auth.login(email.trim(), password);

    this.enviando.set(true);
    peticion.subscribe({
      next: () => {
        const destino = this.volver()?.startsWith('/') ? this.volver()! : '/resumen';
        void this.router.navigateByUrl(this.registro() ? '/importar' : destino);
      },
      error: (e) => {
        this.enviando.set(false);
        this.error.set(mensajeDeError(e));
      },
    });
  }
}
