// Contratos de la API (Back/Dtos). Las fechas sin hora llegan como 'yyyy-MM-dd'.

export interface Usuario {
  id: number;
  nombre: string;
  email: string;
  rol: 'admin' | 'user';
}

export interface AuthResponse {
  token: string;
  expiraEn: string;
  usuario: Usuario;
}

export type RegimenTributario = 'General' | 'RimpeEmprendedor' | 'RimpeNegocioPopular';

export interface Perfil {
  nombre: string;
  nombres: string;
  /** Vacío en cuentas creadas antes de pedir apellidos. */
  apellidos: string;
  fechaNacimiento: string | null;
  email: string;
  cedula: string | null;
  ruc: string | null;
  razonSocial: string | null;
  regimen: RegimenTributario | null;
  cargasFamiliares: number;
  enfermedadCatastrofica: boolean;
  completo: boolean;
}

export interface PerfilRequest {
  nombres: string;
  apellidos: string;
  /** Null conserva la fecha guardada. */
  fechaNacimiento: string | null;
  cedula: string | null;
  ruc: string | null;
  razonSocial: string | null;
  regimen: RegimenTributario | null;
  cargasFamiliares: number;
  enfermedadCatastrofica: boolean;
}

export interface PerfilActualizado {
  perfil: Perfil;
  /** -1 cuando el RUC no cambió y no hubo reclasificación. */
  comprobantesEmitidos: number;
  comprobantesRecibidos: number;
}

/** Recibido = gasto (soy el comprador). Emitido = ingreso (soy el emisor). */
export type Direccion = 'Recibido' | 'Emitido';

export type CategoriaGasto =
  | 'SinCategoria'
  | 'Vivienda'
  | 'Educacion'
  | 'Salud'
  | 'Alimentacion'
  | 'Vestimenta'
  | 'Turismo'
  | 'NoDeducible';

export type ResultadoImportacion = 'Importado' | 'YaExistia' | 'NoAutorizado' | 'NoEncontrado' | 'NoSoportado' | 'Error';

export interface ClaveImportada {
  claveAcceso: string;
  resultado: ResultadoImportacion;
  detalle: string | null;
  direccion: Direccion | null;
}

export interface ImportacionResponse {
  clavesEncontradas: number;
  importados: number;
  yaExistian: number;
  conProblemas: number;
  ingresos: number;
  perfilCompleto: boolean;
  claves: ClaveImportada[];
}

export interface ComprobanteLista {
  id: number;
  claveAcceso: string;
  direccion: Direccion;
  tipoComprobante: string;
  numeroComprobante: string;
  fechaEmision: string;
  rucEmisor: string;
  razonSocialEmisor: string;
  nombreComercial: string | null;
  identificacionComprador: string;
  razonSocialComprador: string;
  subtotal: number;
  iva: number;
  total: number;
  categoria: CategoriaGasto;
  /** Solo gastos: si la factura está a tu nombre. Null si tu perfil no tiene cédula/RUC. */
  aNombreDelUsuario: boolean | null;
}

export interface ComprobanteDetalle {
  codigo: string | null;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  descuento: number;
  subtotal: number;
}

export interface Comprobante extends ComprobanteLista {
  numeroAutorizacion: string;
  fechaAutorizacion: string | null;
  baseIva0: number;
  baseIvaGravada: number;
  otrosImpuestos: number;
  descuento: number;
  propina: number;
  documentoModificado: string | null;
  detalles: ComprobanteDetalle[];
}

export interface Proveedor {
  ruc: string;
  razonSocial: string;
  categoria: CategoriaGasto | null;
  comprobantes: number;
  total: number;
}

export interface TotalCategoria {
  categoria: CategoriaGasto;
  comprobantes: number;
  total: number;
}

export interface TotalTercero {
  identificacion: string;
  razonSocial: string;
  comprobantes: number;
  total: number;
}

export interface TotalMes {
  mes: number;
  comprobantes: number;
  total: number;
  iva: number;
}

/** Gastos del período (total, iva, …), netos de notas de crédito. */
export interface Resumen {
  anio: number;
  mes: number | null;
  comprobantes: number;
  total: number;
  iva: number;
  totalDeducible: number;
  sinCategoria: number;
  aNombreDeOtros: number;
  porCategoria: TotalCategoria[];
  principalesProveedores: TotalTercero[];
  porMes: TotalMes[] | null;
}

export interface FiltroComprobantes {
  anio?: number;
  mes?: number;
  categoria?: CategoriaGasto | '';
  ruc?: string;
  q?: string;
}

export interface RegistroRequest {
  nombres: string;
  apellidos: string;
  /** 'yyyy-MM-dd' */
  fechaNacimiento: string;
  email: string;
  password: string;
}

export interface Rubro {
  id: number;
  nombre: string;
  limite: number;
  /** Gasto neto del mes de los proveedores asignados al rubro. */
  gastado: number;
  proveedores: number;
}

export interface AsignacionRubro {
  ruc: string;
  rubroId: number;
}

export interface Presupuesto {
  anio: number;
  mes: number;
  ingresoMensual: number | null;
  totalLimites: number;
  totalGastado: number;
  /** Gasto del mes de proveedores sin rubro. */
  gastoSinRubro: number;
  rubros: Rubro[];
  asignaciones: AsignacionRubro[];
}

export interface PresupuestoRequest {
  ingresoMensual: number | null;
  /** id null = rubro nuevo; los rubros que no se envían se eliminan. */
  rubros: { id: number | null; nombre: string; limite: number }[];
}
