import { Input } from "./ui/input";
import { Button } from "./ui/button";
import { useAuth } from "../auth/AuthContext";
import { ErrorDisplay, ValidationError } from "./ErrorBoundary";
import { useNavigate, useLocation } from "react-router-dom";
import { useState } from "react";

export function LoginForm() {
  const [formData, setFormData] = useState({ userName: "", password: "" });
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
    <div className="max-w-md mx-auto mt-8 p-6 bg-white dark:bg-gray-800 dark:text-gray-100 rounded-lg shadow space-y-6 transition-shadow hover:shadow-lg">
      <h2 className="text-2xl font-bold">Login</h2>
      {location.state?.message && (
        <div className="mb-4 p-2 bg-green-100 dark:bg-green-900 text-green-700 dark:text-green-200 rounded">
          {location.state.message}
        </div>
      )}
      {error && <ErrorDisplay error={error} />}
      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Username"
          type="text"
          value={formData.userName}
          onChange={(e) =>
            setFormData({ ...formData, userName: e.target.value })
          }
          placeholder="Enter your username"
          required
        />
        <Input
          label="Password"
          type="password"
          value={formData.password}
          onChange={(e) =>
            setFormData({ ...formData, password: e.target.value })
          }
          placeholder="Enter your password"
          required
        />
        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? "Logging in..." : "Login"}
        </Button>
      </form>
    </div>
  );
}
