export enum UserRole {
  Customer = 0,
  Admin = 1,
}

export enum OperationType {
  Deposit = 0,
  Withdrawal = 1,
  TransferOut = 2,
  TransferIn = 3,
}

// Exact cent values remain below Number.MAX_SAFE_INTEGER at this documented boundary.
export const MAX_MONEY_AMOUNT = 9_000_000_000_000;

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  userId: string;
  username: string;
  role: UserRole;
}

export type AuthSession = AuthResponse;

export interface ProvisionedUser {
  id: string;
  username: string;
  role: UserRole;
}

export interface Account {
  id: string;
  ownerId: string;
  number: string;
  balance: number;
}

export interface AccountPage {
  items: Account[];
  nextCursor: string | null;
}

export interface Operation {
  id: string;
  type: OperationType;
  amount: number;
  occurredAt: string;
  transferId: string | null;
}

export interface OperationPage {
  items: Operation[];
  nextCursor: string | null;
}

export interface TransferResponse {
  id: string;
  fromAccountId: string;
  toAccountId: string;
  initiatedByUserId: string;
  amount: number;
  occurredAt: string;
  isReplay: boolean;
}
