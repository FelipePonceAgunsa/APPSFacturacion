import { ApplicationConfig, inject, provideAppInitializer, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXhr } from '@angular/common/http';
import { provideToastr } from 'ngx-toastr';
import { DatePipe } from '@angular/common';
import { MAT_DATE_LOCALE } from '@angular/material/core';
import { MatPaginatorIntl } from '@angular/material/paginator';
import { provideLuxonDateAdapter } from '@angular/material-luxon-adapter';
import { authInterceptor, provideAuth } from 'angular-auth-oidc-client';
import { jwtInterceptor } from './services/jwt-interceptor.service';
import { dateInterceptor } from './helpers/date-interceptor';
import { oidcConfig } from './helpers/oidc.config';
import { AuthService } from './services/auth.service';
import { provideApi } from './api';
import { environment } from '../environments/environment';
import { getSpanishPaginatorIntl } from './shared/spanish-paginator-intl';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideAuth({ config: oidcConfig }),
    provideHttpClient(withXhr(), withInterceptors([authInterceptor(), jwtInterceptor, dateInterceptor])),
    // Procesa el retorno de Cognito (?code=...) y carga el usuario antes de la primera navegación
    provideAppInitializer(() => inject(AuthService).init()),
    provideToastr({ positionClass: 'toast-top-right' }),
    DatePipe,
    { provide: MAT_DATE_LOCALE, useValue: 'es' },
    { provide: MatPaginatorIntl, useValue: getSpanishPaginatorIntl() },
    provideLuxonDateAdapter(),
    provideApi(environment.apiUrl),
  ],
};
