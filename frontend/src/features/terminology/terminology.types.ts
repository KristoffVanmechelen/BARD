// Mirrors BARD.Contracts.Terminology.* — keep in sync with the backend contracts.

export interface TerminologyEntry {
  key: string;
  category: string;
  module: string;
  screen: string | null;
  defaultNl: string;
  defaultFr: string;
  defaultDe: string;
  defaultEn: string;
  currentNl: string;
  currentFr: string;
  currentDe: string;
  currentEn: string;
  hasOverrideNl: boolean;
  hasOverrideFr: boolean;
  hasOverrideDe: boolean;
  hasOverrideEn: boolean;
  isProtected: boolean;
  isAdministratorConfigurable: boolean;
}

export interface TerminologySearchRequest {
  searchText?: string;
  module?: string;
  screen?: string;
  category?: string;
  onlyMissingTranslations?: boolean;
  onlyModified?: boolean;
  page: number;
  pageSize: number;
}

export interface TerminologySearchResult {
  entries: TerminologyEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type TerminologyChangeSource = 'InlineEditor' | 'CentralAdministration';

export interface UpdateTerminologyRequest {
  key: string;
  nl?: string | null;
  fr?: string | null;
  de?: string | null;
  en?: string | null;
  source: TerminologyChangeSource;
}

export interface TerminologyHistoryEntry {
  id: string;
  language: string;
  previousValue: string | null;
  newValue: string | null;
  changedByDisplayName: string;
  changedAtUtc: string;
  source: string;
}
