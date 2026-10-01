export const ERROR_CODES = {
  unexpected: 'common.unexpected',
  required: 'validation.required',
  tooLong: 'validation.too_long',
  invalidEmail: 'validation.invalid_email',
  passwordPolicy: 'auth.password_policy',
  unauthenticated: 'auth.unauthenticated',
  tokenInvalid: 'auth.token_invalid',
  passwordChangeRequired: 'auth.password_change_required',
  csrfInvalid: 'auth.csrf_invalid',
  concurrencyConflict: 'concurrency.conflict',
  referenceInactive: 'common.reference_inactive',
  emailTaken: 'users.email_taken',
  departmentNameTaken: 'departments.name_taken',
  approverInvalid: 'departments.approver_invalid',
  locationNameTaken: 'locations.name_taken',
  locationCycle: 'locations.cycle',
  teamNameTaken: 'teams.name_taken',
  assetTagTaken: 'assets.tag_taken',
  assetSerialTaken: 'assets.serial_taken',
  categoryNameTaken: 'categories.name_taken',
  categoryTooDeep: 'categories.too_deep',
  requestTypeNameTaken: 'request_types.name_taken',
} as const;

const NETWORK_STATUS = 0;
const NETWORK_CODE = 'network';

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly fieldErrors: Readonly<Record<string, string[]>>;

  constructor(status: number, code: string, fieldErrors: Record<string, string[]> = {}) {
    super(code);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
    this.fieldErrors = fieldErrors;
  }
}

/** The request never reached the server (offline, DNS, CORS). */
export class NetworkError extends ApiError {
  constructor(cause: unknown) {
    super(NETWORK_STATUS, NETWORK_CODE);
    this.name = 'NetworkError';
    this.cause = cause;
  }
}

interface ProblemBody {
  code?: unknown;
  errors?: unknown;
}

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null;

function readFieldErrors(value: unknown): Record<string, string[]> {
  if (!isRecord(value)) {
    return {};
  }
  return Object.fromEntries(
    Object.entries(value).map(([field, codes]) => [field, Array.isArray(codes) ? codes.filter((c): c is string => typeof c === 'string') : []]),
  );
}

export function toApiError(status: number, body: unknown): ApiError {
  const problem: ProblemBody = isRecord(body) ? body : {};
  const code = typeof problem.code === 'string' ? problem.code : ERROR_CODES.unexpected;
  return new ApiError(status, code, readFieldErrors(problem.errors));
}
