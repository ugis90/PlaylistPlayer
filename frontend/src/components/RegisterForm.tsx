import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { apiClient } from "../api/client";
import { toast } from "sonner";

interface PasswordError {
  code: string;
  description: string;
}

export function RegisterForm() {
  const [formData, setFormData] = useState({
    userName: "",
    email: "",
    password: "",
  });
  const [isLoading, setIsLoading] = useState(false);
  const [passwordErrors, setPasswordErrors] = useState<PasswordError[] | null>(
    null,
  );
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setPasswordErrors(null);

    try {
      await apiClient.post("/accounts", formData);
      toast.success("Registration successful! Please login.");
      navigate("/login");
    } catch (err: any) {
      if (err.response?.status === 422 && Array.isArray(err.response.data)) {
        // The response is an array of password requirement errors.
        setPasswordErrors(err.response.data as PasswordError[]);
      } else {
        // Handle other errors if needed
        toast.error("Failed to register. Please try again.");
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="max-w-md mx-auto mt-8 p-6 bg-white dark:bg-gray-800 dark:text-gray-100 rounded-lg shadow space-y-6 transition-shadow hover:shadow-lg">
      <h2 className="text-2xl font-bold">Register</h2>

      <form onSubmit={handleSubmit} className="space-y-4">
        <Input
          label="Username"
          type="text"
          value={formData.userName}
          onChange={(e) =>
            setFormData({ ...formData, userName: e.target.value })
          }
          placeholder="Choose a username"
          required
        />

        <Input
          label="Email"
          type="email"
          value={formData.email}
          onChange={(e) => setFormData({ ...formData, email: e.target.value })}
          placeholder="Your email address"
          required
        />

        <div>
          <Input
            label="Password"
            type="password"
            value={formData.password}
            onChange={(e) =>
              setFormData({ ...formData, password: e.target.value })
            }
            placeholder="Choose a strong password"
            required
            minLength={8}
          />

          {/* Show password requirement errors if present */}
          {passwordErrors && passwordErrors.length > 0 && (
            <ul className="mt-2 list-disc pl-5 text-red-500 text-sm">
              {passwordErrors.map((err) => (
                <li key={err.code}>{err.description}</li>
              ))}
            </ul>
          )}
        </div>

        <Button type="submit" className="w-full" disabled={isLoading}>
          {isLoading ? "Registering..." : "Register"}
        </Button>
      </form>
    </div>
  );
}
