import { Signal, computed } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Observable, catchError, map, of, scan, startWith, switchMap } from 'rxjs';
import { mensajeDeError } from './estado';

export interface Carga<T> {
  cargando: boolean;
  datos: T | null;
  error: string | null;
}

/**
 * Carga datos cada vez que cambia `clave` (p. ej. el mes seleccionado o un contador de recarga).
 * Conserva los datos anteriores mientras recarga para evitar parpadeos.
 * Debe llamarse en un contexto de inyección (inicializador de campo o constructor).
 */
export function cargar<K, T>(clave: () => K, fuente: (clave: K) => Observable<T>): Signal<Carga<T>> {
  const inicial: Carga<T> = { cargando: true, datos: null, error: null };
  return toSignal(
    toObservable(computed(clave)).pipe(
      switchMap((k) =>
        fuente(k).pipe(
          map((datos): Carga<T> | null => ({ cargando: false, datos, error: null })),
          catchError((e) => of<Carga<T>>({ cargando: false, datos: null, error: mensajeDeError(e) })),
          startWith(null),
        ),
      ),
      scan((previo: Carga<T>, actual: Carga<T> | null) =>
        actual === null
          ? { ...previo, cargando: true, error: null }
          : { ...actual, datos: actual.error ? previo.datos : actual.datos }, inicial),
    ),
    { initialValue: inicial },
  );
}
