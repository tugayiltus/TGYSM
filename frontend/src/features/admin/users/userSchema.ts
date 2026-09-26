import { z } from 'zod';

import { ERROR_CODES } from '../../../shared/api/errors';

import type { UserListItem, UserRole } from './api';

/** Mirror backend User.NameMaxLength / EmailMaxLength. */
export const USER_NAME_MAX_LENGTH = 100;
export const USER_EMAIL_MAX_LENGTH = 254;
export const USER_ROLES = ['employee', 'it_staff', 'admin'] as const satisfies readonly UserRole[];
const DEFAULT_ROLE: UserRole = 'employee';
const NO_SELECTION = '';

const name = z.string().trim().min(1, ERROR_CODES.required).max(USER_NAME_MAX_LENGTH, ERROR_CODES.tooLong);

export const userFormSchema = z.object({
  firstName: name,
  lastName: name,
  email: z.string().trim().min(1, ERROR_CODES.required).max(USER_EMAIL_MAX_LENGTH, ERROR_CODES.tooLong).pipe(z.email(ERROR_CODES.invalidEmail)),
  role: z.enum(USER_ROLES),
  departmentId: z.string(),
  locationId: z.string(),
});

export type UserFormValues = z.infer<typeof userFormSchema>;

export function toFormValues(user: UserListItem | null): UserFormValues {
  if (user === null) {
    return { firstName: '', lastName: '', email: '', role: DEFAULT_ROLE, departmentId: NO_SELECTION, locationId: NO_SELECTION };
  }
  return {
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email,
    role: user.role,
    departmentId: user.departmentId ?? NO_SELECTION,
    locationId: user.locationId ?? NO_SELECTION,
  };
}

export function toRequestFields(values: UserFormValues) {
  return {
    firstName: values.firstName,
    lastName: values.lastName,
    email: values.email,
    role: values.role,
    departmentId: values.departmentId === NO_SELECTION ? null : values.departmentId,
    locationId: values.locationId === NO_SELECTION ? null : values.locationId,
  };
}
