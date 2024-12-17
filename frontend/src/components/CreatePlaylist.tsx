import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "./ui/button";
import { apiClient } from "../api/client";
import { ValidationError } from "../types/validation";
import { toast } from "sonner";

export function CreatePlaylist({ categoryId }: { categoryId: string }) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [errors, setErrors] = useState<ValidationError | null>(null);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: async (data: { name: string; description: string }) => {
      return apiClient.post(`/categories/${categoryId}/playlists`, data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["playlists", categoryId] });
      setName("");
      setDescription("");
      setErrors(null);
      toast.success("Playlist created successfully");
    },
    onError: (error: any) => {
      console.log("Validation Error:", error);
      if (error.errors) {
        setErrors({
          type: "https://tools.ietf.org/html/rfc4918#section-11.2",
          title: "Validation Error",
          status: 422,
          errors: {
            name: error.errors.Name || [],
            description: error.errors.Description || [],
          },
        });
        // Show first validation error as toast
        const firstError =
          error.errors.Name?.[0] || error.errors.Description?.[0];
        if (firstError) {
          toast.error(firstError);
        } else {
          toast.error("Please fix the validation errors");
        }
      }
    },
  });

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        mutation.mutate({ name, description });
      }}
    >
      <div className="space-y-4">
        <div>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Playlist name"
            className={`border p-2 rounded w-full ${
              errors?.errors?.name ? "border-red-500" : ""
            }`}
          />
          {errors?.errors?.name && (
            <p className="text-red-500 text-sm mt-1">{errors.errors.name[0]}</p>
          )}
        </div>

        <div>
          <input
            type="text"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="Description"
            className={`border p-2 rounded w-full ${
              errors?.errors?.description ? "border-red-500" : ""
            }`}
          />
          {errors?.errors?.description && (
            <p className="text-red-500 text-sm mt-1">
              {errors.errors.description[0]}
            </p>
          )}
        </div>

        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Creating..." : "Create Playlist"}
        </Button>
      </div>
    </form>
  );
}
