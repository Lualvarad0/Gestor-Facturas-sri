import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth';
import { mensajeDeError } from '../../core/estado';
import { Marca } from '../../shared/marca';
import { matchingPasswords, securePassword } from '../../shared/password';

@Component({
  selector: 'app-recuperacion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, Marca],
  template: `
    <main class="grid min-h-dvh place-items-center bg-bg px-5 py-10">
      <section class="card w-full max-w-md p-7 sm:p-9">
        <a routerLink="/login" aria-label="Nudo, volver al inicio de sesión"><app-marca [tamano]="30" /></a>
        <h1 class="mt-8 text-2xl font-semibold">{{ restablecer ? 'Elige una nueva contraseña' : 'Recupera tu cuenta' }}</h1>
        <p class="mt-3 text-sm text-muted-fg">{{ restablecer ? 'Usa una frase única de 15 a 128 caracteres. Los espacios están permitidos.' : 'Escribe el correo de tu cuenta. Si está registrado, recibirás un enlace válido durante 15 minutos.' }}</p>
        @if (error()) { <div class="aviso-error mt-5" role="alert">{{ error() }}</div> }
        @if (mensaje()) {
          <div class="aviso-ok mt-5" role="status">{{ mensaje() }}</div>
          @if (!restablecer) { <p class="mt-3 text-xs text-muted-fg">Revisa también el correo no deseado. Puedes pedir otro enlace después de dos minutos.</p> }
        } @else if (restablecer && !enlaceValido) {
          <p class="aviso-atencion mt-5" role="alert">El enlace está incompleto. Solicita uno nuevo.</p>
        } @else {
          <form class="mt-6 space-y-4" [formGroup]="form" (ngSubmit)="enviar()" novalidate>
            @if (!restablecer) {
              <div><label for="recovery-email" class="field-label">Correo electrónico</label><input id="recovery-email" type="email" autocomplete="email" class="input" formControlName="email" [attr.aria-invalid]="form.controls.email.invalid && enviado()" />
              @if (form.controls.email.invalid && enviado()) { <p class="field-error">Escribe un correo válido.</p> }</div>
            } @else {
              <div><label for="recovery-password" class="field-label">Nueva contraseña</label><input id="recovery-password" [type]="mostrar() ? 'text' : 'password'" autocomplete="new-password" class="input" formControlName="password" [attr.aria-invalid]="form.controls.password.invalid && enviado()" />
              @if (form.controls.password.invalid && enviado()) { <p class="field-error">Usa entre 15 y 128 caracteres y evita contraseñas comunes.</p> }</div>
              <div><label for="recovery-confirm" class="field-label">Confirma la contraseña</label><input id="recovery-confirm" type="password" autocomplete="new-password" class="input" formControlName="confirmacion" />
              @if (form.hasError('passwordMismatch') && enviado()) { <p class="field-error">Las contraseñas no coinciden.</p> }</div>
              <button type="button" class="text-sm text-primary" [attr.aria-pressed]="mostrar()" (click)="mostrar.set(!mostrar())">{{ mostrar() ? 'Ocultar contraseña' : 'Mostrar contraseña' }}</button>
            }
            <button type="submit" class="btn btn-acento w-full" [disabled]="enviando()">{{ enviando() ? 'Procesando…' : restablecer ? 'Guardar nueva contraseña' : 'Enviar enlace de recuperación' }}</button>
          </form>
        }
        <a routerLink="/login" class="mt-6 block text-center text-sm text-primary hover:underline">Volver a iniciar sesión</a>
        @if (restablecer && !mensaje()) { <a routerLink="/recuperar" class="mt-3 block text-center text-sm text-muted-fg hover:underline">Solicitar otro enlace</a> }
      </section>
    </main>
  `,
})
export class Recuperacion {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(NonNullableFormBuilder);
  protected readonly restablecer = this.route.snapshot.data['restablecer'] === true;
  private readonly parametros = new URLSearchParams(this.route.snapshot.fragment ?? '');
  private readonly token = this.parametros.get('token') ?? '';
  protected readonly enlaceValido = /^[a-fA-F0-9]{64}$/.test(this.token) && !!this.parametros.get('email');
  protected readonly mostrar = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly mensaje = signal<string | null>(null);
  protected readonly enviado = signal(false);
  protected readonly enviando = signal(false);
  protected readonly form = this.fb.group({
    email: [this.parametros.get('email') ?? '', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: [''], confirmacion: [''],
  });

  constructor() {
    if (this.restablecer) {
      // Quita el secreto del historial de esta pestaña después de leerlo.
      window.history.replaceState(window.history.state, '', window.location.pathname);
      this.form.controls.password.addValidators([Validators.required, securePassword]);
      this.form.controls.confirmacion.addValidators(Validators.required);
      this.form.addValidators(matchingPasswords);
      this.form.controls.password.updateValueAndValidity();
      this.form.controls.confirmacion.updateValueAndValidity();
    }
  }

  protected enviar(): void {
    if (this.enviando()) return;
    this.enviado.set(true);
    this.error.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || (this.restablecer && !this.enlaceValido)) return;
    const { email, password } = this.form.getRawValue();
    this.enviando.set(true);
    const request = this.restablecer ? this.auth.restablecer(email.trim(), this.token, password) : this.auth.recuperar(email.trim());
    request.subscribe({
      next: r => {
        if (this.restablecer) this.auth.cerrarSesion(false);
        this.mensaje.set(r.mensaje);
        this.enviando.set(false);
        this.form.reset();
      },
      error: e => { this.error.set(mensajeDeError(e)); this.enviando.set(false); },
    });
  }
}
