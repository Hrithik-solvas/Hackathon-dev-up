import React, { useCallback, useRef, useState } from 'react';
import { Box, Typography, Button } from '@mui/material';
import CloudUploadOutlinedIcon from '@mui/icons-material/CloudUploadOutlined';

interface FileDropZoneProps {
  onFiles: (files: File[]) => void;
  accept?: string;
  disabled?: boolean;
}

export default function FileDropZone({ onFiles, accept, disabled }: FileDropZoneProps) {
  const [isDragging, setIsDragging] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const processFiles = useCallback(
    (files: File[]) => {
      if (files.length > 0 && !disabled) onFiles(files);
    },
    [onFiles, disabled]
  );

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    if (!disabled) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    processFiles(Array.from(e.dataTransfer.files));
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    processFiles(Array.from(e.target.files ?? []));
    if (inputRef.current) inputRef.current.value = '';
  };

  const handleZoneClick = () => {
    if (!disabled) inputRef.current?.click();
  };

  return (
    <Box
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      onClick={handleZoneClick}
      sx={{
        bgcolor: isDragging ? '#E6F3FA' : '#FFFFFF',
        border: `1.5px dashed ${isDragging ? '#01579B' : '#B1D6E9'}`,
        borderRadius: '12px',
        p: 4,
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: 1.5,
        cursor: disabled ? 'not-allowed' : 'pointer',
        transition: 'border-color 0.2s, background-color 0.2s',
        opacity: disabled ? 0.6 : 1,
        userSelect: 'none',
      }}
    >
      <Box
        sx={{
          width: 48,
          height: 48,
          borderRadius: '50%',
          bgcolor: '#E6F3FA',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          flexShrink: 0,
        }}
      >
        <CloudUploadOutlinedIcon sx={{ color: '#01579B', fontSize: 24 }} />
      </Box>

      <Typography sx={{ fontSize: 13, fontWeight: 500, color: '#222222', textAlign: 'center' }}>
        Drag and drop files here, or click to browse
      </Typography>
      <Typography sx={{ fontSize: 11, color: '#8F8F8F', mt: -0.5 }}>
        Supports .md, .txt, .cs, .ts, .pdf
      </Typography>

      <Button
        variant="contained"
        size="small"
        disabled={disabled}
        onClick={(e) => {
          e.stopPropagation();
          inputRef.current?.click();
        }}
        sx={{ mt: 0.5, px: 2.5 }}
      >
        Browse
      </Button>

      <input
        ref={inputRef}
        type="file"
        multiple
        accept={accept}
        style={{ display: 'none' }}
        onChange={handleChange}
      />
    </Box>
  );
}
