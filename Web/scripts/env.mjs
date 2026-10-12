// Genera src/environments/*.ts (no versionados) antes de compilar o servir.
// - API_URL definida: escribe environment.ts con esa URL (Vercel).
// - Sin API_URL: conserva los archivos locales o crea los valores por defecto
//   ('/api' para la imagen Docker, que sirve frontend y API en el mismo dominio).
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';

const dir = new URL('../src/environments/', import.meta.url);
mkdirSync(dir, { recursive: true });

const escribir = (archivo, apiUrl) =>
  writeFileSync(new URL(archivo, dir), `export const environment = {\n  apiUrl: ${JSON.stringify(apiUrl)},\n};\n`);

const apiUrl = process.env.API_URL?.trim().replace(/\/+$/, '');
if (apiUrl) {
  if (!/^https:\/\/[^/]+\/api$/.test(apiUrl)) {
    console.error(`API_URL debe tener la forma https://<dominio>/api (recibido: ${apiUrl}).`);
    process.exit(1);
  }
  escribir('environment.ts', apiUrl);
} else if (process.env.VERCEL) {
  console.error('Falta la variable API_URL en Vercel (https://<servicio>.onrender.com/api).');
  process.exit(1);
} else if (!existsSync(new URL('environment.ts', dir))) {
  escribir('environment.ts', '/api');
}

if (!existsSync(new URL('environment.development.ts', dir)))
  escribir('environment.development.ts', 'http://localhost:5000/api');
