import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Card, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { apiClient } from "../api/client";
import type { Category, ApiResponse } from "../api/types";
import { useNavigate } from "react-router-dom";
import { useState } from "react";
import { CreateCategory } from "./CreateCategory";
import { Breadcrumb } from "./Breadcrumb";
import { toast } from "sonner";
import { Modal } from "./Modal";
import { Pencil, Trash, Eye } from "lucide-react";

export function CategoryList() {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [deleteId, setDeleteId] = useState<number | null>(null);
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: number; data: { description: string } }) =>
      apiClient.put(`/categories/${id}`, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      setEditingId(null);
      toast.success("Category updated successfully");
    },
    onError: (error: any) => {
      if (error.response?.status === 401 || error.response?.status === 403) {
        toast.error("You must be logged in to perform this action.");
        return;
      }

      if (error.errors?.Description) {
        toast.error(error.errors.Description[0]);
      } else {
        toast.error("Failed to update category");
      }
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (categoryId: number) =>
      apiClient.delete(`/categories/${categoryId}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["categories"] });
      toast.success("Category deleted successfully");
      setDeleteId(null);
    },
    onError: (error: any) => {
      if (error.response?.status === 401 || error.response?.status === 403) {
        toast.error("You must be logged in to perform this action.");
        return;
      }
      toast.error("Failed to delete category");
    },
  });

  const { data, isLoading, error } = useQuery<ApiResponse<Category>>({
    queryKey: ["categories", page],
    queryFn: async () => {
      const response = await apiClient.get<ApiResponse<Category>>(
        `/categories?pageNumber=${page}&pageSize=${pageSize}`,
      );
      return response.data;
    },
  });

  if (isLoading) return <div className="text-center">Loading...</div>;
  if (error)
    return (
      <div className="text-center text-red-500">
        Error: {(error as Error).message}
      </div>
    );
  if (!data?.resource)
    return <div className="text-center">No categories found</div>;

  return (
    <div className="space-y-4">
      <Breadcrumb />
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold">Categories</h1>
        <CreateCategory />
      </div>
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {data.resource.map(({ resource: category }) => (
          <Card
            key={category.id}
            className="bg-white dark:bg-gray-800 text-black dark:text-gray-100 p-4 transition-transform transform hover:scale-105 hover:shadow-lg"
          >
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
                  <CardTitle className="text-xl font-semibold mb-2">
                    {category.name}
                  </CardTitle>
                  <label className="block mb-1 text-sm">Description</label>
                  <textarea
                    name="description"
                    defaultValue={category.description}
                    className="border p-2 rounded w-full bg-gray-100 dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
                  />
                  {updateMutation.error?.errors?.Description && (
                    <p className="text-red-500 text-sm mt-1">
                      {updateMutation.error.errors.Description[0]}
                    </p>
                  )}
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
                  {/* Example adapted image */}
                  <img
                    src="/assets/category-placeholder.png"
                    alt={`${category.name}`}
                    className="rounded mb-2"
                  />
                  <CardTitle className="text-xl font-semibold mb-2">
                    {category.name}
                  </CardTitle>
                  <p className="text-sm text-gray-700 dark:text-gray-300 mb-4">
                    {category.description}
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      onClick={() => setEditingId(category.id)}
                    >
                      <Pencil className="h-4 w-4 mr-1" /> Edit
                    </Button>
                    <Button
                      variant="outline"
                      onClick={() => setDeleteId(category.id)}
                      disabled={deleteMutation.isPending}
                    >
                      {deleteMutation.isPending ? (
                        "Deleting..."
                      ) : (
                        <>
                          <Trash className="h-4 w-4 mr-1" /> Delete
                        </>
                      )}
                    </Button>
                    <Button
                      variant="secondary"
                      onClick={() =>
                        navigate(`/categories/${category.id}/playlists`)
                      }
                    >
                      <Eye className="h-4 w-4 mr-1" />
                      View Playlists
                    </Button>
                  </div>
                </>
              )}
            </CardHeader>
          </Card>
        ))}
      </div>
      <div className="flex justify-center gap-2 mt-4">
        <Button onClick={() => setPage((p) => p - 1)} disabled={page === 1}>
          Previous
        </Button>
        <span className="self-center">Page {page}</span>
        <Button
          onClick={() => setPage((p) => p + 1)}
          disabled={!data?.links?.find((l) => l.rel === "next")}
        >
          Next
        </Button>
      </div>

      {deleteId && (
        <Modal
          isOpen={true}
          onClose={() => setDeleteId(null)}
          title="Confirm Deletion"
        >
          <p className="text-sm mb-4">
            Are you sure you want to delete this category?
          </p>
          <div className="flex gap-2">
            <Button
              variant="destructive"
              onClick={() => deleteId && deleteMutation.mutate(deleteId)}
              disabled={deleteMutation.isPending}
            >
              {deleteMutation.isPending ? "Deleting..." : "Yes, Delete"}
            </Button>
            <Button variant="outline" onClick={() => setDeleteId(null)}>
              Cancel
            </Button>
          </div>
        </Modal>
      )}
    </div>
  );
}
