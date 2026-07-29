import { ChangeEvent, DragEvent, KeyboardEvent, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Chip,
  IconButton,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  Typography,
} from '@mui/material';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import DescriptionOutlinedIcon from '@mui/icons-material/DescriptionOutlined';
import InsertDriveFileOutlinedIcon from '@mui/icons-material/InsertDriveFileOutlined';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { useTranslation } from 'react-i18next';
import {
  formatFileSize,
  getFileKey,
  isAcceptedDossierFile,
  isExcelFile,
  isPdfFile,
} from './fileUtils';

interface FileDropZoneProps {
  files: File[];
  onFilesChange: (files: File[]) => void;
  disabled?: boolean;
}

export function FileDropZone({
  files,
  onFilesChange,
  disabled = false,
}: FileDropZoneProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [warning, setWarning] = useState<string | null>(null);

  const excelFile = files.find(isExcelFile) ?? null;
  const pdfFiles = files.filter(isPdfFile);

  const addFiles = (incomingFiles: File[]) => {
    setWarning(null);

    const unsupported = incomingFiles.filter((file) => !isAcceptedDossierFile(file));
    const incomingExcelFiles = incomingFiles.filter(isExcelFile);
    const incomingPdfFiles = incomingFiles.filter(isPdfFile);

    if (unsupported.length > 0) {
      setWarning(
        t(
          'dossier.upload.unsupported_files',
          'Only Excel files (.xlsx or .xls) and PDF files are allowed.',
        ),
      );
    }

    let nextFiles = [...files];

    if (incomingExcelFiles.length > 0) {
      const latestExcelFile = incomingExcelFiles[incomingExcelFiles.length - 1];
      nextFiles = nextFiles.filter((file) => !isExcelFile(file));
      nextFiles.push(latestExcelFile);

      if (incomingExcelFiles.length > 1) {
        setWarning(
          t(
            'dossier.upload.single_excel_warning',
            'Only one Excel claim can be processed. The last selected Excel file was kept.',
          ),
        );
      }
    }

    const existingKeys = new Set(nextFiles.map(getFileKey));
    for (const pdfFile of incomingPdfFiles) {
      const key = getFileKey(pdfFile);
      if (!existingKeys.has(key)) {
        nextFiles.push(pdfFile);
        existingKeys.add(key);
      }
    }

    onFilesChange(nextFiles);
  };

  const handleInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    addFiles(Array.from(event.target.files ?? []));
    event.target.value = '';
  };

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    event.preventDefault();
    setIsDragging(false);
    if (!disabled) addFiles(Array.from(event.dataTransfer.files));
  };

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (disabled) return;
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      inputRef.current?.click();
    }
  };

  const removeFile = (fileToRemove: File) => {
    onFilesChange(files.filter((file) => getFileKey(file) !== getFileKey(fileToRemove)));
  };

  return (
    <Stack spacing={2}>
      <input
        ref={inputRef}
        hidden
        type="file"
        accept=".xlsx,.xls,.pdf"
        multiple
        disabled={disabled}
        onChange={handleInputChange}
      />

      <Box
        role="button"
        tabIndex={disabled ? -1 : 0}
        aria-label={t('dossier.upload.zone_aria_label', 'Select dossier documents')}
        onClick={() => !disabled && inputRef.current?.click()}
        onKeyDown={handleKeyDown}
        onDragEnter={(event) => {
          event.preventDefault();
          if (!disabled) setIsDragging(true);
        }}
        onDragOver={(event) => {
          event.preventDefault();
          if (!disabled) setIsDragging(true);
        }}
        onDragLeave={(event) => {
          event.preventDefault();
          if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
            setIsDragging(false);
          }
        }}
        onDrop={handleDrop}
        sx={{
          border: '2px dashed',
          borderColor: isDragging ? 'primary.main' : 'divider',
          borderRadius: 2,
          px: 3,
          py: 4,
          textAlign: 'center',
          cursor: disabled ? 'not-allowed' : 'pointer',
          opacity: disabled ? 0.6 : 1,
          bgcolor: isDragging ? 'action.hover' : 'background.default',
          transition: 'border-color 120ms ease, background-color 120ms ease',
          '&:hover, &:focus-visible': disabled
            ? {}
            : {
                borderColor: 'primary.main',
                bgcolor: 'action.hover',
                outline: 'none',
              },
        }}
      >
        <UploadFileIcon color="primary" sx={{ fontSize: 42, mb: 1 }} />

        <Typography variant="h6">
          {t('dossier.upload.zone_title', 'Upload documents')}
        </Typography>

        <Typography color="text.secondary" sx={{ mt: 0.5 }}>
          {t(
            'dossier.upload.zone_instruction',
            'Drag the Excel claim and all dossier PDFs here, or click to select files.',
          )}
        </Typography>

        <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>
          {t('dossier.upload.zone_file_types', 'Accepted: XLSX, XLS and PDF')}
        </Typography>
      </Box>

      {warning && (
        <Alert severity="warning" onClose={() => setWarning(null)}>
          {warning}
        </Alert>
      )}

      {files.length > 0 && (
        <>
          <Stack direction="row" spacing={1} useFlexGap flexWrap="wrap">
            <Chip
              size="small"
              color={excelFile ? 'success' : 'default'}
              label={
                excelFile
                  ? t('dossier.upload.excel_detected', 'Excel claim detected')
                  : t('dossier.upload.excel_missing', 'Excel claim missing')
              }
            />

            <Chip
              size="small"
              color={pdfFiles.length > 0 ? 'success' : 'default'}
              label={t('dossier.upload.pdf_count', '{{count}} PDF file(s)', {
                count: pdfFiles.length,
              })}
            />
          </Stack>

          <List dense disablePadding>
            {files.map((file, index) => (
              <ListItem
                key={getFileKey(file)}
                divider={index < files.length - 1}
                secondaryAction={
                  <IconButton
                    edge="end"
                    disabled={disabled}
                    aria-label={t('dossier.upload.remove_file', 'Remove file')}
                    onClick={() => removeFile(file)}
                  >
                    <DeleteOutlineIcon />
                  </IconButton>
                }
              >
                <ListItemIcon sx={{ minWidth: 40 }}>
                  {isExcelFile(file) ? (
                    <DescriptionOutlinedIcon color="success" />
                  ) : (
                    <InsertDriveFileOutlinedIcon color="primary" />
                  )}
                </ListItemIcon>

                <ListItemText
                  primary={file.name}
                  secondary={
                    isExcelFile(file)
                      ? `${t('dossier.upload.file_type_excel', 'Excel claim')} · ${formatFileSize(file.size)}`
                      : `${t(
                          'dossier.upload.file_type_pdf',
                          'PDF — classification follows automatically',
                        )} · ${formatFileSize(file.size)}`
                  }
                />
              </ListItem>
            ))}
          </List>
        </>
      )}

      <Typography variant="caption" color="text.secondary">
        {t(
          'dossier.upload.classification_note',
          'No sorting needed. BARD recognises the Excel claim and classifies each PDF during processing.',
        )}
      </Typography>
    </Stack>
  );
}
