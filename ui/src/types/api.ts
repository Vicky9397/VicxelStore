/**
 * Hand-written until the API's openapi.json is generated into this folder; the
 * shapes mirror the Identity module contracts (spec 06 section 6.5).
 */

export type Role = 'Buyer' | 'Seller' | 'Admin' | 'Moderator' | 'Support';

export interface User {
  id: string;
  email: string;
  emailVerified: boolean;
  displayName: string;
  roles: Role[];
}

export interface AuthResponse {
  accessToken: string;
  expiresInSeconds: number;
  user: User;
}

export interface FieldError {
  field: string;
  message: string;
}

/** Normalized shape of an RFC 9457 problem+json response (spec 06 section 6.4). */
export interface ApiError {
  code: string;
  title: string;
  status: number;
  fieldErrors: FieldError[];
}
