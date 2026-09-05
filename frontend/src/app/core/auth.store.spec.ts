import { AuthStore } from './auth.store';
import { AuthResponse, UserRole } from './models';

describe('AuthStore', () => {
  const session: AuthResponse = {
    accessToken: 'test-token',
    expiresAt: '2099-01-01T00:00:00.000Z',
    userId: '756a36bb-2a84-49d9-a889-06a576c0322b',
    username: 'alice',
    role: UserRole.Customer,
  };

  beforeEach(() => sessionStorage.clear());

  afterEach(() => {
    vi.useRealTimers();
    sessionStorage.clear();
  });

  it('persists a valid session and restores it for the browser tab', () => {
    const first = new AuthStore();
    first.save(session);

    const restored = new AuthStore();

    expect(restored.isAuthenticated()).toBe(true);
    expect(restored.accessToken()).toBe('test-token');
    expect(restored.session()?.username).toBe('alice');
    first.logout();
    restored.logout();
  });

  it('removes the persisted session on logout', () => {
    const store = new AuthStore();
    store.save(session);

    store.logout();

    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.length).toBe(0);
  });

  it('clears the session exactly when the JWT expires', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-09-05T12:00:00.000Z'));
    const store = new AuthStore();
    store.save({
      ...session,
      expiresAt: '2026-09-05T12:00:01.000Z',
    });

    vi.advanceTimersByTime(999);
    expect(store.isAuthenticated()).toBe(true);

    vi.advanceTimersByTime(1);
    expect(store.isAuthenticated()).toBe(false);
    expect(sessionStorage.length).toBe(0);
  });
});
