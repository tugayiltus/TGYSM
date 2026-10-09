import { z } from 'zod';

import { ERROR_CODES } from '../../../shared/api/errors';

/** Mirrors backend CatalogName.MaxLength. */
const NAME_MAX_LENGTH = 100;

export const nameSchema = z.object({ name: z.string().trim().min(1, ERROR_CODES.required).max(NAME_MAX_LENGTH, ERROR_CODES.tooLong) });
export type NameValues = z.infer<typeof nameSchema>;

export const NAME_FIELDS = ['name'] as const;
/** A name that differs from an existing one only in case, spacing or Turkish i is a duplicate (shown under the name). */
export const NAME_CODE_FIELDS = { [ERROR_CODES.assetCatalogDuplicate]: 'name' } as const;
