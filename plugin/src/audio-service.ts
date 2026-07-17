import { spawn, type ChildProcess } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const STATUS_URL = 'http://127.0.0.1:17891/api/status';
let child: ChildProcess | undefined;

async function isRunning(): Promise<boolean> {
  try {
    const response = await fetch(STATUS_URL, { signal: AbortSignal.timeout(750) });
    return response.ok;
  } catch {
    return false;
  }
}

export async function ensureAudioService(): Promise<void> {
  if (await isRunning()) return;

  const pluginRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
  const executable = path.join(pluginRoot, 'service', 'NobleSampler.AudioService.exe');
  child = spawn(executable, [], {
    cwd: path.dirname(executable),
    windowsHide: true,
    stdio: 'ignore'
  });

  child.once('error', error => console.error(`Unable to start Noble Sampler audio service: ${String(error)}`));
  process.once('exit', () => child?.kill());

  for (let attempt = 0; attempt < 20; attempt++) {
    await new Promise(resolve => setTimeout(resolve, 250));
    if (await isRunning()) return;
  }
  throw new Error('Noble Sampler audio service did not become ready.');
}
