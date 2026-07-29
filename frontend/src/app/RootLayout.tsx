import { useState } from 'react';
import { Outlet, Link as RouterLink } from 'react-router-dom';
import {
  AppBar,
  Toolbar,
  Typography,
  Box,
  Select,
  MenuItem,
  FormControlLabel,
  Switch,
  Button,
  Container,
  type SelectChangeEvent,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useInlineEditMode } from '@/features/terminology/InlineEditContext';
import { SUPPORTED_LANGUAGES } from '@/shared/i18n/i18n';

const LANGUAGE_LABELS: Record<string, string> = {
  'nl-BE': 'Nederlands',
  'fr-BE': 'Français',
  'de-BE': 'Deutsch',
  en: 'English',
};

export function RootLayout() {
  const { t, i18n } = useTranslation();
  const { isEditModeActive, toggleEditMode, canUseInlineEdit } = useInlineEditMode();
  const [language, setLanguage] = useState(i18n.language);

  const handleLanguageChange = (event: SelectChangeEvent) => {
    const next = event.target.value;
    setLanguage(next);
    i18n.changeLanguage(next);
    // TODO: persist to User.PreferredLanguage via a dedicated profile
    // endpoint once the user-profile module is implemented.
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="static" color={isEditModeActive ? 'warning' : 'primary'}>
        <Toolbar sx={{ gap: 2 }}>
          <Typography variant="h6" sx={{ flexGrow: 0 }}>
            BARD
          </Typography>
          <Typography variant="body2" sx={{ flexGrow: 1, opacity: 0.85 }}>
            {t('app.subtitle', 'Belgian Accise Refund & Document Analyzer')}
          </Typography>

          <Button color="inherit" component={RouterLink} to="/dossiers">
            {t('nav.dossiers', 'Dossiers')}
          </Button>
          <Button color="inherit" component={RouterLink} to="/settings/excise-rates">
            {t('nav.excise_rates', 'Excise Rates')}
          </Button>
          <Button color="inherit" component={RouterLink} to="/settings/terminology">
            {t('nav.terminology', 'Terminology')}
          </Button>

          <Select size="small" value={language} onChange={handleLanguageChange}
            sx={{ color: 'inherit', '& .MuiSvgIcon-root': { color: 'inherit' } }}>
            {SUPPORTED_LANGUAGES.map((lng) => (
              <MenuItem key={lng} value={lng}>{LANGUAGE_LABELS[lng]}</MenuItem>
            ))}
          </Select>

          {canUseInlineEdit && (
            <FormControlLabel
              control={<Switch checked={isEditModeActive} onChange={toggleEditMode} color="default" />}
              label={t('nav.edit_texts', 'Edit texts')}
            />
          )}
        </Toolbar>
      </AppBar>

      <Container maxWidth="xl" sx={{ flexGrow: 1, py: 2 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
