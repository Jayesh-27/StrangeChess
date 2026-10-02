import type { GameState, BotResponse, ErrorResponse } from './types';

const BASE = '';  // same origin in production; proxied in dev

async function request<T>(url: string, body?: unknown): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);

  try {
    const res = await fetch(`${BASE}${url}`, {
      method: body ? 'POST' : 'GET',
      headers: body ? { 'Content-Type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      signal: controller.signal,
    });

    if (!res.ok) {
      const err: ErrorResponse = await res.json().catch(() => ({ error: `HTTP ${res.status}` }));
      throw new Error(err.error || `Request failed (${res.status})`);
    }

    return await res.json();
  } finally {
    clearTimeout(timeout);
  }
}

export async function newGame(mode: string = 'classic'): Promise<GameState> {
  return request<GameState>('/api/new', { mode });
}

export async function makeMove(state: GameState, uci: string): Promise<GameState> {
  return request<GameState>('/api/move', { state, uci });
}

export async function botMove(
  state: GameState,
  preset: { depth: number; timeMs: number },
): Promise<BotResponse> {
  return request<BotResponse>('/api/bot', {
    state,
    depth: preset.depth,
    timeMs: preset.timeMs,
  });
}

export async function healthCheck(): Promise<boolean> {
  try {
    const res = await fetch(`${BASE}/health`, { signal: AbortSignal.timeout(5000) });
    return res.ok;
  } catch {
    return false;
  }
}
