import { computed, Injectable, signal } from '@angular/core';
import { AuthResponse, AuthSession, UserRole } from './models';

const SESSION_KEY = 'banking.auth.session';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private static readonly maximumTimerDelay = 2_147_483_647;
  private readonly state = signal<AuthSession | null>(this.readSession());
  private expiryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly session = this.state.asReadonly();
  readonly isAuthenticated = computed(() => this.hasValidSession(this.state()));
  readonly isAdmin = computed(() => this.state()?.role === UserRole.Admin);

  constructor() {
    this.scheduleExpiry();
  }

  save(response: AuthResponse): void {
    const session: AuthSession = { ...response };
    this.state.set(session);
    try {
      globalThis.sessionStorage?.setItem(SESSION_KEY, JSON.stringify(session));
    } catch {
      // Storage can be disabled; the in-memory session still works.
    }
    this.scheduleExpiry();
  }

  accessToken(): string | null {
    const session = this.state();
    return this.hasValidSession(session) ? session.accessToken : null;
  }

  logout(): void {
    this.clearExpiryTimer();
    this.state.set(null);
    try {
      globalThis.sessionStorage?.removeItem(SESSION_KEY);
    } catch {
      // Clearing in-memory state is sufficient when storage is unavailable.
    }
  }

  private readSession(): AuthSession | null {
    try {
      const serialized = globalThis.sessionStorage?.getItem(SESSION_KEY);
      if (!serialized) return null;
      const candidate = JSON.parse(serialized) as Partial<AuthSession>;
      if (
        typeof candidate.accessToken !== 'string' ||
        typeof candidate.expiresAt !== 'string' ||
        typeof candidate.userId !== 'string' ||
        typeof candidate.username !== 'string' ||
        (candidate.role !== UserRole.Customer && candidate.role !== UserRole.Admin)
      ) {
        globalThis.sessionStorage?.removeItem(SESSION_KEY);
        return null;
      }
      const session = candidate as AuthSession;
      if (!this.hasValidSession(session)) {
        globalThis.sessionStorage?.removeItem(SESSION_KEY);
        return null;
      }
      return session;
    } catch {
      return null;
    }
  }

  private hasValidSession(session: AuthSession | null): session is AuthSession {
    return session !== null && Date.parse(session.expiresAt) > Date.now();
  }

  private scheduleExpiry(): void {
    this.clearExpiryTimer();
    const session = this.state();
    if (!session) return;

    const remaining = Date.parse(session.expiresAt) - Date.now();
    if (!Number.isFinite(remaining) || remaining <= 0) {
      this.logout();
      return;
    }
    this.expiryTimer = globalThis.setTimeout(
      () => {
        if (this.hasValidSession(this.state())) this.scheduleExpiry();
        else this.logout();
      },
      Math.min(remaining, AuthStore.maximumTimerDelay),
    );
  }

  private clearExpiryTimer(): void {
    if (this.expiryTimer !== null) {
      globalThis.clearTimeout(this.expiryTimer);
      this.expiryTimer = null;
    }
  }
}
