import { HttpErrorResponse } from '@angular/common/http';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Observable, of, Subject, throwError } from 'rxjs';
import { AuthStore } from '../../core/auth.store';
import { BankingApiService } from '../../core/banking-api.service';
import {
  Account,
  AccountPage,
  MAX_MONEY_AMOUNT,
  Operation,
  OperationPage,
  OperationType,
  TransferResponse,
  UserRole,
} from '../../core/models';
import { DashboardComponent } from './dashboard.component';

const sourceId = '11111111-1111-1111-1111-111111111111';
const secondId = '22222222-2222-2222-2222-222222222222';
const externalId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const ownerId = '99999999-9999-9999-9999-999999999999';

describe('DashboardComponent', () => {
  beforeEach(() => {
    Object.defineProperty(globalThis, 'localStorage', {
      configurable: true,
      value: new MemoryStorage(),
    });
    sessionStorage.clear();
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    sessionStorage.clear();
    localStorage.clear();
  });

  it('loads accounts, computes the portfolio total, and selects the first account', () => {
    const accounts: Account[] = [
      { id: sourceId, ownerId: 'owner', number: 'ACCOUNT-A', balance: 25 },
      { id: secondId, ownerId: 'owner', number: 'ACCOUNT-B', balance: 75 },
    ];
    const api = {
      listAccounts: vi.fn(() => of({ items: accounts, nextCursor: null })),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    const fixture = createFixture(api);

    expect(fixture.componentInstance.loadedBalance()).toBe('$100.00');
    expect(fixture.componentInstance.selectedAccountId()).toBe(sourceId);
    expect(api.getOperations).toHaveBeenCalledWith(sourceId, null);
  });

  it('formats a loaded aggregate exactly after its cent total exceeds Number safe integer', () => {
    const accounts: Account[] = Array.from({ length: 12 }, (_, index) => ({
      id: `account-${index}`,
      ownerId,
      number: `ACCOUNT-${index}`,
      balance: index === 11 ? MAX_MONEY_AMOUNT - 0.01 : MAX_MONEY_AMOUNT,
    }));
    const api = {
      listAccounts: vi.fn(() => of({ items: accounts, nextCursor: null })),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    const fixture = createFixture(api);

    expect(fixture.componentInstance.loadedBalance()).toBe('$107,999,999,999,999.99');
    expect(fixture.nativeElement.querySelector('.total-card')?.textContent).toContain(
      'Total balance',
    );
  });

  it('ignores an out-of-order ledger response after the selected account changes', () => {
    const accounts: Account[] = [
      { id: sourceId, ownerId: 'owner', number: 'ACCOUNT-A', balance: 25 },
      { id: secondId, ownerId: 'owner', number: 'ACCOUNT-B', balance: 75 },
    ];
    const firstPage = new Subject<OperationPage>();
    const secondPage = new Subject<OperationPage>();
    const api = {
      listAccounts: vi.fn(() => of({ items: accounts, nextCursor: null })),
      getOperations: vi.fn((accountId: string) =>
        accountId === sourceId ? firstPage.asObservable() : secondPage.asObservable(),
      ),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    const staleOperation = operation('aaaaaaaa-0000-0000-0000-000000000001');
    const currentOperation = operation('bbbbbbbb-0000-0000-0000-000000000002');

    component.selectAccount(secondId);
    expect(component.operations()).toEqual([]);
    firstPage.next({ items: [staleOperation], nextCursor: 'stale' });
    expect(component.operations()).toEqual([]);

    secondPage.next({ items: [currentOperation], nextCursor: null });
    expect(component.operations()).toEqual([currentOperation]);
  });

  it('ignores an older account-list response after a newer refresh completes', () => {
    const oldList = new Subject<AccountPage>();
    const newList = new Subject<AccountPage>();
    let listCall = 0;
    const api = {
      listAccounts: vi.fn(() => {
        listCall += 1;
        return listCall === 1 ? oldList.asObservable() : newList.asObservable();
      }),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;

    component.loadAccounts(true, sourceId);
    newList.next({
      items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 125 }],
      nextCursor: null,
    });
    newList.complete();
    expect(component.accounts()[0]?.balance).toBe(125);
    expect(component.loadingAccounts()).toBe(false);

    oldList.next({
      items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 25 }],
      nextCursor: null,
    });
    oldList.complete();
    expect(component.accounts()[0]?.balance).toBe(125);
    expect(api.getOperations).toHaveBeenCalledTimes(1);
  });

  it('appends one account page, preserves selection, and guards duplicate load-more clicks', () => {
    const nextPage = new Subject<AccountPage>();
    let call = 0;
    const api = {
      listAccounts: vi.fn(() => {
        call += 1;
        return call === 1
          ? of({
              items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 25 }],
              nextCursor: 'next-page',
            })
          : nextPage.asObservable();
      }),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;

    component.loadMoreAccounts();
    component.loadMoreAccounts();
    expect(api.listAccounts).toHaveBeenCalledTimes(2);
    nextPage.next({
      items: [{ id: secondId, ownerId, number: 'ACCOUNT-B', balance: 75 }],
      nextCursor: null,
    });
    nextPage.complete();

    expect(component.accounts().map((account) => account.id)).toEqual([sourceId, secondId]);
    expect(component.selectedAccountId()).toBe(sourceId);
    expect(component.accountNextCursor()).toBeNull();
  });

  it('ignores a stale account page after a full refresh starts', () => {
    const stalePage = new Subject<AccountPage>();
    let call = 0;
    const api = {
      listAccounts: vi.fn(() => {
        call += 1;
        if (call === 1) {
          return of({
            items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 25 }],
            nextCursor: 'next-page',
          });
        }
        if (call === 2) return stalePage.asObservable();
        return of({
          items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 125 }],
          nextCursor: null,
        });
      }),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;

    component.loadMoreAccounts();
    component.loadAccounts();
    stalePage.next({
      items: [{ id: secondId, ownerId, number: 'ACCOUNT-B', balance: 75 }],
      nextCursor: null,
    });

    expect(component.accounts()).toEqual([
      { id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 125 },
    ]);
  });

  it('keeps a newly created account visible and selected beyond the loaded first page', () => {
    const initialAccounts: Account[] = Array.from({ length: 20 }, (_, index) => ({
      id: `loaded-account-${index}`,
      ownerId,
      number: `ACCOUNT-${String(index).padStart(2, '0')}`,
      balance: index + 1,
    }));
    const createdAccount: Account = {
      id: externalId,
      ownerId,
      number: 'ACCOUNT-99',
      balance: 25,
    };
    const created = new Subject<Account>();
    const api = {
      listAccounts: vi.fn(() => of({ items: initialAccounts, nextCursor: 'page-two' })),
      getOperations: vi.fn((accountId: string) =>
        of({
          items: accountId === createdAccount.id ? [] : [operation('initial-operation')],
          nextCursor: null,
        }),
      ),
      createAccount: vi.fn(() => created.asObservable()),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    component.createForm.setValue({
      number: createdAccount.number,
      initialBalance: createdAccount.balance,
      ownerId: '',
    });

    component.createAccount();
    created.next(createdAccount);
    created.complete();
    fixture.detectChanges();

    expect(api.listAccounts).toHaveBeenCalledTimes(1);
    expect(component.accounts()).toHaveLength(21);
    expect(component.accounts()).toContainEqual(createdAccount);
    expect(component.selectedAccountId()).toBe(createdAccount.id);
    expect(component.accountNextCursor()).toBe('page-two');
    expect(component.operations()).toEqual([]);
    expect(api.getOperations).toHaveBeenCalledWith(createdAccount.id, null);
    expect(fixture.nativeElement.querySelector('.total-card')?.textContent).toContain(
      'Loaded balance',
    );
    expect(fixture.nativeElement.querySelector('.total-card')?.textContent).toContain(
      'Across 21 loaded accounts',
    );
    expect(fixture.nativeElement.textContent).toContain(createdAccount.number);
  });

  it.each([
    { action: 'deposit' as const, balance: 110 },
    { action: 'withdraw' as const, balance: 90 },
  ])(
    'refreshes an outside-first-page account after $action without replacing loaded pages',
    ({ action, balance }) => {
      const firstPage = accountBatch(20);
      const outsideAccount: Account = {
        id: sourceId,
        ownerId,
        number: 'ACCOUNT-99',
        balance: 100,
      };
      const api = {
        listAccounts: vi.fn((cursor?: string | null) =>
          cursor
            ? of({ items: [outsideAccount], nextCursor: 'page-three' })
            : of({ items: firstPage, nextCursor: 'page-two' }),
        ),
        getAccount: vi.fn(() => of({ ...outsideAccount, balance })),
        getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
        deposit: vi.fn(() => of(undefined)),
        withdraw: vi.fn(() => of(undefined)),
      };
      const fixture = createFixture(api);
      const component = fixture.componentInstance;
      component.loadMoreAccounts();
      component.selectAccount(outsideAccount.id);
      component.balanceForm.setValue({ action, amount: 10 });

      component.submitBalanceChange();

      expect(api[action]).toHaveBeenCalledWith(outsideAccount.id, 10);
      expect(api.listAccounts).toHaveBeenCalledTimes(2);
      expect(api.getAccount).toHaveBeenCalledWith(outsideAccount.id);
      expect(component.accounts()).toHaveLength(21);
      expect(
        component.accounts().find((account) => account.id === outsideAccount.id)?.balance,
      ).toBe(balance);
      expect(component.accountNextCursor()).toBe('page-three');
      expect(component.selectedAccountId()).toBe(outsideAccount.id);
      expect(api.getOperations).toHaveBeenLastCalledWith(outsideAccount.id, null);
    },
  );

  it('ignores an ABA-stale targeted refresh after a full refresh and newer mutation', () => {
    const initial: Account = {
      id: sourceId,
      ownerId,
      number: 'ACCOUNT-A',
      balance: 100,
    };
    const firstTargetedRefresh = new Subject<Account>();
    const secondTargetedRefresh = new Subject<Account>();
    let listCall = 0;
    let accountCall = 0;
    const api = {
      listAccounts: vi.fn(() => {
        listCall += 1;
        return of({
          items: [{ ...initial, balance: listCall === 1 ? 100 : 105 }],
          nextCursor: null,
        });
      }),
      getAccount: vi.fn(() => {
        accountCall += 1;
        return accountCall === 1
          ? firstTargetedRefresh.asObservable()
          : secondTargetedRefresh.asObservable();
      }),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      deposit: vi.fn(() => of(undefined)),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    component.balanceForm.setValue({ action: 'deposit', amount: 10 });
    component.submitBalanceChange();

    component.loadAccounts(true, sourceId);
    component.balanceForm.setValue({ action: 'deposit', amount: 20 });
    component.submitBalanceChange();
    secondTargetedRefresh.next({ ...initial, balance: 130 });
    secondTargetedRefresh.complete();
    firstTargetedRefresh.next({ ...initial, balance: 110 });
    firstTargetedRefresh.error(new HttpErrorResponse({ status: 0 }));

    expect(api.getAccount).toHaveBeenCalledTimes(2);
    expect(component.accounts()[0]?.balance).toBe(130);
    expect(component.error()).toBeNull();
  });

  it('refreshes loaded transfer accounts without replacing pages or changing selection', () => {
    const destination: Account = {
      id: externalId,
      ownerId,
      number: 'ACCOUNT-00',
      balance: 50,
    };
    const firstPage = [destination, ...accountBatch(19, 1)];
    const source: Account = {
      id: sourceId,
      ownerId,
      number: 'ACCOUNT-99',
      balance: 100,
    };
    const api = {
      listAccounts: vi.fn((cursor?: string | null) =>
        cursor
          ? of({ items: [source], nextCursor: 'page-three' })
          : of({ items: firstPage, nextCursor: 'page-two' }),
      ),
      getAccount: vi.fn((accountId: string) =>
        of(accountId === source.id ? { ...source, balance: 90 } : { ...destination, balance: 60 }),
      ),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      transfer: vi.fn(() => of(transferResponse(10))),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    component.loadMoreAccounts();
    component.selectAccount(source.id);
    component.transferForm.setValue({
      fromAccountId: source.id,
      toAccountId: destination.id,
      amount: 10,
    });

    component.submitTransfer();

    expect(api.listAccounts).toHaveBeenCalledTimes(2);
    expect(api.getAccount).toHaveBeenCalledTimes(2);
    expect(api.getAccount).toHaveBeenCalledWith(source.id);
    expect(api.getAccount).toHaveBeenCalledWith(destination.id);
    expect(component.accounts()).toHaveLength(21);
    expect(component.accounts().find((account) => account.id === source.id)?.balance).toBe(90);
    expect(component.accounts().find((account) => account.id === destination.id)?.balance).toBe(60);
    expect(component.accountNextCursor()).toBe('page-three');
    expect(component.selectedAccountId()).toBe(source.id);
    expect(api.getOperations).toHaveBeenLastCalledWith(source.id, null);
    expect(component.success()).toBe('Transfer completed successfully.');
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).toBeNull();
  });

  it('keeps confirmed transfer success when an account refresh fails', () => {
    const source: Account = {
      id: sourceId,
      ownerId,
      number: 'ACCOUNT-A',
      balance: 100,
    };
    const api = {
      listAccounts: vi.fn(() => of({ items: [source], nextCursor: null })),
      getAccount: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 0 }))),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      transfer: vi.fn(() => of(transferResponse(10))),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    component.transferForm.setValue({
      fromAccountId: source.id,
      toAccountId: externalId,
      amount: 10,
    });

    component.submitTransfer();

    expect(component.success()).toBe('Transfer completed successfully.');
    expect(component.error()).toContain('transfer was confirmed');
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).toBeNull();
  });

  it('persists one unresolved transfer without allowing another payload to overwrite it', () => {
    const accounts: Account[] = [
      { id: sourceId, ownerId: 'owner', number: 'ACCOUNT-A', balance: 100 },
    ];
    const keys: string[] = [];
    let call = 0;
    const api = {
      listAccounts: vi.fn(() => of({ items: accounts, nextCursor: null })),
      getAccount: vi.fn(() => of(accounts[0])),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      transfer: vi.fn(
        (
          _fromAccountId: string,
          _toAccountId: string,
          _amount: number,
          idempotencyKey: string,
        ): Observable<TransferResponse> => {
          keys.push(idempotencyKey);
          call += 1;
          if (call === 1 || call === 2) {
            return throwError(() => new HttpErrorResponse({ status: 0 }));
          }
          if (call === 4) {
            return throwError(
              () =>
                new HttpErrorResponse({
                  status: 400,
                  error: { detail: 'Transfer was rejected.' },
                }),
            );
          }
          return of(transferResponse(_amount));
        },
      ),
    };
    vi.spyOn(crypto, 'randomUUID')
      .mockReturnValueOnce('10000000-0000-4000-8000-000000000001')
      .mockReturnValueOnce('10000000-0000-4000-8000-000000000002')
      .mockReturnValueOnce('10000000-0000-4000-8000-000000000003');
    const fixture = createFixture(api);
    let component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('#transfer-to')).toBeInstanceOf(HTMLInputElement);

    component.transferForm.setValue({
      fromAccountId: ` ${sourceId.toUpperCase()} `,
      toAccountId: ` ${externalId.toUpperCase()} `,
      amount: 10,
    });
    component.submitTransfer();
    expect(component.error()).toContain('exact same transfer');
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).not.toBeNull();
    component.logout();
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).not.toBeNull();

    fixture.destroy();
    const restoredFixture = TestBed.createComponent(DashboardComponent);
    restoredFixture.detectChanges();
    component = restoredFixture.componentInstance;
    expect(component.transferForm.getRawValue()).toEqual({
      fromAccountId: sourceId,
      toAccountId: externalId,
      amount: 10,
    });
    component.transferForm.controls.toAccountId.setValue(externalId.toUpperCase());
    component.submitTransfer();
    expect(keys[1]).toBe(keys[0]);

    component.transferForm.controls.amount.setValue(11);
    component.submitTransfer();
    expect(keys).toHaveLength(2);
    expect(component.transferForm.getRawValue()).toEqual({
      fromAccountId: sourceId,
      toAccountId: externalId,
      amount: 10,
    });
    expect(component.error()).toContain('Resolve the restored unconfirmed transfer');

    component.submitTransfer();
    expect(keys[2]).toBe(keys[0]);
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).toBeNull();

    component.transferForm.controls.amount.setValue(10);
    component.submitTransfer();
    expect(keys[3]).not.toBe(keys[2]);
    expect(localStorage.getItem(`banking.pending-transfer.${ownerId}`)).toBeNull();

    component.submitTransfer();
    expect(keys[4]).not.toBe(keys[3]);
  });

  it('blocks transfer submission when recovery metadata cannot be persisted', () => {
    const api = {
      listAccounts: vi.fn(() =>
        of({
          items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 100 }],
          nextCursor: null,
        }),
      ),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      transfer: vi.fn(() => of(transferResponse(10))),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    vi.spyOn(localStorage, 'setItem').mockImplementation(() => {
      throw new DOMException('Storage blocked');
    });
    component.transferForm.setValue({
      fromAccountId: sourceId,
      toAccountId: externalId,
      amount: 10,
    });

    component.submitTransfer();

    expect(api.transfer).not.toHaveBeenCalled();
    expect(component.error()).toContain('recovery metadata could not be saved');
  });

  it('ignores and removes malformed or cross-user recovery metadata', () => {
    const storageKey = `banking.pending-transfer.${ownerId}`;
    const api = {
      listAccounts: vi.fn(() =>
        of({
          items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 100 }],
          nextCursor: null,
        }),
      ),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
    };
    localStorage.setItem(storageKey, '{invalid');
    const malformedFixture = createFixture(api);
    expect(malformedFixture.componentInstance.error()).toBeNull();
    expect(localStorage.getItem(storageKey)).toBeNull();
    malformedFixture.destroy();

    localStorage.setItem(
      storageKey,
      JSON.stringify({
        userId: externalId,
        fromAccountId: sourceId,
        toAccountId: externalId,
        amount: 10,
        fingerprint: JSON.stringify([sourceId, externalId, '10.00']),
        idempotencyKey: 'safe-key',
      }),
    );
    const crossUserFixture = TestBed.createComponent(DashboardComponent);
    crossUserFixture.detectChanges();
    expect(crossUserFixture.componentInstance.error()).toBeNull();
    expect(localStorage.getItem(storageKey)).toBeNull();
  });

  it('rejects amounts above the exact-cent browser boundary without calling the API', () => {
    const api = {
      listAccounts: vi.fn(() =>
        of({
          items: [{ id: sourceId, ownerId, number: 'ACCOUNT-A', balance: 100 }],
          nextCursor: null,
        }),
      ),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      deposit: vi.fn(() => of(undefined)),
      transfer: vi.fn(() => of(transferResponse(10))),
    };
    const fixture = createFixture(api);
    const component = fixture.componentInstance;
    component.balanceForm.setValue({ action: 'deposit', amount: MAX_MONEY_AMOUNT });
    component.transferForm.setValue({
      fromAccountId: sourceId,
      toAccountId: externalId,
      amount: MAX_MONEY_AMOUNT,
    });
    expect(component.balanceForm.valid).toBe(true);
    expect(component.transferForm.valid).toBe(true);

    component.balanceForm.setValue({ action: 'deposit', amount: MAX_MONEY_AMOUNT + 0.01 });
    component.submitBalanceChange();
    component.transferForm.setValue({
      fromAccountId: sourceId,
      toAccountId: externalId,
      amount: MAX_MONEY_AMOUNT + 0.01,
    });
    component.submitTransfer();

    expect(api.deposit).not.toHaveBeenCalled();
    expect(api.transfer).not.toHaveBeenCalled();
  });

  it('lets an administrator provision a user and prepares their account owner ID', () => {
    const provision = new Subject<{
      id: string;
      username: string;
      role: UserRole;
    }>();
    const api = {
      listAccounts: vi.fn(() => of({ items: [], nextCursor: null })),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      provisionUser: vi.fn(() => provision.asObservable()),
    };
    const fixture = createFixture(api, true);
    const component = fixture.componentInstance;
    expect(fixture.nativeElement.querySelector('#provision-username')).toBeInstanceOf(
      HTMLInputElement,
    );
    component.provisionForm.setValue({
      username: ' managed-customer ',
      password: 'temporary-password',
      role: UserRole.Customer,
    });

    component.provisionUser();
    expect(component.provisioning()).toBe(true);
    expect(api.provisionUser).toHaveBeenCalledWith(
      'managed-customer',
      'temporary-password',
      UserRole.Customer,
    );
    provision.next({ id: ownerId, username: 'managed-customer', role: UserRole.Customer });
    provision.complete();

    expect(component.provisioning()).toBe(false);
    expect(component.createForm.controls.ownerId.value).toBe(ownerId);
    expect(component.provisionSuccess()).toContain('managed-customer was provisioned');
  });

  it('does not render or invoke user provisioning for a customer', () => {
    const api = {
      listAccounts: vi.fn(() => of({ items: [], nextCursor: null })),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      provisionUser: vi.fn(() => of({ id: ownerId, username: 'blocked', role: UserRole.Customer })),
    };
    const fixture = createFixture(api);

    expect(fixture.nativeElement.querySelector('#provision-username')).toBeNull();
    fixture.componentInstance.provisionForm.setValue({
      username: 'blocked',
      password: 'temporary-password',
      role: UserRole.Customer,
    });
    fixture.componentInstance.provisionUser();
    expect(api.provisionUser).not.toHaveBeenCalled();
  });

  it('shows an admin provisioning API error without changing the account owner', () => {
    const api = {
      listAccounts: vi.fn(() => of({ items: [], nextCursor: null })),
      getOperations: vi.fn(() => of({ items: [], nextCursor: null })),
      provisionUser: vi.fn(() =>
        throwError(
          () =>
            new HttpErrorResponse({
              status: 409,
              error: { detail: 'Username already exists.' },
            }),
        ),
      ),
    };
    const fixture = createFixture(api, true);
    const component = fixture.componentInstance;
    component.provisionForm.setValue({
      username: 'existing-user',
      password: 'temporary-password',
      role: UserRole.Customer,
    });

    component.provisionUser();

    expect(component.provisionError()).toBe('Username already exists.');
    expect(component.createForm.controls.ownerId.value).toBe('');
    expect(component.provisioning()).toBe(false);
  });
});

function createFixture(api: object, admin = false): ComponentFixture<DashboardComponent> {
  const auth = {
    session: signal({
      accessToken: 'token',
      expiresAt: '2099-01-01T00:00:00.000Z',
      userId: ownerId,
      username: 'alice',
      role: admin ? UserRole.Admin : UserRole.Customer,
    }).asReadonly(),
    isAdmin: signal(admin).asReadonly(),
    logout: vi.fn(),
  };
  TestBed.configureTestingModule({
    imports: [DashboardComponent],
    providers: [
      { provide: BankingApiService, useValue: api },
      { provide: AuthStore, useValue: auth },
      { provide: Router, useValue: { navigate: vi.fn() } },
    ],
  });
  const fixture = TestBed.createComponent(DashboardComponent);
  fixture.detectChanges();
  return fixture;
}

function operation(id: string): Operation {
  return {
    id,
    type: OperationType.Deposit,
    amount: 10,
    occurredAt: '2026-09-05T12:00:00.000Z',
    transferId: null,
  };
}

function accountBatch(count: number, offset = 0): Account[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `loaded-account-${offset + index}`,
    ownerId,
    number: `ACCOUNT-${String(offset + index).padStart(2, '0')}`,
    balance: offset + index + 1,
  }));
}

function transferResponse(amount: number): TransferResponse {
  return {
    id: 'cccccccc-0000-0000-0000-000000000003',
    fromAccountId: sourceId,
    toAccountId: externalId,
    initiatedByUserId: ownerId,
    amount,
    occurredAt: '2026-09-05T12:00:00.000Z',
    isReplay: false,
  };
}

class MemoryStorage implements Storage {
  private readonly values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  clear(): void {
    this.values.clear();
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  key(index: number): string | null {
    return [...this.values.keys()][index] ?? null;
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }
}
