import { AbstractControl, ValidationErrors } from '@angular/forms';

export function securePassword(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');
  return value.length >= 15 && value.length <= 128 && value.trim().length > 0
    && !['123456789012345', 'passwordpassword', 'contraseñacontraseña'].includes(value.toLowerCase())
    ? null : { securePassword: true };
}

export function matchingPasswords(control: AbstractControl): ValidationErrors | null {
  return control.get('password')?.value === control.get('confirmacion')?.value ? null : { passwordMismatch: true };
}

export interface DatosPersonales {
  nombres?: string;
  apellidos?: string;
  email?: string;
  /** 'yyyy-MM-dd' */
  fechaNacimiento?: string;
}

/** Minúsculas, sin tildes ni separadores (igual que PasswordSecurity.Normalizar en el backend). */
function normalizar(texto: string): string {
  return texto.normalize('NFD').toLowerCase().replace(/[^\p{L}\p{N}]/gu, '');
}

/**
 * Si la contraseña contiene nombres, apellidos, el correo (parte local) o la fecha de nacimiento.
 * Ignora palabras de menos de 4 letras. Misma regla que PasswordSecurity.ContieneDatosPersonales.
 */
export function contieneDatosPersonales(password: string, datos: DatosPersonales): boolean {
  const clave = normalizar(password);
  const local = (datos.email ?? '').split('@')[0];
  const fragmentos = [datos.nombres, datos.apellidos, local]
    .flatMap((t) => (t ?? '').split(/[^\p{L}\p{N}]+/u))
    .concat(local)
    .map(normalizar)
    .filter((t) => t.length >= 4);
  const fecha = /^(\d{4})-(\d{2})-(\d{2})$/.exec(datos.fechaNacimiento ?? '');
  if (fecha) {
    const [, a, m, d] = fecha;
    fragmentos.push(a, d + m + a, a + m + d, m + d + a, d + m + a.slice(2));
  }
  return fragmentos.some((f) => clave.includes(f));
}

export type EstadoCoincidencia = 'vacia' | 'coinciden' | 'incompleta' | 'distinta';

/**
 * Compara la confirmación con la contraseña mientras se escribe.
 * `posicion` es el primer carácter (desde 1) en que difieren.
 */
export function compararClaves(clave: string, confirmacion: string): { estado: EstadoCoincidencia; posicion: number; faltan: number } {
  if (!confirmacion) return { estado: 'vacia', posicion: 0, faltan: 0 };
  if (clave === confirmacion) return { estado: 'coinciden', posicion: 0, faltan: 0 };
  let i = 0;
  while (i < clave.length && i < confirmacion.length && clave[i] === confirmacion[i]) i++;
  return i === confirmacion.length
    ? { estado: 'incompleta', posicion: 0, faltan: clave.length - confirmacion.length }
    : { estado: 'distinta', posicion: i + 1, faltan: 0 };
}
