import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { Perfil, PerfilActualizado, PerfilRequest, RegimenTributario } from './models';

export const REGIMENES: readonly { valor: RegimenTributario; nombre: string; descripcion: string }[] = [
  { valor: 'General', nombre: 'Régimen general', descripcion: 'Declaración de IVA mensual' },
  { valor: 'RimpeEmprendedor', nombre: 'RIMPE emprendedor', descripcion: 'Declaración de IVA semestral' },
  { valor: 'RimpeNegocioPopular', nombre: 'RIMPE negocio popular', descripcion: 'Cuota fija anual, sin IVA en ventas' },
];

/** Perfil tributario del usuario autenticado; lo comparten todas las páginas. */
@Injectable({ providedIn: 'root' })
export class PerfilService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/perfil`;

  readonly perfil = signal<Perfil | null>(null);
  readonly completo = computed(() => this.perfil()?.completo ?? true);
  readonly tieneRuc = computed(() => !!this.perfil()?.ruc);

  cargar(): void {
    this.http.get<Perfil>(this.url).subscribe({
      next: (p) => this.perfil.set(p),
      // Sin perfil la app sigue funcionando (sin avisos de perfil); cada página muestra sus propios errores de red.
      error: () => this.perfil.set(null),
    });
  }

  guardar(request: PerfilRequest): Observable<PerfilActualizado> {
    return this.http.put<PerfilActualizado>(this.url, request).pipe(tap((r) => this.perfil.set(r.perfil)));
  }
}

/** Valida la cédula ecuatoriana (módulo 10), igual que el backend, para avisar mientras se escribe. */
export function cedulaValida(cedula: string): boolean {
  if (!/^\d{10}$/.test(cedula)) return false;
  const provincia = Number(cedula.slice(0, 2));
  if (!((provincia >= 1 && provincia <= 24) || provincia === 30) || Number(cedula[2]) >= 6) return false;
  let suma = 0;
  for (let i = 0; i < 9; i++) {
    const producto = Number(cedula[i]) * (i % 2 === 0 ? 2 : 1);
    suma += producto > 9 ? producto - 9 : producto;
  }
  return (10 - (suma % 10)) % 10 === Number(cedula[9]);
}

export function rucValido(ruc: string): boolean {
  if (!/^\d{13}$/.test(ruc)) return false;
  const provincia = Number(ruc.slice(0, 2));
  if (!((provincia >= 1 && provincia <= 24) || provincia === 30)) return false;
  const tercero = Number(ruc[2]);
  if (tercero < 6) return cedulaValida(ruc.slice(0, 10)) && ruc.slice(10) !== '000';
  if (tercero === 6) return ruc.slice(9) !== '0000';
  if (tercero === 9) return ruc.slice(10) !== '000';
  return false;
}

export function soloDigitos(valor: string): string {
  return valor.replace(/\D/g, '');
}
