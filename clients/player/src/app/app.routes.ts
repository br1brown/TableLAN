import { Routes } from '@angular/router';
import { SessionComponent } from './pages/session/session.component';
import { EditSheetComponent } from './pages/edit-sheet/edit-sheet.component';

export const routes: Routes = [
  { path: '', component: SessionComponent },
  { path: 'edit/:id', component: EditSheetComponent },
  { path: '**', redirectTo: '' }
];
