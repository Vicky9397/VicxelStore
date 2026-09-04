import { z } from 'zod';

/** Mirrors the API's FluentValidation rules so both surfaces agree. */
export const registerSchema = z
  .object({
    displayName: z.string().min(2, 'Display name must be at least 2 characters.').max(120),
    email: z.string().email('Enter a valid email address.').max(256),
    password: z.string().min(12, 'Password must be at least 12 characters.'),
  })
  .refine((data) => data.password.toLowerCase() !== data.email.split('@')[0]?.toLowerCase(), {
    path: ['password'],
    message: 'Password cannot equal the email local part.',
  });

export const loginSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
  password: z.string().min(1, 'Enter your password.'),
});

export const resendVerificationSchema = z.object({
  email: z.string().email('Enter a valid email address.'),
});

export type RegisterValues = z.infer<typeof registerSchema>;
export type LoginValues = z.infer<typeof loginSchema>;
export type ResendVerificationValues = z.infer<typeof resendVerificationSchema>;
