import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';

/**
 * URL veri (History API), non più hash: la scheda è `/pg-kael`, la console
 * `/master`. Più puliti da leggere e da condividere. Il prezzo è che un
 * reload su un deep-link chiede al server una rotta che non è un file: lo
 * gestisce il fallback SPA in ServerBootstrap, che restituisce l'index e
 * lascia decidere al router qui.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Zoneless: niente Zone.js. La change detection la guidano i signal, gli
    // eventi del template e i markForCheck — che è esattamente come l'app è già
    // fatta dopo il passaggio a OnPush. Meno lavoro a ogni tick, niente monkey
    // patch globale, bundle più leggero.
    provideZonelessChangeDetection(),
    provideRouter(routes)
  ]
};
