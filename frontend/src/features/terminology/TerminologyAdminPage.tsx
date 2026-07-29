import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Typography,
  TextField,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Checkbox,
  FormControlLabel,
  Chip,
  IconButton,
  Tooltip,
  Pagination,
  Stack,
} from '@mui/material';
import RestoreIcon from '@mui/icons-material/RestartAlt';
import HistoryIcon from '@mui/icons-material/History';
import { useTerminologySearch, useRestoreTerminologyDefault } from './terminology.api';
import type { TerminologyEntry } from './terminology.types';

/**
 * Settings > Terminology — the central administration page required by
 * decision #6. Search/filter, view default vs current translation per
 * language, identify missing/modified entries, restore to default.
 *
 * Writes go through the same useUpdateTerminology/useRestoreTerminologyDefault
 * hooks as the inline editor (EditableText.tsx) — one write path, per
 * decision #7's "changes appear in both places" guarantee.
 */
export function TerminologyAdminPage() {
  const { t } = useTranslation();
  const [searchText, setSearchText] = useState('');
  const [onlyMissing, setOnlyMissing] = useState(false);
  const [onlyModified, setOnlyModified] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const { data, isLoading } = useTerminologySearch({
    searchText: searchText || undefined,
    onlyMissingTranslations: onlyMissing || undefined,
    onlyModified: onlyModified || undefined,
    page,
    pageSize,
  });

  const restoreMutation = useRestoreTerminologyDefault();

  const totalPages = data ? Math.ceil(data.totalCount / pageSize) : 1;

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h5" gutterBottom>
        {t('terminology.admin.title', 'Settings > Terminology')}
      </Typography>

      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
        <TextField
          label={t('terminology.admin.search_label', 'Search')}
          size="small"
          value={searchText}
          onChange={(e) => {
            setSearchText(e.target.value);
            setPage(1);
          }}
        />
        <FormControlLabel
          control={<Checkbox checked={onlyMissing} onChange={(e) => setOnlyMissing(e.target.checked)} />}
          label={t('terminology.admin.only_missing_label', 'Only missing translations')}
        />
        <FormControlLabel
          control={<Checkbox checked={onlyModified} onChange={(e) => setOnlyModified(e.target.checked)} />}
          label={t('terminology.admin.only_modified_label', 'Only administrator-modified')}
        />
      </Stack>

      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t('terminology.admin.column_key', 'Key')}</TableCell>
            <TableCell>{t('terminology.admin.column_module_screen', 'Module / Screen')}</TableCell>
            <TableCell>NL</TableCell>
            <TableCell>FR</TableCell>
            <TableCell>DE</TableCell>
            <TableCell>EN</TableCell>
            <TableCell align="right">{t('common.actions', 'Actions')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {!isLoading &&
            data?.entries.map((entry) => <TerminologyRow key={entry.key} entry={entry} />)}
        </TableBody>
      </Table>

      <Stack alignItems="center" sx={{ mt: 2 }}>
        <Pagination count={totalPages} page={page} onChange={(_, p) => setPage(p)} />
      </Stack>

      {restoreMutation.isError && (
        <Typography color="error" sx={{ mt: 1 }}>
          {t('terminology.admin.restore_failed', 'Failed to restore default value.')}
        </Typography>
      )}
    </Box>
  );
}

function TerminologyRow({ entry }: { entry: TerminologyEntry }) {
  const { t } = useTranslation();
  const restoreMutation = useRestoreTerminologyDefault();

  const cell = (value: string, hasOverride: boolean) => (
    <TableCell>
      {value || <em style={{ color: '#b71c1c' }}>{t('terminology.admin.missing', 'missing')}</em>}
      {hasOverride && <Chip label={t('terminology.admin.modified_chip', 'modified')} size="small" sx={{ ml: 1 }} />}
    </TableCell>
  );

  return (
    <TableRow hover>
      <TableCell>
        <Typography variant="body2" fontFamily="monospace">
          {entry.key}
        </Typography>
        {entry.isProtected && <Chip label={t('terminology.admin.protected_chip', 'protected')} size="small" color="warning" sx={{ mt: 0.5 }} />}
      </TableCell>
      <TableCell>
        {entry.module}
        {entry.screen ? ` / ${entry.screen}` : ''}
      </TableCell>
      {cell(entry.currentNl, entry.hasOverrideNl)}
      {cell(entry.currentFr, entry.hasOverrideFr)}
      {cell(entry.currentDe, entry.hasOverrideDe)}
      {cell(entry.currentEn, entry.hasOverrideEn)}
      <TableCell align="right">
        <Tooltip title={t('terminology.admin.view_history_tooltip', 'View history')}>
          <IconButton size="small">
            <HistoryIcon fontSize="inherit" />
          </IconButton>
        </Tooltip>
        <Tooltip title={t('terminology.admin.restore_all_tooltip', 'Restore all languages to default')}>
          <span>
            <IconButton
              size="small"
              disabled={
                !entry.hasOverrideNl && !entry.hasOverrideFr && !entry.hasOverrideDe && !entry.hasOverrideEn
              }
              onClick={() => restoreMutation.mutate({ key: entry.key, language: null })}
            >
              <RestoreIcon fontSize="inherit" />
            </IconButton>
          </span>
        </Tooltip>
      </TableCell>
    </TableRow>
  );
}
