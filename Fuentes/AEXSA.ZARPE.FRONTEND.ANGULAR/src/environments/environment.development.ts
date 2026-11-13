/** Ambiente local (`ng serve`). Debe usar el mismo app client que el backend local (Auth:ClientId). */
export const environment = {
  apiUrl: 'http://localhost:5228',
  /**
   * Modo abierto de desarrollo: no pasa por Cognito y la API (Auth:ModoAbierto en Development) atiende cada
   * request como el usuario local Administrador. Solo para desarrollo, mientras no estén los valores de PA-009.
   */
  modoAbierto: true,
  cognito: {
    authority: '',
    clientId: '',
    domain: '',
    scope: 'openid email profile',
  },
};
