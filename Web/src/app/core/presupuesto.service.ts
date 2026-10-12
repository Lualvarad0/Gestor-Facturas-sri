import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Presupuesto, PresupuestoRequest } from './models';

@Injectable({ providedIn: 'root' })
export class PresupuestoService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}/presupuesto`;

  obtener(anio: number, mes: number): Observable<Presupuesto> {
    return this.http.get<Presupuesto>(this.url, { params: new HttpParams().set('anio', anio).set('mes', mes) });
  }

  guardar(request: PresupuestoRequest): Observable<void> {
    return this.http.put<void>(this.url, request);
  }

  asignarProveedor(ruc: string, rubroId: number | null): Observable<void> {
    return this.http.put<void>(`${this.url}/proveedores/${ruc}/rubro`, { rubroId });
  }
}
