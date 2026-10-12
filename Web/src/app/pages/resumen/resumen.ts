import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { cargar } from '../../core/carga';
import { ComprobantesService } from '../../core/comprobantes.service';
import { nombreMes, PeriodoService } from '../../core/estado';
import { PresupuestoService } from '../../core/presupuesto.service';
import { categoria } from '../../core/categorias';
import { DineroPipe } from '../../shared/formato';
import { ErrorCarga } from '../../shared/controles';
import { Titulo } from '../../shared/marca';

@Component({
  selector: 'app-resumen',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DineroPipe, ErrorCarga, Titulo],
  templateUrl: './resumen.html',
})
export class ResumenPagina {
  protected readonly periodo = inject(PeriodoService);
  private readonly servicio = inject(ComprobantesService);
  protected readonly recarga = signal(0);
  protected readonly carga = cargar(
    () => ({ anio: this.periodo.anio(), mes: this.periodo.mes(), recarga: this.recarga() }),
    ({ anio, mes }) => forkJoin({ mensual: this.servicio.resumen(anio, mes), anual: this.servicio.resumen(anio) }),
  );
  protected readonly resumen = computed(() => this.carga().datos?.mensual);
  private readonly presupuestoService = inject(PresupuestoService);
  /** Aparte del resumen: si falla, el panel de gastos sigue funcionando. */
  protected readonly presupuesto = cargar(
    () => ({ anio: this.periodo.anio(), mes: this.periodo.mes(), recarga: this.recarga() }),
    ({ anio, mes }) => this.presupuestoService.obtener(anio, mes),
  );
  /** Rubros ordenados por cuánto del límite llevan gastado. */
  protected readonly rubros = computed(() => (this.presupuesto().datos?.rubros ?? [])
    .map(r => ({ ...r, avance: r.limite > 0 ? r.gastado / r.limite * 100 : r.gastado > 0 ? 100 : 0 }))
    .sort((a, b) => b.avance - a.avance));
  protected readonly categorias = computed(() => (this.resumen()?.porCategoria ?? [])
    .map(c => ({ ...c, ...categoria(c.categoria) })).sort((a, b) => b.total - a.total));
  protected readonly baseCategorias = computed(() => Math.max(1, ...this.categorias().map(c => Math.abs(c.total))));
  protected readonly meses = computed(() => {
    const datos = this.carga().datos?.anual.porMes ?? [];
    const maximo = Math.max(1, ...datos.map(m => Math.abs(m.total)));
    return datos.map(m => ({ ...m, nombre: nombreMes(m.mes).slice(0, 3), altura: Math.abs(m.total) / maximo * 100 }));
  });
  protected readonly promedio = computed(() => {
    const r = this.resumen();
    return r?.comprobantes ? r.total / r.comprobantes : 0;
  });
  protected seleccionarMes(mes: number): void {
    this.periodo.seleccionar(`${this.periodo.anio()}-${String(mes).padStart(2, '0')}`);
  }
  protected reintentar(): void {
    this.recarga.update(n => n + 1);
  }
}
