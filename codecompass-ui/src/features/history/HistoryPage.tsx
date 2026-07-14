import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Box,
  Typography,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TextField,
  InputAdornment,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { loadHistory } from '../chat/useChat';
import Badge, { BadgeVariant } from '../../components/common/Badge';
import { Conversation } from '../../types/chat';

const PAGE_SIZE = 10;

function formatRelativeTime(isoString: string): string {
  const date = new Date(isoString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} min ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
  if (diffDays === 1) return 'Yesterday';
  const month = date.toLocaleDateString('en-US', { month: 'short' });
  const day = date.getDate();
  return `${month} ${day}`;
}

function getConversationStatus(conv: Conversation): { label: string; variant: BadgeVariant } {
  const lastMsg = conv.messages[conv.messages.length - 1];
  // If last message is from user (no assistant response yet), it's "active"
  if (lastMsg?.role === 'user') return { label: 'Active', variant: 'active' };
  return { label: 'Completed', variant: 'neutral' };
}

export default function HistoryPage() {
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const history = loadHistory();
  const filtered = history.filter(
    (c) =>
      c.title.toLowerCase().includes(search.toLowerCase()) ||
      c.preview.toLowerCase().includes(search.toLowerCase())
  );
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paginated = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const handleResume = (conv: Conversation) => {
    navigate(`/chat/${conv.id}`);
  };

  return (
    <Box sx={{ p: 3, height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden', bgcolor: '#FFFFFF' }}>
      {/* Page heading */}
      <Typography variant="h2" sx={{ mb: 0.5 }}>
        Conversation History
      </Typography>
      <Typography sx={{ fontSize: 12, color: '#53565A', mb: 2.5 }}>
        Browse and resume past conversations
      </Typography>

      {/* Search bar */}
      <TextField
        size="small"
        placeholder="Search conversations..."
        value={search}
        onChange={(e) => { setSearch(e.target.value); setPage(1); }}
        InputProps={{
          startAdornment: (
            <InputAdornment position="start">
              <SearchIcon sx={{ fontSize: 16, color: '#8F8F8F' }} />
            </InputAdornment>
          ),
        }}
        sx={{
          width: 320,
          mb: 2,
          flexShrink: 0,
          '& .MuiOutlinedInput-root': { borderRadius: '8px', fontSize: 12 },
        }}
      />

      {/* Table */}
      <Box
        sx={{
          flex: 1,
          minHeight: 0,
          display: 'flex',
          flexDirection: 'column',
          width: '100%',
          overflow: 'hidden',
          borderRadius: '6px',
          boxShadow: '0 1px 3px rgba(0,0,0,0.04)',
        }}
      >
        <Box sx={{ flex: 1, overflowY: 'auto', bgcolor: '#FFFFFF', display: 'flex', flexDirection: 'column' }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell sx={{ minWidth: 280 }}>Conversation</TableCell>
              <TableCell sx={{ width: 100 }}>Messages</TableCell>
              <TableCell sx={{ width: 140 }}>Last Active</TableCell>
              <TableCell sx={{ width: 120 }}>Status</TableCell>
              <TableCell sx={{ width: 100 }}>Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {paginated.length === 0 ? null : (
              paginated.map((conv) => {
                const { label, variant } = getConversationStatus(conv);
                return (
                  <TableRow key={conv.id}>
                    <TableCell>
                      <Typography
                        sx={{
                          fontSize: 12,
                          fontWeight: 500,
                          color: '#01579B',
                          cursor: 'pointer',
                          '&:hover': { textDecoration: 'underline' },
                          mb: 0.25,
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          maxWidth: 280,
                        }}
                        onClick={() => handleResume(conv)}
                      >
                        {conv.title}
                      </Typography>
                      <Typography
                        sx={{
                          fontSize: 10,
                          color: '#53565A',
                          overflow: 'hidden',
                          textOverflow: 'ellipsis',
                          whiteSpace: 'nowrap',
                          maxWidth: 280,
                        }}
                      >
                        {conv.preview}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography sx={{ fontSize: 12, color: '#53565A' }}>
                        {conv.messages.length}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography sx={{ fontSize: 12, color: '#53565A' }}>
                        {formatRelativeTime(conv.updatedAt)}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Badge label={label} variant={variant} />
                    </TableCell>
                    <TableCell>
                      <Typography
                        sx={{
                          fontSize: 11,
                          fontWeight: 500,
                          color: '#01579B',
                          cursor: 'pointer',
                          whiteSpace: 'nowrap',
                          '&:hover': { textDecoration: 'underline' },
                        }}
                        onClick={() => handleResume(conv)}
                      >
                        Resume →
                      </Typography>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
        {paginated.length === 0 && (
          <Box sx={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Typography sx={{ fontSize: 13, color: '#8F8F8F' }}>
              {history.length === 0
                ? 'No conversations yet — start chatting to see history here'
                : 'No conversations match your search'}
            </Typography>
          </Box>
        )}
        </Box>

        {/* Pagination bar */}
        <Box
          sx={{
            bgcolor: '#FFFFFF',
            px: 2,
            py: 0.75,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <Typography sx={{ fontSize: 11, color: '#53565A' }}>
            {filtered.length} conversation{filtered.length !== 1 ? 's' : ''}
          </Typography>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <Typography sx={{ fontSize: 11, color: '#53565A' }}>Page</Typography>
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
              <Box
                key={p}
                onClick={() => setPage(p)}
                sx={{
                  width: 22,
                  height: 22,
                  borderRadius: '4px',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  cursor: 'pointer',
                  bgcolor: p === page ? '#01579B' : '#FFFFFF',
                  border: `1px solid ${p === page ? '#01579B' : '#D9D9D9'}`,
                  fontSize: 10,
                  fontWeight: 600,
                  color: p === page ? '#FFFFFF' : '#53565A',
                  transition: 'all 0.15s',
                  '&:hover': p !== page ? { bgcolor: '#F5F5F5' } : {},
                }}
              >
                {p}
              </Box>
            ))}
          </Box>
        </Box>
      </Box>
    </Box>
  );
}
