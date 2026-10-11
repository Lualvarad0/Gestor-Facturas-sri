import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideArrowRight, LucideCircleCheck, LucideShieldCheck } from '@lucide/angular';
import { Credito, Marca } from '../../shared/marca';
import { FormularioAcceso, ModoAcceso } from './formulario-acceso';

/**
 * Pantalla de acceso: iniciar sesión a la izquierda, registro a la derecha y un panel que se
 * desliza sobre el formulario que no se usa. /login y /registro comparten la misma ruta para
 * que el componente no se recree y el panel pueda viajar de un lado al otro.
 */
@Component({
  selector: 'app-acceso',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, FormularioAcceso, Marca, Credito, LucideArrowRight, LucideCircleCheck, LucideShieldCheck],
  templateUrl: './acceso.html',
})
export class Acceso {
  /** Parámetro de la ruta (/login o /registro). */
  readonly modo = input<ModoAcceso>('login');
  /** Query param ?volver= que deja el guard. */
  readonly volver = input<string | undefined>();

  /** El panel tapa el formulario que no se usa e invita a cambiar al otro. */
  protected readonly paneles = [
    {
      modo: 'login',
      titulo: '¿Nuevo en',
      enfasis: 'Nudo?',
      texto: 'Crea tu cuenta gratis y ordena los gastos del mes con tus facturas del SRI.',
      boton: 'Crear cuenta',
      ruta: '/registro',
    },
    {
      modo: 'registro',
      titulo: 'Qué bueno',
      enfasis: 'verte',
      texto: '¿Ya tienes cuenta? Inicia sesión y revisa tus gastos del mes.',
      boton: 'Iniciar sesión',
      ruta: '/login',
    },
  ] as const;

  protected readonly escenas = [
    { titulo: 'Supermaxi · Alimentación', monto: '−$101,68', ingreso: false },
    { titulo: 'Kywi · Vivienda', monto: '−$38,50', ingreso: false },
    { titulo: 'Fybeca · Salud', monto: '−$43,92', ingreso: false },
  ];
}
