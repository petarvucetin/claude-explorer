import sharp from 'sharp';
import toIco from 'to-ico';
import { writeFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outIco = resolve(__dirname, '../src/ClaudeExplorer.App/app.ico');
const outPng = resolve(__dirname, '../src/ClaudeExplorer.App/wwwroot/app-icon.png');

// Blueprint aesthetic: dark navy bg, electric-blue corner ticks + crosshair explorer symbol
const svg = `<svg width="256" height="256" xmlns="http://www.w3.org/2000/svg">
  <!-- Background -->
  <rect width="256" height="256" rx="36" ry="36" fill="#16202E"/>

  <!-- Corner ticks (Blueprint drafting marks) -->
  <polyline points="20,52 20,20 52,20" fill="none" stroke="#1F47D6" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="204,20 236,20 236,52" fill="none" stroke="#1F47D6" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="20,204 20,236 52,236" fill="none" stroke="#1F47D6" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="204,236 236,236 236,204" fill="none" stroke="#1F47D6" stroke-width="5" stroke-linecap="round" stroke-linejoin="round"/>

  <!-- Outer ring -->
  <circle cx="128" cy="128" r="72" fill="none" stroke="#1F47D6" stroke-width="3" opacity="0.5"/>

  <!-- Inner ring -->
  <circle cx="128" cy="128" r="40" fill="none" stroke="#1F47D6" stroke-width="3"/>

  <!-- Crosshair lines -->
  <line x1="128" y1="44" x2="128" y2="82" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="128" y1="174" x2="128" y2="212" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="44" y1="128" x2="82" y2="128" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="174" y1="128" x2="212" y2="128" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>

  <!-- Cardinal tick marks on inner ring -->
  <line x1="128" y1="88" x2="128" y2="96" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="128" y1="160" x2="128" y2="168" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="88" y1="128" x2="96" y2="128" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>
  <line x1="160" y1="128" x2="168" y2="128" stroke="#1F47D6" stroke-width="3" stroke-linecap="round"/>

  <!-- Center dot -->
  <circle cx="128" cy="128" r="7" fill="#1F47D6"/>
  <circle cx="128" cy="128" r="3" fill="#EEF1F5"/>
</svg>`;

const sizes = [16, 32, 48, 256];

const pngBuffers = await Promise.all(
  sizes.map(size =>
    sharp(Buffer.from(svg))
      .resize(size, size)
      .png()
      .toBuffer()
  )
);

// Write .ico (all sizes embedded)
const ico = await toIco(pngBuffers);
writeFileSync(outIco, ico);
console.log(`app.ico written (${(ico.length / 1024).toFixed(1)} KB)`);

// Write 256px PNG for Photino window icon
writeFileSync(outPng, pngBuffers[3]);
console.log(`app-icon.png written`);
