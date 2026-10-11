import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Titulo } from '../../shared/marca';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import {
  LucideCircleAlert,
  LucideCircleCheck,
  LucideCircleDashed,
  LucideCircleMinus,
  LucideCircleX,
  LucideClipboardPaste,
  LucideFileUp,
  LucideLoader,
  LucideUpload,
  LucideX,
} from '@lucide/angular';
import { ComprobantesService } from '../../core/comprobantes.service';
import { mensajeDeError } from '../../core/estado';
import { ImportacionResponse, ResultadoImportacion } from '../../core/models';

type Modo = 'archivos' | 'claves';

const ESTADOS: Record<ResultadoImportacion, { texto: string; tono: 'exito' | 'neutro' | 'aviso' | 'error' }> = {
  Importado: { texto: 'Importado', tono: 'exito' },
  YaExistia: { texto: 'Ya estaba', tono: 'neutro' },
  NoSoportado: { texto: 'Omitido', tono: 'neutro' },
  NoEncontrado: { texto: 'No encontrado', tono: 'aviso' },
  NoAutorizado: { texto: 'No autorizado', tono: 'error' },
  Error: { texto: 'Error', tono: 'error' },
};

const MAX_BYTES = 10 * 1024 * 1024;

@Component({
  selector: 'app-importar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Titulo,
    RouterLink, LucideUpload, LucideFileUp, LucideClipboardPaste, LucideLoader, LucideX,
    LucideCircleCheck, LucideCircleMinus, LucideCircleDashed, LucideCircleX, LucideCircleAlert,
  ],
  templateUrl: './importar.html',
})
export class Importar {
  private readonly comprobantes = inject(ComprobantesService);

  protected readonly modo = signal<Modo>('archivos');
  protected readonly archivos = signal<File[]>([]);
  protected readonly texto = signal('');
  protected readonly arrastrando = signal(false);
  protected readonly importando = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly resultado = signal<ImportacionResponse | null>(null);

  protected readonly estados = ESTADOS;
  protected readonly pesoTotal = computed(() => this.archivos().reduce((s, a) => s + a.size, 0));
  protected readonly puedeImportar = computed(() =>
    !this.importando() && (this.modo() === 'archivos' ? this.archivos().length > 0 : this.texto().trim().length > 0),
  );
  protected readonly problemas = computed(() =>
    (this.resultado()?.claves ?? []).filter((c) => c.resultado !== 'Importado' && c.resultado !== 'YaExistia'),
  );

  protected agregar(lista: FileList | null): void {
    if (!lista) return;
    const validos = Array.from(lista).filter((a) => /\.(txt|xml|csv)$/i.test(a.name));
    if (validos.length < lista.length) this.error.set('Solo se aceptan archivos .txt o .xml (el reporte del SRI o los XML de tus facturas).');
    else this.error.set(null);

    const nombres = new Set(this.archivos().map((a) => a.name));
    this.archivos.update((actuales) => [...actuales, ...validos.filter((a) => !nombres.has(a.name))]);
    if (this.pesoTotal() > MAX_BYTES) this.error.set('Los archivos superan 10 MB en total. Súbelos en partes.');
  }

  protected quitar(archivo: File): void {
    this.archivos.update((lista) => lista.filter((a) => a !== archivo));
  }

  protected soltar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastrando.set(false);
    this.agregar(evento.dataTransfer?.files ?? null);
  }

  protected importar(): void {
    if (!this.puedeImportar() || this.pesoTotal() > MAX_BYTES) return;
    const peticion: Observable<ImportacionResponse> = this.modo() === 'archivos'
      ? this.comprobantes.importarArchivos(this.archivos())
      : this.comprobantes.importarTexto(this.texto());

    this.importando.set(true);
    this.error.set(null);
    this.resultado.set(null);
    peticion.subscribe({
      next: (r) => {
        this.importando.set(false);
        this.resultado.set(r);
        this.archivos.set([]);
        this.texto.set('');
      },
      error: (e) => {
        this.importando.set(false);
        this.error.set(mensajeDeError(e));
      },
    });
  }

  protected tamano(bytes: number): string {
    return bytes < 1024 ? `${bytes} B` : bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }
}
