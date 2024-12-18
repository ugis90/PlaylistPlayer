import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "../api/client";
import { Button } from "./ui/button";
import { Input } from "./ui/input";
import { toast } from "sonner";
import { ValidationError } from "../types/validation";

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
      if (error.errors) {
        setErrors({
          type: "validation",
          title: "Validation Error",
          status: 422,
          errors: {
            title: error.errors.Title || [],
            artist: error.errors.Artist || [],
            duration: error.errors.Duration || [],
          },
        });
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
        mutation.mutate({ title, artist, duration: parseInt(duration) });
      }}
      className="space-y-4 mb-6 bg-white dark:bg-gray-800 dark:text-gray-100 p-4 rounded shadow transition-shadow hover:shadow-lg max-w-md"
    >
      <Input
        label="Song Title"
        type="text"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        placeholder="Song title"
        error={errors?.errors?.title ? errors.errors.title[0] : undefined}
        required
      />

      <Input
        label="Artist"
        type="text"
        value={artist}
        onChange={(e) => setArtist(e.target.value)}
        placeholder="Artist name"
        error={errors?.errors?.artist ? errors.errors.artist[0] : undefined}
        required
      />

      <Input
        label="Duration (seconds)"
        type="number"
        value={duration}
        onChange={(e) => setDuration(e.target.value)}
        placeholder="Enter duration in seconds"
        error={errors?.errors?.duration ? errors.errors.duration[0] : undefined}
        required
      />

      <Button type="submit" disabled={mutation.isPending} className="w-full">
        {mutation.isPending ? "Adding..." : "Add Song"}
      </Button>
    </form>
  );
}
