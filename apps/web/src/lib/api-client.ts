"use client";

import type { ApiErrorBody } from "@/lib/api-types";
import { clearAccessToken, getAccessToken } from "@/lib/auth";
import { apiBaseUrl } from "@/lib/config";

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly traceId: string;
  readonly fieldErrors?: Record<string, string[]>;

  constructor(status: number, payload: ApiErrorBody) {
    super(payload.error.message);
    this.name = "ApiError";
    this.status = status;
    this.code = payload.error.code;
    this.traceId = payload.error.traceId;
    this.fieldErrors = payload.error.fieldErrors;
  }
}

function onUnauthorized() {
  clearAccessToken();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("fieldledger:unauthorized"));
    if (window.location.pathname !== "/login") {
      const returnTo = `${window.location.pathname}${window.location.search}`;
      window.location.assign(`/login?returnTo=${encodeURIComponent(returnTo)}`);
    }
  }
}

async function request(path: string, init?: RequestInit) {
  const token = getAccessToken();
  const headers = new Headers(init?.headers);
  if (init?.body && !(init.body instanceof FormData) && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });
  if (response.status === 401) onUnauthorized();

  if (!response.ok) {
    let payload: ApiErrorBody;
    try {
      payload = (await response.json()) as ApiErrorBody;
    } catch {
      payload = {
        error: {
          code: "unexpected_response",
          message: response.statusText || "The server returned an unexpected response.",
          traceId: "unavailable",
        },
      };
    }
    throw new ApiError(response.status, payload);
  }

  return response;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await request(path, init);
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function apiFetchBlob(path: string): Promise<Blob> {
  const response = await request(path, { headers: { Accept: "text/csv" } });
  return response.blob();
}
