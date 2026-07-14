export interface HealthService {
  name: string;
  status: string;
  responseTimeMs: number;
}

export interface HealthResponse {
  status: string;
  services: HealthService[];
}
