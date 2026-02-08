import axios from 'axios'
import { Favorite, Folder } from '../types'

const API_BASE_URL = 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json'
  }
})

export const favoritesApi = {
  getAll: async (folderId?: number, orderBy?: string): Promise<Favorite[]> => {
    const params = new URLSearchParams()
    if (folderId) params.append('folderId', folderId.toString())
    if (orderBy) params.append('orderBy', orderBy)
    const response = await api.get(`/favorites?${params.toString()}`)
    return response.data
  },

  create: async (url: string, folderId?: number): Promise<Favorite> => {
    const response = await api.post('/favorites', { url, folderId })
    return response.data
  },

  update: async (id: number, favorite: Partial<Favorite>): Promise<Favorite> => {
    const response = await api.put(`/favorites/${id}`, favorite)
    return response.data
  },

  delete: async (id: number): Promise<void> => {
    await api.delete(`/favorites/${id}`)
  },

  export: async (): Promise<Favorite[]> => {
    const response = await api.get('/favorites/export')
    return response.data
  }
}

export const foldersApi = {
  getAll: async (): Promise<Folder[]> => {
    const response = await api.get('/folders')
    return response.data
  },

  create: async (folder: Omit<Folder, 'id' | 'createdAt'>): Promise<Folder> => {
    const response = await api.post('/folders', folder)
    return response.data
  },

  delete: async (id: number): Promise<void> => {
    await api.delete(`/folders/${id}`)
  }
}
