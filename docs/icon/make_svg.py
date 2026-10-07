import math, sys
def polar(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r*math.cos(a), cy + r*math.sin(a)

def mark(cx, cy, R, stroke, small=False, ring="url(#ring)", dot="#8EA2FF"):
    # ring with a gap top right (svg angles: 0 = right, negative = up)
    gap_from, gap_to = -66, -26          # the opening
    x0, y0 = polar(cx, cy, R, gap_to)     # arc starts after the gap, runs clockwise round to gap_from
    x1, y1 = polar(cx, cy, R, gap_from + 360)
    sweep = (gap_from + 360) - gap_to
    large = 1 if sweep > 180 else 0
    out = [f'<path d="M {x0:.2f} {y0:.2f} A {R} {R} 0 {large} 1 {x1:.2f} {y1:.2f}" fill="none" stroke="{ring}" stroke-width="{stroke}" stroke-linecap="round"/>']
    # particles leaving through the gap
    if small:
        dots = [(-50, R + stroke*0.05, stroke*0.42, 1.0), (-40, R + stroke*1.25, stroke*0.27, 0.8)]
    else:
        dots = [(-46, R + stroke*0.10, stroke*0.36, 1.0), (-39, R + stroke*1.15, stroke*0.24, 0.8), (-33, R + stroke*2.0, stroke*0.14, 0.55)]
    for deg, r, size, op in dots:
        x, y = polar(cx, cy, r, deg)
        out.append(f'<circle cx="{x:.2f}" cy="{y:.2f}" r="{size:.2f}" fill="{dot}" fill-opacity="{op}"/>')
    return "\n".join(out)

def tile_svg(size, mac=False, small=False):
    W = 1024
    if mac:
        tx, ty, ts, rad = 100, 92, 824, 186
    else:
        tx, ty, ts, rad = 24, 24, 976, 220
    cx, cy = tx + ts/2, ty + ts/2
    if small:
        R, stroke = ts*0.27, ts*0.135
    else:
        R, stroke = ts*0.265, ts*0.098
    shadow = ''
    if mac:
        shadow = f'<rect x="{tx}" y="{ty+14}" width="{ts}" height="{ts}" rx="{rad}" fill="#000" opacity="0.35" filter="url(#blur)"/>'
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {W} {W}">
<defs>
  <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2E3038"/><stop offset="1" stop-color="#16171B"/></linearGradient>
  <linearGradient id="ring" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="1" stop-color="#D5D6DC"/></linearGradient>
  <linearGradient id="edge" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0.10"/><stop offset="0.5" stop-color="#FFFFFF" stop-opacity="0.03"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0.06"/></linearGradient>
  <filter id="blur" x="-20%" y="-20%" width="140%" height="140%"><feGaussianBlur stdDeviation="22"/></filter>
</defs>
{shadow}
<rect x="{tx}" y="{ty}" width="{ts}" height="{ts}" rx="{rad}" fill="url(#bg)"/>
<rect x="{tx+3}" y="{ty+3}" width="{ts-6}" height="{ts-6}" rx="{rad-3}" fill="none" stroke="url(#edge)" stroke-width="6"/>
{mark(cx, cy, R, stroke, small)}
</svg>'''

def glyph_svg(size, color_ring, color_dot):
    # just the mark, transparent background (used inside the app)
    W = 1024
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 {W} {W}">
{mark(W/2, W/2+20, 300, 120, ring=color_ring, dot=color_dot)}
</svg>'''

if __name__ == "__main__":
    out = sys.argv[1]
    open(f"{out}/mac.svg","w").write(tile_svg(1024, mac=True))
    open(f"{out}/win.svg","w").write(tile_svg(256))
    open(f"{out}/win_small.svg","w").write(tile_svg(32, small=True))
    print("ok")
