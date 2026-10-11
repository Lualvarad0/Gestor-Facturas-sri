import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { Titulo } from '../../shared/marca';
import { Router, RouterLink } from '@angular/router';
import { LucideFileUp, LucideTriangleAlert, LucideX } from '@lucide/angular';
import { cargar } from '../../core/carga';
import { Buscador, ErrorCarga } from '../../shared/controles';
import { CATEGORIAS, categoria } from '../../core/categorias';
import { ComprobantesService } from '../../core/comprobantes.service';
import { AvisosService, PeriodoService } from '../../core/estado';
import { CategoriaGasto, ComprobanteLista } from '../../core/models';
import { CategoriaSelect } from '../../shared/categoria';
import { DineroPipe, FechaCortaPipe } from '../../shared/formato';

@Component({
  selector: 'app-gastos',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Buscador, ErrorCarga, Titulo, RouterLink, DineroPipe, FechaCortaPipe, CategoriaSelect, LucideFileUp, LucideX, LucideTriangleAlert],
  templateUrl: './gastos.html',
})
export class Gastos {
  /** Query params (?categoria=SinCategoria, ?ruc=...) enlazados por el router. */
  readonly categoria = input<CategoriaGasto | ''>();
  readonly ruc = input<string | undefined>();
  /** ?ajenos=true: solo facturas que no están a nombre del usuario. */
  readonly ajenos = input(false, { transform: (v: unknown) => v === true || v === 'true' });

  private readonly comprobantes = inject(ComprobantesService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  protected readonly periodo = inject(PeriodoService);

  protected readonly categorias = CATEGORIAS;
  protected readonly busqueda = signal('');
  private readonly busquedaAplicada = signal('');
  private readonly recargas = signal(0);
  protected readonly guardando = signal<number | null>(null);
  private temporizador?: ReturnType<typeof setTimeout>;

  protected readonly etiquetaPeriodo = this.periodo.etiqueta;

  protected readonly carga = cargar(
    () => ({
      anio: this.periodo.anio(),
      mes: this.periodo.mes(),
      categoria: this.categoria() || undefined,
      ruc: this.ruc(),
      q: this.busquedaAplicada() || undefined,
      _: this.recargas(),
    }),
    ({ _, ...filtro }) => this.comprobantes.listar(filtro),
  );

  protected readonly lista = computed(() => {
    const datos = this.carga().datos ?? [];
    return this.ajenos() ? datos.filter((c) => c.aNombreDelUsuario === false) : datos;
  });
  protected readonly total = computed(() => this.lista().reduce((suma, c) => suma + signo(c) * c.total, 0));
  protected readonly proveedorFiltrado = computed(() => {
    const ruc = this.ruc();
    return ruc ? (this.lista().find((c) => c.rucEmisor === ruc)?.razonSocialEmisor ?? `RUC ${ruc}`) : null;
  });
  protected readonly hayFiltros = computed(() => !!(this.categoria() || this.ruc() || this.ajenos() || this.busquedaAplicada()));

  protected buscar(texto: string): void {
    this.busqueda.set(texto);
    clearTimeout(this.temporizador);
    this.temporizador = setTimeout(() => this.busquedaAplicada.set(texto.trim()), 300);
  }

  protected filtrarCategoria(valor: string): void {
    void this.router.navigate([], { queryParams: { categoria: valor || null }, queryParamsHandling: 'merge' });
  }

  protected limpiarFiltros(): void {
    this.busqueda.set('');
    this.busquedaAplicada.set('');
    void this.router.navigate([], { queryParams: {} });
  }

  protected cambiarCategoria(c: ComprobanteLista, nueva: CategoriaGasto): void {
    this.guardando.set(c.id);
    this.comprobantes.cambiarCategoria(c.id, nueva, true).subscribe({
      next: () => {
        this.guardando.set(null);
        this.avisos.exito(`${categoria(nueva).nombre}: aplicado a ${c.razonSocialEmisor} y a todos sus comprobantes.`);
        this.recargas.update((n) => n + 1);
      },
      error: (e) => {
        this.guardando.set(null);
        this.avisos.error(e);
        this.recargas.update((n) => n + 1);
      },
    });
  }

  protected esNotaCredito(c: ComprobanteLista): boolean {
    return signo(c) < 0;
  }
}

function signo(c: ComprobanteLista): number {
  return c.tipoComprobante === 'Nota de crédito' ? -1 : 1;
}
