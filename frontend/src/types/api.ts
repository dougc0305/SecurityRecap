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
  /** Subset of propertyIds whose nightly summary this user is emailed. */
  summaryPropertyIds: string[];
  /** Subset of propertyIds whose pickup failures this user is emailed about. */
  alertPropertyIds: string[];
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

export interface ApiKey {
  id: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface CreateApiKeyRequest {
  name: string;
}

export interface CreateApiKeyResponse {
  apiKey: ApiKey;
  rawKey: string;
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
  mdSummaryUrl: string | null;
  aiSummaryHtml: string | null;
  officerNames: string[];
  createdAt: string;
}

export interface MailboxIngestConfig {
  propertyId: string;
  graphTenantId: string;
  graphClientId: string;
  hasClientSecret: boolean;
  mailboxAddress: string;
  folderName: string;
  fromAddress: string | null;
  subjectContains: string | null;
  attachmentNameContains: string | null;
  lookbackDays: number;
  pollIntervalMinutes: number;
  activeWindowStart: string | null;
  activeWindowEnd: string | null;
  activeWindowPollMinutes: number;
  scheduleTimeZone: string | null;
  staleAfterHours: number;
  markAsRead: boolean;
  moveToFolder: string | null;
  sendSummaryEmail: boolean;
  summaryRecipients: string[];
  summarySubjectPrefix: string | null;
  isEnabled: boolean;
  lastPolledAt: string | null;
  lastSuccessAt: string | null;
  lastMessageReceivedAt: string | null;
  lastError: string | null;
  consecutiveFailures: number;
  lastReportIngestedAt: string | null;
  lastAlertAt: string | null;
}

export interface SaveMailboxIngestConfigRequest {
  propertyId: string;
  graphTenantId: string;
  graphClientId: string;
  /** Omit to keep the stored secret; supply a value to replace it. */
  graphClientSecret?: string | null;
  mailboxAddress: string;
  folderName: string;
  fromAddress: string | null;
  subjectContains: string | null;
  attachmentNameContains: string | null;
  lookbackDays: number;
  pollIntervalMinutes: number;
  activeWindowStart: string | null;
  activeWindowEnd: string | null;
  activeWindowPollMinutes: number;
  scheduleTimeZone: string | null;
  staleAfterHours: number;
  markAsRead: boolean;
  moveToFolder: string | null;
  sendSummaryEmail: boolean;
  summaryRecipients: string[];
  summarySubjectPrefix: string | null;
  isEnabled: boolean;
}

export interface MailboxMessageOutcome {
  subject: string;
  fromAddress: string;
  receivedAtUtc: string;
  fileName: string | null;
  reportId: string | null;
  alreadyIngested: boolean;
  summaryEmailSent: boolean;
  error: string | null;
}

export interface MailboxPollResult {
  propertyId: string;
  propertyName: string;
  succeeded: boolean;
  messagesExamined: number;
  reportsIngested: number;
  messagesSkipped: number;
  messages: MailboxMessageOutcome[];
  error: string | null;
}

export interface MailboxVerifyResult {
  mailbox: string;
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

export interface OfficerShifts { officer: string; shifts: number }

export interface RecapCoverage {
  from: string;
  to: string;
  nightsInPeriod: number;
  reportsReceived: number;
  usableReports: number;
  missingDates: string[];
  emptyReportDates: string[];
  officers: OfficerShifts[];
  averagePatrolsPerShift: number;
  minPatrols: number;
  maxPatrols: number;
}

export interface RecapCategoryCount {
  category: string; high: number; medium: number; low: number; total: number;
}

export interface RecapEntry {
  reportDate: string;
  localTime: string | null;
  incidentType: string;
  severity: string;
  location: string | null;
  description: string;
}

export interface RecapViolation {
  reportDate: string;
  localTime: string | null;
  plateNumber: string | null;
  plateState: string | null;
  vehicle: string | null;
  location: string | null;
  violationType: string | null;
  severity: string;
  noticeIssued: boolean;
  towNotified: boolean;
  plateViolationsAllTime: number;
  plateFirstSeen: string | null;
  plateViolationsInPeriod: number;
}

export interface RecapRecurringItem {
  description: string; location: string | null; occurrences: number; first: string; last: string;
}

export interface RecapData {
  propertyName: string;
  timeZone: string;
  coverage: RecapCoverage;
  totalEntries: number;
  routineEntries: number;
  substantiveEntries: number;
  byCategory: RecapCategoryCount[];
  substantiveDetail: RecapEntry[];
  highSeverity: RecapEntry[];
  violations: RecapViolation[];
  recurringMaintenance: RecapRecurringItem[];
  repeatLocations: RecapRecurringItem[];
}

export interface RecapAttentionItem {
  title: string;
  when: string | null;
  what: string;
  whyItMatters: string;
  significance: string;
}

export interface RecapNarrative {
  headline: string;
  attentionItems: RecapAttentionItem[];
  dataNotes: string[];
  markdownSummary: string;
}

export interface RecapResult {
  data: RecapData;
  narrative: RecapNarrative | null;
  narrativeError: string | null;
}
