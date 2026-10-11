import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Titulo } from '../../shared/marca';
import { RouterLink } from '@angular/router';
import { LucideCircleAlert } from '@lucide/angular';
import { cargar } from '../../core/carga';
import { Buscador, ErrorCarga } from '../../shared/controles';
import { categoria } from '../../core/categorias';
import { ComprobantesService } from '../../core/comprobantes.service';
import { AvisosService } from '../../core/estado';
import { CategoriaGasto, Proveedor } from '../../core/models';
import { CategoriaSelect } from '../../shared/categoria';
import { DineroPipe } from '../../shared/formato';

@Component({
  selector: 'app-proveedores',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Buscador, ErrorCarga, Titulo, RouterLink, DineroPipe, CategoriaSelect],
  template: `
    <app-titulo linea1="Tus" linea2="proveedores" subtitulo="Asigna una categoría a cada proveedor una sola vez: se aplica a todos sus comprobantes, incluidos los que importes después." />

    <div class="mt-6 flex flex-wrap items-center gap-3">
      <app-buscador placeholder="Buscar por nombre o RUC" [valor]="busqueda()" (valorChange)="busqueda.set($event)" />
      <label class="inline-flex min-h-10 items-center gap-2 text-sm">
        <input type="checkbox" class="size-4 accent-[var(--accent)]" [checked]="soloSinCategoria()" (change)="soloSinCategoria.set($any($event.target).checked)" />
        Solo sin categoría
      </label>
    </div>

    <app-error-carga [mensaje]="carga().error" />

    <section class="card mt-4 overflow-hidden" [attr.aria-busy]="carga().cargando">
      @if (carga().cargando && !carga().datos) {
        <div class="space-y-3 p-5">
          @for (i of [1, 2, 3, 4]; track i) { <div class="h-12 animate-pulse rounded-lg bg-muted"></div> }
        </div>
      } @else if (filtrados().length === 0) {
        <p class="px-6 py-14 text-center text-sm text-muted-fg">
          {{ (carga().datos?.length ?? 0) === 0 ? 'Aún no tienes proveedores. Importa tus comprobantes del SRI.' : 'Ningún proveedor coincide.' }}
        </p>
      } @else {
        <ul class="divide-y divide-border">
          @for (p of filtrados(); track p.ruc) {
            <li class="grid gap-3 px-4 py-4 sm:grid-cols-[1fr_auto_14rem] sm:items-center sm:px-5">
              <div class="min-w-0">
                <a [routerLink]="['/gastos']" [queryParams]="{ ruc: p.ruc }" class="block truncate font-medium hover:text-accent hover:underline">{{ p.razonSocial }}</a>
                <span class="text-xs text-muted-fg">RUC {{ p.ruc }} · {{ p.comprobantes }} {{ p.comprobantes === 1 ? 'comprobante' : 'comprobantes' }}</span>
              </div>
              <span class="money text-sm font-semibold sm:text-right">{{ p.total | dinero }}</span>
              <app-categoria-select
                [valor]="p.categoria ?? 'SinCategoria'"
                [deshabilitado]="guardando() === p.ruc"
                [etiqueta]="'Categoría de ' + p.razonSocial"
                (cambio)="asignar(p, $event)"
              />
            </li>
          }
        </ul>
      }
    </section>
  `,
})
export class Proveedores {
  private readonly comprobantes = inject(ComprobantesService);
  private readonly avisos = inject(AvisosService);

  private readonly recargas = signal(0);
  protected readonly busqueda = signal('');
  protected readonly soloSinCategoria = signal(false);
  protected readonly guardando = signal<string | null>(null);

  protected readonly carga = cargar(() => this.recargas(), () => this.comprobantes.proveedores());

  protected readonly filtrados = computed(() => {
    const texto = this.busqueda().trim().toLowerCase();
    return (this.carga().datos ?? []).filter((p) =>
      (!this.soloSinCategoria() || !p.categoria || p.categoria === 'SinCategoria')
      && (!texto || p.razonSocial.toLowerCase().includes(texto) || p.ruc.includes(texto)));
  });

  protected asignar(p: Proveedor, nueva: CategoriaGasto): void {
    this.guardando.set(p.ruc);
    this.comprobantes.categorizarProveedor(p.ruc, nueva).subscribe({
      next: () => {
        this.guardando.set(null);
        this.avisos.exito(`${p.razonSocial}: ${categoria(nueva).nombre}.`);
        this.recargas.update((n) => n + 1);
      },
      error: (e) => {
        this.guardando.set(null);
        this.avisos.error(e);
      },
    });
  }
}
