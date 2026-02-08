# PolaperLinku

A simple bookmark management application similar to Raindrop.io, built with .NET 10 Minimal API and React.

## Features

- Create favorites by simply pasting a URL
- **Automatic metadata extraction with smart caching (24h TTL)**
- **Enhanced extraction for various URL types:**
  - Open Graph tags (og:title, og:description, og:image)
- Twitter/X tweets (extracts tweet content and user info)
- Fallback to HTML title and description
- First paragraph extraction for blog posts
- Domain-based fallback for generic URLs
- Organize favorites in folders
- **"All Favorites" view - shows all favorites across all folders (default view)**
- **"Unclassified" section for favorites without a folder**
- **Folder-based organization with default folder selection**
- View favorites as a compact todolist with mini browser previews
- Dark mode interface
- Sort by date or title
- Export favorites to JSON
- SQLite database for persistence
- Clean code with extensions for endpoints and DI

## Architecture

### Backend (.NET 10 Minimal API)
- Minimal API with clean code principles
- SQLite with Entity Framework Core
- Extensions for service registration and endpoint mapping
- Auto-initializes database on startup
- **Smart metadata extraction with:**
  - In-memory caching (24h TTL)
  - Multi-source fallback strategy
   - **Playwright (headless Chromium) for rendering JavaScript-heavy sites (Twitter/X)**
   - Special handling for Twitter/X URLs
   - HTML parsing with HtmlAgilityPack
   - HTTP client with proper User-Agent headers

### Frontend (React + TypeScript + Vite)
- Modern React with hooks
- Axios for HTTP communication
- **Tailwind CSS for styling**
- Dark mode design
- Compact todolist-style interface with optimized spacing
- Simple URL-only input form
- Smooth transitions and hover effects

## Local Development

### Run Backend and Frontend Together

Choose one of these options:

**Option 1: Docker Compose (recommended)**
```bash
docker-compose up
```

**Option 2: Script (Linux/Mac)**
```bash
./start.sh
```

**Option 3: npm concurrently**
```bash
cd polaper-linku-web
npm run dev:all
```

**Option 4: Two terminals**
```bash
# Terminal 1
cd PolaperLinku.Api
dotnet restore
dotnet run

# Terminal 2
cd polaper-linku-web
npm install
npm run dev
```

### Backend Only

```bash
cd PolaperLinku.Api
dotnet restore
dotnet run
```

The API will be available at `http://localhost:5000`

API Documentation (Swagger): `http://localhost:5000/swagger`

### Frontend Only

```bash
cd polaper-linku-web
npm install
npm run dev
```

The web app will be available at `http://localhost:5173`

## Docker

Build and run with Docker Compose:

```bash
docker-compose up --build
```

The application will be available at:
- Web interface: `http://localhost:80`
- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`

## API Endpoints

### Folders

- `GET /api/folders` - Get all folders
- `POST /api/folders` - Create a new folder
- `DELETE /api/folders/{id}` - Delete a folder

### Favorites

- `GET /api/favorites?folderId={id}&orderBy={date|title}` - Get favorites
  - `folderId` optional: if not provided, returns all favorites
  - `folderId=0`: returns only unclassified favorites (without folder)
  - `folderId=1,2,...`: returns favorites from specific folder
- `POST /api/favorites` - Create a favorite (automatically extracts Open Graph metadata from URL with caching)
  - Request body: `{ "url": "https://example.com", "folderId": 1 }`
  - `folderId` optional: if not provided, favorite is unclassified
- `PUT /api/favorites/{id}` - Update a favorite
- `DELETE /api/favorites/{id}` - Delete a favorite
- `GET /api/favorites/export` - Export all favorites as JSON

### Metadata

- `DELETE /api/metadata/clear` - Clear metadata cache

## Data Model

### Favorite
```typescript
{
  id: number
  title: string (auto-extracted from Open Graph)
  url: string (required)
  description: string (auto-extracted from Open Graph)
  createdAt: string
  folderId: number | null
  previewImage: string | null (auto-extracted from og:image)
}
```

### Folder
```typescript
{
  id: number
  name: string
  createdAt: string
}
```

## UI Features

- Simple URL-only input form (no need to manually enter title/description)
- Automatic Open Graph metadata extraction (title, description, image)
- Dark mode theme with Tailwind CSS
- Compact todolist-style layout with smaller elements
- Simple image preview for each link (no browser chrome)
- Separated by horizontal lines (no cards)
- Small, efficient display with optimized spacing
- Sidebar with "All Favorites" as default view
- Folder-based organization with navigation
- "Unclassified" section for favorites without a folder
- Folder selector in add form to choose destination folder
- "Current View" option in folder selector to use current folder
- Quick edit and delete actions
- Smooth transitions and hover effects

## Metadata Extraction

The application automatically extracts metadata from URLs using a smart multi-source approach:

### Extraction Priority

1. **Open Graph Tags** (most reliable)
   - `og:title`, `og:description`, `og:image`
   - Standard for social media sharing

2. **Twitter/X Cards**
   - `twitter:title`, `twitter:description`, `twitter:image`
   - **Uses Playwright (headless Chromium) for X/Twitter URLs** to render JavaScript-heavy content
   - Extracts tweet content, user bios, and images dynamically
   - Enhanced with custom X.com extraction for tweets

3. **HTML Title and Meta Description**
   - Fallback to `<title>` tag
   - Fallback to `<meta name="description">`

4. **Content Extraction**
   - First paragraph extraction for blog posts
   - User bio extraction for X/Twitter profiles
   - Tweet content extraction for X/Twitter posts

5. **Domain Fallback**
   - Uses domain name if no metadata found

### Special URL Handling

**Twitter/X Tweets** (e.g., `https://x.com/user/status/123`)
- Uses Playwright (headless Chromium) to render JavaScript-heavy X/Twitter content
- Waits for DOM content to load and extracts tweet text dynamically
- Extracts tweet content (truncated to 200 chars)
- Extracts user handle and bio
- Format: "@handle: tweet content"

**Twitter/X Profiles** (e.g., `https://x.com/username`)
- Uses Playwright (headless Chromium) to render JavaScript-heavy X/Twitter content
- Extracts user bio and handle dynamically
- Format: "@handle - bio"

**Regular Websites** (e.g., `https://www.snapsbyfox.com/blog/...`)
- Open Graph tags first
- Fallback to HTML title/description
- First paragraph extraction as last resort

### Caching

- In-memory cache with 24-hour TTL
- Prevents redundant HTTP requests
- Automatic cleanup of expired entries
- Manual cache clearing via `DELETE /api/metadata/cache`

### Technical Details

- Uses HtmlAgilityPack for HTML parsing
- XPath queries for precise metadata extraction
- Proper User-Agent header to avoid blocking
- Handles relative URLs for images
- Text cleaning and truncation for display
