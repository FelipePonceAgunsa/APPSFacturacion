import { Access } from './access.constants';

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
  { label: 'Operaciones', icon: 'local_shipping', route: '/operaciones', access: [Access.Operaciones] },
  { label: 'Convenios', icon: 'handshake', route: '/convenios', access: [Access.Convenios] },
  { label: 'Catálogo SAP', icon: 'inventory_2', route: '/catalogo', access: [Access.CatalogoSap] },
  {
    label: 'Configuración', icon: 'settings', access: [Access.Configuracion],
    children: [
      { label: 'Terminales', icon: 'directions_boat', route: '/configuracion/terminales', access: [Access.Configuracion] },
      { label: 'Tipos de inspección', icon: 'fact_check', route: '/configuracion/tipos-inspeccion', access: [Access.Configuracion] },
    ],
  },
];
