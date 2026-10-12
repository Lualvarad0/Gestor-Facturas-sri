import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import {
  LucideChevronsUpDown,
  LucideFileUp,
  LucideLayoutDashboard,
  LucideLogOut,
  LucideMenu,
  LucideMoon,
  LucideReceipt,
  LucideSettings,
  LucideStore,
  LucideSun,
  LucideUserPen,
  LucideWallet,
  LucideX,
} from '@lucide/angular';
import { AuthService } from '../core/auth';
import { PeriodoService, PreferenciasService } from '../core/estado';
import { PerfilService } from '../core/perfil.service';
import { Avisos } from '../shared/avisos';
import { SelectorPeriodo } from '../shared/calendario';
import { Credito, Marca } from '../shared/marca';

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, Avisos, Marca, Credito, SelectorPeriodo,
    LucideLayoutDashboard, LucideReceipt, LucideFileUp, LucideStore, LucideSettings, LucideLogOut, LucideMenu, LucideX, LucideMoon, LucideSun, LucideWallet,
    LucideUserPen, LucideChevronsUpDown,
  ],
  templateUrl: './shell.html',
  host: { '(document:click)': 'clicFuera($event)' },
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly periodo = inject(PeriodoService);
  protected readonly preferencias = inject(PreferenciasService);
  protected readonly perfil = inject(PerfilService);
  protected readonly menuAbierto = signal(false);
  protected readonly menuUsuario = signal(false);
  private readonly menuCuenta = viewChild<ElementRef<HTMLElement>>('menuCuenta');
  private readonly botonCuenta = viewChild<ElementRef<HTMLButtonElement>>('botonCuenta');

  /** El perfil se actualiza al guardarlo; la sesión conserva el nombre con el que se inició. */
  protected readonly nombre = computed(() => this.perfil.perfil()?.nombre || this.auth.usuario()?.nombre || '');
  protected readonly iniciales = computed(() => {
    const p = this.perfil.perfil();
    const partes = p?.nombres && p.apellidos ? [p.nombres, p.apellidos] : this.nombre().split(/\s+/);
    return partes.filter(Boolean).slice(0, 2).map((t) => t.trim()[0]!.toUpperCase()).join('');
  });

  constructor() {
    this.perfil.cargar();

    // Cierra el menú lateral en móvil y el de la cuenta al navegar.
    inject(Router).events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => {
        this.menuAbierto.set(false);
        this.menuUsuario.set(false);
      });
  }

  protected alternarMenuUsuario(): void {
    this.menuUsuario.update((abierto) => !abierto);
    // Al abrir, el foco pasa a la primera opción.
    if (this.menuUsuario()) setTimeout(() => this.opciones()[0]?.focus());
  }

  protected clicFuera(evento: MouseEvent): void {
    if (this.menuUsuario() && !this.menuCuenta()?.nativeElement.contains(evento.target as Node)) this.menuUsuario.set(false);
  }

  /** Escape cierra y devuelve el foco; las flechas recorren las opciones. */
  protected teclaMenu(evento: KeyboardEvent): void {
    if (!this.menuUsuario()) return;
    const opciones = this.opciones();
    const actual = opciones.indexOf(document.activeElement as HTMLElement);
    if (evento.key === 'Escape') {
      this.menuUsuario.set(false);
      this.botonCuenta()?.nativeElement.focus();
    } else if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
      evento.preventDefault();
      const paso = evento.key === 'ArrowDown' ? 1 : -1;
      opciones[(actual + paso + opciones.length) % opciones.length]?.focus();
    } else if (evento.key === 'Tab') {
      this.menuUsuario.set(false);
    }
  }

  private opciones(): HTMLElement[] {
    return Array.from(this.menuCuenta()?.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? []);
  }
}
