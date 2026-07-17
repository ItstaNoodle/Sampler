import { action, KeyAction, KeyDownEvent, SingletonAction, WillAppearEvent, WillDisappearEvent } from '@elgato/streamdeck';
import { getBank, setBank, trackBank, untrackBank } from '../state.js';

type BankSettings = { direction?: 'next' | 'previous'; bank?: number };

@action({ UUID: 'com.noble.sampler.bank' })
export class BankAction extends SingletonAction<BankSettings> {
  override async onWillAppear(ev: WillAppearEvent<BankSettings>): Promise<void> {
    trackBank(ev.action.id, ev.action as KeyAction);
    await ev.action.setTitle(`BANK\n${getBank()} / 4`);
  }
  override onWillDisappear(ev: WillDisappearEvent<BankSettings>): void { untrackBank(ev.action.id); }
  override async onKeyDown(ev: KeyDownEvent<BankSettings>): Promise<void> {
    const settings = ev.payload.settings;
    let bank = Number(settings.bank ?? 0);
    if (bank < 1 || bank > 4) {
      bank = settings.direction === 'previous' ? (getBank() + 2) % 4 + 1 : getBank() % 4 + 1;
    }
    await setBank(bank);
  }
}
