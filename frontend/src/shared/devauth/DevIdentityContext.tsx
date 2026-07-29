import { createContext, useContext, useState, type ReactNode } from 'react';

export type DevRole = 'Officer' | 'Administrator';

const STORAGE_KEY = 'bard-dev-identity-role';

interface DevIdentityContextValue {
  role: DevRole | null;
  setRole: (role: DevRole) => void;
}

const DevIdentityContext = createContext<DevIdentityContextValue | undefined>(undefined);

/**
 * Codespaces/local-development-only identity switcher. Only ever
 * rendered when VITE_DEV_MODE=true (see .env.development) — production
 * builds never include this in the auth path at all; MSAL/Entra ID is
 * used instead (see App.tsx).
 */
export function DevIdentityProvider({ children }: { children: ReactNode }) {
  const [role, setRoleState] = useState<DevRole | null>(
    (localStorage.getItem(STORAGE_KEY) as DevRole | null) ?? null,
  );

  const setRole = (newRole: DevRole) => {
    localStorage.setItem(STORAGE_KEY, newRole);
    setRoleState(newRole);
  };

  return <DevIdentityContext.Provider value={{ role, setRole }}>{children}</DevIdentityContext.Provider>;
}

export function useDevIdentity(): DevIdentityContextValue {
  const ctx = useContext(DevIdentityContext);
  // Safe default when no provider is mounted (production build, where
  // DevIdentityProvider is never rendered) — avoids forcing callers to
  // conditionally call this hook, which would violate the Rules of Hooks.
  return ctx ?? { role: null, setRole: () => {} };
}

export const isDevMode = (): boolean => import.meta.env.VITE_DEV_MODE === 'true';
