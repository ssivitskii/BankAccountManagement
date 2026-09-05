import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/auth'], { queryParams: { returnUrl: state.url } });
};
