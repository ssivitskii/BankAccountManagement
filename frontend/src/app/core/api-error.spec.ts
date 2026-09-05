import { HttpErrorResponse } from '@angular/common/http';
import { extractApiError } from './api-error';

describe('extractApiError', () => {
  it('flattens ASP.NET Core model-state errors', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Amount: ['Amount must be greater than zero.'] } },
    });

    expect(extractApiError(error)).toBe('Amount must be greater than zero.');
  });

  it('prefers the Problem Details detail', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { title: 'Conflict', detail: 'The account has insufficient funds.' },
    });

    expect(extractApiError(error)).toBe('The account has insufficient funds.');
  });
});
