import { Routes } from '@angular/router';
import { SessionComponent } from './pages/session/session.component';
import { EditSheetComponent } from './pages/edit-sheet/edit-sheet.component';

/**
 * Rotte del giocatore. La scheda vive su `/:playerGuid`: un indirizzo che è la
 * scheda stessa, condivisibile e ricaricabile: chi riapre `/<guid>` ritrova
 * il suo personaggio senza ripassare dal "Chi sei?". La radice resta la
 * scelta del personaggio. Sono URL veri (History API): un deep-link ricaricato
 * lo serve il fallback SPA del server (vedi ServerBootstrap).
 *
 * La console del Master (`/master`) è caricata lazy: i giocatori non scaricano
 * quel codice. `master` viene prima di `:playerGuid` (che ha un solo segmento),
 * altrimenti verrebbe scambiato per un guid; `edit/:id` ne ha due e non collide.
 */
export const routes: Routes = [
  { path: '', component: SessionComponent },
  { path: 'edit/:id', component: EditSheetComponent },
  { path: 'master', loadChildren: () => import('./pages/master/master.routes').then(m => m.MASTER_ROUTES) },
  { path: ':playerGuid', component: SessionComponent },
  { path: '**', redirectTo: '' },
];
