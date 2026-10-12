import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { matchingPasswords, securePassword } from './password';

describe('Contraseñas de cuenta', () => {
  it('permite frases largas con espacios y letras acentuadas', () => {
    expect(securePassword(new FormControl('Una frase única para mi cuenta'))).toBeNull();
    expect(securePassword(new FormControl('x'.repeat(128)))).toBeNull();
  });
  it('rechaza claves cortas, comunes, vacías o demasiado largas', () => {
    for (const value of ['corta123', '123456789012345', ' '.repeat(15), 'x'.repeat(129)]) {
      expect(securePassword(new FormControl(value))).not.toBeNull();
    }
  });
  it('comprueba que la confirmación coincida exactamente', () => {
    const form = new FormGroup({ password: new FormControl('Una frase única de prueba'), confirmacion: new FormControl('Una frase única de prueba') });
    expect(matchingPasswords(form)).toBeNull();
    form.controls.confirmacion.setValue('Una frase única de prueba ');
    expect(matchingPasswords(form)).toEqual({ passwordMismatch: true });
  });
});
