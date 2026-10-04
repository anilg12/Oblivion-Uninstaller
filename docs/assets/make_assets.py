"""Generates the README artwork in docs/assets (run from the repository root):

    python docs/assets/make_assets.py

Everything follows the app's "Graphite" look: warm dark greys, one indigo accent,
hairline borders and the broken-ring mark from the app icon.
"""
import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "icon"))
from make_svg import mark  # noqa: E402

OUT = os.path.dirname(os.path.abspath(__file__))

BG = "#141416"
CARD = "#1C1C1F"
STROKE = "#2A2A2F"
TEXT = "#EDEDEF"
SUB = "#A3A3AA"
FAINT = "#6E6E76"
ACCENT = "#4F6BED"
ACCENT_TEXT = "#A3B4FF"
DOT = "#8EA2FF"

FONT = ('"Segoe UI Variable Display", "Segoe UI", -apple-system, BlinkMacSystemFont, '
        '"SF Pro Display", "Helvetica Neue", Arial, sans-serif')

DEFS = f"""
    <linearGradient id="tile" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#2E3038"/><stop offset="1" stop-color="#16171B"/></linearGradient>
    <linearGradient id="ring" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FFFFFF"/><stop offset="1" stop-color="#D5D6DC"/></linearGradient>
    <pattern id="grid" width="32" height="32" patternUnits="userSpaceOnUse"><path d="M32 0H0V32" fill="none" stroke="#FFFFFF" stroke-opacity="0.035" stroke-width="1"/></pattern>
"""


def write(name, svg):
    with open(os.path.join(OUT, name), "w", encoding="utf-8") as f:
        f.write(svg)
    print("wrote", name)


def esc(t):
    return t.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def polar(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r * math.cos(a), cy + r * math.sin(a)


# --------------------------------------------------------------------------- hero

def hero():
    W, H = 1200, 420
    ts = 208
    tx, ty = 104, (H - ts) / 2 - 6
    cx, cy = tx + ts / 2, ty + ts / 2
    R, sw = ts * 0.265, ts * 0.098

    # Particles drifting out of the ring's opening, on a loop.
    parts = []
    for i, (deg, size, dur, delay) in enumerate([(-46, 7.5, 3.6, 0), (-40, 5, 3.6, 1.2), (-34, 3.4, 3.6, 2.4),
                                                 (-50, 2.6, 4.8, 0.6), (-30, 2.2, 4.8, 3.0)]):
        x0, y0 = polar(cx, cy, R + sw * 0.1, deg)
        x1, y1 = polar(cx, cy, R + sw * 3.4, deg - 4)
        parts.append(
            f'<circle cx="{x0:.1f}" cy="{y0:.1f}" r="{size}" fill="{DOT}" opacity="0">'
            f'<animate attributeName="cx" values="{x0:.1f};{x1:.1f}" dur="{dur}s" begin="{delay}s" repeatCount="indefinite" calcMode="spline" keySplines=".2 .6 .3 1"/>'
            f'<animate attributeName="cy" values="{y0:.1f};{y1:.1f}" dur="{dur}s" begin="{delay}s" repeatCount="indefinite" calcMode="spline" keySplines=".2 .6 .3 1"/>'
            f'<animate attributeName="opacity" values="0;1;0" keyTimes="0;.15;1" dur="{dur}s" begin="{delay}s" repeatCount="indefinite"/>'
            f'<animate attributeName="r" values="{size};{size * 0.35:.2f}" dur="{dur}s" begin="{delay}s" repeatCount="indefinite"/>'
            f'</circle>')

    # A very large, faint copy of the mark that slowly turns at the right edge.
    big_r = 250
    bx, by = 1080, 210
    gx0, gy0 = polar(bx, by, big_r, -26)
    gx1, gy1 = polar(bx, by, big_r, 294)

    pill_y = 300
    pills = [("Windows 10 · 11", 140, False), ("macOS 14+", 112, False), ("Apple Silicon + Intel", 196, False), ("v3.1", 72, True)]
    px = 392
    pill_svg = []
    for label, w, accent in pills:
        if accent:
            pill_svg.append(f'<rect x="{px}" y="{pill_y}" width="{w}" height="34" rx="9" fill="{ACCENT}" fill-opacity="0.16" stroke="{ACCENT}" stroke-opacity="0.5"/>'
                            f'<text x="{px + w / 2}" y="{pill_y + 22.5}" text-anchor="middle" fill="{ACCENT_TEXT}">{label}</text>')
        else:
            pill_svg.append(f'<rect x="{px}" y="{pill_y}" width="{w}" height="34" rx="9" fill="{CARD}" stroke="{STROKE}"/>'
                            f'<text x="{px + w / 2}" y="{pill_y + 22.5}" text-anchor="middle" fill="{TEXT}">{esc(label)}</text>')
        px += w + 12

    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" role="img" aria-label="Oblivion — uninstaller and system cleaner for Windows and macOS">
  <title>Oblivion — uninstaller and system cleaner for Windows and macOS</title>
  <defs>{DEFS}
    <radialGradient id="glow" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="{ACCENT}" stop-opacity="0.55"/><stop offset="1" stop-color="{ACCENT}" stop-opacity="0"/></radialGradient>
    <linearGradient id="fade" x1="0" y1="0" x2="1" y2="0"><stop offset="0.45" stop-color="{BG}" stop-opacity="0"/><stop offset="1" stop-color="{BG}" stop-opacity="0.85"/></linearGradient>
    <linearGradient id="sheen" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0"/><stop offset="0.5" stop-color="#FFFFFF" stop-opacity="0.07"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></linearGradient>
    <clipPath id="card"><rect width="{W}" height="{H}" rx="22"/></clipPath>
    <clipPath id="tileclip"><rect x="{tx}" y="{ty}" width="{ts}" height="{ts}" rx="47"/></clipPath>
    <filter id="soft" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="16"/></filter>
    <style>
      text {{ font-family: {FONT}; }}
      .rise1 {{ animation: rise .8s cubic-bezier(.2,.7,.2,1) both .05s; }}
      .rise2 {{ animation: rise .8s cubic-bezier(.2,.7,.2,1) both .2s; }}
      .rise3 {{ animation: rise .8s cubic-bezier(.2,.7,.2,1) both .35s; }}
      .breathe {{ animation: breathe 6s ease-in-out infinite; transform-origin: {cx}px {cy}px; }}
      .turn {{ animation: turn 90s linear infinite; transform-origin: {bx}px {by}px; }}
      .sheen {{ animation: sheen 7s ease-in-out infinite 1.5s; }}
      @keyframes rise {{ from {{ opacity: 0; transform: translateY(10px); }} to {{ opacity: 1; transform: none; }} }}
      @keyframes breathe {{ 0%, 100% {{ opacity: .35; transform: scale(1); }} 50% {{ opacity: .6; transform: scale(1.06); }} }}
      @keyframes turn {{ to {{ transform: rotate(360deg); }} }}
      @keyframes sheen {{ 0% {{ transform: translateX(-260px); }} 35%, 100% {{ transform: translateX(300px); }} }}
      @media (prefers-reduced-motion: reduce) {{ .rise1, .rise2, .rise3, .breathe, .turn, .sheen {{ animation: none; }} }}
    </style>
  </defs>
  <g clip-path="url(#card)">
    <rect width="{W}" height="{H}" fill="{BG}"/>
    <rect width="{W}" height="{H}" fill="url(#grid)"/>
    <g class="turn" opacity="0.07">
      <path d="M {gx0:.1f} {gy0:.1f} A {big_r} {big_r} 0 1 1 {gx1:.1f} {gy1:.1f}" fill="none" stroke="#FFFFFF" stroke-width="44" stroke-linecap="round"/>
    </g>
    <rect width="{W}" height="{H}" fill="url(#fade)"/>
  </g>
  <rect x="0.5" y="0.5" width="{W - 1}" height="{H - 1}" rx="21.5" fill="none" stroke="{STROKE}"/>

  <g class="rise1">
    <circle class="breathe" cx="{cx}" cy="{cy}" r="{ts * 0.78:.0f}" fill="url(#glow)"/>
    <rect x="{tx}" y="{ty + 12}" width="{ts}" height="{ts}" rx="47" fill="#000" opacity="0.55" filter="url(#soft)"/>
    <rect x="{tx}" y="{ty}" width="{ts}" height="{ts}" rx="47" fill="url(#tile)"/>
    <rect x="{tx + 1}" y="{ty + 1}" width="{ts - 2}" height="{ts - 2}" rx="46" fill="none" stroke="#FFFFFF" stroke-opacity="0.09" stroke-width="2"/>
    <g clip-path="url(#tileclip)"><rect class="sheen" x="{tx}" y="{ty}" width="200" height="{ts}" fill="url(#sheen)"/></g>
    {mark(cx, cy, R, sw)}
    {"".join(parts)}
  </g>

  <g class="rise2">
    <text x="388" y="196" font-size="84" font-weight="600" letter-spacing="-2" fill="{TEXT}">Oblivion</text>
    <text x="391" y="244" font-size="25" fill="{SUB}">Programları iz bırakmadan kaldır, bilgisayarını güvenle temizle.</text>
  </g>
  <g class="rise3" font-size="15" font-weight="600">
    {"".join(pill_svg)}
  </g>
</svg>
'''


# --------------------------------------------------------------------------- download buttons

WIN_GLYPH = '''<g fill="{c}"><rect x="0" y="0" width="12" height="12" rx="1.5"/><rect x="14" y="0" width="12" height="12" rx="1.5"/><rect x="0" y="14" width="12" height="12" rx="1.5"/><rect x="14" y="14" width="12" height="12" rx="1.5"/></g>'''
GLOBE_GLYPH = '''<g fill="none" stroke="{c}" stroke-width="2" stroke-linecap="round"><circle cx="13" cy="13" r="11"/><path d="M2 13h22M13 2c3.2 3.2 4.6 6.8 4.6 11S16.2 20.8 13 24M13 2C9.8 5.2 8.4 8.8 8.4 13s1.4 7.8 4.6 11"/></g>'''
MAC_GLYPH = '''<g fill="none" stroke="{c}" stroke-width="2.2" stroke-linejoin="round" stroke-linecap="round"><rect x="3" y="2" width="20" height="15" rx="2.5"/><path d="M0 22h26"/></g>'''


def button(name, glyph, title, detail, primary, external=False):
    W, H = 420, 92
    fill = ACCENT if primary else CARD
    stroke = ACCENT if primary else STROKE
    title_c = "#FFFFFF" if primary else TEXT
    detail_c = "#DCE3FF" if primary else SUB
    icon_bg = "#FFFFFF" if primary else "#26262A"
    icon_op = "0.16" if primary else "1"
    glyph_c = "#FFFFFF" if primary else TEXT
    arrow_c = "#FFFFFF" if primary else ACCENT_TEXT
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" role="img" aria-label="{esc(title)} — {esc(detail)}">
  <title>{esc(title)} — {esc(detail)}</title>
  <defs>
    <linearGradient id="shine" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="#FFFFFF" stop-opacity="0"/><stop offset="0.5" stop-color="#FFFFFF" stop-opacity="{0.16 if primary else 0.05}"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></linearGradient>
    <clipPath id="c"><rect x="1" y="1" width="{W - 2}" height="{H - 2}" rx="15"/></clipPath>
    <style>
      text {{ font-family: {FONT}; }}
      .shine {{ animation: shine 5s ease-in-out infinite {1.2 if primary else 2.6}s; }}
      .nudge {{ animation: nudge 2.4s ease-in-out infinite; }}
      @keyframes shine {{ 0% {{ transform: translateX(-180px); }} 40%, 100% {{ transform: translateX({W + 40}px); }} }}
      @keyframes nudge {{ 0%, 60%, 100% {{ transform: translateY(0); }} 30% {{ transform: translateY(3px); }} }}
      @media (prefers-reduced-motion: reduce) {{ .shine, .nudge {{ animation: none; }} }}
    </style>
  </defs>
  <rect x="1" y="1" width="{W - 2}" height="{H - 2}" rx="15" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>
  <g clip-path="url(#c)"><rect class="shine" x="0" y="0" width="160" height="{H}" fill="url(#shine)"/></g>
  <rect x="20" y="20" width="52" height="52" rx="12" fill="{icon_bg}" fill-opacity="{icon_op}"/>
  <g transform="translate(33 {34 if glyph is MAC_GLYPH else 33})">{glyph.format(c=glyph_c)}</g>
  <text x="90" y="42" font-size="20" font-weight="600" fill="{title_c}">{esc(title)}</text>
  <text x="90" y="66" font-size="14" fill="{detail_c}">{esc(detail)}</text>
  <g transform="translate({W - 46} 34)"><g class="nudge" fill="none" stroke="{arrow_c}" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round">
    <path d="{'M5 19L19 5M8 5h11v11' if external else 'M12 2v18M4 13l8 8 8-8'}"/>
  </g></g>
</svg>
'''


# --------------------------------------------------------------------------- feature cards

ICONS = {
    # 24x24 line icons
    "trash": '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3"/>',
    "pulse": '<path d="M2 12h4l2.5-6 4 13 3-9 1.5 2H22"/>',
    "sparkle": '<path d="M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z"/><path d="M19 16l.7 1.8 1.8.7-1.8.7L19 21l-.7-1.8-1.8-.7 1.8-.7z"/>',
    "target": '<circle cx="12" cy="12" r="8.5"/><circle cx="12" cy="12" r="4"/><path d="M12 1v4M12 19v4M1 12h4M19 12h4"/>',
    "power": '<path d="M12 3v8"/><path d="M6.3 6.8a8 8 0 1 0 11.4 0"/>',
    "shield": '<path d="M12 2.5l8 3v6.2c0 4.6-3.3 8.4-8 9.8-4.7-1.4-8-5.2-8-9.8V5.5z"/><path d="M8.5 12l2.5 2.5 4.5-5"/>',
}


def wrap(text, width):
    words, lines, cur = text.split(), [], ""
    for w in words:
        if len(cur) + len(w) + 1 > width and cur:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        lines.append(cur)
    return lines


def cards(name, items, cols=3, label="Oblivion"):
    W = 1200
    gap = 20
    cw = (W - gap * (cols - 1)) / cols
    ch = 196
    rows = math.ceil(len(items) / cols)
    H = rows * ch + (rows - 1) * gap
    out = []
    for i, (icon, title, body, tag) in enumerate(items):
        r, c = divmod(i, cols)
        x, y = c * (cw + gap), r * (ch + gap)
        delay = 0.08 * i
        lines = wrap(body, 44)
        text = "".join(f'<tspan x="{x + 28}" dy="{0 if j == 0 else 22}">{esc(l)}</tspan>' for j, l in enumerate(lines))
        tag_svg = ""
        if tag:
            tw = 12 + len(tag) * 7.6
            tag_svg = (f'<rect x="{x + cw - 28 - tw}" y="{y + 30}" width="{tw}" height="22" rx="6" fill="{ACCENT}" fill-opacity="0.16"/>'
                       f'<text x="{x + cw - 28 - tw / 2}" y="{y + 45.5}" text-anchor="middle" font-size="11" font-weight="700" letter-spacing=".6" fill="{ACCENT_TEXT}">{esc(tag)}</text>')
        out.append(f'''
  <g class="card" style="animation-delay:{delay:.2f}s">
    <rect x="{x + 0.5}" y="{y + 0.5}" width="{cw - 1}" height="{ch - 1}" rx="16" fill="{CARD}" stroke="{STROKE}"/>
    <rect x="{x + 28}" y="{y + 26}" width="44" height="44" rx="11" fill="#26262A" stroke="{STROKE}"/>
    <g transform="translate({x + 38} {y + 36})" fill="none" stroke="{TEXT}" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">{ICONS[icon]}</g>
    {tag_svg}
    <text x="{x + 28}" y="{y + 104}" font-size="19" font-weight="600" fill="{TEXT}">{esc(title)}</text>
    <text x="{x + 28}" y="{y + 134}" font-size="14.5" fill="{SUB}">{text}</text>
  </g>''')
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" role="img" aria-label="{esc(label)}">
  <title>{esc(label)}</title>
  <defs>
    <style>
      text {{ font-family: {FONT}; }}
      .card {{ animation: up .7s cubic-bezier(.2,.7,.2,1) both; }}
      @keyframes up {{ from {{ opacity: 0; transform: translateY(12px); }} to {{ opacity: 1; transform: none; }} }}
      @media (prefers-reduced-motion: reduce) {{ .card {{ animation: none; }} }}
    </style>
  </defs>{"".join(out)}
</svg>
'''


# --------------------------------------------------------------------------- section title

def section(name, number, title, subtitle):
    W, H = 1200, 84
    return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" role="img" aria-label="{esc(title)}">
  <title>{esc(title)}</title>
  <defs>
    <linearGradient id="line" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{ACCENT}"/><stop offset="0.6" stop-color="{ACCENT}" stop-opacity="0.15"/><stop offset="1" stop-color="{ACCENT}" stop-opacity="0"/></linearGradient>
    <clipPath id="c"><rect width="{W}" height="{H}" rx="16"/></clipPath>
    <style>
      text {{ font-family: {FONT}; }}
      .draw {{ animation: draw 1.4s cubic-bezier(.2,.7,.2,1) both .15s; transform-origin: 0 0; }}
      .in {{ animation: in .8s cubic-bezier(.2,.7,.2,1) both; }}
      @keyframes draw {{ from {{ transform: scaleX(0); }} to {{ transform: scaleX(1); }} }}
      @keyframes in {{ from {{ opacity: 0; transform: translateX(-8px); }} to {{ opacity: 1; transform: none; }} }}
      @media (prefers-reduced-motion: reduce) {{ .draw, .in {{ animation: none; }} }}
    </style>
  </defs>
  <g clip-path="url(#c)">
    <rect width="{W}" height="{H}" fill="{BG}"/>
    <rect width="{W}" height="{H}" fill="url(#grid)"/>
    <rect class="draw" x="0" y="{H - 2}" width="{W}" height="2" fill="url(#line)"/>
  </g>
  <rect x="0.5" y="0.5" width="{W - 1}" height="{H - 1}" rx="15.5" fill="none" stroke="{STROKE}"/>
  <defs>{DEFS}</defs>
  <g class="in">
    <rect x="28" y="24" width="44" height="36" rx="9" fill="{ACCENT}" fill-opacity="0.16" stroke="{ACCENT}" stroke-opacity="0.45"/>
    <text x="50" y="48" text-anchor="middle" font-size="15" font-weight="700" letter-spacing="1" fill="{ACCENT_TEXT}">{esc(number)}</text>
    <text x="92" y="52" font-size="27" font-weight="600" letter-spacing="-0.4" fill="{TEXT}">{esc(title)}<tspan fill="{FAINT}" font-size="17" font-weight="400" dx="16">{esc(subtitle)}</tspan></text>
  </g>
</svg>
'''


if __name__ == "__main__":
    write("banner.svg", hero())
    write("download-windows.svg", button("w", WIN_GLYPH, "Windows için indir", "Windows 10 · 11 — kurulum sihirbazı", True))
    write("download-mac.svg", button("m", MAC_GLYPH, "Mac için indir", "macOS 14+ — Apple Silicon ve Intel", False))
    write("features.svg", cards("features", [
        ("trash", "İz bırakmadan kaldırma", "Programı kendi kaldırıcısıyla kaldırır, sonra dosya ve kayıt kalıntılarını bulur.", ""),
        ("pulse", "Canlı sistem izleyici", "İşlemci, bellek, sıcaklık, ağ, diskler, pil ve en yoğun işlemler — anlık.", "YENİ"),
        ("sparkle", "Güvenli temizlik", "Hiçbir şey kendiliğinden seçilmez. Silinecek her dosyayı tek tek görürsün.", ""),
        ("target", "Avcı modu", "Bir pencereye nişan al; Oblivion programı bulsun, kapatsın ya da kaldırsın.", ""),
        ("power", "Başlangıç yöneticisi", "Açılışta çalışan öğeleri silmeden kapat, istediğinde yeniden aç.", ""),
        ("shield", "Önce güvenlik", "Her silmeden önce tam liste ve onay. Sistem klasörleri asla önerilmez.", ""),
    ], label="Oblivion özellikleri"))
    write("portfolio.svg", button("p", GLOBE_GLYPH, "Portfolyomu ziyaret et", "anilg12.github.io", False, external=True))
    for key, num, title, sub in [("features", "01", "Neler yapar?", "Tek uygulama, temiz bir sistem"),
                                 ("screens", "02", "Bir bakışta", "Windows ve macOS"),
                                 ("download", "03", "İndir", "Ücretsiz ve açık kaynak"),
                                 ("more", "04", "Daha fazlası", "Derleme, sorular ve geliştirici")]:
        write(f"section-{key}.svg", section(key, num, title, sub))
