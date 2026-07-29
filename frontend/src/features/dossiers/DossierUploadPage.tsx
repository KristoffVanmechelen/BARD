import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Typography,
  TextField,
  Button,
  Stack,
  Paper,
  Grid,
  Alert,
  LinearProgress,
  Chip,
  List,
  ListItem,
  ListItemText,
} from '@mui/material';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { useProcessDossier } from './dossierProcessing.api';

/**
 * Phase 2 — upload/process workflow. Company name + enterprise/VAT
 * number are required per decision #4; address fields are optional.
 * Excel-derived company info is never used as the source of truth here
 * — only these explicitly supplied values are sent as authoritative.
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
  const [excelFile, setExcelFile] = useState<File | null>(null);
  const [pdfFiles, setPdfFiles] = useState<File[]>([]);

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
              onChange={(e) => setForm((f) => ({ ...f, dossierReference: e.target.value }))}
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
              onChange={(e) => setForm((f) => ({ ...f, refundApplicationDate: e.target.value }))}
              helperText={t('dossier.upload.field_application_date_help',
                'The date this claim was submitted — used for the 12-month deadline check, never today\'s date.')}
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
              onChange={(e) => setForm((f) => ({ ...f, companyName: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              required
              label={t('dossier.upload.field_enterprise_number', 'Enterprise / VAT number')}
              value={form.enterpriseNumber}
              onChange={(e) => setForm((f) => ({ ...f, enterpriseNumber: e.target.value }))}
              helperText={t('dossier.upload.field_enterprise_number_help',
                'Used to match this dossier to an existing company record.')}
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_address', 'Address (optional)')}
              value={form.companyAddressLine}
              onChange={(e) => setForm((f) => ({ ...f, companyAddressLine: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_postal_code', 'Postal code')}
              value={form.companyPostalCode}
              onChange={(e) => setForm((f) => ({ ...f, companyPostalCode: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_city', 'City')}
              value={form.companyCity}
              onChange={(e) => setForm((f) => ({ ...f, companyCity: e.target.value }))}
            />
          </Grid>
          <Grid item xs={12} sm={2}>
            <TextField
              fullWidth
              label={t('dossier.upload.field_country', 'Country')}
              value={form.companyCountry}
              onChange={(e) => setForm((f) => ({ ...f, companyCountry: e.target.value }))}
            />
          </Grid>
        </Grid>

        <Typography variant="subtitle1" gutterBottom>
          {t('dossier.upload.section_documents', 'Documents')}
        </Typography>
        <Stack spacing={2}>
          <Button component="label" variant="outlined" startIcon={<UploadFileIcon />}>
            {excelFile ? excelFile.name : t('dossier.upload.excel_button', 'Upload company Excel claim')}
            <input hidden type="file" accept=".xlsx,.xls"
              onChange={(e) => setExcelFile(e.target.files?.[0] ?? null)} />
          </Button>

          <Button component="label" variant="outlined" startIcon={<UploadFileIcon />}>
            {t('dossier.upload.pdfs_button', 'Upload dossier PDFs (invoices, AC4s, ...)')}
            <input hidden type="file" accept=".pdf" multiple
              onChange={(e) => setPdfFiles(Array.from(e.target.files ?? []))} />
          </Button>

          {pdfFiles.length > 0 && (
            <List dense>
              {pdfFiles.map((f, i) => (
                <ListItem key={i}>
                  <ListItemText primary={f.name} secondary={`${(f.size / 1024).toFixed(1)} KB`} />
                </ListItem>
              ))}
            </List>
          )}

          <Typography variant="caption" color="text.secondary">
            {t('dossier.upload.classification_note',
              'No sorting needed — every PDF is automatically classified as invoice, AC4, or unrecognised.')}
          </Typography>
        </Stack>
      </Paper>

      {processDossier.isPending && <LinearProgress sx={{ mb: 2 }} />}

      {processDossier.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {t('dossier.upload.submit_error', 'Processing failed. Please check the files and try again.')}
        </Alert>
      )}

      {processDossier.data && processDossier.data.errors.length > 0 && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {processDossier.data.errors.join(' ')}
        </Alert>
      )}

      {processDossier.data && processDossier.data.errors.length === 0 && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {t('dossier.upload.result_summary', 'Processed {{rows}} row(s), {{invoices}} invoice(s), {{ac4}} AC4(s).', {
            rows: processDossier.data.rowCount,
            invoices: processDossier.data.invoiceCount,
            ac4: processDossier.data.ac4Count,
          })}
          {processDossier.data.unclassifiedFiles.length > 0 && (
            <Box sx={{ mt: 1 }}>
              {t('dossier.upload.unclassified_warning', 'Could not classify:')}{' '}
              {processDossier.data.unclassifiedFiles.map((f) => (
                <Chip key={f} label={f} size="small" sx={{ mr: 0.5 }} />
              ))}
            </Box>
          )}
        </Alert>
      )}

      <Button variant="contained" size="large" disabled={!canSubmit} onClick={handleSubmit}>
        {t('dossier.upload.submit_button', 'Analyze')}
      </Button>
    </Box>
  );
}
