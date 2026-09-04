import { apiRequest } from '@/lib/apiClient';
import type { AuthResponse, User } from '@/types/api';

export interface RegisterPayload {
  email: string;
  password: string;
  displayName: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export function register(payload: RegisterPayload): Promise<{ message: string }> {
  return apiRequest('/auth/register', { method: 'POST', body: payload });
}

export function login(payload: LoginPayload): Promise<AuthResponse> {
  return apiRequest('/auth/login', { method: 'POST', body: payload, skipRefresh: true });
}

export function refresh(): Promise<AuthResponse> {
  return apiRequest('/auth/token/refresh', { method: 'POST', skipRefresh: true });
}

export function logout(): Promise<void> {
  return apiRequest('/auth/logout', { method: 'POST', skipRefresh: true });
}

export function verifyEmail(token: string): Promise<{ message: string }> {
  return apiRequest('/auth/email/verify', { method: 'POST', body: { token }, skipRefresh: true });
}

export function resendVerification(email: string): Promise<{ message: string }> {
  return apiRequest('/auth/email/resend', { method: 'POST', body: { email }, skipRefresh: true });
}

export function getMe(): Promise<User> {
  return apiRequest('/me');
}
