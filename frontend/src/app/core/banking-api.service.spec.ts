import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { BankingApiService } from './banking-api.service';
import { UserRole } from './models';

describe('BankingApiService', () => {
  let api: BankingApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(BankingApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts login, registration, and admin provisioning contracts', () => {
    api.login('alice', 'password-1').subscribe();
    const login = http.expectOne('/api/auth/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({ username: 'alice', password: 'password-1' });
    login.flush({});

    api.register('new-user', 'password-2').subscribe();
    const register = http.expectOne('/api/auth/register');
    expect(register.request.method).toBe('POST');
    expect(register.request.body).toEqual({ username: 'new-user', password: 'password-2' });
    register.flush({});

    api.provisionUser('managed-user', 'password-3', UserRole.Customer).subscribe();
    const provision = http.expectOne('/api/admin/users');
    expect(provision.request.method).toBe('POST');
    expect(provision.request.body).toEqual({
      username: 'managed-user',
      password: 'password-3',
      role: UserRole.Customer,
    });
    provision.flush({});
  });

  it('uses account list, create, and bounded operation page contracts', () => {
    api.listAccounts().subscribe();
    const list = http.expectOne(
      (request) =>
        request.url === '/api/accounts' &&
        request.params.get('limit') === '20' &&
        !request.params.has('cursor'),
    );
    expect(list.request.method).toBe('GET');
    list.flush({ items: [], nextCursor: null });

    api.listAccounts('account-cursor', 100).subscribe();
    const nextAccounts = http.expectOne(
      (request) =>
        request.url === '/api/accounts' &&
        request.params.get('limit') === '100' &&
        request.params.get('cursor') === 'account-cursor',
    );
    nextAccounts.flush({ items: [], nextCursor: null });

    api.createAccount('PORTFOLIO-1', 125.5).subscribe();
    const customerCreate = http.expectOne('/api/accounts');
    expect(customerCreate.request.body).toEqual({ number: 'PORTFOLIO-1', initialBalance: 125.5 });
    customerCreate.flush({});

    api.createAccount('ADMIN-1', 0, 'owner-1').subscribe();
    const adminCreate = http.expectOne('/api/accounts');
    expect(adminCreate.request.body).toEqual({
      number: 'ADMIN-1',
      initialBalance: 0,
      ownerId: 'owner-1',
    });
    adminCreate.flush({});

    api.getAccount('account-1').subscribe();
    const account = http.expectOne('/api/accounts/account-1');
    expect(account.request.method).toBe('GET');
    account.flush({});

    api.getOperations('account-1').subscribe();
    const firstPage = http.expectOne(
      (request) =>
        request.url === '/api/accounts/account-1/operations' &&
        request.params.get('limit') === '20' &&
        !request.params.has('cursor'),
    );
    expect(firstPage.request.method).toBe('GET');
    firstPage.flush({ items: [], nextCursor: null });

    api.getOperations('account-1', 'opaque-cursor').subscribe();
    const nextPage = http.expectOne(
      (request) =>
        request.url === '/api/accounts/account-1/operations' &&
        request.params.get('limit') === '20' &&
        request.params.get('cursor') === 'opaque-cursor',
    );
    expect(nextPage.request.method).toBe('GET');
    nextPage.flush({ items: [], nextCursor: null });
  });

  it('posts balance mutations and sends transfer idempotency only in its header', () => {
    api.deposit('account-1', 10.25).subscribe();
    const deposit = http.expectOne('/api/accounts/account-1/deposit');
    expect(deposit.request.method).toBe('POST');
    expect(deposit.request.body).toEqual({ amount: 10.25 });
    deposit.flush(null);

    api.withdraw('account-1', 4.5).subscribe();
    const withdraw = http.expectOne('/api/accounts/account-1/withdraw');
    expect(withdraw.request.body).toEqual({ amount: 4.5 });
    withdraw.flush(null);

    api.transfer('account-1', 'account-2', 3, 'transfer-key').subscribe();
    const transfer = http.expectOne('/api/transfers');
    expect(transfer.request.method).toBe('POST');
    expect(transfer.request.body).toEqual({
      fromAccountId: 'account-1',
      toAccountId: 'account-2',
      amount: 3,
    });
    expect(transfer.request.headers.get('Idempotency-Key')).toBe('transfer-key');
    expect(transfer.request.body.idempotencyKey).toBeUndefined();
    transfer.flush({});
  });
});
