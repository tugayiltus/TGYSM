import { CAlert } from '@coreui/react';

export function FormAlert({ message }: { message: string | null | undefined }) {
  if (!message) {
    return null;
  }
  return (
    <CAlert color="danger" className="mb-0">
      {message}
    </CAlert>
  );
}
