import { mkdir, readdir, rm } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const packageRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const buildRoot = resolve(packageRoot, 'build');

await mkdir(buildRoot, { recursive: true });
for (const entry of await readdir(buildRoot, { withFileTypes: true })) {
  if (entry.name === '.gitkeep') continue;
  await rm(resolve(buildRoot, entry.name), { recursive: true, force: true });
}
