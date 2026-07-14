import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Box, Tooltip, Typography } from '@mui/material';
import ChatBubbleOutlineIcon from '@mui/icons-material/ChatBubbleOutline';
import LibraryBooksOutlinedIcon from '@mui/icons-material/LibraryBooksOutlined';
import HistoryIcon from '@mui/icons-material/History';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';

export const SIDEBAR_COLLAPSED = 56;
export const SIDEBAR_EXPANDED = 200;

interface NavItem {
  icon: React.ReactNode;
  label: string;
  path: string;
}

const navItems: NavItem[] = [
  { icon: <ChatBubbleOutlineIcon sx={{ fontSize: 20 }} />, label: 'Chat', path: '/' },
  { icon: <LibraryBooksOutlinedIcon sx={{ fontSize: 20 }} />, label: 'Knowledge Sources', path: '/knowledge' },
  { icon: <HistoryIcon sx={{ fontSize: 20 }} />, label: 'History', path: '/history' },
];

interface SidebarProps {
  expanded: boolean;
  onToggle: () => void;
}

export default function Sidebar({ expanded, onToggle }: SidebarProps) {
  const navigate = useNavigate();
  const location = useLocation();

  const isActive = (item: NavItem) => {
    if (item.path === '/') return location.pathname === '/' || location.pathname.startsWith('/chat');
    return location.pathname.startsWith(item.path);
  };

  const width = expanded ? SIDEBAR_EXPANDED : SIDEBAR_COLLAPSED;

  return (
    <Box
      sx={{
        width,
        minHeight: '100vh',
        bgcolor: '#E6F3FA',
        borderRight: '1px solid #D9D9D9',
        display: 'flex',
        flexDirection: 'column',
        alignItems: expanded ? 'flex-start' : 'center',
        position: 'fixed',
        left: 0,
        top: 0,
        bottom: 0,
        zIndex: 1200,
        overflow: 'hidden',
        transition: 'width 0.2s ease',
      }}
    >
      {/* Logo row */}
      <Box
        onClick={() => navigate('/')}
        sx={{
          display: 'flex',
          alignItems: 'center',
          gap: 1.25,
          px: expanded ? 1.5 : 0,
          width: '100%',
          justifyContent: expanded ? 'flex-start' : 'center',
          py: '13px',
          cursor: 'pointer',
          flexShrink: 0,
        }}
      >
        <Box
          sx={{
            width: 30,
            height: 30,
            borderRadius: '50%',
            bgcolor: '#01579B',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            flexShrink: 0,
          }}
        >
          <Box
            component="span"
            sx={{ color: '#FFFFFF', fontWeight: 700, fontSize: 10, fontFamily: 'Inter, sans-serif', userSelect: 'none' }}
          >
            CC
          </Box>
        </Box>
        {expanded && (
          <Typography
            sx={{
              fontSize: 13,
              fontWeight: 700,
              color: '#222222',
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              userSelect: 'none',
            }}
          >
            CodeCompass
          </Typography>
        )}
      </Box>

      {/* Nav items */}
      <Box sx={{ flex: 1, width: '100%', px: expanded ? 0.75 : 0, display: 'flex', flexDirection: 'column', alignItems: expanded ? 'stretch' : 'center' }}>
        {navItems.map((item) => {
          const active = isActive(item);
          const content = (
            <Box
              key={item.path}
              onClick={() => navigate(item.path)}
              sx={{
                display: 'flex',
                alignItems: 'center',
                gap: expanded ? 1.25 : 0,
                px: expanded ? 1.5 : 0,
                width: expanded ? '100%' : 48,
                height: 36,
                borderRadius: '6px',
                cursor: 'pointer',
                bgcolor: active ? '#D3E7F2' : 'transparent',
                color: active ? '#013654' : '#53565A',
                mb: 0.5,
                justifyContent: expanded ? 'flex-start' : 'center',
                transition: 'background-color 0.15s, color 0.15s',
                flexShrink: 0,
                '&:hover': { bgcolor: active ? '#D3E7F2' : '#D3E7F2' },
              }}
            >
              {item.icon}
              {expanded && (
                <Typography
                  sx={{
                    fontSize: 12,
                    fontWeight: active ? 600 : 400,
                    color: 'inherit',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                  }}
                >
                  {item.label}
                </Typography>
              )}
            </Box>
          );

          return expanded ? (
            <React.Fragment key={item.path}>{content}</React.Fragment>
          ) : (
            <Tooltip key={item.path} title={item.label} placement="right" arrow>
              {content}
            </Tooltip>
          );
        })}
      </Box>

      {/* Expand / collapse toggle */}
      <Box
        onClick={onToggle}
        sx={{
          width: expanded ? '100%' : 48,
          height: 36,
          display: 'flex',
          alignItems: 'center',
          justifyContent: expanded ? 'flex-end' : 'center',
          px: expanded ? 1.5 : 0,
          mb: 1,
          cursor: 'pointer',
          color: '#53565A',
          flexShrink: 0,
          borderRadius: '6px',
          '&:hover': { bgcolor: '#D3E7F2', color: '#013654' },
          transition: 'background-color 0.15s, color 0.15s',
        }}
      >
        {expanded ? (
          <ChevronLeftIcon sx={{ fontSize: 20 }} />
        ) : (
          <ChevronRightIcon sx={{ fontSize: 20 }} />
        )}
      </Box>
    </Box>
  );
}
