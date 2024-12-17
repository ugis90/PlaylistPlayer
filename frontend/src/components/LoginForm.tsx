import { useState } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import { Button } from "./ui/button";
import { useAuth } from "../auth/AuthContext";
import { ErrorDisplay, ValidationError } from "./ErrorBoundary";

export function LoginForm() {
  const [formData, setFormData] = useState({
    userName: "",
    password: "",
  });
  const [error, setError] = useState<ValidationError | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      await login(formData.userName, formData.password);
      navigate("/");
    } catch (err: any) {
      if (err.response?.status === 422) {
        setError(err.response.data);
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto mt-8 p-6 bg-white rounded-lg shadow">
      <h2 className="text-2xl font-bold mb-6">Login</h2>
      {location.state?.message && (
        <div className="mb-4 p-2 bg-green-50 text-green-700 rounded">
          {location.state.message}
        </div>
      )}
      {error && <ErrorDisplay error={error} />}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium mb-1">Username</label>
          <input
            type="text"
            value={formData.userName}
            onChange={(e) =>
              setFormData({ ...formData, userName: e.target.value })
            }
            className="w-full border p-2 rounded"
            required
          />
        </div>
        <div>
          <label className="block text-sm font-medium mb-1">Password</label>
          <input
            type="password"
            value={formData.password}
            onChange={(e) =>
              setFormData({ ...formData, password: e.target.value })
            }
            className="w-full border p-2 rounded"
            required
          />
        </div>
        <Button type="submit" disabled={isLoading} className="w-full">
          {isLoading ? "Logging in..." : "Login"}
        </Button>
      </form>
    </div>
  );
}
