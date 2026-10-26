import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { join } from 'node:path';

const lock = JSON.parse(await readFile('package-lock.json', 'utf8'));
const notices = [];
for (const [path, entry] of Object.entries(lock.packages)) {
  if (!path || entry.dev || entry.optional) continue;
  const pkg = JSON.parse(await readFile(join(path, 'package.json'), 'utf8'));
  let license;
  for (const name of ['LICENSE', 'LICENSE.md', 'LICENSE.txt']) {
    try { license = await readFile(join(path, name), 'utf8'); break; }
    catch (error) { if (error.code !== 'ENOENT') throw error; }
  }
  if (!license) throw new Error(`Missing license text for runtime package ${pkg.name}`);
  notices.push(`${pkg.name} ${pkg.version}\n${license}`);
}
await mkdir('dist/home-assets/licenses', { recursive: true });
await writeFile('dist/home-assets/licenses/ThirdParty.txt', notices.join('\n\n---\n\n'));
