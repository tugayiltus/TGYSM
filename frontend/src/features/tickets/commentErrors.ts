import { describeError } from '../../shared/api/errorMessage';
import { ApiError } from '../../shared/api/errors';

const FILES_FIELD = 'files';
const BODY_FIELD = 'body';

/** File errors arrive as a field error on "files"; body errors on "body". */
export function describeCommentError(error: unknown): string {
  if (error instanceof ApiError) {
    const code = error.fieldErrors[FILES_FIELD]?.[0] ?? error.fieldErrors[BODY_FIELD]?.[0];
    if (code !== undefined) {
      return describeError(new ApiError(error.status, code));
    }
  }
  return describeError(error);
}
