import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Box,
  IconButton,
  Popover,
  Stack,
  TextField,
  Button,
  Typography,
  Divider,
} from '@mui/material';
import EditIcon from '@mui/icons-material/Edit';
import RestoreIcon from '@mui/icons-material/RestartAlt';
import { useInlineEditMode } from './InlineEditContext';
import { useUpdateTerminology, useRestoreTerminologyDefault } from './terminology.api';

interface EditableTextProps {
  /** The stable, immutable localization key (e.g. "dossier.list.title"). */
  localizationKey: string;
  /** Rendered element type — defaults to a plain span so this works inline anywhere. */
  as?: 'span' | 'div';
  className?: string;
}

/**
 * Wraps a piece of user-visible text so it is always rendered through
 * the localization service (never hardcoded — decision #5) and, when
 * an administrator has activated "Edit texts" mode, is directly
 * editable in place (decision #7).
 *
 * Every configurable label/button/heading/etc. in the application
 * should render through this component rather than calling t()
 * directly, so the inline-editing guarantee is structural rather than
 * something each screen has to remember to implement.
 */
export function EditableText({ localizationKey, as = 'span', className }: EditableTextProps) {
  const { t } = useTranslation();
  const { isEditModeActive, canUseInlineEdit } = useInlineEditMode();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);

  const displayText = t(localizationKey);
  const Wrapper = as;

  if (!isEditModeActive || !canUseInlineEdit) {
    return <Wrapper className={className}>{displayText}</Wrapper>;
  }

  return (
    <Wrapper className={className} style={{ position: 'relative', display: 'inline-flex', alignItems: 'center', gap: 4 }}>
      {displayText}
      <IconButton
        size="small"
        aria-label={`Edit "${localizationKey}"`}
        onClick={(e) => setAnchorEl(e.currentTarget)}
      >
        <EditIcon fontSize="inherit" />
      </IconButton>
      <Popover
        open={Boolean(anchorEl)}
        anchorEl={anchorEl}
        onClose={() => setAnchorEl(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
      >
        <TerminologyQuickEditor localizationKey={localizationKey} onClose={() => setAnchorEl(null)} />
      </Popover>
    </Wrapper>
  );
}

function TerminologyQuickEditor({ localizationKey, onClose }: { localizationKey: string; onClose: () => void }) {
  const { t, i18n } = useTranslation();
  const updateMutation = useUpdateTerminology();
  const restoreMutation = useRestoreTerminologyDefault();

  const [values, setValues] = useState({
    nl: i18n.getFixedT('nl-BE')(localizationKey),
    fr: i18n.getFixedT('fr-BE')(localizationKey),
    de: i18n.getFixedT('de-BE')(localizationKey),
    en: i18n.getFixedT('en')(localizationKey),
  });

  const handleSave = async () => {
    await updateMutation.mutateAsync({
      key: localizationKey,
      nl: values.nl,
      fr: values.fr,
      de: values.de,
      en: values.en,
      source: 'InlineEditor',
    });
    onClose();
  };

  const handleRestoreAll = async () => {
    await restoreMutation.mutateAsync({ key: localizationKey, language: null });
    onClose();
  };

  return (
    <Box sx={{ p: 2, width: 360 }}>
      <Typography variant="caption" color="text.secondary">
        {t('terminology.editor.key_label', 'Localization key')}: {localizationKey}
      </Typography>
      <Divider sx={{ my: 1 }} />
      <Stack spacing={1.5}>
        <TextField label="Nederlands (NL)" size="small" fullWidth value={values.nl}
          onChange={(e) => setValues((v) => ({ ...v, nl: e.target.value }))} />
        <TextField label="Français (FR)" size="small" fullWidth value={values.fr}
          onChange={(e) => setValues((v) => ({ ...v, fr: e.target.value }))} />
        <TextField label="Deutsch (DE)" size="small" fullWidth value={values.de}
          onChange={(e) => setValues((v) => ({ ...v, de: e.target.value }))} />
        <TextField label="English (EN)" size="small" fullWidth value={values.en}
          onChange={(e) => setValues((v) => ({ ...v, en: e.target.value }))} />
      </Stack>
      <Stack direction="row" spacing={1} sx={{ mt: 2 }} justifyContent="space-between">
        <Button
          size="small"
          color="inherit"
          startIcon={<RestoreIcon />}
          onClick={handleRestoreAll}
          disabled={restoreMutation.isPending}
        >
          {t('terminology.editor.restore_default', 'Restore default')}
        </Button>
        <Stack direction="row" spacing={1}>
          <Button size="small" onClick={onClose}>
            {t('common.cancel', 'Cancel')}
          </Button>
          <Button size="small" variant="contained" onClick={handleSave} disabled={updateMutation.isPending}>
            {t('common.save', 'Save')}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}
