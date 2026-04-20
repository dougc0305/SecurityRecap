export interface ApiResponse<T> {
  data: T | null;
  error: string | null;
  success: boolean;
}

export interface PagedResponse<T> {
  data: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  success: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}

export interface UserInfo {
  id: string;
  email: string;
  fullName: string;
  role: string;
  tenantId: string;
  mustChangePassword: boolean;
}

export interface ManagedUser {
  id: string;
  email: string;
  fullName: string;
  role: string;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  propertyIds: string[];
}

export interface CreateUserRequest {
  email: string;
  fullName: string;
  role: string;
  propertyIds: string[];
}

export interface CreateUserResponse {
  user: ManagedUser;
  tempPassword: string;
}

export interface UpdateUserRequest {
  fullName: string;
  role: string;
}

export interface ResetPasswordResponse {
  tempPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface Property {
  id: string;
  name: string;
  address: string;
  city: string;
  state: string;
  zip: string;
  securityCompany: string | null;
  reportEmail: string | null;
  timezone: string;
  isActive: boolean;
  createdAt: string;
}

export interface Report {
  id: string;
  propertyId: string;
  reportDate: string;
  periodStart: string | null;
  periodEnd: string | null;
  rawPdfUrl: string | null;
  aiSummaryHtml: string | null;
  officerNames: string[];
  createdAt: string;
}

export interface Incident {
  id: string;
  reportId: string;
  propertyId: string;
  incidentTime: string | null;
  incidentType: string;
  severity: string;
  location: string | null;
  description: string;
  officerName: string | null;
  lawEnforcement: boolean;
  caseNumber: string | null;
  createdAt: string;
}

export interface Violation {
  id: string;
  incidentId: string;
  vehicleId: string | null;
  violationType: string;
  location: string | null;
  noticeIssued: boolean;
  towNotified: boolean;
  createdAt: string;
}

export interface Vehicle {
  id: string;
  propertyId: string;
  plateNumber: string;
  plateState: string | null;
  make: string | null;
  model: string | null;
  color: string | null;
  firstSeen: string | null;
  lastSeen: string | null;
  violationCount: number;
  notes: string | null;
  createdAt: string;
  violations?: Violation[];
}

export interface AddressOfInterest {
  id: string;
  propertyId: string;
  address: string;
  label: string | null;
  incidentCount: number;
  firstFlagged: string | null;
  lastIncident: string | null;
  notes: string | null;
  createdAt: string;
}

export interface ChatRequest {
  propertyId: string;
  message: string;
  conversationHistory?: { role: string; content: string }[];
}

export interface ChatResponse {
  response: string;
}
