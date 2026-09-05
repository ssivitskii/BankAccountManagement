import { HttpErrorResponse } from '@angular/common/http';

interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
  errors?: unknown;
}

export function extractApiError(error: unknown): string {
  const response = error instanceof HttpErrorResponse ? error : null;
  const payload = response?.error as ProblemDetails | string | null | undefined;

  if (response?.status === 0) {
    return 'The API is unavailable. Check that the .NET service is running on port 8080.';
  }
  if (typeof payload === 'string' && payload.trim()) return payload;
  if (payload && typeof payload === 'object') {
    const validationMessages = extractValidationMessages(payload.errors);
    if (validationMessages.length > 0) return validationMessages.join(' ');
    if (typeof payload.detail === 'string' && payload.detail.trim()) return payload.detail;
    if (typeof payload.title === 'string' && payload.title.trim()) return payload.title;
  }
  return response?.message || 'The request could not be completed. Please try again.';
}

function extractValidationMessages(errors: unknown): string[] {
  if (!errors || typeof errors !== 'object' || Array.isArray(errors)) return [];
  return Object.values(errors).flatMap((value) =>
    Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [],
  );
}
