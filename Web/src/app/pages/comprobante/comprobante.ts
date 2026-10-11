import { ChangeDetectionStrategy, Component, computed, inject, input, numberAttribute, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { LucideArrowLeft, LucideCircleAlert, LucideDownload, LucideLoader, LucideTrash, LucideTriangleAlert } from '@lucide/angular';
import { cargar } from '../../core/carga';
import { categoria } from '../../core/categorias';
import { ComprobantesService } from '../../core/comprobantes.service';
import { AvisosService } from '../../core/estado';
import { CategoriaGasto } from '../../core/models';
import { CategoriaSelect } from '../../shared/categoria';
import { DineroPipe, FechaCortaPipe } from '../../shared/formato';

@Component({
  selector: 'app-comprobante',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DineroPipe, FechaCortaPipe, CategoriaSelect, LucideArrowLeft, LucideDownload, LucideTrash, LucideLoader, LucideCircleAlert, LucideTriangleAlert],
  templateUrl: './comprobante.html',
})
export class ComprobantePagina {
  /** Parámetro :id de la ruta. */
  readonly id = input.required({ transform: numberAttribute });

  private readonly comprobantes = inject(ComprobantesService);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);

  private readonly recargas = signal(0);
  protected readonly guardando = signal(false);
  protected readonly descargando = signal(false);
  protected readonly confirmandoEliminar = signal(false);
  protected readonly eliminando = signal(false);
  protected readonly aplicarAlProveedor = signal(true);

  protected readonly carga = cargar(() => ({ id: this.id(), _: this.recargas() }), ({ id }) => this.comprobantes.obtener(id));
  protected readonly c = computed(() => this.carga().datos);
  protected readonly esNotaCredito = computed(() => this.c()?.tipoComprobante === 'Nota de crédito');
  protected readonly esIngreso = computed(() => this.c()?.direccion === 'Emitido');

  protected cambiarCategoria(nueva: CategoriaGasto): void {
    const actual = this.c();
    if (!actual) return;
    this.guardando.set(true);
    this.comprobantes.cambiarCategoria(actual.id, nueva, this.aplicarAlProveedor()).subscribe({
      next: () => {
        this.guardando.set(false);
        this.avisos.exito(this.aplicarAlProveedor()
          ? `${categoria(nueva).nombre}: aplicado a todos los comprobantes de ${actual.razonSocialEmisor}.`
          : `Categoría cambiada a ${categoria(nueva).nombre}.`);
        this.recargas.update((n) => n + 1);
      },
      error: (e) => {
        this.guardando.set(false);
        this.avisos.error(e);
      },
    });
  }

  protected descargarXml(): void {
    const actual = this.c();
    if (!actual) return;
    this.descargando.set(true);
    this.comprobantes.descargarXml(actual.id).subscribe({
      next: (blob) => {
        this.descargando.set(false);
        const url = URL.createObjectURL(blob);
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.download = `${actual.claveAcceso}.xml`;
        enlace.click();
        URL.revokeObjectURL(url);
      },
      error: (e) => {
        this.descargando.set(false);
        this.avisos.error(e);
      },
    });
  }

  protected eliminar(): void {
    const actual = this.c();
    if (!actual) return;
    this.eliminando.set(true);
    this.comprobantes.eliminar(actual.id).subscribe({
      next: () => {
        this.avisos.exito('Comprobante eliminado. Puedes volver a importarlo cuando quieras.');
        void this.router.navigate(['/gastos']);
      },
      error: (e) => {
        this.eliminando.set(false);
        this.confirmandoEliminar.set(false);
        this.avisos.error(e);
      },
    });
  }
}
