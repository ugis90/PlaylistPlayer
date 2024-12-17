export interface Category {
  id: number;
  name: string;
  description: string;
  createdOn: string;
}

export interface ApiResponse<T> {
  resource: Array<{
    resource: T;
    links: Array<{ href: string; rel: string; method: string }>;
  }>;
  links: Array<{ href: string; rel: string; method: string }>;
}

export interface CategoryResponse {
  resources: Array<{
    resource: Category;
    links: Array<{ href: string; rel: string; method: string }>;
  }>;
}

export interface Playlist {
  id: number;
  name: string;
  description: string;
  categoryId: number;
  createdOn: string;
}

export interface Song {
  id: number;
  title: string;
  artist: string;
  duration: number;
  orderId: number;
  playlistId: number;
  createdOn: string;
}
