import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation } from '@tanstack/react-query';
import { useEffect, type ReactElement } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useSearchParams } from 'react-router-dom';
import { TextField } from '@/components/forms/TextField';
import { resendVerification, verifyEmail } from '@/features/auth/api/authApi';
import {
  resendVerificationSchema,
  type ResendVerificationValues,
} from '@/lib/zodSchemas/auth';

export function VerifyEmailPage(): ReactElement {
  const { t } = useTranslation();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const verification = useMutation({ mutationFn: verifyEmail });
  const { mutate: runVerification } = verification;

  useEffect(() => {
    if (token !== null && token !== '') {
      runVerification(token);
    }
  }, [token, runVerification]);

  const {
    register: registerField,
    handleSubmit,
    formState: { errors },
  } = useForm<ResendVerificationValues>({ resolver: zodResolver(resendVerificationSchema) });

  const resend = useMutation({ mutationFn: (values: ResendVerificationValues) => resendVerification(values.email) });

  return (
    <div className="row justify-content-center">
      <div className="col-md-6 col-lg-5">
        <h1 className="h3">{t('auth.verifyTitle')}</h1>

        {token !== null && token !== '' ? (
          <>
            {verification.isPending ? <p className="text-muted">{t('auth.verifyPending')}</p> : null}
            {verification.isSuccess ? (
              <div className="alert alert-success" role="alert">
                {t('auth.verifySuccess')}{' '}
                <Link to="/login">{t('nav.login')}</Link>
              </div>
            ) : null}
            {verification.isError ? (
              <div className="alert alert-danger" role="alert">
                {t('auth.verifyFailed')}
              </div>
            ) : null}
          </>
        ) : (
          <p className="text-muted">{t('auth.verifyMissingToken')}</p>
        )}

        <hr />

        <h2 className="h5">{t('auth.resendTitle')}</h2>
        {resend.isSuccess ? (
          <div className="alert alert-info" role="alert">
            {t('auth.resendSuccess')}
          </div>
        ) : null}
        {resend.isError ? (
          <div className="alert alert-warning" role="alert">
            {t('errors.rateLimited')}
          </div>
        ) : null}

        <form
          noValidate
          onSubmit={(event) => {
            void handleSubmit((values) => resend.mutate(values))(event);
          }}
        >
          <TextField
            id="resend-email"
            type="email"
            autoComplete="email"
            label={t('auth.email')}
            registration={registerField('email')}
            error={errors.email}
          />
          <button className="btn btn-outline-primary" type="submit" disabled={resend.isPending}>
            {t('auth.resendSubmit')}
          </button>
        </form>
      </div>
    </div>
  );
}
