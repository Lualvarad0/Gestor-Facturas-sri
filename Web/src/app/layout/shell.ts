import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import {
  LucideFileUp,
  LucideLayoutDashboard,
  LucideMenu,
  LucideMoon,
  LucideReceipt,
  LucideStore,
  LucideSun,
  LucideWallet,
  LucideX,
} from '@lucide/angular';
import { PeriodoService, PreferenciasService } from '../core/estado';
import { PerfilService } from '../core/perfil.service';
import { Avisos } from '../shared/avisos';
import { SelectorPeriodo } from '../shared/calendario';
import { Credito, Marca } from '../shared/marca';
import { MenuCuenta } from './menu-cuenta';

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, Avisos, Marca, Credito, SelectorPeriodo, MenuCuenta,
    LucideLayoutDashboard, LucideReceipt, LucideFileUp, LucideStore, LucideMenu, LucideX, LucideMoon, LucideSun, LucideWallet,
  ],
  templateUrl: './shell.html',
})
export class Shell {
  protected readonly periodo = inject(PeriodoService);
  protected readonly preferencias = inject(PreferenciasService);
  private readonly perfil = inject(PerfilService);
  protected readonly menuAbierto = signal(false);

  constructor() {
    this.perfil.cargar();

    // Cierra el menú lateral en móvil al navegar.
    inject(Router).events
      .pipe(filter((e) => e instanceof NavigationEnd), takeUntilDestroyed())
      .subscribe(() => this.menuAbierto.set(false));
  }
}
