import React, { useEffect, useRef } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Box, Typography, Button } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ChatMessage from '../../components/chat/ChatMessage';
import ChatInput from '../../components/chat/ChatInput';
import TypingIndicator from '../../components/chat/TypingIndicator';
import CitationCard from '../../components/chat/CitationCard';
import { useChat, loadHistory } from './useChat';
import { Conversation } from '../../types/chat';

const SUGGESTED_QUESTIONS = [
  { text: 'How does authentication work in our API?', category: 'Security' },
  { text: 'Explain the RAG pipeline architecture', category: 'Architecture' },
  { text: 'What vector stores are supported?', category: 'Infrastructure' },
  { text: 'How to ingest new documentation?', category: 'Workflow' },
];

function groupConversations(conversations: Conversation[]) {
  const now = new Date();
  const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const yesterdayStart = new Date(todayStart.getTime() - 86400000);

  const today: Conversation[] = [];
  const yesterday: Conversation[] = [];
  const older: Conversation[] = [];

  for (const c of conversations) {
    const d = new Date(c.updatedAt);
    if (d >= todayStart) today.push(c);
    else if (d >= yesterdayStart) yesterday.push(c);
    else older.push(c);
  }
  return { today, yesterday, older };
}

export default function ChatPage() {
  const { sessionId: urlSessionId } = useParams<{ sessionId?: string }>();
  const navigate = useNavigate();
  const {
    messages,
    sessionId,
    isLoading,
    latestCitations,
    sendUserMessage,
    loadConversation,
    startNewChat,
  } = useChat(urlSessionId);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const history = loadHistory();
  const groups = groupConversations(history);
  const hasMessages = messages.length > 0;

  // Auto-scroll to bottom on new message
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  // Update URL when session starts
  useEffect(() => {
    if (sessionId && !urlSessionId) {
      navigate(`/chat/${sessionId}`, { replace: true });
    }
  }, [sessionId, urlSessionId, navigate]);

  const handleSuggestedQuestion = (question: string) => {
    sendUserMessage(question);
  };

  const handleNewChat = () => {
    startNewChat();
    navigate('/', { replace: true });
  };

  const handleResumeConversation = (conversation: Conversation) => {
    loadConversation(conversation);
    navigate(`/chat/${conversation.id}`, { replace: true });
  };

  // ─── Empty state ───────────────────────────────────────────────────────────
  if (!hasMessages) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
        {/* Welcome content */}
        <Box
          sx={{
            flex: 1,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            px: 3,
            pb: 4,
          }}
        >
          {/* Logo circle */}
          <Box
            sx={{
              width: 64,
              height: 64,
              borderRadius: '50%',
              bgcolor: '#E6F3FA',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              mb: 2,
            }}
          >
            <Box
              sx={{
                width: 36,
                height: 36,
                borderRadius: '50%',
                bgcolor: '#01579B',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <Typography sx={{ color: '#FFFFFF', fontWeight: 700, fontSize: 11 }}>CC</Typography>
            </Box>
          </Box>

          <Typography variant="h1" sx={{ mb: 1, textAlign: 'center' }}>
            How can I help you today?
          </Typography>
          <Typography sx={{ fontSize: 13, color: '#53565A', mb: 4, textAlign: 'center' }}>
            Ask questions grounded in your documentation and source code
          </Typography>

          {/* Suggested question cards — 2×2 grid */}
          <Box
            sx={{
              display: 'grid',
              gridTemplateColumns: 'repeat(2, 228px)',
              gap: 2,
            }}
          >
            {SUGGESTED_QUESTIONS.map((q) => (
              <Box
                key={q.text}
                onClick={() => handleSuggestedQuestion(q.text)}
                sx={{
                  bgcolor: '#FFFFFF',
                  borderRadius: '10px',
                  p: 2,
                  boxShadow: '0 2px 8px rgba(0,0,0,0.05)',
                  cursor: 'pointer',
                  transition: 'box-shadow 0.2s, transform 0.1s',
                  '&:hover': {
                    boxShadow: '0 4px 16px rgba(0,0,0,0.1)',
                    transform: 'translateY(-1px)',
                  },
                }}
              >
                <Typography sx={{ fontSize: 13, fontWeight: 500, color: '#222222', mb: 1 }}>
                  {q.text}
                </Typography>
                <Typography sx={{ fontSize: 10, color: '#8F8F8F' }}>{q.category}</Typography>
              </Box>
            ))}
          </Box>
        </Box>

        <ChatInput onSend={sendUserMessage} disabled={isLoading} />
      </Box>
    );
  }

  // ─── Active chat state ─────────────────────────────────────────────────────
  return (
    <Box sx={{ display: 'flex', flex: 1, overflow: 'hidden', height: '100%' }}>
      {/* Left: conversation list panel */}
      <Box
        sx={{
          width: 240,
          flexShrink: 0,
          bgcolor: '#FFFFFF',
          borderRight: '1px solid #E7E7E7',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        {/* New chat button */}
        <Box sx={{ p: 1.25 }}>
          <Button
            fullWidth
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleNewChat}
            sx={{ borderRadius: '6px', fontSize: 12 }}
          >
            New Chat
          </Button>
        </Box>

        {/* Conversation list */}
        <Box sx={{ flex: 1, overflowY: 'auto', px: 0.75 }}>
          {groups.today.length > 0 && (
            <>
              <Typography
                sx={{
                  fontSize: 10,
                  fontWeight: 600,
                  color: '#8F8F8F',
                  letterSpacing: '0.5px',
                  px: 1,
                  py: 0.75,
                  textTransform: 'uppercase',
                }}
              >
                Today
              </Typography>
              {groups.today.map((conv) => (
                <ConvItem
                  key={conv.id}
                  conv={conv}
                  active={conv.id === sessionId}
                  onSelect={() => handleResumeConversation(conv)}
                />
              ))}
            </>
          )}
          {groups.yesterday.length > 0 && (
            <>
              <Typography
                sx={{
                  fontSize: 10,
                  fontWeight: 600,
                  color: '#8F8F8F',
                  letterSpacing: '0.5px',
                  px: 1,
                  py: 0.75,
                  textTransform: 'uppercase',
                }}
              >
                Yesterday
              </Typography>
              {groups.yesterday.map((conv) => (
                <ConvItem
                  key={conv.id}
                  conv={conv}
                  active={conv.id === sessionId}
                  onSelect={() => handleResumeConversation(conv)}
                />
              ))}
            </>
          )}
          {groups.older.length > 0 && (
            <>
              <Typography
                sx={{
                  fontSize: 10,
                  fontWeight: 600,
                  color: '#8F8F8F',
                  letterSpacing: '0.5px',
                  px: 1,
                  py: 0.75,
                  textTransform: 'uppercase',
                }}
              >
                Earlier
              </Typography>
              {groups.older.map((conv) => (
                <ConvItem
                  key={conv.id}
                  conv={conv}
                  active={conv.id === sessionId}
                  onSelect={() => handleResumeConversation(conv)}
                />
              ))}
            </>
          )}
        </Box>
      </Box>

      {/* Center: messages */}
      <Box
        sx={{
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          minWidth: 0,
        }}
      >
        <Box
          sx={{
            flex: 1,
            overflowY: 'auto',
            px: 3,
            pt: 2.5,
            pb: 1,
          }}
        >
          {messages.map((msg) => (
            <Box key={msg.id}>
              {msg.isStreaming && msg.role === 'assistant' && msg.content === '' ? (
                <Box sx={{ mb: 2 }}>
                  <TypingIndicator />
                </Box>
              ) : (
                <ChatMessage message={msg} />
              )}
            </Box>
          ))}
          <div ref={messagesEndRef} />
        </Box>

        <ChatInput onSend={sendUserMessage} disabled={isLoading} />
      </Box>

      {/* Right: citations panel */}
      <Box
        sx={{
          width: 220,
          flexShrink: 0,
          bgcolor: '#FFFFFF',
          borderLeft: '1px solid #E7E7E7',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
        }}
      >
        <Box sx={{ px: 2, pt: 2, pb: 1 }}>
          <Typography
            sx={{
              fontSize: 11,
              fontWeight: 600,
              color: '#53565A',
              letterSpacing: '0.3px',
              textTransform: 'uppercase',
            }}
          >
            Citations
          </Typography>
          <Typography sx={{ fontSize: 11, color: '#8F8F8F', mt: 0.25 }}>
            {latestCitations.length} source{latestCitations.length !== 1 ? 's' : ''}
          </Typography>
        </Box>
        <Box sx={{ flex: 1, overflowY: 'auto', px: 2, pb: 2 }}>
          {latestCitations.map((citation, i) => (
            <CitationCard key={i} citation={citation} />
          ))}
          {latestCitations.length === 0 && (
            <Typography sx={{ fontSize: 11, color: '#8F8F8F', fontStyle: 'italic' }}>
              Sources will appear here
            </Typography>
          )}
        </Box>
      </Box>
    </Box>
  );
}

function ConvItem({
  conv,
  active,
  onSelect,
}: {
  conv: Conversation;
  active: boolean;
  onSelect: () => void;
}) {
  return (
    <Box
      onClick={onSelect}
      sx={{
        px: 1.5,
        py: 1.25,
        borderRadius: '6px',
        cursor: 'pointer',
        bgcolor: active ? '#E6F3FA' : 'transparent',
        mb: 0.25,
        transition: 'background-color 0.15s',
        '&:hover': { bgcolor: active ? '#E6F3FA' : '#F5F5F5' },
      }}
    >
      <Typography
        sx={{
          fontSize: 12,
          fontWeight: active ? 600 : 500,
          color: active ? '#013654' : '#222222',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}
      >
        {conv.title}
      </Typography>
      <Typography
        sx={{
          fontSize: 11,
          color: active ? '#53565A' : '#53565A',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}
      >
        {conv.preview}
      </Typography>
    </Box>
  );
}
