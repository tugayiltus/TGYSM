import { z } from 'zod';

import { ERROR_CODES } from '../../shared/api/errors';

/** Mirrors backend PasswordRules (ADR-0004). */
export const PASSWORD_MIN_LENGTH = 12;
export const PASSWORD_MAX_LENGTH = 128;
const LETTER_PATTERN = /\p{L}/u;
const DIGIT_PATTERN = /\d/;
const MISMATCH_MESSAGE_KEY = 'auth:changePassword.mismatch';

export const newPasswordRule = z
  .string()
  .min(PASSWORD_MIN_LENGTH, ERROR_CODES.passwordPolicy)
  .max(PASSWORD_MAX_LENGTH, ERROR_CODES.passwordPolicy)
  .regex(LETTER_PATTERN, ERROR_CODES.passwordPolicy)
  .regex(DIGIT_PATTERN, ERROR_CODES.passwordPolicy);

export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, ERROR_CODES.required),
    newPassword: newPasswordRule,
    confirmPassword: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: MISMATCH_MESSAGE_KEY,
    path: ['confirmPassword'],
  });

export type ChangePasswordValues = z.infer<typeof changePasswordSchema>;

export const setPasswordSchema = z
  .object({ newPassword: newPasswordRule, confirmPassword: z.string() })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: MISMATCH_MESSAGE_KEY,
    path: ['confirmPassword'],
  });

export type SetPasswordValues = z.infer<typeof setPasswordSchema>;
