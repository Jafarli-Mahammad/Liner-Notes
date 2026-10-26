import { copyFile, cp, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const root = new URL('../', import.meta.url);
const webroot = new URL('../wwwroot/', root);
// Copy only Home-owned outputs: never empty wwwroot or touch manual-test/legacy assets.
await mkdir(new URL('home-assets/', webroot), { recursive: true });
await cp(new URL('dist/home-assets/', root), new URL('home-assets/', webroot), { recursive: true });
await copyFile(new URL('dist/index.html', root), new URL('index.html', webroot));
console.log(`Home build copied to ${fileURLToPath(webroot)}; existing assets retained.`);
