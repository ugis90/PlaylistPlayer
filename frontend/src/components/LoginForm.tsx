// src/components/LoginForm.tsx
import { useAuth } from "../auth/AuthContext";
import { useNavigate } from "react-router-dom";
import { useState } from "react";
import { Input } from "./ui/input";
import { Button } from "./ui/button";
import { toast } from "sonner";
import apiClient from "../api/client";

export function LoginForm() {
  const [formData, setFormData] = useState({ userName: "", password: "" });
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      // --- Removed Mock Logins ---
      // Rely solely on the actual API login now

      await login(formData.userName, formData.password);
      navigate("/"); // Navigate to dashboard on successful login
    } catch (err: any) {
      // Error is handled and toasted within the login function in AuthContext
      console.error("Login component caught error:", err);
      // Optionally add more specific UI feedback here if needed
    } finally {
      setIsLoading(false);
    }
  };

  return (
    // *** FIX: Reduced bottom padding/margin if needed ***
    <div className="max-w-md mx-auto mt-8 p-6 bg-white rounded-lg shadow space-y-6">
      <h2 className="text-2xl font-bold text-center mb-6">Login</h2>{" "}
      {/* Centered Title */}
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
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
        </div>
        <div>
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
        </div>
        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? "Logging in..." : "Login"}
        </Button>
      </form>
      <p className="mt-4 text-center text-sm">
        Don't have an account?{" "}
        <a href="/register" className="text-blue-600 hover:underline">
          Register
        </a>
      </p>
    </div>
  );
}
