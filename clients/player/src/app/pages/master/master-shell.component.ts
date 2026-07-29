import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TableService } from '../../table.service';
import { UpdateStatus } from '../../models';

/**
 * Cornice della console del Master, ora dentro la stessa app Angular dei
 * giocatori invece che quattro pagine HTML statiche. La navigazione, la
 * versione del build e lo stato della connessione — che la vecchia console
 * ricostruiva a mano in `console.js` — qui li danno il router e i signal di
 * TableService.
 *
 * L'UI è servita in modo generico: un telefono in LAN può caricarla, ma le
 * rotte `/api/admin/*` che la alimentano rispondono solo su loopback, quindi
 * da fuori resta una console vuota. Il muro è di rete, non di pagina.
 */
@Component({
  selector: 'app-master-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="navbar navbar-expand bg-body-tertiary border-bottom mb-3">
      <div class="container-fluid">
        <span class="navbar-brand d-flex align-items-center gap-2 mb-0">
          <i class="ra ra-dragon text-warning"></i> TableLAN
        </span>
        <ul class="navbar-nav me-auto">
          @for (p of pages; track p.path) {
            <li class="nav-item">
              <a class="nav-link d-flex align-items-center gap-1"
                 [routerLink]="p.path"
                 routerLinkActive="active"
                 [routerLinkActiveOptions]="{ exact: true }">
                <i class="ra" [class]="p.icon"></i> {{ p.label }}
              </a>
            </li>
          }
        </ul>
        <div class="d-flex align-items-center gap-3 small">
          @if (joinUrl()) {
            <span class="font-monospace text-warning d-none d-xl-inline">{{ joinUrl() }}</span>
          }
          <!-- C'è una versione nuova: un badge discreto che porta alla release.
               Compare solo se il controllo (best-effort) l'ha trovata; offline o
               già aggiornati, non c'è nulla. -->
          @if (update()?.updateAvailable) {
            <a class="badge rounded-pill text-bg-warning text-decoration-none"
               [href]="update()!.url" target="_blank" rel="noopener"
               [title]="'Sei alla ' + update()!.current + ', scarica la ' + update()!.latest">
              <i class="ra ra-upgrade"></i> Aggiorna a {{ update()!.latest }}
            </a>
          }
          @if (version()) {
            <span class="text-secondary d-none d-md-inline">{{ version() }}</span>
          }
          <span class="badge rounded-pill"
                [class.text-bg-success]="table.connected()"
                [class.text-bg-danger]="!table.connected()">
            {{ table.connected() ? 'Connesso' : 'Server non raggiungibile' }}
          </span>
        </div>
      </div>
    </nav>

    <div class="container-fluid">
      <router-outlet></router-outlet>
    </div>
  `,
})
export class MasterShellComponent implements OnInit {
  protected readonly table = inject(TableService);

  protected readonly joinUrl = signal<string | null>(null);
  protected readonly version = signal<string | null>(null);
  protected readonly update = signal<UpdateStatus | null>(null);

  // L'ordine racconta la giornata: prima si prepara, poi si gioca. Ma la
  // Sessione è prima, perché è quella che apri quando i giocatori sono già lì.
  protected readonly pages = [
    { path: '/master', label: 'Sessione', icon: 'ra-hourglass' },
    { path: '/master/schede', label: 'Schede', icon: 'ra-pawn' },
    { path: '/master/bestiario', label: 'Bestiario', icon: 'ra-monster-skull' },
    { path: '/master/sistema', label: 'Sistema', icon: 'ra-book' },
  ];

  async ngOnInit(): Promise<void> {
    // Non cambiano mentre l'app gira: si chiedono una volta.
    this.joinUrl.set(await this.table.joinUrl());
    this.version.set(await this.table.version());
    // A parte, e senza bloccare il resto: la rete può metterci qualche secondo
    // o non esserci affatto. Se non risponde, il badge semplicemente non compare.
    this.table.updateCheck().then(u => this.update.set(u));
  }
}
