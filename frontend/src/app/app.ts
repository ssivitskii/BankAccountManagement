import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { AuthStore } from './core/auth.store';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  constructor() {
    effect(() => {
      if (!this.auth.isAuthenticated() && this.router.url.startsWith('/dashboard')) {
        void this.router.navigate(['/auth']);
      }
    });
  }
}
