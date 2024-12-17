import { motion } from "framer-motion";
import { Card, CardHeader, CardTitle } from "./ui/card";
import { Button } from "./ui/button";
import { Music } from "lucide-react";
import type { Category } from "../api/types";

interface CategoryCardProps {
  category: Category;
  onEdit: (categoryId: number) => void;
  onDelete: (categoryId: number) => void;
  onViewPlaylists: (categoryId: number) => void;
  isEditing: boolean;
  isDeleting: boolean;
}

export function CategoryCard({
  category,
  onEdit,
  onDelete,
  onViewPlaylists,
  isDeleting,
}: CategoryCardProps) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
    >
      <Card className="hover:shadow-lg transition-shadow duration-300">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Music className="h-5 w-5" />
            {category.name}
          </CardTitle>
          <p className="mt-2 text-gray-600">{category.description}</p>
          <p className="text-sm text-gray-500 mt-1">
            Created: {new Date(category.createdOn).toLocaleDateString()}
          </p>
          <div className="flex gap-2 mt-4">
            <Button variant="outline" onClick={() => onEdit(category.id)}>
              Edit
            </Button>
            <Button
              variant="outline"
              onClick={() => {
                if (confirm("Delete this category?")) {
                  onDelete(category.id);
                }
              }}
              disabled={isDeleting}
            >
              {isDeleting ? "Deleting..." : "Delete"}
            </Button>
            <Button
              variant="secondary"
              onClick={() => onViewPlaylists(category.id)}
            >
              View Playlists
            </Button>
          </div>
        </CardHeader>
      </Card>
    </motion.div>
  );
}
