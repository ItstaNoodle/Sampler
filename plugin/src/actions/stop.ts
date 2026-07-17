import { action, KeyDownEvent, SingletonAction } from '@elgato/streamdeck';
import { SamplerServiceClient } from '../service-client.js';
const service = new SamplerServiceClient();
@action({ UUID: 'nobles.sampler.stop' })
export class StopAction extends SingletonAction {
  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try { await service.stop(); await ev.action.showOk(); } catch { await ev.action.showAlert(); }
  }
}
