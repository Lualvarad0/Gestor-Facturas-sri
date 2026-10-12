import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PreferenciasService } from '../../core/estado';
import { Segmentado } from '../../shared/controles';
import { Titulo } from '../../shared/marca';

/** Ajustes de la aplicación en este navegador. Los datos de la persona están en Editar perfil (/perfil). */
@Component({
  selector: 'app-configuracion',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Titulo, Segmentado, RouterLink],
  template: `
    <app-titulo linea1="Configuración" linea2="del sistema" subtitulo="Ajustes de Nudo en este navegador." />

    <section class="card mt-6 max-w-3xl p-5 sm:p-6" aria-labelledby="titulo-apariencia">
      <h2 id="titulo-apariencia" class="font-semibold">Apariencia</h2>
      <p class="mt-1 text-sm text-muted-fg">Elige cómo se ve Nudo. Se guarda en este navegador.</p>
      <app-segmentado class="mt-4 block" [opciones]="temas" [(valor)]="preferencias.tema" etiqueta="Tema" />
    </section>

    <p class="mt-4 max-w-3xl text-sm text-muted-fg">
      ¿Buscas tus datos personales, cédula o RUC? Están en <a routerLink="/perfil" class="font-medium text-primary hover:underline">Editar perfil</a>.
    </p>
  `,
})
export class Configuracion {
  protected readonly preferencias = inject(PreferenciasService);
  protected readonly temas = [
    { valor: 'light', etiqueta: 'Claro' },
    { valor: 'dark', etiqueta: 'Oscuro' },
  ] as const;
}
