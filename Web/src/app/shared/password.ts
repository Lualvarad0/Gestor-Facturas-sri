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
