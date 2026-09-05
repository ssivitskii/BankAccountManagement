import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { AuthStore } from './core/auth.store';
import { UserRole } from './core/models';

@Component({ template: '' })
class EmptyRouteComponent {}

describe('App', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([
          { path: 'dashboard', component: EmptyRouteComponent },
          { path: 'auth', component: EmptyRouteComponent },
        ]),
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('leaves the protected dashboard when the session is cleared', async () => {
    const auth = TestBed.inject(AuthStore);
    const router = TestBed.inject(Router);
    auth.save({
      accessToken: 'token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      userId: '756a36bb-2a84-49d9-a889-06a576c0322b',
      username: 'alice',
      role: UserRole.Customer,
    });
    await router.navigateByUrl('/dashboard');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    auth.logout();
    await fixture.whenStable();

    expect(router.url).toBe('/auth');
  });
});
