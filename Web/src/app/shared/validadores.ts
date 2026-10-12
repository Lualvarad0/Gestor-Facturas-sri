import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Letras (con tildes), espacios, apóstrofos, puntos y guiones (igual que DatosPersonales.PatronNombre). */
const PATRON_NOMBRE = /^\p{L}(?:[\p{L}\p{M}'’ .-]*[\p{L}\p{M}.])?$/u;

export function nombreValido(control: AbstractControl): ValidationErrors | null {
  const valor = String(control.value ?? '').trim();
  return valor.length >= 2 && valor.length <= 60 && PATRON_NOMBRE.test(valor) ? null : { nombre: true };
}

/** 'yyyy-MM-dd' de hoy menos `anios` años (fecha local). */
export function haceAnios(anios: number): string {
  const d = new Date();
  d.setFullYear(d.getFullYear() - anios);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** Fecha de nacimiento de una persona de 18 a 120 años. Vacío es válido (usa Validators.required si es obligatoria). */
export function mayorDeEdad(control: AbstractControl): ValidationErrors | null {
  const fecha = String(control.value ?? '');
  if (!fecha) return null;
  return /^\d{4}-\d{2}-\d{2}$/.test(fecha) && fecha <= haceAnios(18) && fecha >= haceAnios(120) ? null : { mayorDeEdad: true };
}
