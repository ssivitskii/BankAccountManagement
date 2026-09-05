import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AuthStore } from './auth.store';
import { authInterceptor } from './auth.interceptor';
import { UserRole } from './models';
import { HttpClient } from '@angular/common/http';

describe('authInterceptor', () => {
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate: vi.fn().mockResolvedValue(true) } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('adds the bearer token and clears the session after a 401', () => {
    const auth = TestBed.inject(AuthStore);
    const router = TestBed.inject(Router);
    auth.save({
      accessToken: 'secret-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      userId: '756a36bb-2a84-49d9-a889-06a576c0322b',
      username: 'alice',
      role: UserRole.Customer,
    });

    TestBed.inject(HttpClient)
      .get('/api/accounts')
      .subscribe({ error: () => undefined });
    const request = TestBed.inject(HttpTestingController).expectOne('/api/accounts');
    expect(request.request.headers.get('Authorization')).toBe('Bearer secret-token');
    request.flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth']);
  });

  it('never sends the bearer token to an absolute cross-origin URL', () => {
    const auth = TestBed.inject(AuthStore);
    auth.save({
      accessToken: 'secret-token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      userId: '756a36bb-2a84-49d9-a889-06a576c0322b',
      username: 'alice',
      role: UserRole.Customer,
    });

    TestBed.inject(HttpClient).get('https://example.test/api/collect').subscribe();
    const request = TestBed.inject(HttpTestingController).expectOne(
      'https://example.test/api/collect',
    );
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ ok: true });
    auth.logout();
  });
});
