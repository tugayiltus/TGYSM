export function FormAlert({ message }: { message: string | null | undefined }) {
  if (!message) {
    return null;
  }
  return (
    <div role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800">
      {message}
    </div>
  );
}
