import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { LucideChevronDown, LucideChevronsUpDown, LucideLogOut, LucideSettings, LucideUserPen } from '@lucide/angular';
import { filter } from 'rxjs';
import { AuthService } from '../core/auth';
import { PerfilService } from '../core/perfil.service';

let siguienteId = 0;

/**
 * Menú de la cuenta: Mi perfil, Configuración del sistema y Cerrar sesión.
 * 'avatar' es el botón de la cabecera (abre hacia abajo); 'tarjeta', el pie del menú lateral (abre hacia arriba).
 */
@Component({
  selector: 'app-menu-cuenta',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, RouterLink, RouterLinkActive, LucideUserPen, LucideSettings, LucideLogOut, LucideChevronsUpDown, LucideChevronDown],
  host: { class: 'relative block', '(document:click)': 'clicFuera($event)', '(keydown)': 'tecla($event)' },
  template: `
    @if (variante() === 'avatar') {
      <button #boton type="button" class="flex items-center gap-1.5 rounded-full p-1 pr-2 transition-colors hover:bg-muted"
        [class.bg-muted]="abierto()" aria-haspopup="menu" [attr.aria-controls]="idMenu" [attr.aria-expanded]="abierto()"
        [attr.aria-label]="'Tu cuenta: ' + nombre() + (perfil.completo() ? '' : ' (perfil incompleto)')" (click)="alternar()">
        <ng-container *ngTemplateOutlet="avatar" />
        <svg lucideChevronDown class="size-4 text-muted-fg transition-transform" [class.rotate-180]="abierto()" aria-hidden="true"></svg>
      </button>
    } @else {
      <button #boton type="button" class="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left transition-colors hover:bg-muted"
        [class.bg-muted]="abierto()" aria-haspopup="menu" [attr.aria-controls]="idMenu" [attr.aria-expanded]="abierto()" (click)="alternar()">
        <ng-container *ngTemplateOutlet="avatar" />
        <span class="min-w-0 flex-1">
          <span class="block truncate text-sm font-medium">{{ nombre() }}</span>
          <span class="block truncate text-xs text-muted-fg">{{ auth.usuario()?.email }}</span>
        </span>
        <svg lucideChevronsUpDown class="size-4 shrink-0 text-muted-fg" aria-hidden="true"></svg>
        <span class="sr-only">Abrir el menú de tu cuenta{{ perfil.completo() ? '' : ' (perfil incompleto)' }}</span>
      </button>
    }

    @if (abierto()) {
      <div [id]="idMenu" role="menu" aria-label="Tu cuenta"
        class="absolute z-50 min-w-60 overflow-hidden rounded-xl border border-border bg-card p-1.5 shadow-dashboard"
        [class]="variante() === 'avatar' ? 'top-full right-0 mt-2' : 'inset-x-0 bottom-full mb-2'">
        <div class="flex items-center gap-3 px-3 pt-2 pb-2.5">
          <ng-container *ngTemplateOutlet="avatar" />
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold">{{ nombre() }}</p>
            <p class="truncate text-xs text-muted-fg">{{ auth.usuario()?.email }}</p>
          </div>
        </div>
        <div class="my-1 h-px bg-border" role="separator"></div>
        <a routerLink="/perfil" routerLinkActive="nav-activo" role="menuitem" class="nav-item">
          <svg lucideUserPen class="size-5" aria-hidden="true"></svg>
          <span class="flex-1">Mi perfil</span>
          @if (!perfil.completo()) {
            <span class="size-2 shrink-0 rounded-full bg-warning" title="Perfil incompleto" aria-hidden="true"></span>
            <span class="sr-only">(incompleto)</span>
          }
        </a>
        <a routerLink="/configuracion" routerLinkActive="nav-activo" role="menuitem" class="nav-item">
          <svg lucideSettings class="size-5" aria-hidden="true"></svg> Configuración del sistema
        </a>
        <div class="my-1 h-px bg-border" role="separator"></div>
        <button type="button" role="menuitem" class="nav-item w-full hover:bg-danger-soft hover:text-danger" (click)="auth.cerrarSesion()">
          <svg lucideLogOut class="size-5" aria-hidden="true"></svg> Cerrar sesión
        </button>
      </div>
    }

    <ng-template #avatar>
      <span class="relative grid size-9 shrink-0 place-items-center rounded-full bg-primary-soft text-sm font-semibold text-primary" aria-hidden="true">
        {{ iniciales() }}
        @if (!perfil.completo()) {
          <span class="absolute -top-0.5 -right-0.5 size-2.5 rounded-full bg-warning ring-2 ring-card"></span>
        }
      </span>
    </ng-template>
  `,
})
export class MenuCuenta {
  readonly variante = input<'avatar' | 'tarjeta'>('tarjeta');

  protected readonly auth = inject(AuthService);
  protected readonly perfil = inject(PerfilService);
  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly boton = viewChild<ElementRef<HTMLButtonElement>>('boton');
  protected readonly abierto = signal(false);
  protected readonly idMenu = `menu-cuenta-${++siguienteId}`;

  /** El perfil se actualiza al guardarlo; la sesión conserva el nombre con el que se inició. */
  protected readonly nombre = computed(() => this.perfil.perfil()?.nombre || this.auth.usuario()?.nombre || '');
  protected readonly iniciales = computed(() => {
    const p = this.perfil.perfil();
    const partes = p?.nombres && p.apellidos ? [p.nombres, p.apellidos] : this.nombre().split(/\s+/);
    return partes.filter(Boolean).slice(0, 2).map((t) => t.trim()[0]!.toUpperCase()).join('');
  });

  constructor() {
    inject(Router).events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.abierto.set(false));
  }

  protected alternar(): void {
    this.abierto.update((a) => !a);
    // Al abrir, el foco pasa a la primera opción.
    if (this.abierto()) setTimeout(() => this.opciones()[0]?.focus());
  }

  protected clicFuera(evento: MouseEvent): void {
    if (this.abierto() && !this.elemento.nativeElement.contains(evento.target as Node)) this.abierto.set(false);
  }

  /** Escape cierra y devuelve el foco; las flechas recorren las opciones; Tab sale del menú. */
  protected tecla(evento: KeyboardEvent): void {
    if (!this.abierto()) return;
    const opciones = this.opciones();
    const actual = opciones.indexOf(document.activeElement as HTMLElement);
    if (evento.key === 'Escape') {
      this.abierto.set(false);
      this.boton()?.nativeElement.focus();
    } else if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
      evento.preventDefault();
      const paso = evento.key === 'ArrowDown' ? 1 : -1;
      opciones[(actual + paso + opciones.length) % opciones.length]?.focus();
    } else if (evento.key === 'Tab') {
      this.abierto.set(false);
    }
  }

  private opciones(): HTMLElement[] {
    return Array.from(this.elemento.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]'));
  }
}
