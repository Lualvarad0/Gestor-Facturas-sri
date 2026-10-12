import { Routes } from '@angular/router';
import { authGuard, invitadoGuard } from './core/auth';

export const routes: Routes = [
  { path: 'recuperar', title: 'Recuperar cuenta · Nudo', loadComponent: () => import('./pages/auth/recuperacion').then(m => m.Recuperacion) },
  { path: 'restablecer', title: 'Restablecer contraseña · Nudo', data: { restablecer: true }, loadComponent: () => import('./pages/auth/recuperacion').then(m => m.Recuperacion) },
  {
    // /login y /registro son la misma ruta para que el panel de acceso se deslice sin recrear la página.
    matcher: (url) =>
      url.length === 1 && (url[0].path === 'login' || url[0].path === 'registro') ? { consumed: url, posParams: { modo: url[0] } } : null,
    title: (ruta) => (ruta.params['modo'] === 'registro' ? 'Crear cuenta · Nudo' : 'Iniciar sesión · Nudo'),
    canActivate: [invitadoGuard],
    loadComponent: () => import('./pages/auth/acceso').then((m) => m.Acceso),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'resumen' },
      { path: 'resumen', title: 'Panel de gastos · Nudo', loadComponent: () => import('./pages/resumen/resumen').then((m) => m.ResumenPagina) },
      { path: 'gastos', title: 'Gastos · Nudo', loadComponent: () => import('./pages/gastos/gastos').then((m) => m.Gastos) },
      { path: 'gastos/:id', redirectTo: 'comprobantes/:id' },
      { path: 'comprobantes/:id', title: 'Comprobante · Nudo', loadComponent: () => import('./pages/comprobante/comprobante').then((m) => m.ComprobantePagina) },
      { path: 'importar', title: 'Traer facturas · Nudo', loadComponent: () => import('./pages/importar/importar').then((m) => m.Importar) },
      { path: 'presupuesto', title: 'Presupuesto · Nudo', loadComponent: () => import('./pages/presupuesto/presupuesto').then((m) => m.PresupuestoPagina) },
      { path: 'proveedores', title: 'Proveedores · Nudo', loadComponent: () => import('./pages/proveedores/proveedores').then((m) => m.Proveedores) },
      { path: 'perfil', title: 'Editar perfil · Nudo', loadComponent: () => import('./pages/perfil/perfil').then((m) => m.EditarPerfil) },
      { path: 'configuracion', title: 'Configuración · Nudo', loadComponent: () => import('./pages/configuracion/configuracion').then((m) => m.Configuracion) },
    ],
  },
  { path: '**', redirectTo: '' },
];
