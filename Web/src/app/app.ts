import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PreferenciasService } from './core/estado';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  template: '<router-outlet />',
})
export class App {
  // Instancia temprana: aplica el tema guardado.
  private readonly preferencias = inject(PreferenciasService);
}
