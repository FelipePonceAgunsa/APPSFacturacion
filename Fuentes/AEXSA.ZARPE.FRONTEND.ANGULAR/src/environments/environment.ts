/**
 * Ambiente que hornea `ng build` (imagen Docker). Fijar los valores reales antes de construir.
 * Ningún valor es secreto: el app client de Cognito es público (Authorization Code + PKCE).
 */
export const environment = {
  apiUrl: 'http://localhost:5228',
  /** Modo abierto de desarrollo. Siempre false fuera de environment.development.ts. */
  modoAbierto: false,
  cognito: {
    /** https://cognito-idp.<region>.amazonaws.com/<userPoolId>  [POR CONFIRMAR PA-009] */
    authority: '',
    /** App client del Satélite en el user pool de la empresa  [POR CONFIRMAR PA-009] */
    clientId: '',
    /** Dominio del Hosted UI, para cerrar sesión: https://<dominio>.auth.<region>.amazoncognito.com */
    domain: '',
    scope: 'openid email profile',
  },
};
