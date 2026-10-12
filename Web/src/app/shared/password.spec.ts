import { FormControl, FormGroup } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { compararClaves, contieneDatosPersonales, matchingPasswords, securePassword } from './password';

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
  it('indica en vivo si la confirmación va bien o dónde difiere', () => {
    expect(compararClaves('frase larga', '')).toEqual({ estado: 'vacia', posicion: 0, faltan: 0 });
    expect(compararClaves('frase larga', 'frase')).toEqual({ estado: 'incompleta', posicion: 0, faltan: 6 });
    expect(compararClaves('frase larga', 'frasE')).toEqual({ estado: 'distinta', posicion: 5, faltan: 0 });
    expect(compararClaves('frase larga', 'frase larga')).toEqual({ estado: 'coinciden', posicion: 0, faltan: 0 });
  });
  it('rechaza datos personales igual que el backend', () => {
    const datos = { nombres: 'David Ana', apellidos: 'Alvarado de la Cruz', email: 'yo.cuenta@example.ec', fechaNacimiento: '1990-05-15' };
    for (const clave of ['mi frase con david adentro', 'Mi frase de ÁLVARADO segura', 'frase segura y única 1990', 'frase segura 15/05/1990 aquí', 'frase con mi correo yo.cuenta']) {
      expect(contieneDatosPersonales(clave, datos)).toBe(true);
    }
    for (const clave of ['una frase segura y única', 'frase con de la y ana corta']) {
      expect(contieneDatosPersonales(clave, datos)).toBe(false);
    }
  });
});
