import { CategoriaGasto } from './models';

export interface CategoriaInfo {
  valor: CategoriaGasto;
  nombre: string;
  /** Token CSS del color categórico (orden fijo de la paleta validada). */
  color: string;
  deducible: boolean;
}

export const CATEGORIAS: readonly CategoriaInfo[] = [
  { valor: 'Vivienda', nombre: 'Vivienda', color: 'var(--cat-1)', deducible: true },
  { valor: 'Alimentacion', nombre: 'Alimentación', color: 'var(--cat-2)', deducible: true },
  { valor: 'Salud', nombre: 'Salud', color: 'var(--cat-3)', deducible: true },
  { valor: 'Educacion', nombre: 'Educación, arte y cultura', color: 'var(--cat-4)', deducible: true },
  { valor: 'Vestimenta', nombre: 'Vestimenta', color: 'var(--cat-5)', deducible: true },
  { valor: 'Turismo', nombre: 'Turismo', color: 'var(--cat-6)', deducible: true },
  { valor: 'NoDeducible', nombre: 'No deducible', color: 'var(--cat-7)', deducible: false },
  { valor: 'SinCategoria', nombre: 'Sin categoría', color: 'var(--cat-neutral)', deducible: false },
];

const POR_VALOR = new Map(CATEGORIAS.map((c) => [c.valor, c]));

export function categoria(valor: CategoriaGasto): CategoriaInfo {
  return POR_VALOR.get(valor) ?? CATEGORIAS[CATEGORIAS.length - 1];
}
