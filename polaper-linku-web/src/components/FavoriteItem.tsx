import { useState } from 'react'
import { Favorite } from '../types'
import { favoritesApi } from '../services/api'

interface FavoriteItemProps {
  favorite: Favorite
  onUpdate: () => void
  onDelete: () => void
}

export default function FavoriteItem({ favorite, onUpdate, onDelete }: FavoriteItemProps) {
  const [isEditing, setIsEditing] = useState(false)
  const [title, setTitle] = useState(favorite.title)
  const [description, setDescription] = useState(favorite.description)
  const [url, setUrl] = useState(favorite.url)

  const handleUpdate = async () => {
    if (!url.trim()) {
      alert('URL is required')
      return
    }
    await favoritesApi.update(favorite.id, { title, description, url, folderId: favorite.folderId })
    setIsEditing(false)
    onUpdate()
  }

  const handleDelete = async () => {
    if (window.confirm('Are you sure you want to delete this favorite?')) {
      await favoritesApi.delete(favorite.id)
      onDelete()
    }
  }

  return (
    <div className="favorite-item">
      {favorite.previewImage && (
        <div className="link-preview">
          <img src={favorite.previewImage} alt="Preview" loading="lazy" />
        </div>
      )}
      <div className="favorite-content">
        {isEditing ? (
          <div className="edit-form">
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Title"
              className="edit-input"
            />
            <input
              type="text"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="URL (required)"
              className="edit-input"
              required
            />
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Description (optional)"
              className="edit-textarea"
            />
            <div className="edit-actions">
              <button onClick={handleUpdate} className="btn-save">Save</button>
              <button onClick={() => setIsEditing(false)} className="btn-cancel">Cancel</button>
            </div>
          </div>
        ) : (
          <>
            <div className="favorite-header">
              <h3>
                <a href={favorite.url} target="_blank" rel="noopener noreferrer">
                  {favorite.title || favorite.url}
                </a>
              </h3>
              <span className="favorite-url">{favorite.url}</span>
            </div>
            {favorite.description && <p className="favorite-description">{favorite.description}</p>}
            <div className="favorite-meta">
              <small>{new Date(favorite.createdAt).toLocaleDateString()}</small>
              <div className="favorite-actions">
                <button onClick={() => setIsEditing(true)} className="btn-edit">Edit</button>
                <button onClick={handleDelete} className="btn-delete">Delete</button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  )
}
