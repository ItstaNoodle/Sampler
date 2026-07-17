import commonjs from '@rollup/plugin-commonjs';
import { nodeResolve } from '@rollup/plugin-node-resolve';
import typescript from '@rollup/plugin-typescript';

export default {
  input: 'src/plugin.ts',
  output: {
    file: 'nobles.sampler.sdPlugin/bin/plugin.js',
    format: 'esm',
    sourcemap: true
  },
  plugins: [nodeResolve(), commonjs(), typescript()],
  external: ['fs', 'path', 'os', 'util', 'events', 'stream', 'buffer', 'http', 'https', 'url', 'crypto', 'zlib']
};
