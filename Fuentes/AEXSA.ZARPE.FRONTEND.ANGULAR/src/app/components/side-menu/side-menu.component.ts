import { Component, ElementRef, computed, inject, signal, viewChild, afterNextRender, ViewEncapsulation, ChangeDetectionStrategy } from '@angular/core';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { MatNavList, MatListItem } from '@angular/material/list';
import { MatIcon } from '@angular/material/icon';
import { MatIconButton } from '@angular/material/button';
import { MatToolbar } from '@angular/material/toolbar';
import { MatDialog } from '@angular/material/dialog';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { DialogConfirmLogoutComponent } from './dialog-confirm-logout/dialog-confirm-logout.component';
import { MenuItem, MENU_ITEMS } from '../../helpers/menu.constants';
import { AppVersionService } from '../../services/app-version.service';

@Component({
    selector: 'app-side-menu',
    templateUrl: './side-menu.component.html',
    styleUrl: './side-menu.component.scss',
    encapsulation: ViewEncapsulation.None,
    changeDetection: ChangeDetectionStrategy.Eager,
    imports: [
        MatSidenavContainer,
        MatSidenav,
        MatSidenavContent,
        MatNavList,
        MatListItem,
        MatIcon,
        MatIconButton,
        MatToolbar,
        RouterLink,
        RouterLinkActive,
        RouterOutlet,
    ]
})
export class SideMenuComponent {
  private readonly authService = inject(AuthService);
  private readonly breakpointObserver = inject(BreakpointObserver);
  private readonly dialog = inject(MatDialog);

  readonly appVersion = inject(AppVersionService).version;

  readonly userName = computed(() => {
    const usuario = this.authService.usuario();
    return usuario?.email || usuario?.userName || '';
  });

  readonly actorRoles = computed(() => this.authService.usuario()?.roles ?? []);

  readonly initials = computed(() =>
    this.userName()
      .split(/[\s.@_-]+/)
      .filter(Boolean)
      .slice(0, 2)
      .map(w => w[0].toUpperCase())
      .join('')
  );

  readonly sidenav = viewChild.required<MatSidenav>('sidenav');
  private readonly swipeArea = viewChild.required('swipeArea', { read: ElementRef });

  private touchStartX = 0;
  private touchStartY = 0;
  private readonly swipeThreshold = 50;

  /** Responsive: móvil usa mode over y empieza cerrado */
  isMobile = signal(false);

  /** Estado de submenús expandidos (por índice) */
  expandedMenus = signal<Set<number>>(new Set());

  readonly menuItems: MenuItem[] = MENU_ITEMS;

  constructor() {
    this.breakpointObserver.observe(['(max-width: 600px)']).subscribe(result => {
      this.isMobile.set(result.matches);
    });

    afterNextRender(() => this.initializeSwipeListeners());
  }

  isMenuVisible(item: MenuItem): boolean {
    // Un grupo sin hijos visibles se oculta completo
    if (item.children && !item.children.some(c => this.isMenuVisible(c))) {
      return false;
    }
    if (!item.access || item.access.length === 0) return true;
    const has = this.authService.hasAccess(item.access);
    if (item.excludeAccess && item.excludeAccess.length > 0) {
      return has && !this.authService.hasAccess(item.excludeAccess);
    }
    return has;
  }

  toggleSubmenu(index: number): void {
    const current = new Set(this.expandedMenus());
    if (current.has(index)) {
      current.delete(index);
    } else {
      current.add(index);
    }
    this.expandedMenus.set(current);
  }

  isExpanded(index: number): boolean {
    return this.expandedMenus().has(index);
  }

  confirmLogout(): void {
    const ref = this.dialog.open(DialogConfirmLogoutComponent, { width: '360px' });
    ref.afterClosed().subscribe(confirmed => {
      if (confirmed) {
        this.authService.cierreSesion();
      }
    });
  }

  private initializeSwipeListeners(): void {
    const el = this.swipeArea().nativeElement;

    el.addEventListener('touchstart', (e: TouchEvent) => {
      this.touchStartX = e.touches[0].clientX;
      this.touchStartY = e.touches[0].clientY;
    }, { passive: true });

    el.addEventListener('touchend', (e: TouchEvent) => {
      const deltaX = e.changedTouches[0].clientX - this.touchStartX;
      const deltaY = e.changedTouches[0].clientY - this.touchStartY;

      if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > this.swipeThreshold) {
        if (deltaX > 0 && !this.sidenav().opened) {
          this.sidenav().open();
        } else if (deltaX < 0 && this.sidenav().opened) {
          this.sidenav().close();
        }
      }
    }, { passive: true });
  }
}
