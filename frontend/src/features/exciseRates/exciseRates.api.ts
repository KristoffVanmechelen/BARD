import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/apiClient';

export interface ExciseRate {
  id: string;
  exciseCode: string;
  description: string;
  currentRate: number;
  calculationUnit: string;
  effectiveFrom: string;
  isActive: boolean;
  administrativeComment: string | null;
  lastModifiedAtUtc: string | null;
}

export interface CreateExciseRateRequest {
  exciseCode: string;
  description: string;
  initialRate: number;
  calculationUnit: string;
  effectiveFrom: string;
  administrativeComment?: string;
}

export interface PublishExciseRateVersionRequest {
  rate: number;
  calculationUnit: string;
  effectiveFrom: string;
}

const QUERY_KEY = 'excise-rates';

export function useExciseRates(activeOnly?: boolean) {
  return useQuery<ExciseRate[]>({
    queryKey: [QUERY_KEY, activeOnly],
    queryFn: async () => {
      const { data } = await apiClient.get<ExciseRate[]>('/excise-rates', { params: { activeOnly } });
      return data;
    },
  });
}

export function useCreateExciseRate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (request: CreateExciseRateRequest) => {
      const { data } = await apiClient.post<string>('/excise-rates', request);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] }),
  });
}

export function usePublishExciseRateVersion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, request }: { id: string; request: PublishExciseRateVersionRequest }) => {
      await apiClient.post(`/excise-rates/${id}/versions`, request);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] }),
  });
}

/** Deactivate is the ONLY retirement action exposed for a rate that may already be in use (decision #11). */
export function useSetExciseRateActiveStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, isActive, reason }: { id: string; isActive: boolean; reason?: string }) => {
      await apiClient.post(`/excise-rates/${id}/${isActive ? 'activate' : 'deactivate'}`, JSON.stringify(reason ?? null), {
        headers: { 'Content-Type': 'application/json' },
      });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [QUERY_KEY] }),
  });
}
