import React, { useState, useRef } from 'react';
import { Box } from '@mui/material';
import SendIcon from '@mui/icons-material/Send';

interface ChatInputProps {
  onSend: (message: string) => void;
  disabled?: boolean;
  placeholder?: string;
}

export default function ChatInput({ onSend, disabled, placeholder = 'Ask CodeCompass...' }: ChatInputProps) {
  const [value, setValue] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);

  const canSend = value.trim().length > 0 && !disabled;

  const handleSend = () => {
    const trimmed = value.trim();
    if (!trimmed || disabled) return;
    onSend(trimmed);
    setValue('');
    setTimeout(() => inputRef.current?.focus(), 0);
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <Box
      sx={{
        bgcolor: '#FFFFFF',
        borderTop: '1px solid #E7E7E7',
        px: 3,
        py: 1.5,
        flexShrink: 0,
      }}
    >
      <Box
        sx={{
          maxWidth: 700,
          mx: 'auto',
          display: 'flex',
          alignItems: 'center',
          gap: 1,
        }}
      >
        {/* Pill input */}
        <Box
          sx={{
            flex: 1,
            bgcolor: '#F5F5F5',
            border: '1px solid #D9D9D9',
            borderRadius: '21px',
            px: 2.5,
            py: '10px',
            display: 'flex',
            alignItems: 'center',
            transition: 'border-color 0.2s',
            '&:focus-within': { borderColor: '#01579B' },
          }}
        >
          <input
            ref={inputRef}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={placeholder}
            disabled={disabled}
            style={{
              border: 'none',
              outline: 'none',
              background: 'transparent',
              width: '100%',
              fontSize: 13,
              color: value ? '#222222' : '#8F8F8F',
              fontFamily: 'Inter, sans-serif',
              cursor: disabled ? 'not-allowed' : 'text',
            }}
          />
        </Box>

        {/* Send button */}
        <Box
          onClick={canSend ? handleSend : undefined}
          sx={{
            width: 34,
            height: 34,
            borderRadius: '50%',
            bgcolor: canSend ? '#01579B' : '#E7E7E7',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            cursor: canSend ? 'pointer' : 'default',
            transition: 'background-color 0.2s',
            flexShrink: 0,
            '&:hover': canSend ? { bgcolor: '#013654' } : {},
          }}
        >
          <SendIcon
            sx={{
              fontSize: 16,
              color: canSend ? '#FFFFFF' : '#8F8F8F',
              transform: 'rotate(-45deg)',
              mt: '-2px',
            }}
          />
        </Box>
      </Box>
    </Box>
  );
}
