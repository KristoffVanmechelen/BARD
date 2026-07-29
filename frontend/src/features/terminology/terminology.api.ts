import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/apiClient';
import type {
  TerminologyEntry,
  TerminologyHistoryEntry,
  TerminologySearchRequest,
  TerminologySearchResult,
  UpdateTerminologyRequest,
} from './terminology.types';

const TERMINOLOGY_QUERY_KEY = 'terminology-search';

export function useTerminologySearch(request: TerminologySearchRequest) {
  return useQuery<TerminologySearchResult>({
    queryKey: [TERMINOLOGY_QUERY_KEY, request],
    queryFn: async () => {
      const { data } = await apiClient.post<TerminologySearchResult>('/terminology/search', request);
      return data;
    },
  });
}

export function useTerminologyHistory(key: string, enabled: boolean) {
  return useQuery<TerminologyHistoryEntry[]>({
    queryKey: ['terminology-history', key],
    enabled,
    queryFn: async () => {
      const { data } = await apiClient.get<TerminologyHistoryEntry[]>(
        `/terminology/${encodeURIComponent(key)}/history`,
      );
      return data;
    },
  });
}

/**
 * Single mutation used by BOTH the central admin page and the inline
 * editor (source field distinguishes them) — mirrors the backend's
 * single PUT /terminology endpoint, so the "changes appear in both
 * places" guarantee (decision #7) holds structurally on the frontend
 * too: there is only one code path that can write a terminology change.
 */
export function useUpdateTerminology() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: UpdateTerminologyRequest) => {
      await apiClient.put('/terminology', request);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [TERMINOLOGY_QUERY_KEY] });
      // Also invalidate the i18next-loaded bundle so open screens refresh —
      // i18next-http-backend caches per-language; simplest safe approach
      // is a full reload of the active language namespace.
      window.dispatchEvent(new CustomEvent('bard:terminology-changed'));
    },
  });
}

export function useRestoreTerminologyDefault() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (params: { key: string; language: string | null }) => {
      await apiClient.post('/terminology/restore-default', params);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [TERMINOLOGY_QUERY_KEY] });
      window.dispatchEvent(new CustomEvent('bard:terminology-changed'));
    },
  });
}

export type { TerminologyEntry };
