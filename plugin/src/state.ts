import type { KeyAction } from '@elgato/streamdeck';

export type SamplerSettings = {
  slot?: number;
  label?: string;
  bufferSeconds?: number;
  holdMilliseconds?: number;
  volume?: number;
};

let activeBank = 1;
const samplerKeys = new Map<string, { action: KeyAction<SamplerSettings>; settings: SamplerSettings }>();
const bankKeys = new Map<string, KeyAction>();

export function getBank(): number { return activeBank; }
export async function setBank(bank: number): Promise<void> {
  activeBank = Math.min(4, Math.max(1, bank));
  await Promise.all([
    ...Array.from(samplerKeys.values()).map(({ action, settings }) => updateSamplerTitle(action, settings)),
    ...Array.from(bankKeys.values()).map(action => action.setTitle(`BANK\n${activeBank} / 4`))
  ]);
}
export function trackSampler(id: string, action: KeyAction<SamplerSettings>, settings: SamplerSettings): void {
  samplerKeys.set(id, { action, settings });
}
export function untrackSampler(id: string): void { samplerKeys.delete(id); }
export function trackBank(id: string, action: KeyAction): void { bankKeys.set(id, action); }
export function untrackBank(id: string): void { bankKeys.delete(id); }
export async function updateSamplerTitle(action: KeyAction<SamplerSettings>, settings: SamplerSettings, status?: string): Promise<void> {
  const slot = settings.slot ?? 1;
  const label = settings.label?.trim() || `Slot ${slot}`;
  await action.setTitle(status ? `${status}\nB${activeBank}:${slot}` : `${label}\nB${activeBank}:${slot}`);
}
