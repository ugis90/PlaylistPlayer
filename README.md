# PlaylistPlayer

## Problem Description:

Managing playlists across different categories is a common feature in modern music streaming services. Users need a structured way to organize their music into playlists, with each playlist containing multiple songs. Managing these entities efficiently with role-based access control and a user-friendly interface is essential for seamless music interaction.

## Purpose of the System:

The system will allow users to create, organize, and manage their music playlists. It will allow different user roles (e.g., guest, member, administrator) to interact with the music library in varying capacities, providing CRUD functionalities for `Category`, `Playlist`, and `Song`. The application will include features like adding/removing songs from playlists and organizing playlists into categories.

## Functional Requirements:

- **Categories**: CRUD operations for managing categories.
- **Playlists**: CRUD operations for managing playlists under specific categories.
- **Songs**: CRUD operations for adding and removing songs from playlists.
- **Role-based access**:
  - **Guest**: Can view public categories, playlists, and songs.
  - **Member**: Can create and manage their playlists.
  - **Administrator**: Full access to manage categories, playlists, and users.
- **Authentication**: JWT-based user authentication with token renewal strategy.
- **Hierarchical Method**: A method that returns all playlists within a category or all songs within a playlist.

## Selected Technologies:

- **Backend**: ASP.NET Core Web API.
- **Frontend**: React.
- **Database**: SQL Server.
- **Authentication**: JWT with ASP.NET Core Identity.
- **API Documentation**: OpenAPI.
- **Cloud Hosting**: DigitalOcean.
