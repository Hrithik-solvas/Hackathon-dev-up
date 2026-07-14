import React, { useState } from 'react';
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
  Snackbar,
  Alert,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import FileDropZone from '../../components/common/FileDropZone';
import Badge from '../../components/common/Badge';
import { useIngest } from './useIngest';
import { IndexedSource } from '../../types/ingest';

const PAGE_SIZE = 10;

export default function KnowledgePage() {
  const { sources, isUploading, uploadError, uploadSuccess, uploadFiles, clearSuccess, clearError } =
    useIngest();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const filtered = sources.filter((s) =>
    s.name.toLowerCase().includes(search.toLowerCase())
  );
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paginated = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const totalChunks = sources
    .filter((s) => s.status === 'Indexed')
    .reduce((sum, s) => sum + (s.chunks ?? 0), 0);

  const handleFiles = (files: File[]) => {
    setPage(1);
    uploadFiles(files);
  };

  return (
    <Box sx={{ p: 3, overflowY: 'auto', height: '100%' }}>
      {/* Page heading */}
      <Typography variant="h2" sx={{ mb: 0.5 }}>
        Knowledge Sources
      </Typography>
      <Typography sx={{ fontSize: 12, color: '#53565A', mb: 3 }}>
        Upload documentation and code to build your knowledge base
      </Typography>

      {/* Upload zone */}
      <Box sx={{ maxWidth: 600, mb: 3, mx: 'auto' }}>
        <FileDropZone
          onFiles={handleFiles}
          accept=".md,.txt,.cs,.ts,.tsx,.js,.jsx,.py,.go,.rs,.java,.pdf"
          disabled={isUploading}
        />
      </Box>

      {/* Sources table section */}
      <Box sx={{ width: '100%' }}>
        {/* Section header */}
        <Box sx={{ display: 'flex', alignItems: 'center', mb: 1.5, gap: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1.5 }}>
            <Typography variant="h4">Indexed Sources</Typography>
            <Typography sx={{ fontSize: 12, color: '#8F8F8F' }}>
              {sources.length} source{sources.length !== 1 ? 's' : ''} · {totalChunks} chunks
            </Typography>
          </Box>
          <Box sx={{ flex: 1 }} />
          <TextField
            size="small"
            placeholder="Search..."
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
              width: 160,
              '& .MuiOutlinedInput-root': { borderRadius: '6px', fontSize: 11 },
            }}
          />
        </Box>

        {/* Table */}
        <Box
          sx={{
            borderRadius: '6px',
            overflow: 'hidden',
            boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
          }}
        >
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Source Name</TableCell>
                <TableCell sx={{ width: 120 }}>Type</TableCell>
                <TableCell sx={{ width: 90 }}>Chunks</TableCell>
                <TableCell sx={{ width: 110 }}>Date</TableCell>
                <TableCell sx={{ width: 110 }}>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {paginated.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} sx={{ textAlign: 'center', py: 4, color: '#8F8F8F' }}>
                    {sources.length === 0
                      ? 'No sources yet — upload files above to get started'
                      : 'No sources match your search'}
                  </TableCell>
                </TableRow>
              ) : (
                paginated.map((source) => (
                  <SourceRow key={source.id} source={source} />
                ))
              )}
            </TableBody>
          </Table>

          {/* Pagination bar */}
          <Box
            sx={{
              bgcolor: '#FFFFFF',
              borderTop: '1px solid #E7E7E7',
              px: 2,
              py: 0.75,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <Typography sx={{ fontSize: 11, color: '#53565A' }}>
              {filtered.length} source{filtered.length !== 1 ? 's' : ''}
            </Typography>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <Typography sx={{ fontSize: 11, color: '#53565A', mr: 1 }}>
                Rows per page: {PAGE_SIZE}
              </Typography>
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

      {/* Snackbars */}
      <Snackbar open={!!uploadSuccess} autoHideDuration={4000} onClose={clearSuccess}>
        <Alert severity="success" onClose={clearSuccess} sx={{ width: '100%' }}>
          {uploadSuccess}
        </Alert>
      </Snackbar>
      <Snackbar open={!!uploadError} autoHideDuration={6000} onClose={clearError}>
        <Alert severity="error" onClose={clearError} sx={{ width: '100%' }}>
          {uploadError}
        </Alert>
      </Snackbar>
    </Box>
  );
}

function SourceRow({ source }: { source: IndexedSource }) {
  const badgeVariant =
    source.status === 'Indexed' ? 'success' : 'warning';

  return (
    <TableRow>
      <TableCell>
        <Typography
          sx={{
            fontSize: 12,
            color: source.status === 'Indexed' ? '#01579B' : '#222222',
            fontWeight: source.status === 'Indexed' ? 400 : 400,
          }}
        >
          {source.name}
        </Typography>
      </TableCell>
      <TableCell>
        <Typography sx={{ fontSize: 11, color: '#53565A' }}>{source.type}</Typography>
      </TableCell>
      <TableCell>
        <Typography sx={{ fontSize: 11, color: '#53565A' }}>
          {source.chunks !== null ? source.chunks : '—'}
        </Typography>
      </TableCell>
      <TableCell>
        <Typography sx={{ fontSize: 11, color: '#53565A' }}>{source.date}</Typography>
      </TableCell>
      <TableCell>
        <Badge label={source.status} variant={badgeVariant} />
      </TableCell>
    </TableRow>
  );
}
