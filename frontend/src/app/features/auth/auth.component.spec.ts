import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthStore } from '../../core/auth.store';
import { BankingApiService } from '../../core/banking-api.service';
import { AuthResponse, UserRole } from '../../core/models';
import { AuthComponent } from './auth.component';

const response: AuthResponse = {
  accessToken: 'token',
  expiresAt: '2099-01-01T00:00:00.000Z',
  userId: '99999999-9999-9999-9999-999999999999',
  username: 'alice',
  role: UserRole.Customer,
};

describe('AuthComponent', () => {
  it('saves a successful login and follows a safe local return URL', () => {
    const { component, api, auth, router } = createFixture('/dashboard?focus=accounts');
    component.form.setValue({ username: ' alice ', password: 'password-1' });

    component.submit();

    expect(api.login).toHaveBeenCalledWith('alice', 'password-1');
    expect(auth.save).toHaveBeenCalledWith(response);
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard?focus=accounts');
    expect(component.loading()).toBe(false);
  });

  it('rejects a protocol-relative return URL and navigates to the dashboard', () => {
    const { component, router } = createFixture('//example.test/collect');
    component.form.setValue({ username: 'alice', password: 'password-1' });

    component.submit();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');
  });

  it('marks invalid credentials fields without calling the API', () => {
    const { component, api } = createFixture(null);
    component.form.setValue({ username: 'ab', password: 'short' });

    component.submit();

    expect(api.login).not.toHaveBeenCalled();
    expect(api.register).not.toHaveBeenCalled();
    expect(component.form.controls.username.touched).toBe(true);
    expect(component.form.controls.password.touched).toBe(true);
  });

  it('uses registration mode and exposes a typed API failure', () => {
    const apiError = new HttpErrorResponse({
      status: 409,
      error: { detail: 'Username is already registered.' },
    });
    const { component, api, auth } = createFixture(null, apiError);
    component.setMode('register');
    component.form.setValue({ username: 'alice', password: 'password-1' });

    component.submit();

    expect(api.register).toHaveBeenCalledWith('alice', 'password-1');
    expect(auth.save).not.toHaveBeenCalled();
    expect(component.error()).toBe('Username is already registered.');
    expect(component.loading()).toBe(false);
  });
});

function createFixture(
  returnUrl: string | null,
  error?: HttpErrorResponse,
): {
  fixture: ComponentFixture<AuthComponent>;
  component: AuthComponent;
  api: { login: ReturnType<typeof vi.fn>; register: ReturnType<typeof vi.fn> };
  auth: { save: ReturnType<typeof vi.fn> };
  router: { navigateByUrl: ReturnType<typeof vi.fn> };
} {
  const result = error ? throwError(() => error) : of(response);
  const api = {
    login: vi.fn(() => result),
    register: vi.fn(() => result),
  };
  const auth = { save: vi.fn() };
  const router = { navigateByUrl: vi.fn().mockResolvedValue(true) };
  TestBed.configureTestingModule({
    imports: [AuthComponent],
    providers: [
      { provide: BankingApiService, useValue: api },
      { provide: AuthStore, useValue: auth },
      { provide: Router, useValue: router },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { queryParamMap: convertToParamMap({ returnUrl }) } },
      },
    ],
  });
  const fixture = TestBed.createComponent(AuthComponent);
  fixture.detectChanges();
  return { fixture, component: fixture.componentInstance, api, auth, router };
}
