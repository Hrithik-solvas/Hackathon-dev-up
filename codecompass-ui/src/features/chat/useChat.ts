import { useState, useCallback, useRef } from 'react';
import { Message, Conversation, Citation } from '../../types/chat';
import { sendMessage } from '../../services/chatService';

const HISTORY_KEY = 'codecompass_history';

function genId(): string {
  return crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).slice(2);
}

export function loadHistory(): Conversation[] {
  try {
    const raw = localStorage.getItem(HISTORY_KEY);
    return raw ? (JSON.parse(raw) as Conversation[]) : [];
  } catch {
    return [];
  }
}

function saveHistory(conversations: Conversation[]) {
  localStorage.setItem(HISTORY_KEY, JSON.stringify(conversations));
}

async function simulateStreaming(
  fullText: string,
  onToken: (partial: string) => void
): Promise<void> {
  const words = fullText.split(' ');
  let revealed = '';
  for (const word of words) {
    revealed += (revealed ? ' ' : '') + word;
    onToken(revealed);
    await new Promise<void>((r) => setTimeout(r, 30));
  }
}

export function useChat(initialSessionId?: string) {
  const [messages, setMessages] = useState<Message[]>([]);
  const [sessionId, setSessionId] = useState<string | undefined>(initialSessionId);
  const [isLoading, setIsLoading] = useState(false);
  const [latestCitations, setLatestCitations] = useState<Citation[]>([]);

  // Keep a ref so closure inside simulateStreaming can access latest messages
  const messagesRef = useRef<Message[]>(messages);
  messagesRef.current = messages;

  const sendUserMessage = useCallback(
    async (text: string) => {
      const userMsg: Message = { id: genId(), role: 'user', content: text };
      const assistantMsgId = genId();
      const assistantMsg: Message = {
        id: assistantMsgId,
        role: 'assistant',
        content: '',
        isStreaming: true,
        citations: [],
      };

      setMessages((prev) => [...prev, userMsg, assistantMsg]);
      setIsLoading(true);

      try {
        const response = await sendMessage({ question: text, sessionId });

        const newSessionId = response.sessionId;
        if (!sessionId) setSessionId(newSessionId);
        setLatestCitations(response.citations);

        // Stream the answer word by word
        await simulateStreaming(response.answer, (partial) => {
          setMessages((prev) =>
            prev.map((m) => (m.id === assistantMsgId ? { ...m, content: partial } : m))
          );
        });

        // Finalize message with citations
        const finalAssistantMsg: Message = {
          id: assistantMsgId,
          role: 'assistant',
          content: response.answer,
          citations: response.citations,
          isStreaming: false,
        };
        setMessages((prev) =>
          prev.map((m) => (m.id === assistantMsgId ? finalAssistantMsg : m))
        );

        // Persist conversation to localStorage
        const history = loadHistory();
        const existingIdx = history.findIndex((c) => c.id === newSessionId);
        const updatedMessages = [
          ...messagesRef.current.filter(
            (m) => m.id !== assistantMsgId
          ),
          userMsg,
          finalAssistantMsg,
        ];

        if (existingIdx >= 0) {
          history[existingIdx] = {
            ...history[existingIdx],
            messages: updatedMessages,
            updatedAt: new Date().toISOString(),
          };
        } else {
          const title = text.length > 60 ? text.slice(0, 60) + '...' : text;
          history.unshift({
            id: newSessionId,
            title,
            preview: text,
            messages: updatedMessages,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          });
        }
        saveHistory(history);
      } catch {
        setMessages((prev) =>
          prev.map((m) =>
            m.id === assistantMsgId
              ? {
                  ...m,
                  content: 'Sorry, something went wrong. Please check the API and try again.',
                  isStreaming: false,
                }
              : m
          )
        );
      } finally {
        setIsLoading(false);
      }
    },
    [sessionId]
  );

  const loadConversation = useCallback((conversation: Conversation) => {
    setMessages(conversation.messages);
    setSessionId(conversation.id);
    const last = conversation.messages
      .slice()
      .reverse()
      .find((m) => m.role === 'assistant' && m.citations);
    setLatestCitations(last?.citations ?? []);
  }, []);

  const startNewChat = useCallback(() => {
    setMessages([]);
    setSessionId(undefined);
    setLatestCitations([]);
  }, []);

  return {
    messages,
    sessionId,
    isLoading,
    latestCitations,
    sendUserMessage,
    loadConversation,
    startNewChat,
  };
}
