export function failMessage(err: unknown) {
  return err instanceof Error ? err.message : "Request failed.";
}
