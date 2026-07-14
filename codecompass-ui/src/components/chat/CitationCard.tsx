import React from 'react';
import { Box, Typography } from '@mui/material';
import { Citation } from '../../types/chat';

interface CitationCardProps {
  citation: Citation;
}

export default function CitationCard({ citation }: CitationCardProps) {
  const filename = citation.sourceUri.split(/[/\\]/).pop() ?? citation.sourceUri;
  const relevancePct = Math.round(citation.relevanceScore * 100);

  return (
    <Box
      sx={{
        bgcolor: '#F5F5F5',
        borderRadius: '8px',
        p: 1.5,
        mb: 1,
      }}
    >
      <Typography
        sx={{ fontSize: 11, fontWeight: 600, color: '#222222', mb: 0.5, wordBreak: 'break-word' }}
      >
        {filename}
      </Typography>
      <Box
        sx={{
          display: 'inline-flex',
          bgcolor: '#EAF2EA',
          borderRadius: '8px',
          px: 1,
          py: 0.25,
          mb: 0.75,
        }}
      >
        <Typography sx={{ fontSize: 9, fontWeight: 600, color: '#2E7D32' }}>
          {relevancePct}%
        </Typography>
      </Box>
      <Typography
        sx={{
          fontSize: 10,
          color: '#53565A',
          lineHeight: 1.5,
          display: '-webkit-box',
          WebkitLineClamp: 4,
          WebkitBoxOrient: 'vertical',
          overflow: 'hidden',
        }}
      >
        {citation.chunkContent}
      </Typography>
    </Box>
  );
}
