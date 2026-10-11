import { Pipe, PipeTransform } from '@angular/core';

const MONEDA = new Intl.NumberFormat('es-EC', { style: 'currency', currency: 'USD' });

/** 1234.5 → "$1.234,50" */
@Pipe({ name: 'dinero' })
export class DineroPipe implements PipeTransform {
  transform(valor: number | null | undefined): string {
    return MONEDA.format(valor ?? 0);
  }
}

/** '2026-10-03' → '03/10/2026' (sin conversión de zona horaria). */
@Pipe({ name: 'fechaCorta' })
export class FechaCortaPipe implements PipeTransform {
  transform(valor: string | null | undefined): string {
    if (!valor) return '';
    const [anio, mes, dia] = valor.slice(0, 10).split('-');
    return `${dia}/${mes}/${anio}`;
  }
}
