import api from './api';
import { HealthResponse } from '../types/health';

export async function getHealth(): Promise<HealthResponse> {
  const { data } = await api.get<HealthResponse>('/Health');
  return data;
}
