import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { TextField } from '@/components/forms/TextField';
import { register as registerAccount } from '@/features/auth/api/authApi';
import { ApiRequestError } from '@/lib/apiClient';
import { registerSchema, type RegisterValues } from '@/lib/zodSchemas/auth';

export function RegisterPage(): ReactElement {
  const { t } = useTranslation();

  const {
    register: registerField,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<RegisterValues>({ resolver: zodResolver(registerSchema) });

  const mutation = useMutation({
    mutationFn: registerAccount,
    onError: (error) => {
      // Server-side field errors land in the same surface as client validation.
      if (error instanceof ApiRequestError) {
        for (const fieldError of error.fieldErrors) {
          if (fieldError.field === 'email' || fieldError.field === 'password' || fieldError.field === 'displayName') {
            setError(fieldError.field, { message: fieldError.message });
          }
        }
      }
    },
  });

  if (mutation.isSuccess) {
    return (
      <div className="row justify-content-center">
        <div className="col-md-6 col-lg-5">
          <div className="alert alert-success" role="alert">
            {t('auth.registerSuccess')}
          </div>
          <Link className="btn btn-outline-primary" to="/login">
            {t('nav.login')}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="row justify-content-center">
      <div className="col-md-6 col-lg-5">
        <h1 className="h3">{t('auth.registerTitle')}</h1>
        <p className="text-muted">{t('auth.registerSubtitle')}</p>

        <FormError error={mutation.error} />

        <form
          noValidate
          onSubmit={(event) => {
            void handleSubmit((values) => mutation.mutate(values))(event);
          }}
        >
          <TextField
            id="displayName"
            autoComplete="name"
            label={t('auth.displayName')}
            registration={registerField('displayName')}
            error={errors.displayName}
          />
          <TextField
            id="email"
            type="email"
            autoComplete="email"
            label={t('auth.email')}
            registration={registerField('email')}
            error={errors.email}
          />
          <TextField
            id="password"
            type="password"
            autoComplete="new-password"
            label={t('auth.password')}
            registration={registerField('password')}
            error={errors.password}
          />
          <button className="btn btn-primary w-100" type="submit" disabled={mutation.isPending}>
            {t('auth.submitRegister')}
          </button>
        </form>

        <p className="mt-3 mb-0">
          {t('auth.haveAccount')} <Link to="/login">{t('nav.login')}</Link>
        </p>
      </div>
    </div>
  );
}
