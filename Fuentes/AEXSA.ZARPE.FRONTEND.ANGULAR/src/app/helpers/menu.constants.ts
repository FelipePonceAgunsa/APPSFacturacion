export interface MenuItem {
  label: string;
  icon: string;
  route?: string;
  access: string[];
  excludeAccess?: string[];
  children?: MenuItem[];
}

/**
 * Definición del menú lateral. También determina el home de cada usuario:
 * tras el login se navega al primer ítem al que tenga acceso
 * (AuthService.getDefaultRoute), con /inicio como respaldo.
 *
 * Al agregar un módulo: { label: 'Convenios', icon: 'handshake', route: '/convenios', access: [Access.Convenios] }
 */
export const MENU_ITEMS: MenuItem[] = [
  { label: 'Inicio', icon: 'home', route: '/inicio', access: [] },
];
