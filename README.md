# 🔖 PolaperLinku

> A powerful **local alternative to Raindrop.io** for managing your bookmarks and favorites with automatic metadata extraction and smart organization.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-18-61DAFB?logo=react&logoColor=black)](https://reactjs.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Tailwind CSS](https://img.shields.io/badge/Tailwind-3-06B6D4?logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)

## ✨ Features

- 🌐 **Simple URL Input** - Just paste a URL and let the app do the rest
- 🤖 **Automatic Metadata Extraction** - Smart extraction from Open Graph, Twitter Cards, and more
- 🧠 **Intelligent Caching** - 24-hour TTL to reduce redundant requests
- 🐦 **Special Twitter/X Support** - Uses headless Chromium to extract tweet content and user info
- 📁 **Folder Organization** - Organize bookmarks into folders or keep them unclassified
- 👀 **"All Favorites" View** - See everything at a glance (default view)
- 🌙 **Dark Mode Interface** - Easy on the eyes with Tailwind CSS
- 🔄 **Sort & Filter** - Order by date or title
- 💾 **Export to JSON** - Backup your bookmarks anytime
- 🗄️ **SQLite Database** - Local storage for complete privacy and control

## 🛠️ Tech Stack

### Backend
- **[.NET 10](https://dotnet.microsoft.com/)** - Minimal API with clean architecture
- **[Entity Framework Core](https://docs.microsoft.com/ef/core/)** - ORM for SQLite
- **[Playwright](https://playwright.dev/)** - Headless Chromium for JavaScript-heavy sites
- **[HtmlAgilityPack](https://html-agility-pack.net/)** - HTML parsing and XPath queries
- **SQLite** - Lightweight, file-based database

### Frontend
- **[React 18](https://reactjs.org/)** - Modern UI library with hooks
- **[TypeScript](https://www.typescriptlang.org/)** - Type-safe JavaScript
- **[Vite](https://vitejs.dev/)** - Lightning-fast build tool
- **[Tailwind CSS](https://tailwindcss.com/)** - Utility-first CSS framework
- **[Axios](https://axios-http.com/)** - HTTP client for API calls

### DevOps
- **[Docker](https://www.docker.com/)** & **Docker Compose** - Containerized deployment

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) and npm
- [Docker](https://www.docker.com/) (optional, for containerized deployment)

### 🐳 Option 1: Docker Compose (Recommended)

```bash
docker-compose up
```

**Access the application:**
- 🌐 Web Interface: http://localhost:80
- 🔌 API: http://localhost:5000
- 📖 Swagger Docs: http://localhost:5000/swagger

### 🔧 Option 2: Local Development

**Using the start script (Linux/Mac):**
```bash
chmod +x start.sh
./start.sh
```

**Using npm concurrently:**
```bash
cd polaper-linku-web
npm install
npm run dev:all
```

**Manual setup (two terminals):**

Terminal 1 - Backend:
```bash
cd PolaperLinku.Api
dotnet restore
dotnet run
```

Terminal 2 - Frontend:
```bash
cd polaper-linku-web
npm install
npm run dev
```

**Access the application:**
- 🌐 Frontend: http://localhost:5173
- 🔌 API: http://localhost:5000
- 📖 Swagger: http://localhost:5000/swagger

## 📡 API Endpoints

### Folders
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/folders` | Get all folders |
| `POST` | `/api/folders` | Create a new folder |
| `DELETE` | `/api/folders/{id}` | Delete a folder |

### Favorites
| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/favorites?folderId={id}&orderBy={date\|title}` | Get favorites (filtered by folder, ordered by date/title) |
| `POST` | `/api/favorites` | Create a favorite (auto-extracts metadata) |
| `PUT` | `/api/favorites/{id}` | Update a favorite |
| `DELETE` | `/api/favorites/{id}` | Delete a favorite |
| `GET` | `/api/favorites/export` | Export all favorites as JSON |

**Query Parameters for GET /api/favorites:**
- `folderId` (optional): 
  - Not provided = returns all favorites
  - `0` = returns only unclassified favorites
  - `1,2,...` = returns favorites from specific folder
- `orderBy` (optional): `date` or `title`

**Request body for POST /api/favorites:**
```json
{
  "url": "https://example.com",
  "folderId": 1  // optional
}
```

### Metadata
| Method | Endpoint | Description |
|--------|----------|-------------|
| `DELETE` | `/api/metadata/clear` | Clear metadata cache |

## 📊 Data Model

### Favorite
```typescript
{
  id: number
  title: string              // Auto-extracted from Open Graph
  url: string                // Required
  description: string        // Auto-extracted from Open Graph
  createdAt: string
  folderId: number | null
  previewImage: string | null // Auto-extracted from og:image
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

## 🎨 UI Features

- ✨ **Simple URL-only input** - No need to manually enter title/description
- 🖼️ **Automatic metadata extraction** - Title, description, and preview images
- 🌙 **Dark mode** - Beautiful dark theme with Tailwind CSS
- 📋 **Compact todolist layout** - Efficient use of space
- 🖼️ **Image previews** - Visual representation of your bookmarks
- 📁 **Folder navigation** - Sidebar with "All Favorites" as default
- 📂 **Unclassified section** - For bookmarks without a folder
- 🎯 **Folder selector** - Choose destination when adding bookmarks
- ⚡ **Quick actions** - Edit and delete with smooth transitions

## 🧠 Intelligent Metadata Extraction

PolaperLinku uses a smart multi-source approach to extract metadata from URLs:

### Extraction Priority

1. **🔷 Open Graph Tags** (most reliable)
   - `og:title`, `og:description`, `og:image`
   - Standard for social media sharing

2. **🐦 Twitter/X Cards**
   - `twitter:title`, `twitter:description`, `twitter:image`
   - **Uses Playwright (headless Chromium)** for JavaScript-heavy content
   - Extracts tweet content, user bios, and images dynamically

3. **📄 HTML Meta Tags**
   - Fallback to `<title>` tag
   - Fallback to `<meta name="description">`

4. **📝 Content Extraction**
   - First paragraph for blog posts
   - User bio for Twitter/X profiles
   - Tweet content for Twitter/X posts

5. **🌐 Domain Fallback**
   - Uses domain name if no metadata found

### Special URL Handling

**Twitter/X Tweets** (`https://x.com/user/status/123`)
- Uses Playwright to render JavaScript
- Extracts tweet text (truncated to 200 chars)
- Extracts user handle and bio
- Format: `@handle: tweet content`

**Twitter/X Profiles** (`https://x.com/username`)
- Extracts user bio and handle dynamically
- Format: `@handle - bio`

**Regular Websites**
- Open Graph tags first
- HTML title/description fallback
- First paragraph extraction as last resort

### Caching Strategy

- ⚡ **In-memory cache** with 24-hour TTL
- 🚫 **Prevents redundant requests** to the same URLs
- 🧹 **Automatic cleanup** of expired entries
- 🗑️ **Manual cache clearing** via API endpoint

## 🏗️ Project Structure

```
PolaperLinku/
├── PolaperLinku.Api/          # .NET 10 Backend
│   ├── Extensions/            # Service & Endpoint extensions
│   ├── Models/                # Entity models & DbContext
│   ├── Services/              # Metadata extraction & caching
│   └── Program.cs             # Minimal API setup
├── polaper-linku-web/         # React Frontend
│   └── src/
│       ├── components/        # React components
│       ├── services/          # API client
│       └── types/             # TypeScript definitions
├── data/                      # SQLite database storage
├── docker-compose.yml         # Container orchestration
└── README.md
```

## 🤝 Contributing

Contributions are welcome! Feel free to:
- 🐛 Report bugs
- 💡 Suggest new features
- 🔧 Submit pull requests

## 📝 License

This project is open source and available under the MIT License.

## 🙏 Acknowledgments

- Inspired by [Raindrop.io](https://raindrop.io/)
- Built with modern web technologies
- Focused on privacy and local control

---

Made with ❤️ by [AlPeFe](https://github.com/AlPeFe)
