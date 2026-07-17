import streamDeck from '@elgato/streamdeck';
import { SamplerAction } from './actions/sampler.js';
import { BankAction } from './actions/bank.js';
import { StopAction } from './actions/stop.js';
import { ensureAudioService } from './audio-service.js';

streamDeck.actions.registerAction(new SamplerAction());
streamDeck.actions.registerAction(new BankAction());
streamDeck.actions.registerAction(new StopAction());
try {
  await ensureAudioService();
} catch (error) {
  streamDeck.logger.error(String(error));
}
streamDeck.connect();
