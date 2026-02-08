import { useState } from 'react'
import { Folder } from '../types'
import { foldersApi } from '../services/api'

interface SidebarProps {
  folders: Folder[]
  selectedFolder: number | null
  onSelectFolder: (folderId: number | null) => void
  onFoldersChange: () => void
}

export default function Sidebar({ folders, selectedFolder, onSelectFolder, onFoldersChange }: SidebarProps) {
  const [showNewFolder, setShowNewFolder] = useState(false)
  const [newFolderName, setNewFolderName] = useState('')

  const handleCreateFolder = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newFolderName.trim()) return

    await foldersApi.create({ name: newFolderName })
    setNewFolderName('')
    setShowNewFolder(false)
    onFoldersChange()
  }

  const handleDeleteFolder = async (folderId: number) => {
    if (window.confirm('Are you sure you want to delete this folder?')) {
      await foldersApi.delete(folderId)
      onFoldersChange()
    }
  }

  return (
    <div className="sidebar">
      <h2>Folders</h2>
      <ul className="folder-list">
        <li
          className={selectedFolder === null ? 'active' : ''}
          onClick={() => onSelectFolder(null)}
        >
          All Favorites
        </li>
        <li
          className={selectedFolder === 0 ? 'active' : ''}
          onClick={() => onSelectFolder(0)}
        >
          Unclassified
        </li>
        {folders.map((folder) => (
          <li
            key={folder.id}
            className={selectedFolder === folder.id ? 'active' : ''}
            onClick={() => onSelectFolder(folder.id)}
          >
            <span className="folder-name">{folder.name}</span>
            <button
              className="btn-delete-folder"
              onClick={(e) => {
                e.stopPropagation()
                handleDeleteFolder(folder.id)
              }}
            >
              ×
            </button>
          </li>
        ))}
      </ul>
      {showNewFolder ? (
        <form onSubmit={handleCreateFolder} className="new-folder-form">
          <input
            type="text"
            value={newFolderName}
            onChange={(e) => setNewFolderName(e.target.value)}
            placeholder="Folder name"
            autoFocus
          />
          <div className="folder-actions">
            <button type="submit" className="btn-save">Create</button>
            <button type="button" onClick={() => setShowNewFolder(false)} className="btn-cancel">
              Cancel
            </button>
          </div>
        </form>
      ) : (
        <button className="btn-new-folder" onClick={() => setShowNewFolder(true)}>
          + New Folder
        </button>
      )}
    </div>
  )
}
