import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Card, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { apiClient } from "../api/client";
import type { Category, ApiResponse } from "../api/types";
import { useNavigate } from "react-router-dom";
import { useState } from "react";
import { CreateCategory } from "./CreateCategory.tsx";
import { Breadcrumb } from "./Breadcrumb";
import { toast } from "sonner";
//import { useMediaQuery } from "../hooks/useMediaQuery.ts";
//import { useSlideIn } from "../hooks/useAnimation.ts";

export function CategoryList() {
  //const isMobile = useMediaQuery("(max-width: 768px)");
  //const animation = useSlideIn();
  const [editingId, setEditingId] = useState<number | null>(null);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const deleteMutation = useMutation({
    mutationFn: (categoryId: number) =>
      apiClient.delete(`/categories/${categoryId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      toast.success("Category deleted successfully");
    },
    onError: () => {
      toast.error("Failed to delete category");
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { description: string } }) =>
      apiClient.put(`/categories/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setEditingId(null);
      toast.success("Category updated successfully");
    },
    onError: (error: any) => {
      console.log("Error object:", error);
      if (error.errors?.Description) {
        toast.error(error.errors.Description[0]);
      } else {
        toast.error("Failed to update category");
      }
    },
  });

  const { data, isLoading, error } = useQuery<ApiResponse<Category>>({
    queryKey: ["categories", page],
    queryFn: async () => {
      try {
        const response = await apiClient.get<ApiResponse<Category>>(
          `/categories?pageNumber=${page}&pageSize=${pageSize}`,
        );
        console.log("Raw API Response:", response);
        console.log("Response Data:", response.data);
        console.log("Resources:", response.data.resource);
        return response.data;
      } catch (err) {
        console.error("API Error:", err);
        throw err;
      }
    },
  });

  if (isLoading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;
  if (!data?.resource) return <div>No categories found</div>;

  return (
    <div>
      <Breadcrumb />
      <div>
        <div className="flex justify-between items-center mb-6">
          <h1 className="text-2xl font-bold">Categories</h1>
          <CreateCategory />
        </div>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {data?.resource.map(({ resource: category }) => (
            <Card key={category.id}>
              <CardHeader>
                {editingId === category.id ? (
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      const formData = new FormData(e.currentTarget);
                      updateMutation.mutate({
                        id: category.id,
                        data: {
                          description: formData.get("description") as string,
                        },
                      });
                    }}
                  >
                    <CardTitle>{category.name}</CardTitle>
                    <div>
                      <textarea
                        name="description"
                        defaultValue={category.description}
                        className={`border p-2 rounded w-full mt-2 ${
                          updateMutation.error?.errors?.Description
                            ? "border-red-500"
                            : ""
                        }`}
                      />
                      {updateMutation.error?.errors?.Description && (
                        <p className="text-red-500 text-sm mt-1">
                          {updateMutation.error.errors.Description[0]}
                        </p>
                      )}
                    </div>
                    <div className="mt-2 space-x-2">
                      <Button type="submit" disabled={updateMutation.isPending}>
                        {updateMutation.isPending ? "Saving..." : "Save"}
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => setEditingId(null)}
                        disabled={updateMutation.isPending}
                      >
                        Cancel
                      </Button>
                    </div>
                  </form>
                ) : (
                  <>
                    <CardTitle>{category.name}</CardTitle>
                    <p>{category.description}</p>
                    <div className="flex gap-2 mt-4">
                      <Button
                        variant="outline"
                        onClick={() => setEditingId(category.id)}
                      >
                        Edit
                      </Button>
                      <Button
                        variant="outline"
                        onClick={() => {
                          if (confirm("Delete this category?")) {
                            deleteMutation.mutate(category.id);
                          }
                        }}
                        disabled={deleteMutation.isPending}
                      >
                        {deleteMutation.isPending ? "Deleting..." : "Delete"}
                      </Button>
                      <Button
                        variant="secondary"
                        onClick={() =>
                          navigate(`/categories/${category.id}/playlists`)
                        }
                      >
                        View Playlists
                      </Button>
                    </div>
                  </>
                )}
              </CardHeader>
            </Card>
          ))}
        </div>
      </div>
      <div className="flex justify-center gap-2 mt-4">
        <Button onClick={() => setPage((p) => p - 1)} disabled={page === 1}>
          Previous
        </Button>
        <span>Page {page}</span>
        <Button
          onClick={() => setPage((p) => p + 1)}
          disabled={!data?.links?.find((l) => l.rel === "next")}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
