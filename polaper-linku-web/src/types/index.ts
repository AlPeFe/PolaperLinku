export interface Folder {
  id: number
  name: string
  createdAt: string
}

export interface Favorite {
  id: number
  title: string
  url: string
  description: string
  createdAt: string
  folderId: number | null
  folder: Folder | null
  previewImage: string | null
}
