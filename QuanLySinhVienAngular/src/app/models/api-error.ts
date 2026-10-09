// models/api-error.ts
export interface ApiError {
  statusCode: number;
  message: string;
  details?: string | null;
  errors?: Record<string, string[]> | null;
}

