import { createTheme } from '@mui/material/styles';

/**
 * Base MUI theme. Colours/logo are partially administrator-configurable
 * (decision #10, "interface colours") — this file provides the
 * structural defaults; runtime overrides are merged in at the App root
 * from the configuration service once that admin feature is wired up.
 */
export const bardTheme = createTheme({
  palette: {
    mode: 'light',
    primary: {
      main: '#0F2A4A', // Belgian federal government dark blue
    },
    secondary: {
      main: '#F0AD00',
    },
    background: {
      default: '#F5F6F8',
    },
  },
  typography: {
    fontFamily: '"Segoe UI", "Inter", Roboto, Arial, sans-serif',
  },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
    },
  },
});
