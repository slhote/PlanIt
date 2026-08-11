export function initials(username: string): string {
  return username
    .split(".")
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("")
    .slice(0, 2);
}
