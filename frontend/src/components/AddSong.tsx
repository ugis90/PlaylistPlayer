import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../api/client";
import { Button } from "./ui/button";
import { toast } from "sonner";
import { ValidationError } from "../types/validation.ts";

export function AddSong({
  categoryId,
  playlistId,
}: {
  categoryId: string;
  playlistId: string;
}) {
  const [title, setTitle] = useState("");
  const [artist, setArtist] = useState("");
  const [duration, setDuration] = useState("");
  const [errors, setErrors] = useState<ValidationError | null>(null);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: async (data: {
      title: string;
      artist: string;
      duration: number;
    }) => {
      return apiClient.post(
        `/categories/${categoryId}/playlists/${playlistId}/songs`,
        data,
      );
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ["songs", categoryId, playlistId],
      });
      setTitle("");
      setArtist("");
      setDuration("");
      setErrors(null);
      toast.success("Song added successfully");
    },
    onError: (error: any) => {
      console.log("Validation Error:", error);
      if (error.errors) {
        setErrors({
          type: "https://tools.ietf.org/html/rfc4918#section-11.2",
          title: "Validation Error",
          status: 422,
          errors: {
            title: error.errors.Title || [],
            artist: error.errors.Artist || [],
            duration: error.errors.Duration || [],
          },
        });
        // Show first validation error as toast
        const firstError =
          error.errors.Title?.[0] ||
          error.errors.Artist?.[0] ||
          error.errors.Duration?.[0];
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
        mutation.mutate({
          title,
          artist,
          duration: parseInt(duration),
        });
      }}
      className="space-y-4 mb-6"
    >
      <div>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Song title"
          className={`border p-2 rounded w-full ${
            errors?.errors?.title ? "border-red-500" : ""
          }`}
        />
        {errors?.errors?.title && (
          <p className="text-red-500 text-sm mt-1">{errors.errors.title[0]}</p>
        )}
      </div>

      <div>
        <input
          type="text"
          value={artist}
          onChange={(e) => setArtist(e.target.value)}
          placeholder="Artist"
          className={`border p-2 rounded w-full ${
            errors?.errors?.artist ? "border-red-500" : ""
          }`}
        />
        {errors?.errors?.artist && (
          <p className="text-red-500 text-sm mt-1">{errors.errors.artist[0]}</p>
        )}
      </div>

      <div>
        <input
          type="number"
          value={duration}
          onChange={(e) => setDuration(e.target.value)}
          placeholder="Duration (seconds)"
          className={`border p-2 rounded w-full ${
            errors?.errors?.duration ? "border-red-500" : ""
          }`}
        />
        {errors?.errors?.duration && (
          <p className="text-red-500 text-sm mt-1">
            {errors.errors.duration[0]}
          </p>
        )}
      </div>

      <Button type="submit" disabled={mutation.isPending}>
        {mutation.isPending ? "Adding..." : "Add Song"}
      </Button>
    </form>
  );
}
