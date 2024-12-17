import { Card, CardContent } from "./ui/card";
import { Component, ErrorInfo, ReactNode } from "react";
import { Button } from "./ui/button.tsx";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
  };

  public static getDerivedStateFromError(_: Error): State {
    return { hasError: true };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Uncaught error:", error, errorInfo);
  }

  public render() {
    if (this.state.hasError) {
      return (
        this.props.fallback || (
          <div className="p-4 text-center">
            <h2 className="text-xl font-bold mb-2">Something went wrong</h2>
            <Button onClick={() => this.setState({ hasError: false })}>
              Try again
            </Button>
          </div>
        )
      );
    }

    return this.props.children;
  }
}

export interface ValidationError {
  errors: Record<string, string[]>;
  detail: string;
}

export function ErrorDisplay({ error }: { error: ValidationError }) {
  return (
    <Card className="bg-red-50 border-red-200">
      <CardContent className="p-4">
        <h3 className="text-red-700 font-medium">{error.detail}</h3>
        {error.errors && (
          <ul className="mt-2 list-disc pl-5">
            {Object.entries(error.errors).map(([field, messages]) => (
              <li key={field} className="text-red-600">
                {field}: {messages.join(", ")}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
