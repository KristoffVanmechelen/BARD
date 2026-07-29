import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useParams, Link as RouterLink } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Chip,
  Button,
  Stack,
  Collapse,
  IconButton,
  TextField,
  Paper,
} from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ExpandLessIcon from '@mui/icons-material/ExpandLess';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import CancelIcon from '@mui/icons-material/Cancel';
import DownloadIcon from '@mui/icons-material/Download';
import { useHasPermission } from '@/shared/auth/usePermissions';
import { apiClient } from '@/shared/api/apiClient';

interface DossierSummary {
  id: string;
  dossierReference: string;
  companyName: string;
  refundApplicationDate: string;
  status: string;
  totalLines: number;
  flaggedLines: number;
  totalCalculatedRefund: number | null;
}

interface DossierListResult {
  dossiers: DossierSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function DossierListPage() {
  const { t } = useTranslation();
  const canProcess = useHasPermission('dossier.process');
  const { data, isLoading } = useQuery<DossierListResult>({
    queryKey: ['dossiers'],
    queryFn: async () => {
      const { data } = await apiClient.post<DossierListResult>('/dossiers/search', { page: 1, pageSize: 25 });
      return data;
    },
  });

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="h5">{t('dossier.list.title', 'Dossiers')}</Typography>
        {canProcess && (
          <Button variant="contained" component={RouterLink} to="/dossiers/new">
            {t('dossier.list.new_button', 'New dossier')}
          </Button>
        )}
      </Stack>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t('dossier.list.column_reference', 'Reference')}</TableCell>
            <TableCell>{t('dossier.list.column_company', 'Company')}</TableCell>
            <TableCell>{t('dossier.list.column_application_date', 'Application date')}</TableCell>
            <TableCell>{t('dossier.list.column_status', 'Status')}</TableCell>
            <TableCell align="right">{t('dossier.list.column_lines', 'Lines')}</TableCell>
            <TableCell align="right">{t('dossier.list.column_flagged', 'Flagged')}</TableCell>
            <TableCell align="right">{t('dossier.list.column_refund', 'Calculated refund')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {!isLoading && data?.dossiers.map((d) => (
            <TableRow key={d.id} hover component={RouterLink} to={`/dossiers/${d.id}`}
              sx={{ cursor: 'pointer', textDecoration: 'none' }}>
              <TableCell>{d.dossierReference}</TableCell>
              <TableCell>{d.companyName}</TableCell>
              <TableCell>{d.refundApplicationDate}</TableCell>
              <TableCell><Chip label={d.status} size="small" /></TableCell>
              <TableCell align="right">{d.totalLines}</TableCell>
              <TableCell align="right">
                {d.flaggedLines > 0 ? <Chip label={d.flaggedLines} color="warning" size="small" /> : 0}
              </TableCell>
              <TableCell align="right">{d.totalCalculatedRefund ?? '—'}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {!isLoading && data?.dossiers.length === 0 && (
        <Typography color="text.secondary" sx={{ mt: 2 }}>
          {t('dossier.list.empty_state', 'No dossiers yet. Click "New dossier" to upload one.')}
        </Typography>
      )}
    </Box>
  );
}

interface DossierLine {
  id: string;
  rowIndex: number;
  claimedInvoiceNumber: string | null;
  claimedProductDescription: string | null;
  exciseCode: string | null;
  claimedQuantity: number | null;
  mrn: string | null;
  claimedDestinationCountry: string | null;
  matchStatus: string;
  confidenceScore: number;
  hardBlockReason: string | null;
  matchExplanation: string | null;
  exportStatus: string;
  exportCheckNotes: string | null;
  mrnCumulativeStatus: string;
  mrnCumulativeNotes: string | null;
  ac4Status: string;
  ac4Notes: string | null;
  officerDecision: string;
  officerRemarks: string | null;
  reviewedByDisplayName: string | null;
  reviewedAtUtc: string | null;
  calculatedRefundAmount: number | null;
  calculationNotes: string | null;
  requiresManualReview: boolean;
}

interface DossierDetail {
  id: string;
  dossierReference: string;
  companyName: string;
  companyEnterpriseNumber: string | null;
  status: string;
  lines: DossierLine[];
}

export function DossierDetailPage() {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();

  const { data, isLoading } = useQuery<DossierDetail>({
    queryKey: ['dossier', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data } = await apiClient.get<DossierDetail>(`/dossiers/${id}`);
      return data;
    },
  });

  const exportMutation = useMutation({
    mutationFn: async () => {
      const response = await apiClient.get(`/dossiers/${id}/export`, { responseType: 'blob' });
      const contentDisposition = response.headers['content-disposition'] as string | undefined;
      const fileNameMatch = contentDisposition?.match(/filename="?([^";]+)"?/);
      const fileName = fileNameMatch?.[1] ?? `${data?.dossierReference ?? 'dossier'}_validation_report.xlsx`;

      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    },
  });

  if (isLoading || !data) return <Typography>{t('common.loading', 'Loading…')}</Typography>;

  return (
    <Box>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
        <Typography variant="h5">
          {data.dossierReference} — {data.companyName}
          {data.companyEnterpriseNumber && (
            <Typography component="span" variant="body2" color="text.secondary" sx={{ ml: 1 }}>
              ({data.companyEnterpriseNumber})
            </Typography>
          )}
        </Typography>
        <Button variant="outlined" startIcon={<DownloadIcon />} disabled={exportMutation.isPending}
          onClick={() => exportMutation.mutate()}>
          {t('dossier.detail.export_button', 'Download report (Excel)')}
        </Button>
      </Stack>
      <Chip label={data.status} sx={{ mb: 2 }} />

      {exportMutation.isError && (
        <Typography color="error" sx={{ mb: 2 }}>
          {t('dossier.detail.export_error', 'Could not generate the report. Please try again.')}
        </Typography>
      )}

      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell />
            <TableCell>{t('dossier.detail.column_row', 'Row')}</TableCell>
            <TableCell>{t('dossier.detail.column_invoice', 'Invoice #')}</TableCell>
            <TableCell>{t('dossier.detail.column_product', 'Product')}</TableCell>
            <TableCell>{t('dossier.detail.column_match', 'Match')}</TableCell>
            <TableCell align="right">{t('dossier.detail.column_confidence', 'Confidence')}</TableCell>
            <TableCell>{t('dossier.detail.column_export', 'Export')}</TableCell>
            <TableCell align="right">{t('dossier.detail.column_refund', 'Calculated refund')}</TableCell>
            <TableCell>{t('dossier.detail.column_decision', 'Decision')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {data.lines.map((line) => (
            <DossierLineRow key={line.id} line={line} dossierId={data.id} />
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

function DossierLineRow({ line, dossierId }: { line: DossierLine; dossierId: string }) {
  const { t } = useTranslation();
  const canReview = useHasPermission('dossier.review');
  const [expanded, setExpanded] = useState(line.requiresManualReview);
  const [remarks, setRemarks] = useState(line.officerRemarks ?? '');
  const queryClient = useQueryClient();

  const decisionMutation = useMutation({
    mutationFn: async (decision: 'Approved' | 'Rejected') => {
      await apiClient.post('/dossiers/lines/decision', {
        dossierLineId: line.id,
        decision,
        remarks,
      });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dossier', dossierId] }),
  });

  return (
    <>
      <TableRow hover>
        <TableCell>
          <IconButton size="small" onClick={() => setExpanded((e) => !e)}>
            {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
          </IconButton>
        </TableCell>
        <TableCell>{line.rowIndex}</TableCell>
        <TableCell>{line.claimedInvoiceNumber}</TableCell>
        <TableCell>{line.claimedProductDescription}</TableCell>
        <TableCell>{line.matchStatus}</TableCell>
        <TableCell align="right">{line.confidenceScore}%</TableCell>
        <TableCell>{line.exportStatus}</TableCell>
        <TableCell align="right" title={line.calculationNotes ?? undefined}>
          {line.calculatedRefundAmount ?? '—'}
        </TableCell>
        <TableCell>
          <Chip
            label={line.officerDecision}
            size="small"
            color={line.officerDecision === 'Approved' ? 'success' : line.officerDecision === 'Rejected' ? 'error' : 'warning'}
          />
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={9} sx={{ p: 0, borderBottom: expanded ? undefined : 'none' }}>
          <Collapse in={expanded}>
            <Paper variant="outlined" sx={{ m: 1, p: 2 }}>
              <Stack spacing={0.5} sx={{ mb: 2 }}>
                <Typography variant="body2">
                  <strong>{t('dossier.detail.finding_excise', 'Excise code')}:</strong> {line.exciseCode ?? '—'}
                  {'  '}<strong>{t('dossier.detail.finding_quantity', 'Quantity')}:</strong> {line.claimedQuantity ?? '—'}
                  {'  '}<strong>{t('dossier.detail.finding_mrn', 'MRN')}:</strong> {line.mrn ?? '—'}
                  {'  '}<strong>{t('dossier.detail.finding_country', 'Destination')}:</strong> {line.claimedDestinationCountry ?? '—'}
                </Typography>
                {line.hardBlockReason && (
                  <Typography variant="body2" color="error">
                    <strong>{t('dossier.detail.finding_hard_block', 'Hard block')}:</strong> {line.hardBlockReason}
                  </Typography>
                )}
                {line.matchExplanation && (
                  <Typography variant="body2">
                    <strong>{t('dossier.detail.finding_match_explanation', 'Match explanation')}:</strong> {line.matchExplanation}
                  </Typography>
                )}
                {line.exportCheckNotes && (
                  <Typography variant="body2">
                    <strong>{t('dossier.detail.finding_export_notes', 'Export check')}:</strong> {line.exportCheckNotes}
                  </Typography>
                )}
                <Typography variant="body2">
                  <strong>{t('dossier.detail.finding_mrn_status', 'MRN/AC4 status')}:</strong> {line.mrnCumulativeStatus} / {line.ac4Status}
                </Typography>
                {line.mrnCumulativeNotes && <Typography variant="body2">{line.mrnCumulativeNotes}</Typography>}
                {line.ac4Notes && <Typography variant="body2">{line.ac4Notes}</Typography>}
                {line.reviewedByDisplayName && (
                  <Typography variant="body2" color="text.secondary">
                    {t('dossier.detail.reviewed_by', 'Reviewed by {{name}} on {{date}}', {
                      name: line.reviewedByDisplayName,
                      date: line.reviewedAtUtc ? new Date(line.reviewedAtUtc).toLocaleString() : '',
                    })}
                  </Typography>
                )}
              </Stack>

              <TextField
                fullWidth
                multiline
                minRows={2}
                size="small"
                label={t('dossier.detail.remarks_label', 'Officer remarks')}
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                disabled={!canReview}
                sx={{ mb: 1.5 }}
              />

              {canReview && (
                <Stack direction="row" spacing={1}>
                  <Button
                    variant="contained"
                    color="success"
                    startIcon={<CheckCircleIcon />}
                    disabled={decisionMutation.isPending}
                    onClick={() => decisionMutation.mutate('Approved')}
                  >
                    {t('dossier.detail.approve_button', 'Approve')}
                  </Button>
                  <Button
                    variant="contained"
                    color="error"
                    startIcon={<CancelIcon />}
                    disabled={decisionMutation.isPending}
                    onClick={() => decisionMutation.mutate('Rejected')}
                  >
                    {t('dossier.detail.reject_button', 'Reject')}
                  </Button>
                </Stack>
              )}
            </Paper>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}
