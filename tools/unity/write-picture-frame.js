// Writes art/unity/picture-frame.svg: the gilt picture frame of the design canvas (HomeTR, MapTR), laid out so that the
// game can draw it at any size by repeating the middle stretch of each side. The frame's numbers depend on one another
// (the beads must close around the ring and fall half a gap from each end of the repeating stretch), so the source is
// written from them here and not by hand. Run it after changing a number, then tools/unity/export-gallery-art.js.
//
// usage (from the repository root): node tools/unity/write-picture-frame.js
const fs = require("fs");
const M = 27.625;          // room around the frame for its shadow on the wall
const BORDER = 23;         // the frame's own width
const PITCH = 4.75;        // beads
const TILE = 10 * PITCH;   // the stretch that repeats
const CORNER = 65;         // M + BORDER + inner shade (14.375)
const SIZE = 2 * CORNER + TILE;   // 177.5
const F = SIZE - 2 * M;    // frame's outer side, 122.25
const n = v => Number(v.toFixed(3));
const r = (x, y, w, h, extra = "") => `<rect x="${n(x)}" y="${n(y)}" width="${n(w)}" height="${n(h)}" ${extra}/>`;
const hole = F - 2 * BORDER;
const beads = [];
const start = 16, end = F - 16, side = end - start;      // bead ring, in frame coordinates
const count = Math.round(side / PITCH);                  // 19 gaps: 90.25 / 4.75
if (Math.abs(count * PITCH - side) > 1e-6) throw new Error("the bead ring does not close: " + side / PITCH);
for (let i = 0; i < count; i++) {
  const t = start + i * PITCH;
  beads.push([t, start], [end, t], [end - i * PITCH, end], [start, end - i * PITCH]);
}
const dots = (dx, dy, radius, fill) => beads.map(([x, y]) => `<circle cx="${n(x + dx)}" cy="${n(y + dy)}" r="${radius}" fill="${fill}"/>`).join("");
const rosette = (x, y) => {
  const cx = x + 10.2, cy = y + 10.2;
  const petal = "M-2.39 -4.14A2.5 2.5 0 0 1 2.39 -4.14A2.5 2.5 0 0 1 4.78 0A2.5 2.5 0 0 1 2.39 4.14A2.5 2.5 0 0 1 -2.39 4.14A2.5 2.5 0 0 1 -4.78 0A2.5 2.5 0 0 1 -2.39 -4.14Z";
  return r(x, y, 20.4, 20.4, 'rx="4" fill="#5E3305"')
    + r(x + 1, y + 1, 18.4, 18.4, 'rx="3.2" fill="url(#corner)"')
    + r(x + 0.6, y + 0.5, 18.4, 18.4, 'rx="3.2" fill="none" stroke="#FFF6CC" stroke-opacity=".8" stroke-width="1"')
    + `<path d="${petal}" transform="translate(${n(cx + 0.6)} ${n(cy + 1)})" fill="#9C5C0B"/>`
    + `<path d="${petal}" transform="translate(${n(cx)} ${n(cy)})" fill="#FFF1B4"/>`
    + `<circle cx="${n(cx)}" cy="${n(cy)}" r="1.7" fill="#D38D1A"/>`;
};
const svg = `<!-- The gilt frame of a painting, as the design canvas draws it on the museum's home (HomeTR, the frame of the
     painting being restored): four mitred bars, a row of beads on the inner band, a rosette on each corner, the
     frame's shadow on the wall and its shade on the picture. One unit is one point of the design. The frame is
     ${n(F)} points square at (${M}, ${M}), ${BORDER} wide, around a hole of ${n(hole)} points. The game draws it
     at any size by repeating the middle stretch of each side (x or y from ${CORNER} to ${CORNER + TILE}), which
     holds ten beads from half a gap to half a gap: nothing there may change along its side.
     Written by tools/unity/write-picture-frame.js from the canvas' own numbers; the beads are ${PITCH} points apart. -->
<svg xmlns="http://www.w3.org/2000/svg" width="${SIZE * 4}" height="${SIZE * 4}" viewBox="0 0 ${SIZE} ${SIZE}">
<defs>
<linearGradient id="top" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFF6CC"/><stop offset="1" stop-color="#F2B63C"/></linearGradient>
<linearGradient id="foot" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#C98A1E"/><stop offset="1" stop-color="#9C5C0B"/></linearGradient>
<linearGradient id="left" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFE892"/><stop offset="1" stop-color="#E8A62C"/></linearGradient>
<linearGradient id="right" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#D99826"/><stop offset="1" stop-color="#B4720F"/></linearGradient>
<linearGradient id="corner" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#FFF1B4"/><stop offset=".5" stop-color="#F6B93C"/><stop offset="1" stop-color="#C98A1E"/></linearGradient>
<linearGradient id="shade-top" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2A1606" stop-opacity=".5"/><stop offset="1" stop-color="#2A1606" stop-opacity="0"/></linearGradient>
<linearGradient id="shade-left" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#2A1606" stop-opacity=".32"/><stop offset="1" stop-color="#2A1606" stop-opacity="0"/></linearGradient>
<filter id="soft" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="6"/></filter>
<clipPath id="in-frame">${r(1.5, 1.5, F - 3, F - 3, 'rx="3.5"')}</clipPath>
<clipPath id="beside-the-frame"><path clip-rule="evenodd" d="M0 0H${SIZE}V${SIZE}H0ZM${n(M + 2)} ${n(M + 2)}V${n(M + F - 2)}H${n(M + F - 2)}V${n(M + 2)}Z"/></clipPath>
</defs>
<!-- the shadow on the wall, a little below the frame; none of it shows through the frame's own hole -->
<g clip-path="url(#beside-the-frame)">${r(M + 5, M + 12, F - 10, F - 5, 'rx="6" fill="#031E24" fill-opacity=".62" filter="url(#soft)"')}</g>
<g transform="translate(${M} ${M})">
<!-- the dark ground of the bars, with the hole for the picture -->
<path d="M0 5A5 5 0 0 1 5 0H${n(F - 5)}A5 5 0 0 1 ${n(F)} 5V${n(F - 5)}A5 5 0 0 1 ${n(F - 5)} ${n(F)}H5A5 5 0 0 1 0 ${n(F - 5)}ZM${BORDER} ${BORDER}V${n(F - BORDER)}H${n(F - BORDER)}V${BORDER}Z" fill="#5E3305" fill-rule="evenodd"/>
<!-- the four mitred bars: lit from the upper left -->
<g clip-path="url(#in-frame)">
<path d="M1.5 1.5H${n(F - 1.5)}L${n(F - 13.5)} 13.5H13.5Z" fill="url(#top)"/><path d="M1.5 1.5L13.5 13.5V${n(F - 13.5)}L1.5 ${n(F - 1.5)}Z" fill="url(#left)"/><path d="M${n(F - 13.5)} 13.5L${n(F - 1.5)} 1.5V${n(F - 1.5)}L${n(F - 13.5)} ${n(F - 13.5)}Z" fill="url(#right)"/><path d="M13.5 ${n(F - 13.5)}H${n(F - 13.5)}L${n(F - 1.5)} ${n(F - 1.5)}H1.5Z" fill="url(#foot)"/>
<path d="M9.6 9.6H${n(F - 9.6)}V${n(F - 9.6)}H9.6Z" fill="none" stroke="#9C5C0B" stroke-opacity=".5" stroke-width="1.2"/>
<path d="M5 5H${n(F - 5)}V${n(F - 5)}H5Z" fill="none" stroke="#FFF6CC" stroke-opacity=".95" stroke-width="2"/>
<path d="M7 7H${n(F - 7)}V${n(F - 7)}H7Z" fill="none" stroke="#9C5C0B" stroke-opacity=".3" stroke-width="1"/>
</g>
<!-- the inner band with its beads, each over its own shade -->
<path d="M13.5 13.5H${n(F - 13.5)}L${n(F - 18.5)} 18.5H18.5Z" fill="#FFEDA6"/><path d="M13.5 13.5L18.5 18.5V${n(F - 18.5)}L13.5 ${n(F - 13.5)}Z" fill="#FADA7C"/><path d="M${n(F - 18.5)} 18.5L${n(F - 13.5)} 13.5V${n(F - 13.5)}L${n(F - 18.5)} ${n(F - 18.5)}Z" fill="#E3A635"/><path d="M18.5 ${n(F - 18.5)}H${n(F - 18.5)}L${n(F - 13.5)} ${n(F - 13.5)}H13.5Z" fill="#D2962A"/>
<path d="M13.5 13.5H${n(F - 13.5)}V${n(F - 13.5)}H13.5Z" fill="none" stroke="#9C5C0B" stroke-width="1"/>
${dots(0.5, 0.8, 1.5, "#9C5C0B")}
${dots(0, 0, 1.3, "#FFF6CC")}
<!-- the cream lip around the picture -->
<path d="M18.5 18.5H${n(F - 18.5)}V${n(F - 18.5)}H18.5ZM${BORDER} ${BORDER}H${n(F - BORDER)}V${n(F - BORDER)}H${BORDER}Z" fill="#F6ECD2" fill-rule="evenodd" stroke="#5E3305" stroke-width="1.2"/>
<path d="M19.3 ${n(F - 19.3)}V19.3H${n(F - 19.3)}" fill="none" stroke="#B8985A" stroke-width="1.2"/>
<!-- the rosettes -->
${rosette(0.8, 0.8)}${rosette(F - 21.2, 0.8)}${rosette(F - 21.2, F - 21.2)}${rosette(0.8, F - 21.2)}
<!-- the frame's shade on the picture, along its top and its left side -->
${r(BORDER, BORDER, hole, 11, 'fill="url(#shade-top)"')}${r(BORDER, BORDER, 8, hole, 'fill="url(#shade-left)"')}
${r(BORDER, BORDER, hole, hole, 'fill="none" stroke="#2A1606" stroke-opacity=".55" stroke-width="1"')}
</g>
</svg>
`;
fs.writeFileSync("art/unity/picture-frame.svg", svg);
console.log("frame", SIZE, "hole", hole, "beads per side", count);
