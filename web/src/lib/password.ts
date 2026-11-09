/** Same rules as the server's PasswordPolicy; the server remains the authority. */
export function passwordProblem(password: string, username?: string): string | null {
  if (password.length < 8) return 'password_too_short'
  if (password.length > 128) return 'password_too_long'
  if (!/\p{L}/u.test(password) || !/\d/.test(password)) return 'password_needs_letter_and_digit'
  if (username && username.trim() && password.toLowerCase().includes(username.trim().toLowerCase())) return 'password_contains_username'
  return null
}

/** Readable temporary password (no 0/O/1/l) for administrators to hand out. */
export function generatePassword(): string {
  const letters = 'abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ'
  const digits = '23456789'
  const bytes = new Uint32Array(12)
  crypto.getRandomValues(bytes)
  return Array.from(bytes, (b, i) => {
    const set = i % 4 === 3 ? digits : letters
    return set[b % set.length]
  }).join('')
}
