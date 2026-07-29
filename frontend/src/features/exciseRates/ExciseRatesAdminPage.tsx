import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Switch,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  MenuItem,
  Stack,
  Chip,
} from '@mui/material';
import { useExciseRates, useCreateExciseRate, useSetExciseRateActiveStatus } from './exciseRates.api';

const CALCULATION_UNITS = [
  'PerHectolitre',
  'PerHectolitreOfPureAlcohol',
  'PerDegreePlatoPerHectolitre',
  'PerHectolitreAlcoholicStrength',
  'Other',
];

/**
 * Settings > Excise Rates — administrator-maintained rate table
 * (decision #11). No annual reconfirmation required; deactivation
 * (never deletion once used) is the only retirement path exposed here.
 */
export function ExciseRatesAdminPage() {
  const { t } = useTranslation();
  const { data: rates, isLoading } = useExciseRates();
  const setActiveStatus = useSetExciseRateActiveStatus();
  const [createDialogOpen, setCreateDialogOpen] = useState(false);

  return (
    <Box sx={{ p: 3 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="h5">{t('exciserates.admin.title', 'Settings > Excise Rates')}</Typography>
        <Button variant="contained" onClick={() => setCreateDialogOpen(true)}>
          {t('exciserates.admin.add_button', 'Add excise code')}
        </Button>
      </Stack>

      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>{t('exciserates.admin.column_code', 'Code')}</TableCell>
            <TableCell>{t('exciserates.admin.column_description', 'Description')}</TableCell>
            <TableCell align="right">{t('exciserates.admin.column_rate', 'Rate')}</TableCell>
            <TableCell>{t('exciserates.admin.column_unit', 'Unit')}</TableCell>
            <TableCell>{t('exciserates.admin.column_effective_from', 'Effective from')}</TableCell>
            <TableCell>{t('exciserates.admin.column_status', 'Status')}</TableCell>
            <TableCell align="right">{t('exciserates.admin.column_active', 'Active')}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {!isLoading &&
            rates?.map((rate) => (
              <TableRow key={rate.id} hover>
                <TableCell>
                  <Typography fontFamily="monospace">{rate.exciseCode}</Typography>
                </TableCell>
                <TableCell>{rate.description}</TableCell>
                <TableCell align="right">{rate.currentRate}</TableCell>
                <TableCell>{rate.calculationUnit}</TableCell>
                <TableCell>{rate.effectiveFrom}</TableCell>
                <TableCell>
                  <Chip
                    label={rate.isActive ? t('exciserates.admin.status_active', 'Active') : t('exciserates.admin.status_inactive', 'Inactive')}
                    color={rate.isActive ? 'success' : 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell align="right">
                  <Switch
                    checked={rate.isActive}
                    onChange={(e) =>
                      setActiveStatus.mutate({ id: rate.id, isActive: e.target.checked })
                    }
                  />
                </TableCell>
              </TableRow>
            ))}
        </TableBody>
      </Table>

      <Typography variant="caption" color="text.secondary" sx={{ mt: 2, display: 'block' }}>
        {t('exciserates.admin.no_expiry_notice', 'Rates never expire automatically and require no annual reconfirmation. The organisation is responsible for the legal correctness of stored rates.')}
      </Typography>

      <CreateExciseRateDialog open={createDialogOpen} onClose={() => setCreateDialogOpen(false)} />
    </Box>
  );
}

function CreateExciseRateDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation();
  const createMutation = useCreateExciseRate();
  const [form, setForm] = useState({
    exciseCode: '',
    description: '',
    initialRate: '',
    calculationUnit: CALCULATION_UNITS[0],
    effectiveFrom: new Date().toISOString().slice(0, 10),
    administrativeComment: '',
  });

  const handleSubmit = async () => {
    await createMutation.mutateAsync({
      exciseCode: form.exciseCode,
      description: form.description,
      initialRate: Number(form.initialRate),
      calculationUnit: form.calculationUnit,
      effectiveFrom: form.effectiveFrom,
      administrativeComment: form.administrativeComment || undefined,
    });
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{t('exciserates.admin.add_dialog_title', 'Add excise code')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label={t('exciserates.admin.field_code', 'Excise code')} value={form.exciseCode}
            onChange={(e) => setForm((f) => ({ ...f, exciseCode: e.target.value }))} />
          <TextField label={t('exciserates.admin.field_description', 'Description')} value={form.description}
            onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
          <TextField label={t('exciserates.admin.field_rate', 'Rate')} type="number" value={form.initialRate}
            onChange={(e) => setForm((f) => ({ ...f, initialRate: e.target.value }))} />
          <TextField select label={t('exciserates.admin.field_unit', 'Calculation unit')} value={form.calculationUnit}
            onChange={(e) => setForm((f) => ({ ...f, calculationUnit: e.target.value }))}>
            {CALCULATION_UNITS.map((u) => (
              <MenuItem key={u} value={u}>{u}</MenuItem>
            ))}
          </TextField>
          <TextField label={t('exciserates.admin.field_effective_from', 'Effective from')} type="date" value={form.effectiveFrom}
            InputLabelProps={{ shrink: true }}
            onChange={(e) => setForm((f) => ({ ...f, effectiveFrom: e.target.value }))} />
          <TextField label={t('exciserates.admin.field_comment', 'Administrative comment (optional)')} value={form.administrativeComment}
            onChange={(e) => setForm((f) => ({ ...f, administrativeComment: e.target.value }))} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>{t('common.cancel', 'Cancel')}</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={createMutation.isPending}>
          {t('common.save', 'Save')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
