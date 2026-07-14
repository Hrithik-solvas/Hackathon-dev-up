import React, { useState } from 'react';
import { Box, Typography, IconButton, Tooltip } from '@mui/material';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CheckIcon from '@mui/icons-material/Check';
import { Message } from '../../types/chat';

interface CodeBlockProps {
  children: string;
  className?: string;
}

function CodeBlock({ children, className }: CodeBlockProps) {
  const [copied, setCopied] = useState(false);
  const language = className?.replace('language-', '') ?? 'text';

  const handleCopy = () => {
    navigator.clipboard.writeText(children).catch(() => {});
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Box sx={{ position: 'relative', my: 1 }}>
      <Tooltip title={copied ? 'Copied!' : 'Copy code'}>
        <IconButton
          size="small"
          onClick={handleCopy}
          sx={{
            position: 'absolute',
            top: 6,
            right: 6,
            color: '#8F8F8F',
            bgcolor: 'rgba(0,0,0,0.2)',
            '&:hover': { bgcolor: 'rgba(0,0,0,0.35)', color: '#E0E0E0' },
            zIndex: 1,
            width: 24,
            height: 24,
          }}
        >
          {copied ? <CheckIcon sx={{ fontSize: 13 }} /> : <ContentCopyIcon sx={{ fontSize: 13 }} />}
        </IconButton>
      </Tooltip>
      <SyntaxHighlighter
        language={language}
        style={vscDarkPlus}
        customStyle={{
          borderRadius: 6,
          fontSize: 11,
          margin: 0,
          background: '#222222',
          padding: '14px 16px',
        }}
      >
        {children}
      </SyntaxHighlighter>
    </Box>
  );
}

interface ChatMessageProps {
  message: Message;
}

export default function ChatMessage({ message }: ChatMessageProps) {
  const isUser = message.role === 'user';

  if (isUser) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'flex-end', mb: 2 }}>
        <Box
          sx={{
            bgcolor: '#01579B',
            color: '#FFFFFF',
            borderRadius: '18px',
            px: 2.5,
            py: 1.25,
            maxWidth: '70%',
            fontSize: 13,
            lineHeight: 1.55,
            fontFamily: 'Inter, sans-serif',
          }}
        >
          {message.content}
        </Box>
      </Box>
    );
  }

  return (
    <Box sx={{ display: 'flex', justifyContent: 'flex-start', mb: 2 }}>
      <Box
        sx={{
          bgcolor: '#FFFFFF',
          border: '1px solid #E7E7E7',
          borderRadius: '10px',
          px: 2.5,
          py: 2,
          maxWidth: '88%',
          boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
          '& p': { mt: 0, mb: 1 },
          '& p:last-child': { mb: 0 },
          '& h1,h2,h3,h4': { color: '#222222', mt: 1, mb: 0.5 },
          '& ul,ol': { pl: 2.5, mb: 1 },
          '& li': { mb: 0.25 },
        }}
      >
        <ReactMarkdown
          remarkPlugins={[remarkGfm]}
          components={{
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            code({ className, children, ...props }: any) {
              const isInline = !className && !String(children).includes('\n');
              if (isInline) {
                return (
                  <Box
                    component="code"
                    sx={{
                      fontFamily: 'JetBrains Mono, monospace',
                      fontSize: 11,
                      bgcolor: '#F5F5F5',
                      px: '4px',
                      py: '2px',
                      borderRadius: '3px',
                      color: '#222222',
                    }}
                    {...props}
                  >
                    {children}
                  </Box>
                );
              }
              return (
                <CodeBlock className={className}>
                  {String(children).replace(/\n$/, '')}
                </CodeBlock>
              );
            },
            p({ children }) {
              return (
                <Typography
                  component="p"
                  sx={{ fontSize: 13, color: '#53565A', mb: 1, lineHeight: 1.65 }}
                >
                  {children}
                </Typography>
              );
            },
            strong({ children }) {
              return (
                <Box component="strong" sx={{ fontWeight: 700, color: '#222222' }}>
                  {children}
                </Box>
              );
            },
            h3({ children }) {
              return (
                <Typography sx={{ fontSize: 13, fontWeight: 700, color: '#222222', mb: 0.5, mt: 1 }}>
                  {children}
                </Typography>
              );
            },
          }}
        >
          {message.content}
        </ReactMarkdown>

        {/* Citation badges */}
        {message.citations && message.citations.length > 0 && (
          <Box sx={{ mt: 1.5, pt: 1.5, borderTop: '1px solid #F0F0F0' }}>
            <Typography
              sx={{
                fontSize: 10,
                fontWeight: 600,
                color: '#53565A',
                letterSpacing: '0.3px',
                mb: 0.75,
                textTransform: 'uppercase',
              }}
            >
              Sources
            </Typography>
            <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.75 }}>
              {message.citations.map((citation, i) => {
                const filename =
                  citation.sourceUri.split(/[/\\]/).pop() ?? citation.sourceUri;
                const isDoc =
                  filename.endsWith('.md') ||
                  filename.endsWith('.txt') ||
                  filename.endsWith('.pdf');
                return (
                  <Box
                    key={i}
                    sx={{
                      bgcolor: '#EAF2EA',
                      borderRadius: '12px',
                      px: 1.5,
                      py: 0.5,
                      display: 'inline-flex',
                      alignItems: 'center',
                    }}
                  >
                    <Typography sx={{ fontSize: 10, fontWeight: 500, color: '#2E7D32' }}>
                      {isDoc ? '📄' : '💻'} {filename}
                    </Typography>
                  </Box>
                );
              })}
            </Box>
          </Box>
        )}
      </Box>
    </Box>
  );
}
