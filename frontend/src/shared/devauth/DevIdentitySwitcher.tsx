import { ToggleButton, ToggleButtonGroup, Box, Typography, Paper } from '@mui/material';
import { useDevIdentity, type DevRole } from './DevIdentityContext';

/**
 * Visible only in Codespaces/local development (VITE_DEV_MODE=true).
 * Lets the developer pick which seeded identity (Dev Officer / Dev
 * Administrator) the app acts as — no password, no real Entra ID token.
 */
export function DevIdentitySwitcher() {
  const { role, setRole } = useDevIdentity();

  return (
    <Paper sx={{ p: 1.5, mb: 2, bgcolor: 'warning.light' }}>
      <Typography variant="caption" display="block" sx={{ mb: 0.5 }}>
        Development mode — select a test identity (never available in production):
      </Typography>
      <ToggleButtonGroup
        value={role}
        exclusive
        size="small"
        onChange={(_, value: DevRole | null) => value && setRole(value)}
      >
        <ToggleButton value="Officer">Dev Officer</ToggleButton>
        <ToggleButton value="Administrator">Dev Administrator</ToggleButton>
      </ToggleButtonGroup>
      {!role && (
        <Box sx={{ mt: 0.5 }}>
          <Typography variant="caption" color="error">
            Select an identity to use the application.
          </Typography>
        </Box>
      )}
    </Paper>
  );
}
