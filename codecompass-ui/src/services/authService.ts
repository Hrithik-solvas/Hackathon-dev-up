const AUTH_KEY = 'codecompass_user';

export interface AuthUser {
  username: string;
  loggedInAt: string;
}

export function getUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem(AUTH_KEY);
    return raw ? (JSON.parse(raw) as AuthUser) : null;
  } catch {
    return null;
  }
}

export function login(username: string): void {
  const user: AuthUser = { username, loggedInAt: new Date().toISOString() };
  localStorage.setItem(AUTH_KEY, JSON.stringify(user));
}

export function logout(): void {
  localStorage.removeItem(AUTH_KEY);
}
