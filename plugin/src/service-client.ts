const BASE_URL = 'http://127.0.0.1:17891/api';

export class SamplerServiceClient {
  private async request(path: string, method = 'GET', body?: unknown): Promise<Response> {
    const response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers: body ? { 'content-type': 'application/json' } : undefined,
      body: body ? JSON.stringify(body) : undefined,
      signal: AbortSignal.timeout(3000)
    });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);
    return response;
  }

  async status(): Promise<boolean> {
    try { await this.request('/status'); return true; } catch { return false; }
  }
  async startRecording(bank: number, slot: number, bufferSeconds: number): Promise<void> {
    await this.request('/record/start', 'POST', { bank, slot, bufferSeconds });
  }
  async stopRecording(bank: number, slot: number): Promise<void> {
    await this.request('/record/stop', 'POST', { bank, slot });
  }
  async play(bank: number, slot: number, volume: number): Promise<void> {
    await this.request('/play', 'POST', { bank, slot, volume });
  }
  async stop(): Promise<void> { await this.request('/stop', 'POST'); }
}
