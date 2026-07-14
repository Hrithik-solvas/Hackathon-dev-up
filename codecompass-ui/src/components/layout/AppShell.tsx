import React, { useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Box, Typography, Menu, MenuItem } from '@mui/material';
import Sidebar, { SIDEBAR_COLLAPSED, SIDEBAR_EXPANDED } from './Sidebar';
import { getUser, logout } from '../../services/authService';

function getPageTitle(pathname: string): string {
  if (pathname === '/' || pathname.startsWith('/chat')) return 'Chat';
  if (pathname.startsWith('/knowledge')) return 'Knowledge Sources';
  if (pathname.startsWith('/history')) return 'Conversation History';
  return 'Chat';
}

export default function AppShell() {
  const location = useLocation();
  const navigate = useNavigate();
  const pageTitle = getPageTitle(location.pathname);
  const [sidebarExpanded, setSidebarExpanded] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  const user = getUser();
  const initial = user?.username?.[0]?.toUpperCase() ?? '?';

  const sidebarWidth = sidebarExpanded ? SIDEBAR_EXPANDED : SIDEBAR_COLLAPSED;

  const handleLogout = () => {
    setMenuAnchor(null);
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: '#F5F5F5' }}>
      <Sidebar expanded={sidebarExpanded} onToggle={() => setSidebarExpanded((v) => !v)} />

      <Box
        sx={{
          ml: `${sidebarWidth}px`,
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          minHeight: '100vh',
          overflow: 'hidden',
          transition: 'margin-left 0.2s ease',
        }}
      >
        {/* Top Bar */}
        <Box
          sx={{
            height: 52,
            bgcolor: '#FFFFFF',
            borderBottom: '1px solid #E7E7E7',
            display: 'flex',
            alignItems: 'center',
            px: 2.5,
            flexShrink: 0,
            position: 'sticky',
            top: 0,
            zIndex: 1100,
          }}
        >
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: '#222222', flex: 1 }}>
            {pageTitle}
          </Typography>

          {/* User avatar */}
          <Box
            onClick={(e) => setMenuAnchor(e.currentTarget)}
            sx={{
              width: 32,
              height: 32,
              borderRadius: '50%',
              bgcolor: '#01579B',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              cursor: 'pointer',
              flexShrink: 0,
              '&:hover': { bgcolor: '#013654' },
              transition: 'background-color 0.15s',
            }}
          >
            <Typography sx={{ color: '#FFFFFF', fontWeight: 700, fontSize: 12, userSelect: 'none' }}>
              {initial}
            </Typography>
          </Box>

          <Menu
            anchorEl={menuAnchor}
            open={Boolean(menuAnchor)}
            onClose={() => setMenuAnchor(null)}
            anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
            transformOrigin={{ vertical: 'top', horizontal: 'right' }}
            slotProps={{
              paper: {
                sx: {
                  mt: 0.5,
                  minWidth: 160,
                  boxShadow: '0 4px 12px rgba(0,0,0,0.1)',
                  borderRadius: '8px',
                },
              },
            }}
          >
            {user && (
              <Box sx={{ px: 2, py: 1, borderBottom: '1px solid #E7E7E7' }}>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: '#222222' }}>
                  {user.username}
                </Typography>
              </Box>
            )}
            <MenuItem
              onClick={handleLogout}
              sx={{ fontSize: 13, color: '#222222', py: 1 }}
            >
              Logout
            </MenuItem>
          </Menu>
        </Box>

        {/* Page content */}
        <Box sx={{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}
