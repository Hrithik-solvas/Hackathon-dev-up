import api from './api';
import { ChatRequest, ChatResponse } from '../types/chat';

export async function sendMessage(request: ChatRequest): Promise<ChatResponse> {
  const { data } = await api.post<ChatResponse>('/Chat', request);
  return data;
}
