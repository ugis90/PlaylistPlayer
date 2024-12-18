import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Button } from "./ui/button";
import { apiClient } from "../api/client";
import { ValidationError } from "../types/validation";
import { toast } from "sonner";

export function CreateCategory() {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [categoryType, setCategoryType] = useState("public");
  const [errors, setErrors] = useState<ValidationError | null>(null);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: async (data: {
      name: string;
      description: string;
      type: string;
    }) => {
      return apiClient.post("/categories", data);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setName("");
      setDescription("");
      setCategoryType("public");
      setErrors(null);
      toast.success("Category created successfully");
    },
    onError: (error: any) => {
      // If not logged in, error.response.status might be 401 or 403
      if (error.response?.status === 401 || error.response?.status === 403) {
        toast.error("You must be logged in to perform this action.");
        return;
      }

      if (error.errors) {
        setErrors({
          type: "validation",
          title: "Validation Error",
          status: 422,
          errors: {
            name: error.errors.Name || [],
            description: error.errors.Description || [],
          },
        });
        const firstError =
          error.errors.Name?.[0] || error.errors.Description?.[0];
        if (firstError) {
          toast.error(firstError);
        } else {
          toast.error("Please fix the validation errors");
        }
      } else {
        // Some other error occurred
        toast.error("Failed to create category");
      }
    },
  });

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        mutation.mutate({ name, description, type: categoryType });
      }}
      className="flex flex-col items-start space-y-2"
    >
      <label className="text-sm font-medium">Category Name</label>
      <input
        type="text"
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Category name"
        className={`border p-2 rounded w-full bg-gray-100 dark:bg-gray-700 ${
          errors?.errors?.name ? "border-red-500" : ""
        }`}
      />
      {errors?.errors?.name && (
        <p className="text-red-500 text-sm">{errors.errors.name[0]}</p>
      )}

      <label className="text-sm font-medium">Description</label>
      <textarea
        value={description}
        onChange={(e) => setDescription(e.target.value)}
        placeholder="Description"
        className={`border p-2 rounded w-full bg-gray-100 dark:bg-gray-700 ${
          errors?.errors?.description ? "border-red-500" : ""
        }`}
      />
      {errors?.errors?.description && (
        <p className="text-red-500 text-sm">{errors.errors.description[0]}</p>
      )}

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? "Creating..." : "Create Category"}
      </Button>
    </form>
  );
}
