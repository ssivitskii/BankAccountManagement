import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { extractApiError } from '../../core/api-error';
import { AuthStore } from '../../core/auth.store';
import { BankingApiService } from '../../core/banking-api.service';

@Component({
  selector: 'app-auth',
  imports: [ReactiveFormsModule],
  templateUrl: './auth.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthComponent {
  private readonly api = inject(BankingApiService);
  private readonly auth = inject(AuthStore);
  private readonly formBuilder = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly mode = signal<'login' | 'register'>('login');
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = this.formBuilder.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(200)]],
  });

  setMode(mode: 'login' | 'register'): void {
    this.mode.set(mode);
    this.error.set(null);
  }

  submit(): void {
    if (this.form.invalid || this.loading()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    const { username, password } = this.form.getRawValue();
    const request =
      this.mode() === 'login'
        ? this.api.login(username.trim(), password)
        : this.api.register(username.trim(), password);
    request.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (response) => {
        this.auth.save(response);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(isSafeReturnUrl(returnUrl) ? returnUrl : '/dashboard');
      },
      error: (error: unknown) => this.error.set(extractApiError(error)),
    });
  }
}

function isSafeReturnUrl(value: string | null): value is string {
  return (
    value !== null && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
  );
}
