import axios, { type InternalAxiosRequestConfig } from 'axios';
import type { IPublicClientApplication } from '@azure/msal-browser';
import { isDevMode } from '@/shared/devauth/DevIdentityContext';

/**
 * Central API client. All requests go through /api (proxied to the
 * BARD.API backend by Vite in dev, and by the same-origin/reverse-proxy
 * setup in production).
 */
export const apiClient = axios.create({
  baseURL: '/api/v1',
});

let msalInstance: IPublicClientApplication | null = null;
const apiScopes = [import.meta.env.VITE_API_SCOPE as string];

export function registerMsalInstance(instance: IPublicClientApplication): void {
  msalInstance = instance;
}

apiClient.interceptors.request.use(async (config: InternalAxiosRequestConfig) => {
  if (isDevMode()) {
    // Codespaces/local development only: attach the seeded-identity
    // header instead of a real Entra ID token. See DevAuthenticationHandler
    // (API layer) — only accepted when the API itself was started with
    // Development:SeedTestIdentity=true (never true in production).
    const role = localStorage.getItem('bard-dev-identity-role');
    if (role) config.headers['X-Dev-Identity-Role'] = role;
    return config;
  }

  if (!msalInstance) return config;

  const account = msalInstance.getActiveAccount();
  if (!account) return config;

  try {
    const result = await msalInstance.acquireTokenSilent({ scopes: apiScopes, account });
    config.headers.Authorization = `Bearer ${result.accessToken}`;
  } catch {
    // Interactive fallback is handled by the calling component via
    // the MSAL react hooks — we deliberately don't force a redirect here.
  }

  return config;
});
