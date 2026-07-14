export interface ChatRequest {
  question: string;
  sessionId?: string;
}

export interface Citation {
  sourceUri: string;
  chunkContent: string;
  relevanceScore: number;
}

export interface ChatResponse {
  answer: string;
  citations: Citation[];
  sessionId: string;
}

export interface Message {
  id: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  isStreaming?: boolean;
}

export interface Conversation {
  id: string;
  title: string;
  preview: string;
  messages: Message[];
  createdAt: string;
  updatedAt: string;
}
