export interface ValidationError {
  type: string;
  title: string;
  status: number;
  errors: Record<string, string[]>;
}
