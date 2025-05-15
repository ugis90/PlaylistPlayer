import { Component, ErrorInfo, ReactNode } from "react";
import { AlertTriangle } from "lucide-react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

class MapErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error("Map component error:", error, errorInfo);
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        this.props.fallback || (
          <div
            className="p-4 bg-red-50 border border-red-200 rounded-lg text-red-700 flex flex-col items-center justify-center"
            style={{ minHeight: "400px" }}
          >
            <AlertTriangle className="h-12 w-12 text-red-500 mb-4" />
            <h2 className="text-lg font-semibold mb-2">Map Error</h2>
            <p className="text-sm text-center mb-4">
              There was an error loading the map component.
            </p>
            <details className="text-xs text-red-600 bg-red-50 p-2 rounded w-full">
              <summary>Error details</summary>
              <pre className="mt-2 p-2 bg-red-100 rounded overflow-auto max-h-32">
                {this.state.error?.toString()}
              </pre>
            </details>
            <button
              onClick={() => this.setState({ hasError: false, error: null })}
              className="mt-4 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
            >
              Try Again
            </button>
          </div>
        )
      );
    }

    return this.props.children;
  }
}

export default MapErrorBoundary;
