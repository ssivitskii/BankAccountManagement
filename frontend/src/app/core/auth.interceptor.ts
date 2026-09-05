import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  const token = auth.accessToken();
  const isApiRequest = isSameOriginApiUrl(request.url);
  const authorizedRequest =
    token && isApiRequest
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request;

  return next(authorizedRequest).pipe(
    catchError((error: unknown) => {
      if (isApiRequest && error instanceof HttpErrorResponse && error.status === 401) {
        auth.logout();
        void router.navigate(['/auth']);
      }
      return throwError(() => error);
    }),
  );
};

function isSameOriginApiUrl(url: string): boolean {
  if (url === '/api' || url.startsWith('/api/')) return true;
  if (!globalThis.location?.origin) return false;
  try {
    const parsed = new URL(url, globalThis.location.origin);
    return (
      parsed.origin === globalThis.location.origin &&
      (parsed.pathname === '/api' || parsed.pathname.startsWith('/api/'))
    );
  } catch {
    return false;
  }
}
