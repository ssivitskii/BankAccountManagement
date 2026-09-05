import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Account,
  AccountPage,
  AuthResponse,
  OperationPage,
  ProvisionedUser,
  TransferResponse,
  UserRole,
} from './models';

@Injectable({ providedIn: 'root' })
export class BankingApiService {
  private readonly http = inject(HttpClient);

  login(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', { username, password });
  }

  register(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', { username, password });
  }

  provisionUser(username: string, password: string, role: UserRole): Observable<ProvisionedUser> {
    return this.http.post<ProvisionedUser>('/api/admin/users', { username, password, role });
  }

  listAccounts(cursor?: string | null, limit = 20): Observable<AccountPage> {
    let params = new HttpParams().set('limit', limit);
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<AccountPage>('/api/accounts', { params });
  }

  createAccount(number: string, initialBalance: number, ownerId?: string): Observable<Account> {
    return this.http.post<Account>('/api/accounts', {
      number,
      initialBalance,
      ...(ownerId ? { ownerId } : {}),
    });
  }

  getAccount(accountId: string): Observable<Account> {
    return this.http.get<Account>(`/api/accounts/${accountId}`);
  }

  getOperations(accountId: string, cursor?: string | null): Observable<OperationPage> {
    let params = new HttpParams().set('limit', 20);
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<OperationPage>(`/api/accounts/${accountId}/operations`, { params });
  }

  deposit(accountId: string, amount: number): Observable<void> {
    return this.http.post<void>(`/api/accounts/${accountId}/deposit`, { amount });
  }

  withdraw(accountId: string, amount: number): Observable<void> {
    return this.http.post<void>(`/api/accounts/${accountId}/withdraw`, { amount });
  }

  transfer(
    fromAccountId: string,
    toAccountId: string,
    amount: number,
    idempotencyKey: string,
  ): Observable<TransferResponse> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<TransferResponse>(
      '/api/transfers',
      { fromAccountId, toAccountId, amount },
      { headers },
    );
  }
}
