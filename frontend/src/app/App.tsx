import { Suspense } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, CssBaseline, Box, Container } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MsalProvider } from '@azure/msal-react';

import { msalInstance } from '@/shared/api/authConfig';
import { registerMsalInstance } from '@/shared/api/apiClient';
import { bardTheme } from '@/shared/theme/theme';
import { InlineEditProvider } from '@/features/terminology/InlineEditContext';
import { useHasPermission } from '@/shared/auth/usePermissions';
import { isDevMode, DevIdentityProvider, useDevIdentity } from '@/shared/devauth/DevIdentityContext';
import { DevIdentitySwitcher } from '@/shared/devauth/DevIdentitySwitcher';
import { RootLayout } from './RootLayout';
import { TerminologyAdminPage } from '@/features/terminology/TerminologyAdminPage';
import { ExciseRatesAdminPage } from '@/features/exciseRates/ExciseRatesAdminPage';
import { DossierListPage, DossierDetailPage } from '@/features/dossiers/DossierPages';
import { DossierUploadPage } from '@/features/dossiers/DossierUploadPage';

import '@/shared/i18n/i18n';

registerMsalInstance(msalInstance);

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, staleTime: 30_000 } },
});

function AppRoutes() {
  const canUseInlineEdit = useHasPermission('terminology.edit.inline');

  return (
    <InlineEditProvider canUseInlineEdit={canUseInlineEdit}>
      <BrowserRouter>
        <Routes>
          <Route element={<RootLayout />}>
            <Route index element={<Navigate to="/dossiers" replace />} />
            <Route path="/dossiers" element={<DossierListPage />} />
            <Route path="/dossiers/new" element={<DossierUploadPage />} />
            <Route path="/dossiers/:id" element={<DossierDetailPage />} />
            <Route path="/settings/excise-rates" element={<ExciseRatesAdminPage />} />
            <Route path="/settings/terminology" element={<TerminologyAdminPage />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </InlineEditProvider>
  );
}

/**
 * Codespaces/local-development gate: blocks the app until a dev
 * identity is chosen, so no request goes out with a missing/invalid
 * identity header. Never rendered in a production build (isDevMode()
 * is a compile-time constant derived from VITE_DEV_MODE).
 */
function DevModeGate({ children }: { children: React.ReactNode }) {
  const { role } = useDevIdentity();
  return (
    <Container maxWidth="xl" sx={{ pt: 2 }}>
      <DevIdentitySwitcher />
      <Box sx={{ display: role ? 'block' : 'none' }}>{children}</Box>
    </Container>
  );
}

export function App() {
  const devMode = isDevMode();

  const routes = devMode ? (
    <DevIdentityProvider>
      <DevModeGate>
        <AppRoutes />
      </DevModeGate>
    </DevIdentityProvider>
  ) : (
    <AppRoutes />
  );

  return (
    <MsalProvider instance={msalInstance}>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={bardTheme}>
          <CssBaseline />
          <Suspense fallback={<div>Loading…</div>}>{routes}</Suspense>
        </ThemeProvider>
      </QueryClientProvider>
    </MsalProvider>
  );
}
