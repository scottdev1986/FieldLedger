const TOKEN_KEY = "fieldledger.accessToken";

let accessToken: string | null = null;

export function hydrateAccessToken() {
  if (typeof window !== "undefined") {
    accessToken = window.localStorage.getItem(TOKEN_KEY);
  }
  return accessToken;
}

export function getAccessToken() {
  return accessToken;
}

export function persistAccessToken(token: string) {
  accessToken = token;
  if (typeof window !== "undefined") {
    window.localStorage.setItem(TOKEN_KEY, token);
  }
}

export function clearAccessToken() {
  accessToken = null;
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(TOKEN_KEY);
  }
}

export function logout() {
  clearAccessToken();
  if (typeof window !== "undefined") {
    window.dispatchEvent(new Event("fieldledger:session-cleared"));
  }
}
