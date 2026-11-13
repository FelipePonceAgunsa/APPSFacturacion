/** Ambiente local (`ng serve`). Debe usar el mismo app client que el backend local (Auth:ClientId). */
export const environment = {
  apiUrl: 'http://localhost:5228',
  cognito: {
    authority: '',
    clientId: '',
    domain: '',
    scope: 'openid email profile',
  },
};
