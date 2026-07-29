import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/shared/api/apiClient';

export interface ProcessDossierFormValues {
  dossierReference: string;
  companyName: string;
  enterpriseNumber: string;
  companyAddressLine?: string;
  companyPostalCode?: string;
  companyCity?: string;
  companyCountry?: string;
  refundApplicationDate: string; // yyyy-MM-dd
  excelFile: File;
  pdfFiles: File[];
}

export interface ProcessDossierResult {
  dossierId: string;
  rowCount: number;
  invoiceCount: number;
  ac4Count: number;
  unclassifiedFiles: string[];
  errors: string[];
}

export function useProcessDossier() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (values: ProcessDossierFormValues) => {
      const form = new FormData();
      form.append('dossierReference', values.dossierReference);
      form.append('companyName', values.companyName);
      form.append('enterpriseNumber', values.enterpriseNumber);
      if (values.companyAddressLine) form.append('companyAddressLine', values.companyAddressLine);
      if (values.companyPostalCode) form.append('companyPostalCode', values.companyPostalCode);
      if (values.companyCity) form.append('companyCity', values.companyCity);
      if (values.companyCountry) form.append('companyCountry', values.companyCountry);
      form.append('refundApplicationDate', values.refundApplicationDate);
      form.append('excelFile', values.excelFile);
      values.pdfFiles.forEach((f) => form.append('pdfFiles', f));

      const { data } = await apiClient.post<ProcessDossierResult>('/dossiers/process', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dossiers'] }),
  });
}
