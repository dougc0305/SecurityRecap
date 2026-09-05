import axios from 'axios';
import type {
  ApiResponse, LoginRequest, LoginResponse, PagedResponse,
  Property, Report, Incident, Vehicle, AddressOfInterest, ChatRequest, ChatResponse,
  ManagedUser, CreateUserRequest, CreateUserResponse, UpdateUserRequest,
  ResetPasswordResponse, ChangePasswordRequest,
  ApiKey, CreateApiKeyRequest, CreateApiKeyResponse,
  MailboxIngestConfig, SaveMailboxIngestConfigRequest, MailboxPollResult, MailboxVerifyResult,
} from '../types/api';

const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:5069/api';

const api = axios.create({
  baseURL: API_BASE,
});

// Request interceptor — attach JWT
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor — handle 401 with token refresh
api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;
      const refreshToken = localStorage.getItem('refreshToken');
      if (refreshToken) {
        try {
          const res = await axios.post<ApiResponse<LoginResponse>>(`${API_BASE}/auth/refresh`, {
            refreshToken,
          });
          if (res.data.success && res.data.data) {
            localStorage.setItem('accessToken', res.data.data.accessToken);
            localStorage.setItem('refreshToken', res.data.data.refreshToken);
            originalRequest.headers.Authorization = `Bearer ${res.data.data.accessToken}`;
            return api(originalRequest);
          }
        } catch {
          // Refresh failed — clear tokens and redirect to login
        }
      }
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

// Auth
export const authApi = {
  login: (data: LoginRequest) =>
    api.post<ApiResponse<LoginResponse>>('/auth/login', data),
  refresh: (refreshToken: string) =>
    api.post<ApiResponse<LoginResponse>>('/auth/refresh', { refreshToken }),
  changePassword: (data: ChangePasswordRequest) =>
    api.post<ApiResponse<{ changed: boolean }>>('/auth/change-password', data),
};

// API Keys (admin-only)
export const apiKeysApi = {
  getAll: () =>
    api.get<ApiResponse<ApiKey[]>>('/v1/api-keys'),
  create: (data: CreateApiKeyRequest) =>
    api.post<ApiResponse<CreateApiKeyResponse>>('/v1/api-keys', data),
  revoke: (id: string) =>
    api.post<ApiResponse<{ revoked: boolean }>>(`/v1/api-keys/${id}/revoke`),
};

// Users (admin-only)
export const usersApi = {
  getAll: () =>
    api.get<ApiResponse<ManagedUser[]>>('/v1/users'),
  create: (data: CreateUserRequest) =>
    api.post<ApiResponse<CreateUserResponse>>('/v1/users', data),
  update: (id: string, data: UpdateUserRequest) =>
    api.put<ApiResponse<ManagedUser>>(`/v1/users/${id}`, data),
  setActive: (id: string, isActive: boolean) =>
    api.put<ApiResponse<ManagedUser>>(`/v1/users/${id}/active`, { isActive }),
  setProperties: (id: string, propertyIds: string[]) =>
    api.put<ApiResponse<ManagedUser>>(`/v1/users/${id}/properties`, { propertyIds }),
  resetPassword: (id: string) =>
    api.post<ApiResponse<ResetPasswordResponse>>(`/v1/users/${id}/reset-password`),
};

// Properties
export const propertiesApi = {
  getAll: () =>
    api.get<ApiResponse<Property[]>>('/v1/properties'),
  getById: (id: string) =>
    api.get<ApiResponse<Property>>(`/v1/properties/${id}`),
  create: (data: Partial<Property>) =>
    api.post<ApiResponse<Property>>('/v1/properties', data),
  update: (id: string, data: Partial<Property>) =>
    api.put<ApiResponse<Property>>(`/v1/properties/${id}`, data),
};

// Reports
export const reportsApi = {
  getAll: (params: { propertyId?: string; page?: number; pageSize?: number }) =>
    api.get<PagedResponse<Report>>('/v1/reports', { params }),
  getById: (id: string) =>
    api.get<ApiResponse<Report>>(`/v1/reports/${id}`),
  getPdfBlob: (id: string) =>
    api.get<Blob>(`/v1/reports/${id}/pdf`, { responseType: 'blob' }),
  getSummaryBlob: (id: string) =>
    api.get<Blob>(`/v1/reports/${id}/summary`, { responseType: 'blob' }),
};

// Incidents
export const incidentsApi = {
  getAll: (params: {
    propertyId?: string;
    type?: string;
    severity?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }) => api.get<PagedResponse<Incident>>('/v1/incidents', { params }),
};

// Vehicles
export const vehiclesApi = {
  getAll: (params: { propertyId?: string; plate?: string; page?: number; pageSize?: number }) =>
    api.get<PagedResponse<Vehicle>>('/v1/vehicles', { params }),
  getById: (id: string) =>
    api.get<ApiResponse<Vehicle>>(`/v1/vehicles/${id}`),
};

export const addressesApi = {
  getAll: (params: { propertyId?: string; page?: number; pageSize?: number }) =>
    api.get<PagedResponse<AddressOfInterest>>('/v1/addresses', { params }),
  getById: (id: string) =>
    api.get<ApiResponse<{ address: AddressOfInterest; incidents: Incident[] }>>(`/v1/addresses/${id}`),
};

// Ingest
export const ingestApi = {
  uploadReport: (propertyId: string, file: File) => {
    const formData = new FormData();
    formData.append('propertyId', propertyId);
    formData.append('file', file);
    return api.post<ApiResponse<{ reportId: string }>>('/v1/ingest/report', formData);
  },
};

// Mailbox ingest (admin-only)
export const mailboxIngestApi = {
  get: (propertyId: string) =>
    api.get<ApiResponse<MailboxIngestConfig | null>>(`/v1/mailbox-ingest/${propertyId}`),
  save: (data: SaveMailboxIngestConfigRequest) =>
    api.put<ApiResponse<MailboxIngestConfig>>('/v1/mailbox-ingest', data),
  remove: (propertyId: string) =>
    api.delete<ApiResponse<boolean>>(`/v1/mailbox-ingest/${propertyId}`),
  verify: (propertyId: string) =>
    api.post<ApiResponse<MailboxVerifyResult>>(`/v1/mailbox-ingest/${propertyId}/verify`),
  poll: (propertyId: string) =>
    api.post<ApiResponse<MailboxPollResult>>(`/v1/mailbox-ingest/${propertyId}/poll`),
};

// Chat
export const chatApi = {
  send: (data: ChatRequest) =>
    api.post<ApiResponse<ChatResponse>>('/v1/chat', data),
};

export default api;
