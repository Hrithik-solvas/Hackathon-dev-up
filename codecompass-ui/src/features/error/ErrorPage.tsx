import React from 'react';
import { Box, Typography, Button } from '@mui/material';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import { useNavigate } from 'react-router-dom';

interface Props {
  variant?: 'error' | 'notfound';
  message?: string;
  onTryAgain?: () => void;
}

export default function ErrorPage({ variant = 'error', message, onTryAgain }: Props) {
  const navigate = useNavigate();

  const title = variant === 'notfound' ? 'Page not found' : 'Something went wrong';
  const description =
    variant === 'notfound'
      ? "The page you're looking for doesn't exist."
      : message || 'An unexpected error occurred.';

  const handleTryAgain = () => {
    if (onTryAgain) {
      onTryAgain();
    } else {
      window.location.reload();
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh',
        bgcolor: '#F5F5F5',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
    >
      <Box
        sx={{
          width: '100%',
          maxWidth: 440,
          bgcolor: '#FFFFFF',
          borderRadius: '12px',
          boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
          p: '40px',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        {/* Icon */}
        <Box
          sx={{
            width: 56,
            height: 56,
            borderRadius: '50%',
            bgcolor: '#E6F3FA',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            mb: 2.5,
          }}
        >
          <ErrorOutlineIcon sx={{ color: '#01579B', fontSize: 28 }} />
        </Box>

        {/* Title */}
        <Typography
          sx={{ fontSize: 18, fontWeight: 700, color: '#222222', mb: 1, textAlign: 'center' }}
        >
          {title}
        </Typography>

        {/* Message */}
        <Typography
          sx={{ fontSize: 12, color: '#53565A', mb: 3.5, textAlign: 'center', lineHeight: 1.6 }}
        >
          {description}
        </Typography>

        {/* Buttons */}
        <Box sx={{ display: 'flex', gap: 1.5, width: '100%' }}>
          <Button
            fullWidth
            variant="contained"
            onClick={() => navigate('/')}
            sx={{ py: 1.25, fontSize: 13 }}
          >
            Go to Chat
          </Button>
          <Button
            fullWidth
            variant="outlined"
            onClick={handleTryAgain}
            sx={{
              py: 1.25,
              fontSize: 13,
              borderColor: '#01579B',
              color: '#01579B',
              '&:hover': { borderColor: '#013654', bgcolor: '#E6F3FA' },
            }}
          >
            Try Again
          </Button>
        </Box>

        {/* Footer */}
        <Typography sx={{ fontSize: 10, color: '#8F8F8F', mt: 3 }}>
          CodeCompass v1.0
        </Typography>
      </Box>
    </Box>
  );
}
