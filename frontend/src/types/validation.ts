export interface ValidationError {
  type: string;
  title: string;
  status: number;
  detail?: string;
  errors: Record<string, string[]>;
}
