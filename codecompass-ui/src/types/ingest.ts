export interface IngestResponse {
  chunksIngested: number;
  sourcesProcessed: number;
}

export interface IndexedSource {
  id: string;
  name: string;
  type: 'Document' | 'Code';
  chunks: number | null;
  date: string;
  status: 'Indexed' | 'Processing';
}
