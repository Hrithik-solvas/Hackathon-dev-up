import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Typography, TextField, Button } from '@mui/material';
import { login } from '../../services/authService';

export default function LoginPage() {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!username.trim() || !password.trim()) {
      setError('Please enter both username and password.');
      return;
    }
    login(username.trim());
    navigate('/', { replace: true });
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
        component="form"
        onSubmit={handleSubmit}
        sx={{
          width: 380,
          bgcolor: '#FFFFFF',
          borderRadius: '12px',
          boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
          p: '40px',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 0,
        }}
      >
        {/* Logo */}
        <Box
          sx={{
            width: 48,
            height: 48,
            borderRadius: '50%',
            bgcolor: '#01579B',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            mb: 2.5,
          }}
        >
          <Typography sx={{ color: '#FFFFFF', fontWeight: 700, fontSize: 14, userSelect: 'none' }}>
            CC
          </Typography>
        </Box>

        {/* Title */}
        <Typography
          sx={{ fontSize: 18, fontWeight: 700, color: '#222222', mb: 0.75, textAlign: 'center' }}
        >
          Sign in to CodeCompass
        </Typography>
        <Typography
          sx={{ fontSize: 12, color: '#53565A', mb: 3, textAlign: 'center' }}
        >
          Enter your credentials to continue
        </Typography>

        {/* Fields */}
        <TextField
          fullWidth
          label="Username"
          value={username}
          onChange={(e) => { setUsername(e.target.value); setError(''); }}
          size="small"
          sx={{ mb: 2 }}
          autoFocus
          autoComplete="username"
        />
        <TextField
          fullWidth
          label="Password"
          type="password"
          value={password}
          onChange={(e) => { setPassword(e.target.value); setError(''); }}
          size="small"
          sx={{ mb: error ? 1 : 2.5 }}
          autoComplete="current-password"
        />

        {error && (
          <Typography sx={{ fontSize: 11, color: '#ED6C02', mb: 1.5, alignSelf: 'flex-start' }}>
            {error}
          </Typography>
        )}

        {/* Submit */}
        <Button
          type="submit"
          fullWidth
          variant="contained"
          sx={{ py: 1.25, fontSize: 13, mb: 2.5 }}
        >
          Sign In
        </Button>

        {/* Footer */}
        <Typography sx={{ fontSize: 10, color: '#8F8F8F' }}>CodeCompass v1.0</Typography>
      </Box>
    </Box>
  );
}
