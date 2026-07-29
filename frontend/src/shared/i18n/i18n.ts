import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import HttpBackend from 'i18next-http-backend';

/**
 * Loads translation bundles from the backend's
 * /api/v1/terminology/bundle/{language} endpoint rather than static
 * files, so administrator overrides (Settings > Terminology) take
 * effect without a new frontend build/deploy.
 *
 * Supported languages: nl-BE (default), fr-BE, de-BE, en.
 * Preferred language is stored per-user (User.PreferredLanguage) and
 * applied on login; falls back to nl-BE for anonymous/unset sessions.
 */

export const SUPPORTED_LANGUAGES = ['nl-BE', 'fr-BE', 'de-BE', 'en'] as const;
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number];
export const DEFAULT_LANGUAGE: SupportedLanguage = 'nl-BE';

i18n
  .use(HttpBackend)
  .use(initReactI18next)
  .init({
    lng: DEFAULT_LANGUAGE,
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES,
    interpolation: { escapeValue: false }, // React already escapes
    backend: {
      // Maps directly onto GetLocalizationBundleQuery in the API.
      loadPath: '/api/v1/terminology/bundle/{{lng}}',
    },
    react: {
      useSuspense: true,
    },
  });

export default i18n;
