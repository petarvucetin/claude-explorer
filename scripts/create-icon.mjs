import sharp from 'sharp';
import toIco from 'to-ico';
import { writeFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outIco = resolve(__dirname, '../src/ClaudeExplorer.App/app.ico');
const outPng = resolve(__dirname, '../src/ClaudeExplorer.App/wwwroot/app-icon.png');

// Blueprint: light paper bg, graph-paper grid, corner ticks, magnifying glass
// with a bold "C" inside the lens — "Claude Explorer"
const svg = `<svg width="256" height="256" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <clipPath id="bg-clip">
      <rect width="256" height="256" rx="38" ry="38"/>
    </clipPath>
    <pattern id="grid" width="16" height="16" patternUnits="userSpaceOnUse">
      <line x1="16" y1="0" x2="0" y2="0" stroke="#BDC8D6" stroke-width="0.6"/>
      <line x1="0" y1="0" x2="0" y2="16" stroke="#BDC8D6" stroke-width="0.6"/>
    </pattern>
    <!-- Lens glow -->
    <radialGradient id="lens-fill" cx="42%" cy="38%" r="58%">
      <stop offset="0%" stop-color="#FFFFFF"/>
      <stop offset="100%" stop-color="#E8EDF4"/>
    </radialGradient>
    <!-- Handle gradient -->
    <linearGradient id="handle-grad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#263750"/>
      <stop offset="100%" stop-color="#16202E"/>
    </linearGradient>
  </defs>

  <!-- Background: light paper -->
  <rect width="256" height="256" rx="38" ry="38" fill="#EEF1F5"/>
  <!-- Graph-paper grid (clipped to rounded rect) -->
  <rect width="256" height="256" fill="url(#grid)" clip-path="url(#bg-clip)" opacity="0.55"/>

  <!-- Corner ticks — Blueprint drafting marks -->
  <polyline points="19,54 19,19 54,19"   fill="none" stroke="#16202E" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="202,19 237,19 237,54" fill="none" stroke="#16202E" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="19,202 19,237 54,237" fill="none" stroke="#16202E" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>
  <polyline points="202,237 237,237 237,202" fill="none" stroke="#16202E" stroke-width="5.5" stroke-linecap="round" stroke-linejoin="round"/>

  <!-- Handle shadow/depth -->
  <line x1="155" y1="158" x2="221" y2="224" stroke="#16202E" stroke-width="26" stroke-linecap="round" opacity="0.15"/>
  <!-- Handle body -->
  <line x1="153" y1="155" x2="220" y2="222" stroke="url(#handle-grad)" stroke-width="22" stroke-linecap="round"/>
  <!-- Handle highlight stripe -->
  <line x1="150" y1="152" x2="210" y2="212" stroke="#FFFFFF" stroke-width="5" stroke-linecap="round" opacity="0.18"/>

  <!-- Lens fill (gradient) -->
  <circle cx="100" cy="97" r="67" fill="url(#lens-fill)"/>

  <!-- Inside the lens: bold "C" in electric blue -->
  <!-- Arc for the C -->
  <path d="M 129 62
             A 42 42 0 1 0 129 132"
        fill="none"
        stroke="#1F47D6"
        stroke-width="14"
        stroke-linecap="round"/>
  <!-- Top terminal dot -->
  <circle cx="129" cy="62"  r="7" fill="#1F47D6"/>
  <!-- Bottom terminal dot -->
  <circle cx="129" cy="132" r="7" fill="#1F47D6"/>
  <!-- Tiny connector nubs — wiring-terminal style -->
  <line x1="129" y1="62"  x2="143" y2="62"  stroke="#1F47D6" stroke-width="4" stroke-linecap="round"/>
  <line x1="129" y1="132" x2="143" y2="132" stroke="#1F47D6" stroke-width="4" stroke-linecap="round"/>

  <!-- Lens ring (drawn last so it's on top) -->
  <circle cx="100" cy="97" r="67" fill="none" stroke="#16202E" stroke-width="12"/>
  <!-- Inner ring highlight -->
  <circle cx="100" cy="97" r="67" fill="none" stroke="#FFFFFF" stroke-width="2.5" opacity="0.25"/>
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

const ico = await toIco(pngBuffers);
writeFileSync(outIco, ico);
console.log(`app.ico written (${(ico.length / 1024).toFixed(1)} KB)`);

writeFileSync(outPng, pngBuffers[3]);
console.log('app-icon.png written (256px preview)');
