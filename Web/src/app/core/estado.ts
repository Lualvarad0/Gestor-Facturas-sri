import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, effect, signal } from '@angular/core';

const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

export function nombreMes(mes: number): string {
  return MESES[mes - 1] ?? '';
}

/** Mes de trabajo: la versión gratuita ordena las facturas del mes en curso. */
@Injectable({ providedIn: 'root' })
export class PeriodoService {
  private readonly hoy = new Date();
  readonly anio = signal(this.hoy.getFullYear()).asReadonly();
  readonly mes = signal(this.hoy.getMonth() + 1).asReadonly();

  readonly etiqueta = computed(() => `${capitalizar(nombreMes(this.mes()))} ${this.anio()}`);
}

export type Tema = 'light' | 'dark';

/** Preferencias de este navegador (los datos tributarios viven en el perfil, en el servidor). */
@Injectable({ providedIn: 'root' })
export class PreferenciasService {
  readonly tema = signal<Tema>((document.documentElement.dataset['theme'] as Tema) ?? 'light');

  constructor() {
    effect(() => {
      const tema = this.tema();
      document.documentElement.dataset['theme'] = tema;
      escribir('tema', tema);
    });
  }

  alternarTema(): void {
    this.tema.update((t) => (t === 'dark' ? 'light' : 'dark'));
  }
}

export interface Aviso {
  id: number;
  tipo: 'exito' | 'error';
  texto: string;
}

@Injectable({ providedIn: 'root' })
export class AvisosService {
  private siguiente = 1;
  readonly avisos = signal<Aviso[]>([]);

  exito(texto: string): void {
    this.mostrar('exito', texto);
  }

  error(error: unknown): void {
    this.mostrar('error', mensajeDeError(error));
  }

  cerrar(id: number): void {
    this.avisos.update((lista) => lista.filter((a) => a.id !== id));
  }

  private mostrar(tipo: Aviso['tipo'], texto: string): void {
    const id = this.siguiente++;
    this.avisos.update((lista) => [...lista, { id, tipo, texto }]);
    setTimeout(() => this.cerrar(id), tipo === 'error' ? 8000 : 4500);
  }
}

/** Extrae un mensaje legible de un ProblemDetails / ValidationProblemDetails de la API. */
export function mensajeDeError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'No se pudo conectar con el servidor. Revisa tu conexión.';
    const cuerpo = error.error as { detail?: string; title?: string; errors?: Record<string, string[]> } | null;
    if (cuerpo?.errors) return Object.values(cuerpo.errors).flat().join(' ');
    if (cuerpo?.detail) return cuerpo.detail;
    if (error.status >= 500) return 'Ocurrió un error en el servidor. Inténtalo de nuevo.';
    return cuerpo?.title ?? 'No se pudo completar la operación.';
  }
  return 'Ocurrió un error inesperado.';
}

export function capitalizar(texto: string): string {
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

function escribir(clave: string, valor: string): void {
  try {
    localStorage.setItem(clave, valor);
  } catch {
    /* almacenamiento no disponible: la preferencia dura solo esta sesión */
  }
}
