import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  Box,
  Button,
  Chip,
  Grid,
  LinearProgress,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { FileDropZone, isExcelFile, isPdfFile } from '@/shared/components/FileDropZone';
import { useProcessDossier } from './dossierProcessing.api';

/**
 * Upload/process workflow.
 * The user uploads all dossier files through one upload zone.
 * The frontend still sends one Excel file and all PDFs to the existing backend endpoint.
 */
export function DossierUploadPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const processDossier = useProcessDossier();

  const [form, setForm] = useState({
    dossierReference: '',
    companyName: '',
    enterpriseNumber: '',
    companyAddressLine: '',
    companyPostalCode: '',
    companyCity: '',
    companyCountry: '',
    refundApplicationDate: new Date().toISOString().slice(0, 10),
  });

  const [uploadedFiles, setUploadedFiles] = useState<File[]>([]);

  const excelFile = uploadedFiles.find(isExcelFile) ?? null;
  const pdfFiles = uploadedFiles.filter(isPdfFile);

  const canSubmit =
    form.dossierReference.trim() !== '' &&
    form.companyName.trim() !== '' &&
    form.enterpriseNumber.trim() !== '' &&
    excelFile !== null &&
    pdfFiles.length > 0 &&
    !processDossier.isPending;

  const handleSubmit = async () => {
    if (!excelFile) return;

    const result = await processDossier.mutateAsync({
      ...form,
      companyAddressLine: form.companyAddressLine || undefined,
      companyPostalCode: form.companyPostalCode || undefined,
      companyCity: form.companyCity || undefined,
      companyCountry: form.companyCountry || undefined,
      excelFile,
      pdfFiles,
    });

    if (result.errors.length === 0) {
      navigate(`/dossiers/${result.dossierId}`);
    }
  };

  return (
    <Box sx={{ maxWidth: 800, mx: 'auto' }}>
      <Typography variant="h5" gutterBottom>
        {t('dossier.upload.title', 'New dossier — upload and analyze')}
      </Typography>

      <Paper sx={{ p: 3, mb: 2 }}>
        <Typography variant="subtitle1" gutterBottom>
          {t('dossier.upload.section_dossier', 'Dossier')}
        </Typography>

        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              required
              label={t('dossier.upload.field_reference', 'Dossier reference')}
              value={form.dossierReference}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  dossierReference: event.target.value,
                }))
              }
            />
          </Grid>

          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              required
              type="date"
              label={t('dossier.upload.field_application_date', 'Refund application date')}
              InputLabelProps={{ shrink: true }}
              value={form.refundApplicationDate}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  refundApplicationDate: event.target.value,
                }))
              }
              helperText={t(
                'dossier.upload.field_application_date_help',
                "The date this claim was submitted — used for the 12-month deadline check, never today's date.",
              )}
            />
          </Grid>
        </Grid>

        <Typography variant="subtitle1" gutterBottom>
          {t('dossier.upload.section_company', 'Applicant company')}
        </Typography>

        <Grid container spacing={2} sx={{ mb: 3 }}>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              required
              label={t('dossier.upload.field_company_name', 'Company name')}
              value={form.companyName}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  companyName: event.target.value,
                }))
              }
            />
          </Grid>

          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              required
              label={t('dossier.upload.field_enterprise_number', 'Enterprise / VAT number')}
              value={form.enterpriseNumber}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  enterpriseNumber: event.target.value,
                }))
              }
              helperText={t(
                'dossier.upload.field_enterprise_number_help',
                'Used to match this dossier to an existing company record.',
              )}
            />
          </Grid>

          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_address', 'Address (optional)')}
              value={form.companyAddressLine}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  companyAddressLine: event.target.value,
                }))
              }
            />
          </Grid>

          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_postal_code', 'Postal code')}
              value={form.companyPostalCode}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  companyPostalCode: event.target.value,
                }))
              }
            />
          </Grid>

          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_city', 'City')}
              value={form.companyCity}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  companyCity: event.target.value,
                }))
              }
            />
          </Grid>

          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_country', 'Country')}
              value={form.companyCountry}
              onChange={(event) =>
                setForm((current) => ({
                  ...current,
                  companyCountry: event.target.value,
                }))
              }
            />
          </Grid>
        </Grid>

        <Typography variant="subtitle1" gutterBottom>
          {t('dossier.upload.section_documents', 'Documents')}
        </Typography>

        <FileDropZone
          files={uploadedFiles}
          onFilesChange={setUploadedFiles}
          disabled={processDossier.isPending}
        />
      </Paper>

      {processDossier.isPending && <LinearProgress sx={{ mb: 2 }} />}

      {processDossier.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {t(
            'dossier.upload.submit_error',
            'Processing failed. Please check the files and try again.',
          )}
        </Alert>
      )}

      {processDossier.data && processDossier.data.errors.length > 0 && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {processDossier.data.errors.join(' ')}
        </Alert>
      )}

      {processDossier.data && processDossier.data.errors.length === 0 && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {t(
            'dossier.upload.result_summary',
            'Processed {{rows}} row(s), {{invoices}} invoice(s), {{ac4}} AC4(s).',
            {
              rows: processDossier.data.rowCount,
              invoices: processDossier.data.invoiceCount,
              ac4: processDossier.data.ac4Count,
            },
          )}

          {processDossier.data.unclassifiedFiles.length > 0 && (
            <Box sx={{ mt: 1 }}>
              {t('dossier.upload.unclassified_warning', 'Could not classify:')}{' '}
              {processDossier.data.unclassifiedFiles.map((fileName) => (
                <Chip key={fileName} label={fileName} size="small" sx={{ mr: 0.5 }} />
              ))}
            </Box>
          )}
        </Alert>
      )}

      <Button
        variant="contained"
        size="large"
        disabled={!canSubmit}
        onClick={handleSubmit}
      >
        {processDossier.isPending
          ? t('dossier.upload.processing_button', 'Processing…')
          : t('dossier.upload.submit_button', 'Analyze')}
      </Button>
    </Box>
  );
}
