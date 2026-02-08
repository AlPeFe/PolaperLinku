import { useState, useEffect } from 'react'
import { Favorite, Folder } from './types'
import { favoritesApi, foldersApi } from './services/api'
import FavoriteItem from './components/FavoriteItem'
import Sidebar from './components/Sidebar'
import AddFavoriteForm from './components/AddFavoriteForm'

function App() {
  const [favorites, setFavorites] = useState<Favorite[]>([])
  const [folders, setFolders] = useState<Folder[]>([])
  const [selectedFolder, setSelectedFolder] = useState<number | null>(null)
  const [defaultFolder, setDefaultFolder] = useState<number | null>(null)
  const [orderBy, setOrderBy] = useState<'date' | 'title'>('date')

  useEffect(() => {
    loadFolders()
    loadFavorites()
  }, [selectedFolder, orderBy])

  const loadFavorites = async () => {
    let folderId: number | undefined
    if (selectedFolder === null) {
      folderId = undefined // All favorites
    } else if (selectedFolder === 0) {
      folderId = 0 // Unclassified - favorites without folder
    } else {
      folderId = selectedFolder // Specific folder
    }
    const data = await favoritesApi.getAll(folderId, orderBy)
    setFavorites(data)
  }

  const loadFolders = async () => {
    const data = await foldersApi.getAll()
    setFolders(data)
  }

  const handleExport = async () => {
    const data = await favoritesApi.export()
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'favorites.json'
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="app">
      <Sidebar
        folders={folders}
        selectedFolder={selectedFolder}
        onSelectFolder={setSelectedFolder}
        onFoldersChange={loadFolders}
      />
      <main className="main-content">
        <header>
          <h1>
            {selectedFolder === null
              ? 'All Favorites'
              : selectedFolder === 0
                ? 'Unclassified'
                : folders.find(f => f.id === selectedFolder)?.name || 'Favorites'}
          </h1>
          <div className="header-actions">
            <select
              value={orderBy}
              onChange={(e) => setOrderBy(e.target.value as 'date' | 'title')}
              className="order-select"
            >
              <option value="date">Order by Date</option>
              <option value="title">Order by Title</option>
            </select>
            <button onClick={handleExport} className="btn-export">
              Export JSON
            </button>
          </div>
        </header>
        <AddFavoriteForm
          folderId={selectedFolder}
          folders={folders}
          onAdd={loadFavorites}
        />
        <div className="favorites-list">
          {favorites.length === 0 ? (
            <p className="empty-state">No favorites yet. Add one above!</p>
          ) : (
            favorites.map((favorite) => (
              <FavoriteItem
                key={favorite.id}
                favorite={favorite}
                onUpdate={loadFavorites}
                onDelete={loadFavorites}
              />
            ))
          )}
        </div>
      </main>
    </div>
  )
}

export default App
