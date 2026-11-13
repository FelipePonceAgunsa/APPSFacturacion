import { LogLevel, OpenIdConfiguration } from 'angular-auth-oidc-client';
import { environment } from '../../environments/environment';

/** true cuando el ambiente tiene el user pool y el app client de Cognito (PA-009). */
export const cognitoConfigurado = !!environment.cognito.authority && !!environment.cognito.clientId;

/**
 * Inicio de sesión con Cognito federado con Entra ID (ADR-016): Authorization Code + PKCE.
 * El frontend no maneja contraseñas. El access token se adjunta solo a las llamadas a la API
 * (secureRoutes) mediante authInterceptor().
 */
export const oidcConfig: OpenIdConfiguration = {
  authority: environment.cognito.authority,
  clientId: environment.cognito.clientId,
  redirectUrl: window.location.origin,
  postLogoutRedirectUri: window.location.origin,
  scope: environment.cognito.scope,
  responseType: 'code',
  silentRenew: true,
  useRefreshToken: true,
  renewTimeBeforeTokenExpiresInSeconds: 60,
  secureRoutes: [environment.apiUrl],
  logLevel: LogLevel.Warn,
};
