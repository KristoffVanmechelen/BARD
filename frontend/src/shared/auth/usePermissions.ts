import { useQuery } from '@tanstack/react-query';
import { useMsal } from '@azure/msal-react';
import { apiClient } from '@/shared/api/apiClient';
import { isDevMode, useDevIdentity } from '@/shared/devauth/DevIdentityContext';

export interface CurrentUserProfile {
  userId: string;
  displayName: string;
  preferredLanguage: string;
  permissions: string[];
}

/** Fetches the current user's resolved permission codes (audit finding H3). */
export function useCurrentUserProfile() {
  const { accounts } = useMsal();
  const devMode = isDevMode();
  const devIdentity = useDevIdentity();

  const enabled = devMode ? Boolean(devIdentity.role) : accounts.length > 0;

  return useQuery<CurrentUserProfile>({
    queryKey: ['current-user-profile', devMode ? devIdentity.role : accounts[0]?.homeAccountId],
    enabled,
    queryFn: async () => {
      const { data } = await apiClient.get<CurrentUserProfile>('/users/me');
      return data;
    },
    staleTime: 5 * 60 * 1000,
  });
}

/** True once permissions are loaded AND the given code is present. False while loading (fail-closed, never show a gated action before we know). */
export function useHasPermission(code: string): boolean {
  const { data } = useCurrentUserProfile();
  return data?.permissions.includes(code) ?? false;
}
