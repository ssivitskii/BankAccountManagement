import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { extractApiError } from '../../core/api-error';
import { AuthStore } from '../../core/auth.store';
import { BankingApiService } from '../../core/banking-api.service';
import { Account, MAX_MONEY_AMOUNT, Operation, OperationType, UserRole } from '../../core/models';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

interface PendingTransferIntent {
  userId: string;
  fromAccountId: string;
  toAccountId: string;
  amount: number;
  fingerprint: string;
  idempotencyKey: string;
}

const PENDING_TRANSFER_KEY_PREFIX = 'banking.pending-transfer.';

function atMostTwoDecimals(control: AbstractControl): ValidationErrors | null {
  const value = String(control.value ?? '');
  return value && !/^\d+(\.\d{1,2})?$/.test(value) ? { precision: true } : null;
}

@Component({
  selector: 'app-dashboard',
  imports: [ReactiveFormsModule],
  templateUrl: './dashboard.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardComponent implements OnInit {
  private readonly api = inject(BankingApiService);
  private readonly auth = inject(AuthStore);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  private accountRequestGeneration = 0;
  private operationRequestGeneration = 0;
  private mutationRefreshGeneration = 0;
  private nextAccountRefreshGeneration = 0;
  private readonly accountRefreshGenerations = new Map<string, number>();
  private pendingTransferIntent: PendingTransferIntent | null = null;

  readonly session = this.auth.session;
  readonly isAdmin = this.auth.isAdmin;
  readonly accounts = signal<Account[]>([]);
  readonly accountNextCursor = signal<string | null>(null);
  readonly selectedAccountId = signal<string | null>(null);
  readonly operations = signal<Operation[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loadingAccounts = signal(true);
  readonly loadingMoreAccounts = signal(false);
  readonly loadingOperations = signal(false);
  readonly loadingMore = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly provisioning = signal(false);
  readonly provisionError = signal<string | null>(null);
  readonly provisionSuccess = signal<string | null>(null);
  readonly userRoles = UserRole;
  readonly maximumMoneyAmount = MAX_MONEY_AMOUNT;

  readonly selectedAccount = computed(
    () => this.accounts().find((account) => account.id === this.selectedAccountId()) ?? null,
  );
  readonly loadedBalance = computed(() => formatAccountTotal(this.accounts()));

  readonly createForm = this.formBuilder.nonNullable.group({
    number: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9-]{4,34}$/)]],
    initialBalance: [
      0,
      [Validators.required, Validators.min(0), Validators.max(MAX_MONEY_AMOUNT), atMostTwoDecimals],
    ],
    ownerId: [''],
  });
  readonly provisionForm = this.formBuilder.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(200)]],
    role: [UserRole.Customer],
  });
  readonly balanceForm = this.formBuilder.nonNullable.group({
    action: ['deposit' as 'deposit' | 'withdraw'],
    amount: [
      null as number | null,
      [
        Validators.required,
        Validators.min(0.01),
        Validators.max(MAX_MONEY_AMOUNT),
        atMostTwoDecimals,
      ],
    ],
  });
  readonly transferForm = this.formBuilder.nonNullable.group({
    fromAccountId: ['', Validators.required],
    toAccountId: ['', [Validators.required, Validators.pattern(GUID_PATTERN)]],
    amount: [
      null as number | null,
      [
        Validators.required,
        Validators.min(0.01),
        Validators.max(MAX_MONEY_AMOUNT),
        atMostTwoDecimals,
      ],
    ],
  });

  ngOnInit(): void {
    this.restorePendingTransferIntent();
    this.loadAccounts(true);
  }

  selectAccount(accountId: string): void {
    if (this.selectedAccountId() === accountId) return;
    this.selectedAccountId.set(accountId);
    if (!this.pendingTransferIntent) {
      this.transferForm.controls.fromAccountId.setValue(accountId);
    }
    if (
      !this.pendingTransferIntent &&
      canonicalAccountId(this.transferForm.controls.toAccountId.value) ===
        canonicalAccountId(accountId)
    ) {
      this.transferForm.controls.toAccountId.setValue('');
    }
    this.loadOperations(true);
  }

  loadAccounts(reloadHistory = false, preferredAccountId?: string): void {
    const requestGeneration = ++this.accountRequestGeneration;
    this.accountRefreshGenerations.clear();
    this.loadingAccounts.set(true);
    this.loadingMoreAccounts.set(false);
    this.accountNextCursor.set(null);
    this.api
      .listAccounts()
      .pipe(
        finalize(() => {
          if (requestGeneration === this.accountRequestGeneration) {
            this.loadingAccounts.set(false);
          }
        }),
      )
      .subscribe({
        next: (page) => {
          if (requestGeneration !== this.accountRequestGeneration) return;
          const accounts = page.items;
          this.accounts.set(accounts);
          this.accountNextCursor.set(page.nextCursor);
          const previous = this.selectedAccountId();
          const current = preferredAccountId ?? this.selectedAccountId();
          const selected = accounts.some((account) => account.id === current)
            ? current
            : (accounts[0]?.id ?? null);
          this.selectedAccountId.set(selected);
          this.transferForm.controls.fromAccountId.setValue(
            this.pendingTransferIntent?.fromAccountId ?? selected ?? '',
          );
          if (selected && reloadHistory) this.loadOperations(true);
          else if (!selected || selected !== previous) this.resetOperationState();
        },
        error: (error: unknown) => {
          if (requestGeneration === this.accountRequestGeneration) {
            this.error.set(extractApiError(error));
          }
        },
      });
  }

  loadMoreAccounts(): void {
    const cursor = this.accountNextCursor();
    if (!cursor || this.loadingMoreAccounts() || this.loadingAccounts()) return;
    const requestGeneration = this.accountRequestGeneration;
    this.loadingMoreAccounts.set(true);
    this.api
      .listAccounts(cursor)
      .pipe(
        finalize(() => {
          if (requestGeneration === this.accountRequestGeneration) {
            this.loadingMoreAccounts.set(false);
          }
        }),
      )
      .subscribe({
        next: (page) => {
          if (
            requestGeneration !== this.accountRequestGeneration ||
            cursor !== this.accountNextCursor()
          ) {
            return;
          }
          const existingIds = new Set(this.accounts().map((account) => account.id));
          this.accounts.update((accounts) => [
            ...accounts,
            ...page.items.filter((account) => !existingIds.has(account.id)),
          ]);
          this.accountNextCursor.set(page.nextCursor);
        },
        error: (error: unknown) => {
          if (requestGeneration === this.accountRequestGeneration) {
            this.error.set(extractApiError(error));
          }
        },
      });
  }

  loadOperations(reset: boolean): void {
    if (reset) this.resetOperationState();
    const accountId = this.selectedAccountId();
    if (!accountId) return;
    const cursor = reset ? null : this.nextCursor();
    if (!reset && (!cursor || this.loadingMore())) return;
    const requestGeneration = this.operationRequestGeneration;
    const loading = reset ? this.loadingOperations : this.loadingMore;
    loading.set(true);
    this.api
      .getOperations(accountId, cursor)
      .pipe(
        finalize(() => {
          if (requestGeneration === this.operationRequestGeneration) loading.set(false);
        }),
      )
      .subscribe({
        next: (page) => {
          if (
            requestGeneration !== this.operationRequestGeneration ||
            accountId !== this.selectedAccountId()
          ) {
            return;
          }
          this.operations.update((current) => (reset ? page.items : [...current, ...page.items]));
          this.nextCursor.set(page.nextCursor);
        },
        error: (error: unknown) => {
          if (requestGeneration === this.operationRequestGeneration) {
            this.error.set(extractApiError(error));
          }
        },
      });
  }

  createAccount(): void {
    const ownerId = this.createForm.controls.ownerId.value.trim();
    if (this.createForm.invalid || (this.isAdmin() && !GUID_PATTERN.test(ownerId))) {
      this.createForm.markAllAsTouched();
      if (this.isAdmin() && !GUID_PATTERN.test(ownerId)) {
        this.error.set('Administrators must provide a valid owner ID.');
      }
      return;
    }
    const value = this.createForm.getRawValue();
    this.runSubmission(
      this.api.createAccount(
        value.number.trim(),
        Number(value.initialBalance),
        this.isAdmin() ? ownerId : undefined,
      ),
      (account) => {
        this.createForm.reset({ number: '', initialBalance: 0, ownerId: '' });
        this.success.set(`Account ${account.number} was created.`);
        this.upsertCreatedAccount(account);
      },
    );
  }

  provisionUser(): void {
    if (!this.isAdmin() || this.provisioning()) return;
    if (this.provisionForm.invalid) {
      this.provisionForm.markAllAsTouched();
      return;
    }

    const { username, password, role } = this.provisionForm.getRawValue();
    this.provisioning.set(true);
    this.provisionError.set(null);
    this.provisionSuccess.set(null);
    this.api
      .provisionUser(username.trim(), password, role)
      .pipe(finalize(() => this.provisioning.set(false)))
      .subscribe({
        next: (user) => {
          this.provisionForm.reset({ username: '', password: '', role: UserRole.Customer });
          this.createForm.controls.ownerId.setValue(user.id);
          this.provisionSuccess.set(
            `${user.username} was provisioned. Their owner ID is ready below.`,
          );
        },
        error: (error: unknown) => this.provisionError.set(extractApiError(error)),
      });
  }

  submitBalanceChange(): void {
    const account = this.selectedAccount();
    if (!account || this.balanceForm.invalid) {
      this.balanceForm.markAllAsTouched();
      return;
    }
    const { action, amount } = this.balanceForm.getRawValue();
    const request =
      action === 'deposit'
        ? this.api.deposit(account.id, Number(amount))
        : this.api.withdraw(account.id, Number(amount));
    this.runSubmission(request, () => {
      this.balanceForm.reset({ action, amount: null });
      this.success.set(`${action === 'deposit' ? 'Deposit' : 'Withdrawal'} completed.`);
      this.refreshMutatedAccounts(
        [account.id],
        'The transaction completed, but the updated balance could not be refreshed. Use Refresh to reconcile it.',
      );
    });
  }

  submitTransfer(): void {
    const transfer = this.transferForm.getRawValue();
    const fromAccountId = canonicalAccountId(transfer.fromAccountId);
    const toAccountId = canonicalAccountId(transfer.toAccountId);
    const numericAmount = Number(transfer.amount);
    this.transferForm.setValue({ fromAccountId, toAccountId, amount: numericAmount });
    const fingerprint = transferFingerprint(fromAccountId, toAccountId, numericAmount);
    if (this.pendingTransferIntent && this.pendingTransferIntent.fingerprint !== fingerprint) {
      this.restorePendingTransferForm();
      this.error.set(
        'Resolve the restored unconfirmed transfer before starting a different one. Its original details and idempotency key have been retained.',
      );
      return;
    }
    if (this.transferForm.invalid) {
      this.transferForm.markAllAsTouched();
      return;
    }
    if (fromAccountId === toAccountId) {
      this.error.set('Choose two different accounts for the transfer.');
      return;
    }
    if (this.submitting()) return;
    const userId = canonicalAccountId(this.session()?.userId ?? '');
    if (!GUID_PATTERN.test(userId)) {
      this.error.set(
        'The authenticated user session is invalid. Sign in again before transferring.',
      );
      return;
    }
    const intent =
      this.pendingTransferIntent ??
      ({
        userId,
        fromAccountId,
        toAccountId,
        amount: numericAmount,
        fingerprint,
        idempotencyKey: crypto.randomUUID(),
      } satisfies PendingTransferIntent);
    this.pendingTransferIntent = intent;
    if (!this.persistPendingTransferIntent(intent)) {
      this.error.set(
        'The transfer was not sent because recovery metadata could not be saved securely in this browser. Enable local storage and try again.',
      );
      return;
    }
    this.mutationRefreshGeneration += 1;
    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    this.api
      .transfer(fromAccountId, toAccountId, numericAmount, intent.idempotencyKey)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (transfer) => {
          this.clearPendingTransferIntent();
          this.transferForm.controls.amount.reset(null);
          this.success.set(
            transfer.isReplay ? 'Transfer replay confirmed.' : 'Transfer completed successfully.',
          );
          const affectedAccountIds = [fromAccountId];
          if (
            toAccountId !== fromAccountId &&
            this.accounts().some((account) => account.id === toAccountId)
          ) {
            affectedAccountIds.push(toAccountId);
          }
          this.refreshMutatedAccounts(
            affectedAccountIds,
            'The transfer was confirmed, but one or more updated balances could not be refreshed. Use Refresh to reconcile them.',
          );
        },
        error: (error: unknown) => {
          if (this.isDefinitiveTransferRejection(error)) {
            this.clearPendingTransferIntent();
            this.error.set(extractApiError(error));
          } else {
            this.error.set(
              'We could not confirm whether the transfer completed. Submit the exact same transfer again to retry safely; its idempotency key will be reused.',
            );
          }
        },
      });
  }

  logout(): void {
    this.auth.logout();
    void this.router.navigate(['/auth']);
  }

  operationLabel(type: OperationType): string {
    return ['Deposit', 'Withdrawal', 'Transfer out', 'Transfer in'][type] ?? 'Operation';
  }

  operationSign(type: OperationType): string {
    return type === OperationType.Withdrawal || type === OperationType.TransferOut ? '−' : '+';
  }

  isDebit(type: OperationType): boolean {
    return type === OperationType.Withdrawal || type === OperationType.TransferOut;
  }

  formatMoney(value: number): string {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat('en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  private runSubmission<T>(
    request: import('rxjs').Observable<T>,
    onSuccess: (value: T) => void,
  ): void {
    if (this.submitting()) return;
    this.mutationRefreshGeneration += 1;
    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    request.pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: onSuccess,
      error: (error: unknown) => this.error.set(extractApiError(error)),
    });
  }

  private resetOperationState(): void {
    this.operationRequestGeneration += 1;
    this.operations.set([]);
    this.nextCursor.set(null);
    this.loadingOperations.set(false);
    this.loadingMore.set(false);
  }

  private upsertCreatedAccount(account: Account): void {
    this.invalidateAccountListRequests();
    this.upsertAccount(account);
    this.selectedAccountId.set(account.id);
    if (!this.pendingTransferIntent) {
      this.transferForm.controls.fromAccountId.setValue(account.id);
    }
    this.loadOperations(true);
  }

  private invalidateAccountListRequests(): void {
    this.accountRequestGeneration += 1;
    this.loadingAccounts.set(false);
    this.loadingMoreAccounts.set(false);
  }

  private upsertAccount(account: Account): void {
    this.accounts.update((accounts) => {
      const existingIndex = accounts.findIndex((item) => item.id === account.id);
      if (existingIndex < 0) return [...accounts, account];
      const updated = [...accounts];
      updated[existingIndex] = account;
      return updated;
    });
  }

  private refreshMutatedAccounts(accountIds: string[], warning: string): void {
    this.invalidateAccountListRequests();
    const mutationGeneration = this.mutationRefreshGeneration;
    const distinctAccountIds = [...new Set(accountIds)];
    const selectedAccountId = this.selectedAccountId();
    if (selectedAccountId && distinctAccountIds.includes(selectedAccountId)) {
      this.loadOperations(true);
    }
    for (const accountId of distinctAccountIds) {
      const generation = ++this.nextAccountRefreshGeneration;
      this.accountRefreshGenerations.set(accountId, generation);
      this.api.getAccount(accountId).subscribe({
        next: (account) => {
          if (this.accountRefreshGenerations.get(accountId) === generation) {
            this.upsertAccount(account);
          }
        },
        error: () => {
          if (
            this.mutationRefreshGeneration === mutationGeneration &&
            this.accountRefreshGenerations.get(accountId) === generation
          ) {
            this.error.set(warning);
          }
        },
      });
    }
  }

  private isDefinitiveTransferRejection(error: unknown): boolean {
    if (!(error instanceof HttpErrorResponse) || error.status < 400 || error.status >= 500) {
      return false;
    }
    if (error.status === 408) return false;
    const detail = (error.error as { detail?: unknown } | null)?.detail;
    return !(
      error.status === 409 &&
      typeof detail === 'string' &&
      detail.toLowerCase().includes('still being processed')
    );
  }

  private restorePendingTransferIntent(): void {
    const userId = canonicalAccountId(this.session()?.userId ?? '');
    if (!userId) return;
    const storageKey = `${PENDING_TRANSFER_KEY_PREFIX}${userId}`;
    try {
      const serialized = globalThis.localStorage?.getItem(storageKey);
      if (!serialized) return;
      const candidate = JSON.parse(serialized) as Partial<PendingTransferIntent>;
      if (!isValidPendingTransferIntent(candidate, userId)) {
        globalThis.localStorage?.removeItem(storageKey);
        return;
      }
      this.pendingTransferIntent = candidate as PendingTransferIntent;
      this.restorePendingTransferForm();
      this.error.set(
        'An unconfirmed transfer was restored. Submit these exact details again to retry safely with the original idempotency key.',
      );
    } catch {
      try {
        globalThis.localStorage?.removeItem(storageKey);
      } catch {
        // Storage can be unavailable; no unvalidated intent is restored.
      }
    }
  }

  private restorePendingTransferForm(): void {
    const intent = this.pendingTransferIntent;
    if (!intent) return;
    this.transferForm.setValue({
      fromAccountId: intent.fromAccountId,
      toAccountId: intent.toAccountId,
      amount: intent.amount,
    });
  }

  private persistPendingTransferIntent(intent: PendingTransferIntent): boolean {
    try {
      const storage = globalThis.localStorage;
      if (!storage) return false;
      const key = `${PENDING_TRANSFER_KEY_PREFIX}${intent.userId}`;
      const serialized = JSON.stringify(intent);
      storage.setItem(key, serialized);
      return storage.getItem(key) === serialized;
    } catch {
      return false;
    }
  }

  private clearPendingTransferIntent(): void {
    const intent = this.pendingTransferIntent;
    this.pendingTransferIntent = null;
    if (!intent) return;
    try {
      globalThis.localStorage?.removeItem(`${PENDING_TRANSFER_KEY_PREFIX}${intent.userId}`);
    } catch {
      // In-memory state is already cleared after a definitive outcome.
    }
  }
}

function canonicalAccountId(value: string): string {
  return value.trim().toLowerCase();
}

function formatAccountTotal(accounts: Account[]): string {
  const cents = accounts.reduce(
    (total, account) => total + BigInt(Math.round(account.balance * 100)),
    0n,
  );
  const dollars = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(cents / 100n);
  const fraction = (cents % 100n).toString().padStart(2, '0');
  return `$${dollars}.${fraction}`;
}

function transferFingerprint(fromAccountId: string, toAccountId: string, amount: number): string {
  return JSON.stringify([fromAccountId, toAccountId, amount.toFixed(2)]);
}

function isValidPendingTransferIntent(
  candidate: Partial<PendingTransferIntent>,
  expectedUserId: string,
): candidate is PendingTransferIntent {
  if (
    candidate.userId !== expectedUserId ||
    typeof candidate.fromAccountId !== 'string' ||
    typeof candidate.toAccountId !== 'string' ||
    typeof candidate.amount !== 'number' ||
    !Number.isFinite(candidate.amount) ||
    candidate.amount < 0.01 ||
    candidate.amount > MAX_MONEY_AMOUNT ||
    Math.abs(candidate.amount * 100 - Math.round(candidate.amount * 100)) > 1e-9 ||
    typeof candidate.fingerprint !== 'string' ||
    typeof candidate.idempotencyKey !== 'string' ||
    !/^[\x20-\x7e]{1,128}$/.test(candidate.idempotencyKey)
  ) {
    return false;
  }
  const fromAccountId = canonicalAccountId(candidate.fromAccountId);
  const toAccountId = canonicalAccountId(candidate.toAccountId);
  return (
    GUID_PATTERN.test(fromAccountId) &&
    GUID_PATTERN.test(toAccountId) &&
    candidate.fromAccountId === fromAccountId &&
    candidate.toAccountId === toAccountId &&
    candidate.fingerprint === transferFingerprint(fromAccountId, toAccountId, candidate.amount)
  );
}
