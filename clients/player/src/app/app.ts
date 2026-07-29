import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TableService } from './table.service';

/**
 * La radice apre il canale col tavolo, una volta, per tutte le rotte.
 *
 * Prima lo faceva solo la pagina della sessione: chi apriva /edit/:id di
 * persona — incollando l'URL, o semplicemente ricaricando mentre stava
 * modificando — non connetteva niente, e restava su "Caricamento..." per
 * sempre. Funzionava solo passando dalla scheda, cioè solo dal percorso che
 * qualcuno aveva provato.
 *
 * La connessione non è una proprietà di una pagina: è la ragione per cui
 * l'app è aperta. `connect()` è idempotente, quindi chiamarla qui non
 * disturba nessuno.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet></router-outlet>`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App implements OnInit {
  private table = inject(TableService);

  async ngOnInit(): Promise<void> {
    await this.table.connect();
  }
}
