# PlaylistPlayer

## Overview

PlaylistPlayer is a full-stack application for managing music categories, playlists, and songs. It provides role-based access control, allowing guests to view content, members to manage their playlists, and administrators to manage all categories, playlists, and users.

## Problem Description

Managing playlists across different categories is common in modern music streaming services. Users need a structured interface to quickly organize their music collections and find or modify playlists and songs.

## Purpose of the System

- Users can create and manage categories and song playlists.
- Different user roles have varying permissions: guests (view), members (manage own), admins (full access).
- The system implements JWT-based authentication and integrates a REST API with a frontend UI.

## Functional Requirements

- CRUD for Categories, Playlists, and Songs.
- Role-based access: Guest, Member, Administrator.
- Token-based authentication (JWT).
- Ability to view hierarchical data (playlists in a category, songs in a playlist).

## Technology Stack

- **Backend**: ASP.NET Core Web API with ASP.NET Core Identity and JWT authentication.
- **Frontend**: React, Tailwind CSS, React Query for data fetching, Vite for bundling.
- **Database**: SQL Server
- **Icons**: `lucide-react`
- **UI Components**: Tailwind + Radix UI Dialog for Modal
- **Animations**: Framer Motion for transitions
- **Hosting**: DigitalOcean

## Setup Instructions

### Prerequisites
- Node.js and npm installed.
- .NET 8 and SQL Server environment for the backend.

### Running the Frontend
```bash
cd frontend
npm install
npm run dev
```

Open `http://localhost:3000` in your browser.

Web page: https://chic-jalebi-b61d0b.netlify.app/

Back-end: https://octopus-app-3t93j.ondigitalocean.app/api
