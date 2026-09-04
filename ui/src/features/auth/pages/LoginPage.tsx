import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import type { ReactElement } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { FormError } from '@/components/forms/FormError';
import { TextField } from '@/components/forms/TextField';
import { login } from '@/features/auth/api/authApi';
import { useAuthStore } from '@/features/auth/store';
import { loginSchema, type LoginValues } from '@/lib/zodSchemas/auth';

export function LoginPage(): ReactElement {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const setSession = useAuthStore((state) => state.setSession);

  const {
    register: registerField,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({ resolver: zodResolver(loginSchema) });

  const mutation = useMutation({
    mutationFn: login,
    onSuccess: (response) => {
      setSession(response.user, response.accessToken);
      const state = location.state as { from?: string } | null;
      navigate(state?.from ?? '/', { replace: true });
    },
  });

  return (
    <div className="row justify-content-center">
      <div className="col-md-6 col-lg-5">
        <h1 className="h3">{t('auth.loginTitle')}</h1>
        <p className="text-muted">{t('auth.loginSubtitle')}</p>

        <FormError error={mutation.error} />

        <form
          noValidate
          onSubmit={(event) => {
            void handleSubmit((values) => mutation.mutate(values))(event);
          }}
        >
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
            autoComplete="current-password"
            label={t('auth.password')}
            registration={registerField('password')}
            error={errors.password}
          />
          <button className="btn btn-primary w-100" type="submit" disabled={mutation.isPending}>
            {t('auth.submitLogin')}
          </button>
        </form>

        <p className="mt-3 mb-0">
          {t('auth.noAccount')} <Link to="/register">{t('nav.register')}</Link>
        </p>
      </div>
    </div>
  );
}
