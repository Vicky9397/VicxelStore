import type { ApiError, FieldError } from '@/types/api';

const API_BASE = '/api/v1';

export class ApiRequestError extends Error implements ApiError {
  public readonly code: string;
  public readonly title: string;
  public readonly status: number;
  public readonly fieldErrors: FieldError[];

  public constructor(error: ApiError) {
    super(error.title);
    this.name = 'ApiRequestError';
    this.code = error.code;
    this.title = error.title;
    this.status = error.status;
    this.fieldErrors = error.fieldErrors;
  }
}

let accessToken: string | null = null;
let onAuthLost: (() => void) | null = null;

/** The access token lives in memory only, never in storage (spec 07 section 7.4). */
export function setAccessToken(token: string | null): void {
  accessToken = token;
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAuthLostHandler(handler: (() => void) | null): void {
  onAuthLost = handler;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function toFieldErrors(value: unknown): FieldError[] {
  if (!Array.isArray(value)) {
    return [];
  }

  return value.flatMap((entry): FieldError[] => {
    if (!isRecord(entry)) {
      return [];
    }
    const { field, message } = entry;
    return typeof field === 'string' && typeof message === 'string' ? [{ field, message }] : [];
  });
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown = null;
  try {
    body = await response.json();
  } catch {
    body = null;
  }

  const problem = isRecord(body) ? body : {};
  return {
    code: typeof problem['code'] === 'string' ? problem['code'] : 'INTERNAL',
    title: typeof problem['title'] === 'string' ? problem['title'] : 'Something went wrong.',
    status: response.status,
    fieldErrors: toFieldErrors(problem['errors']),
  };
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE';
  body?: unknown;
  /** Raw bytes sent as-is, for binary endpoints such as file part uploads. */
  rawBody?: BodyInit | undefined;
  idempotencyKey?: string;
  skipRefresh?: boolean;
}

async function send(path: string, options: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json';
  } else if (options.rawBody !== undefined) {
    headers['Content-Type'] = 'application/octet-stream';
  }
  if (accessToken !== null) {
    headers['Authorization'] = `Bearer ${accessToken}`;
  }
  if (options.idempotencyKey !== undefined) {
    headers['Idempotency-Key'] = options.idempotencyKey;
  }

  const body =
    options.body !== undefined
      ? JSON.stringify(options.body)
      : options.rawBody;

  return fetch(`${API_BASE}${path}`, {
    method: options.method ?? 'GET',
    headers,
    credentials: 'include',
    ...(body !== undefined ? { body } : {}),
  });
}

/**
 * On a 401 the client attempts exactly one silent refresh and replays the
 * request; if that fails it clears auth and hands control to the auth-lost
 * handler, which routes to login (spec 07 section 7.4).
 */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await send(path, options);

  if (response.status === 401 && options.skipRefresh !== true) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      response = await send(path, options);
    } else {
      setAccessToken(null);
      onAuthLost?.();
    }
  }

  if (!response.ok) {
    throw new ApiRequestError(await toApiError(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

async function tryRefresh(): Promise<boolean> {
  const response = await send('/auth/token/refresh', { method: 'POST', skipRefresh: true });
  if (!response.ok) {
    return false;
  }

  const body = (await response.json()) as { accessToken: string };
  setAccessToken(body.accessToken);
  return true;
}
