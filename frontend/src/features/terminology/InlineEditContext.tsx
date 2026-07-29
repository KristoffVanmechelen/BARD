import { createContext, useContext, useState, type ReactNode } from 'react';

interface InlineEditContextValue {
  isEditModeActive: boolean;
  toggleEditMode: () => void;
  canUseInlineEdit: boolean; // resolved from the user's permissions
}

const InlineEditContext = createContext<InlineEditContextValue | undefined>(undefined);

/**
 * Wraps the application so any component can check whether "Edit texts"
 * mode is active (decision #7). Only rendered as togglable when the
 * current user actually holds the terminology.edit.inline permission —
 * canUseInlineEdit should be derived from the auth/permissions context
 * once that's wired up; defaulted to false here so nothing is
 * accidentally editable before permissions are confirmed.
 */
export function InlineEditProvider({
  children,
  canUseInlineEdit,
}: {
  children: ReactNode;
  canUseInlineEdit: boolean;
}) {
  const [isEditModeActive, setEditModeActive] = useState(false);

  const toggleEditMode = () => {
    if (!canUseInlineEdit) return;
    setEditModeActive((prev) => !prev);
  };

  return (
    <InlineEditContext.Provider value={{ isEditModeActive, toggleEditMode, canUseInlineEdit }}>
      {children}
    </InlineEditContext.Provider>
  );
}

export function useInlineEditMode(): InlineEditContextValue {
  const ctx = useContext(InlineEditContext);
  if (!ctx) throw new Error('useInlineEditMode must be used within an InlineEditProvider');
  return ctx;
}
