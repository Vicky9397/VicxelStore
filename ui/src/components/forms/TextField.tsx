import type { ReactElement } from 'react';
import type { FieldError, UseFormRegisterReturn } from 'react-hook-form';

interface TextFieldProps {
  id: string;
  label: string;
  type?: 'text' | 'email' | 'password';
  autoComplete?: string;
  registration: UseFormRegisterReturn;
  error?: FieldError | undefined;
}

export function TextField({
  id,
  label,
  type = 'text',
  autoComplete,
  registration,
  error,
}: TextFieldProps): ReactElement {
  const errorId = `${id}-error`;

  return (
    <div className="mb-3">
      <label className="form-label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        type={type}
        className={error ? 'form-control is-invalid' : 'form-control'}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? errorId : undefined}
        {...(autoComplete !== undefined ? { autoComplete } : {})}
        {...registration}
      />
      {error ? (
        <div className="invalid-feedback" id={errorId}>
          {error.message}
        </div>
      ) : null}
    </div>
  );
}
