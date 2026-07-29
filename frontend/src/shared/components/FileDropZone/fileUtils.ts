export const ACCEPTED_DOSSIER_EXTENSIONS = ['.xlsx', '.xls', '.pdf'] as const;

export function getFileExtension(fileName: string): string {
  const dotIndex = fileName.lastIndexOf('.');
  return dotIndex >= 0 ? fileName.slice(dotIndex).toLowerCase() : '';
}

export function isExcelFile(file: File): boolean {
  return ['.xlsx', '.xls'].includes(getFileExtension(file.name));
}

export function isPdfFile(file: File): boolean {
  return getFileExtension(file.name) === '.pdf';
}

export function isAcceptedDossierFile(file: File): boolean {
  return ACCEPTED_DOSSIER_EXTENSIONS.includes(
    getFileExtension(file.name) as (typeof ACCEPTED_DOSSIER_EXTENSIONS)[number],
  );
}

export function getFileKey(file: File): string {
  return `${file.name}-${file.size}-${file.lastModified}`;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
