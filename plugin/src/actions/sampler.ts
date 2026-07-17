import streamDeck, { action, KeyAction, KeyDownEvent, KeyUpEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from '@elgato/streamdeck';
import { SamplerServiceClient } from '../service-client.js';
import { getBank, SamplerSettings, trackSampler, untrackSampler, updateSamplerTitle } from '../state.js';

const service = new SamplerServiceClient();
type HoldState = { timer: ReturnType<typeof setTimeout>; recording: boolean; bank: number; slot: number; settings: SamplerSettings };
const holds = new Map<string, HoldState>();

@action({ UUID: 'com.noble.sampler.sample' })
export class SamplerAction extends SingletonAction<SamplerSettings> {
  override async onWillAppear(ev: WillAppearEvent<SamplerSettings>): Promise<void> {
    const settings = normalize(ev.payload.settings);
    trackSampler(ev.action.id, ev.action as KeyAction<SamplerSettings>, settings);
    await updateSamplerTitle(ev.action as KeyAction<SamplerSettings>, settings);
  }

  override onWillDisappear(ev: WillDisappearEvent<SamplerSettings>): void {
    const hold = holds.get(ev.action.id);
    if (hold) clearTimeout(hold.timer);
    holds.delete(ev.action.id);
    untrackSampler(ev.action.id);
  }

  override async onKeyDown(ev: KeyDownEvent<SamplerSettings>): Promise<void> {
    const settings = normalize(ev.payload.settings);
    const bank = getBank();
    const slot = settings.slot!;
    const state: HoldState = {
      recording: false,
      bank,
      slot,
      settings,
      timer: setTimeout(async () => {
        try {
          await service.startRecording(bank, slot, settings.bufferSeconds!);
          state.recording = true;
          await updateSamplerTitle(ev.action, settings, 'REC');
        } catch (error) {
          streamDeck.logger.error(`Recording failed: ${String(error)}`);
          await ev.action.showAlert();
        }
      }, settings.holdMilliseconds)
    };
    holds.set(ev.action.id, state);
  }

  override async onKeyUp(ev: KeyUpEvent<SamplerSettings>): Promise<void> {
    const state = holds.get(ev.action.id);
    if (!state) return;
    clearTimeout(state.timer);
    holds.delete(ev.action.id);
    try {
      if (state.recording) {
        await service.stopRecording(state.bank, state.slot);
        await updateSamplerTitle(ev.action, state.settings, 'SAVED');
        await ev.action.showOk();
        setTimeout(() => void updateSamplerTitle(ev.action, state.settings), 900);
      } else {
        await service.play(state.bank, state.slot, state.settings.volume!);
      }
    } catch (error) {
      streamDeck.logger.error(`Sampler action failed: ${String(error)}`);
      await ev.action.showAlert();
      await updateSamplerTitle(ev.action, state.settings);
    }
  }
}

function normalize(settings: SamplerSettings): Required<SamplerSettings> {
  return {
    slot: Math.min(32, Math.max(1, Number(settings.slot ?? 1))),
    label: settings.label ?? '',
    bufferSeconds: Math.min(15, Math.max(0, Number(settings.bufferSeconds ?? 5))),
    holdMilliseconds: Math.min(2000, Math.max(200, Number(settings.holdMilliseconds ?? 400))),
    volume: Math.min(2, Math.max(0, Number(settings.volume ?? 1)))
  };
}
