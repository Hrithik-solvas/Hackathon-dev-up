import React from 'react';
import { Box } from '@mui/material';

export default function TypingIndicator() {
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        bgcolor: '#FFFFFF',
        borderRadius: '10px',
        px: 2,
        py: 1.5,
        boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
        width: 'fit-content',
        border: '1px solid #E7E7E7',
      }}
    >
      {[0, 200, 400].map((delayMs, i) => (
        <Box
          key={i}
          sx={{
            width: 7,
            height: 7,
            borderRadius: '50%',
            bgcolor: '#8F8F8F',
            animationName: 'typingPulse',
            animationDuration: '1.2s',
            animationTimingFunction: 'ease-in-out',
            animationDelay: `${delayMs}ms`,
            animationIterationCount: 'infinite',
            '@keyframes typingPulse': {
              '0%, 100%': { opacity: 0.3 },
              '50%': { opacity: 1 },
            },
          }}
        />
      ))}
    </Box>
  );
}
