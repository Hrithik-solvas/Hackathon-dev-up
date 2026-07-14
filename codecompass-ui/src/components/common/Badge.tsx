import React from 'react';
import { Box, Typography } from '@mui/material';

export type BadgeVariant = 'success' | 'warning' | 'neutral' | 'active';

const variantStyles: Record<BadgeVariant, { bg: string; color: string }> = {
  success: { bg: '#EAF2EA', color: '#2E7D32' },
  active: { bg: '#EAF2EA', color: '#2E7D32' },
  warning: { bg: '#FFEADA', color: '#ED6C02' },
  neutral: { bg: '#E7E7E7', color: '#53565A' },
};

interface BadgeProps {
  label: string;
  variant?: BadgeVariant;
}

export default function Badge({ label, variant = 'neutral' }: BadgeProps) {
  const styles = variantStyles[variant];
  return (
    <Box
      sx={{
        display: 'inline-flex',
        bgcolor: styles.bg,
        borderRadius: '9px',
        px: 1.25,
        py: 0.25,
      }}
    >
      <Typography
        sx={{ fontSize: 9, fontWeight: 500, color: styles.color, lineHeight: '16px' }}
      >
        {label}
      </Typography>
    </Box>
  );
}
