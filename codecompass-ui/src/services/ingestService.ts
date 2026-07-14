import api from './api';
import { IngestResponse } from '../types/ingest';

export async function ingestDocs(files: File[]): Promise<IngestResponse> {
  const formData = new FormData();
  files.forEach((file) => formData.append('files', file));
  const { data } = await api.post<IngestResponse>('/Ingest/docs', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}

export async function ingestCode(files: File[], repositoryName?: string): Promise<IngestResponse> {
  const formData = new FormData();
  files.forEach((file) => formData.append('files', file));
  if (repositoryName) formData.append('repositoryName', repositoryName);
  const { data } = await api.post<IngestResponse>('/Ingest/code', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
  return data;
}
