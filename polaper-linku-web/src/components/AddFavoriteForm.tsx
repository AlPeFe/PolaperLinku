import { useState } from 'react'
import { Folder } from '../types'
import { favoritesApi } from '../services/api'

interface AddFavoriteFormProps {
  folderId: number | null
  folders: Folder[]
  onAdd: () => void
}

export default function AddFavoriteForm({ folderId, folders, onAdd }: AddFavoriteFormProps) {
  const [url, setUrl] = useState('')
  const [isLoading, setIsLoading] = useState(false)
  const [selectedFolderId, setSelectedFolderId] = useState<number | null>(null)

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!url.trim()) {
      alert('URL is required')
      return
    }

    setIsLoading(true)
    try {
      let folderIdToUse: number | undefined
      if (selectedFolderId === 0) {
        folderIdToUse = undefined // Unclassified
      } else if (selectedFolderId === null) {
        folderIdToUse = undefined // Use current folder
      } else {
        folderIdToUse = selectedFolderId
      }
      await favoritesApi.create(url.trim(), folderIdToUse)
      setUrl('')
      onAdd()
    } catch (error) {
      alert('Error creating favorite. Please try again.')
    } finally {
      setIsLoading(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="add-favorite-form">
      <h3>Add New Favorite</h3>
      <div style={{ display: 'flex', gap: '8px' }}>
        <input
          type="text"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          placeholder="Paste URL here..."
          required
          style={{ flex: 1 }}
        />
        <select
          value={selectedFolderId?.toString() || ''}
          onChange={(e) => setSelectedFolderId(e.target.value ? parseInt(e.target.value) : null)}
          className="order-select"
          style={{ width: '150px' }}
        >
          <option value="">Current View</option>
          <option value="0">Unclassified</option>
          {folders.map((folder) => (
            <option key={folder.id} value={folder.id}>
              {folder.name}
            </option>
          ))}
        </select>
      </div>
      <button type="submit" className="btn-add" disabled={isLoading}>
        {isLoading ? 'Adding...' : 'Add'}
      </button>
    </form>
  )
}
