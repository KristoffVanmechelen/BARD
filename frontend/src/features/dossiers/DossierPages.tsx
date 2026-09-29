import { Fragment, useState } from 'react';
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

interface DossierExtractedField {
  fieldName: string;
  value: string | null;
  pageNumber: number | null;
  confidence: number;
}

interface DossierDocument {
  id: string;
  originalFileName: string;
  documentKind: string;
  classificationConfidence: number;
  classificationReasons: string | null;
  documentRole: string;
  roleConfidence: number;
  roleReasons: string | null;
  roleConfirmedByUser: boolean;
  roleConfirmedByDisplayName: string | null;
  roleConfirmedAtUtc: string | null;
  extractionMethod: string;
  extractionConfidence: number;
  ocrWasRequired: boolean;
  extractionWarnings: string | null;
  extractedFields: DossierExtractedField[];
}

interface DossierDetail {
  id: string;
  dossierReference: string;
  companyName: string;
  companyEnterpriseNumber: string | null;
  status: string;
  lines: DossierLine[];
  documents: DossierDocument[];
}

function getExtractedField(
  doc: DossierDocument,
  name: string,
) {
  return doc.extractedFields.find(
    (field) => field.fieldName === name,
  )?.value ?? null;
}

function dossierDocumentKindLabel(
  doc: DossierDocument,
) {
  if (doc.documentKind === 'Ac4Declaration') {
    return 'AC4 declaration';
  }

  if (doc.documentKind === 'EadEVadDocument') {
    return getExtractedField(
      doc,
      'MovementDocumentType',
    ) ?? 'e-AD / e-VAD';
  }

  return doc.documentKind;
}

function quantityInLitres(
  quantity: string | null | undefined,
  unit: string | null | undefined,
) {
  if (quantity == null) return null;

  const parsed = Number(quantity);
  if (!Number.isFinite(parsed)) return null;

  return (unit ?? '').toLowerCase().startsWith('hl')
    ? parsed * 100
    : parsed;
}

function formatQuantity(
  quantity: number | null,
) {
  if (quantity == null) return '—';

  return quantity
    .toFixed(3)
    .replace(/\.?0+$/, '');
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

      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>
        {t('dossier.detail.documents_title', 'Uploaded documents')}
      </Typography>

      <Paper variant="outlined" sx={{ mb: 3 }}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>{t('dossier.detail.document_file', 'File')}</TableCell>
              <TableCell>{t('dossier.detail.document_kind', 'Kind')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.documents.map((doc) => {
              const ac4VisibleFields = new Set([
                'DRN',
                'MRN',
                'ARC',
                'LRN',
                'ValidationDate',
                'PeriodStart',
                'PeriodEnd',
                'Declarant',
                'MovementDateTime',
              ]);

              const generalFields = doc.extractedFields.filter((field) => {
                if (field.fieldName.startsWith('Article[')) return false;
                if (field.fieldName === 'MovementDocumentType') return false;

                if (
                  doc.documentKind === 'Ac4Declaration'
                  || doc.documentKind === 'EadEVadDocument'
                ) {
                  return ac4VisibleFields.has(field.fieldName);
                }

                return true;
              });

              const articleMap = new Map<number, Record<string, string | null>>();
              const movementRecordMap = new Map<number, Record<string, string | null>>();

              doc.extractedFields.forEach((field) => {
                const articleMatch =
                  field.fieldName.match(/^Article\[(\d+)\]\.(.+)$/);

                if (articleMatch) {
                  const articleNumber = Number(articleMatch[1]);
                  const propertyName = articleMatch[2];
                  const current = articleMap.get(articleNumber) ?? {};
                  current[propertyName] = field.value;
                  articleMap.set(articleNumber, current);
                  return;
                }

                const movementMatch =
                  field.fieldName.match(/^MovementRecord\[(\d+)\]\.(.+)$/);

                if (movementMatch) {
                  const recordNumber = Number(movementMatch[1]);
                  const propertyName = movementMatch[2];
                  const current = movementRecordMap.get(recordNumber) ?? {};
                  current[propertyName] = field.value;
                  movementRecordMap.set(recordNumber, current);
                }
              });

              const articles = [...articleMap.entries()]
                .sort(([a], [b]) => a - b);

              const movementRecords = [...movementRecordMap.entries()]
                .sort(([a], [b]) => a - b);

              return (
                <Fragment key={doc.id}>
              <TableRow>
                <TableCell>
                  <Typography variant="body2">
                    {doc.originalFileName}
                  </Typography>
                </TableCell>

                <TableCell>
                  <Chip
                    size="small"
                    label={dossierDocumentKindLabel(doc)}
                    color={doc.documentKind === 'Unknown' ? 'warning' : 'default'}
                  />
                </TableCell>
              </TableRow>

              {doc.extractedFields.length > 0 && (
                <TableRow>
                  <TableCell colSpan={2} sx={{ backgroundColor: 'action.hover' }}>
                    <Typography variant="subtitle2" sx={{ mb: 1 }}>
                      {t('dossier.detail.extracted_facts', 'Extracted facts')}
                    </Typography>

                    {generalFields.length > 0 && (
                      <Stack
                        direction="row"
                        spacing={2}
                        useFlexGap
                        flexWrap="wrap"
                        sx={{ mb: articles.length > 0 || movementRecords.length > 0 ? 1.5 : 0 }}
                      >
                        {generalFields.map((field) => (
                          <Typography key={field.fieldName} variant="body2">
                            <strong>{field.fieldName}:</strong>{' '}
                            {field.value ?? '—'}
                          </Typography>
                        ))}
                      </Stack>
                    )}

                    {articles.length > 0 && (
                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell>#</TableCell>
                            <TableCell>S-code</TableCell>
                            <TableCell align="right">Quantity (L)</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {articles.map(([number, article]) => {
                            const litres = quantityInLitres(
                              article.TaxBase,
                              article.Unit,
                            );

                            return (
                              <TableRow key={number}>
                                <TableCell>{number}</TableCell>
                                <TableCell>{article.ExciseCode ?? '—'}</TableCell>
                                <TableCell align="right">
                                  {formatQuantity(litres)}
                                </TableCell>
                              </TableRow>
                            );
                          })}
                        </TableBody>
                      </Table>
                    )}

                    {movementRecords.length > 0 && (
                      <Table size="small">
                        <TableHead>
                          <TableRow>
                            <TableCell>Record</TableCell>
                            <TableCell>EMCS code</TableCell>
                            <TableCell>S-code</TableCell>
                            <TableCell align="right">Quantity (L)</TableCell>
                          </TableRow>
                        </TableHead>
                        <TableBody>
                          {movementRecords.map(([number, record]) => (
                            <TableRow key={number}>
                              <TableCell>{number}</TableCell>
                              <TableCell>{record.EmcsExciseCode ?? '—'}</TableCell>
                              <TableCell>{record.BelgianExciseCode ?? 'unmapped'}</TableCell>
                              <TableCell align="right">{record.QuantityLitres ?? '—'}</TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </TableCell>
                </TableRow>
              )}
                </Fragment>
              );
            })}

            {data.documents.length === 0 && (
              <TableRow>
                <TableCell colSpan={2}>
                  <Typography color="text.secondary">
                    {t('dossier.detail.no_documents', 'No uploaded documents found.')}
                  </Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Paper>

      <ExciseEvidenceComparison documents={data.documents} />

      <Typography variant="h6" sx={{ mb: 1 }}>
        {t('dossier.detail.claim_lines_title', 'Claim lines')}
      </Typography>

      {data.lines.length === 0 && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Typography color="text.secondary">
            {t(
              'dossier.detail.no_claim_lines',
              'No claim lines were identified. The uploaded documents are still retained and shown above.',
            )}
          </Typography>
        </Paper>
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

function ExciseEvidenceComparison({ documents }: { documents: DossierDocument[] }) {
  type Ac4Bucket = { file: string; drn: string; code: string; litres: number };
  type MovementBucket = { file: string; arc: string; code: string; litres: number };

  const readField = (doc: DossierDocument, name: string) =>
    doc.extractedFields.find((field) => field.fieldName === name)?.value ?? '—';

  const ac4Buckets: Ac4Bucket[] = [];

  documents
    .filter((doc) => doc.documentKind === 'Ac4Declaration')
    .forEach((doc) => {
      const records = new Map<number, Record<string, string | null>>();

      doc.extractedFields.forEach((field) => {
        const match = field.fieldName.match(/^Article\[(\d+)\]\.(.+)$/);
        if (!match) return;

        const number = Number(match[1]);
        const current = records.get(number) ?? {};
        current[match[2]] = field.value;
        records.set(number, current);
      });

      const byCode = new Map<string, number>();

      records.forEach((record) => {
        const code = record.ExciseCode;
        const quantity = Number(record.TaxBase);
        const unit = (record.Unit ?? '').toLowerCase();

        if (!code || !Number.isFinite(quantity)) return;

        const litres = unit.startsWith('hl') ? quantity * 100 : quantity;
        byCode.set(code, (byCode.get(code) ?? 0) + litres);
      });

      byCode.forEach((litres, code) => {
        ac4Buckets.push({
          file: doc.originalFileName,
          drn: readField(doc, 'DRN'),
          code,
          litres,
        });
      });
    });

  const movementBuckets: MovementBucket[] = [];

  documents
    .filter((doc) => doc.documentKind === 'EadEVadDocument')
    .forEach((doc) => {
      const records = new Map<number, Record<string, string | null>>();

      doc.extractedFields.forEach((field) => {
        const match = field.fieldName.match(/^MovementRecord\[(\d+)\]\.(.+)$/);
        if (!match) return;

        const number = Number(match[1]);
        const current = records.get(number) ?? {};
        current[match[2]] = field.value;
        records.set(number, current);
      });

      const byCode = new Map<string, number>();

      records.forEach((record) => {
        const code = record.BelgianExciseCode;
        const quantity = Number(record.QuantityLitres);

        if (!code || !Number.isFinite(quantity)) return;

        byCode.set(code, (byCode.get(code) ?? 0) + quantity);
      });

      byCode.forEach((litres, code) => {
        movementBuckets.push({
          file: doc.originalFileName,
          arc: readField(doc, 'ARC'),
          code,
          litres,
        });
      });
    });

  if (movementBuckets.length === 0) return null;

  const rows = movementBuckets.flatMap((movement) => {
    const candidates = ac4Buckets.filter((ac4) => ac4.code === movement.code);

    if (candidates.length === 0) {
      return [{ movement, ac4: null as Ac4Bucket | null }];
    }

    return candidates.map((ac4) => ({ movement, ac4 }));
  });

  return (
    <Paper variant="outlined" sx={{ mb: 3, p: 2 }}>
      <Typography variant="h6" sx={{ mb: 0.5 }}>
        Code / quantity comparison
      </Typography>
      <Typography variant="caption" color="text.secondary" display="block" sx={{ mb: 1.5 }}>
        Candidate comparison only: same S-code and sufficient volume do not by themselves prove that the same commercial goods are covered.
      </Typography>

      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>ARC</TableCell>
            <TableCell>S-code</TableCell>
            <TableCell align="right">Movement (L)</TableCell>
            <TableCell>Candidate DRN</TableCell>
            <TableCell align="right">AC4 available (L)</TableCell>
            <TableCell>Finding</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map(({ movement, ac4 }, index) => {
            const sufficient =
              ac4 !== null && ac4.litres + 0.0001 >= movement.litres;

            return (
              <TableRow key={`${movement.file}-${movement.code}-${ac4?.file ?? 'none'}-${index}`}>
                <TableCell>{movement.arc}</TableCell>
                <TableCell>{movement.code}</TableCell>
                <TableCell align="right">
                  {movement.litres.toFixed(3).replace(/\.?0+$/, '')}
                </TableCell>
                <TableCell>{ac4?.drn ?? '—'}</TableCell>
                <TableCell align="right">
                  {ac4 ? ac4.litres.toFixed(3).replace(/\.?0+$/, '') : '—'}
                </TableCell>
                <TableCell>
                  {ac4 === null
                    ? 'No matching AC4 candidate supplied'
                    : sufficient
                      ? 'S-code matches; candidate volume sufficient'
                      : 'S-code matches; candidate volume insufficient'}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </Paper>
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
