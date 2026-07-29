import { Routes } from '@angular/router';
import { MasterShellComponent } from './master-shell.component';
import { MasterSessioneComponent } from './sessione/master-sessione.component';
import { MasterSchedeComponent } from './schede/master-schede.component';
import { MasterBestiarioComponent } from './bestiario/master-bestiario.component';
import { MasterSistemaComponent } from './sistema/master-sistema.component';

/**
 * Le rotte della console del Master, caricate a parte (lazy) da app.routes:
 * i giocatori non scaricano mai questo codice, e il bundle iniziale della
 * scheda resta leggero. La shell è il genitore comune; le quattro pagine sono
 * le sue figlie.
 */
export const MASTER_ROUTES: Routes = [
  {
    path: '',
    component: MasterShellComponent,
    children: [
      { path: '', component: MasterSessioneComponent },
      { path: 'schede', component: MasterSchedeComponent },
      { path: 'bestiario', component: MasterBestiarioComponent },
      { path: 'sistema', component: MasterSistemaComponent },
    ],
  },
];
