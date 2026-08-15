"""Synthesize a clean sample SALES ORDER image used to drive and test extraction.

Layout is intentionally table-based and labeled so the heuristic normalizer can
be tested deterministically, while still matching the field set described in the
assessment (order date, external ref, debtor, payment, items, VAT, totals).
"""
from PIL import Image, ImageDraw, ImageFont
import os

OUT = os.path.join(os.path.dirname(__file__), "..", "samples", "order-sample.png")

W, H = 1240, 1500
BG = (255, 255, 255)
INK = (20, 20, 20)
GRAY = (90, 90, 90)
LINE = (160, 160, 160)

def font(size):
    for name in ("arial.ttf", "segoeui.ttf", "tahoma.ttf"):
        p = os.path.join("C:\\Windows\\Fonts", name)
        if os.path.exists(p):
            try:
                return ImageFont.truetype(p, size)
            except Exception:
                pass
    return ImageFont.load_default()

F_TITLE = font(30)
F_BIG = font(22)
F_MED = font(18)
F_SMALL = font(15)

def main():
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(img)

    y = 30
    d.text((40, y), "TJM Labs  |  Automation", font=F_TITLE, fill=INK); y += 40
    d.text((40, y), "SALES ORDER", font=F_TITLE, fill=INK); y += 34
    d.text((40, y), "SYNTHETIC DATA  WEB.2026-0714-A17", font=F_MED, fill=GRAY); y += 30

    # order meta
    d.text((40, y), "Order Date: 2026-07-14", font=F_MED, fill=INK); y += 26
    d.text((40, y), "External Reference: WEB.2026-0714-A17", font=F_MED, fill=INK); y += 26
    d.text((40, y), "Customer ID: cusT-1007", font=F_MED, fill=INK); y += 26
    y += 14

    # --- customer block ---
    d.rectangle([30, y, W - 30, y + 260], outline=LINE, width=2)
    d.text((50, y + 12), "CUSTOMER AND CONTACT", font=F_MED, fill=GRAY)
    lines = [
        ("Company", "NorthStar Office GmbH"),
        ("Name", "Marta Klein"),
        ("Alias", "NORTHSTAR-BERLIN"),
        ("Address", "88 Friedrichstra\u00dfe, 10117 Berlin, Germany"),
        ("Email", "marta.klein@northstar.example"),
        ("Phone", "+49 30 5555 1420"),
    ]
    cy = y + 42
    for k, v in lines:
        d.text((50, cy), f"{k}: {v}", font=F_MED, fill=INK); cy += 32
    y += 270

    # --- payment block ---
    d.rectangle([30, y, W - 30, y + 160], outline=LINE, width=2)
    d.text((50, y + 12), "PAYMENT", font=F_MED, fill=GRAY)
    pay = [
        ("Payment Method", "Bank Transfer"),
        ("Payment Status", "PAID"),
        ("Payment Date", "2026-07-18"),
    ]
    py = y + 42
    for k, v in pay:
        d.text((50, py), f"{k}: {v}", font=F_MED, fill=INK); py += 32
    y += 170

    # --- items table ---
    d.text((40, y), "ITEMS", font=F_MED, fill=GRAY); y += 30
    cols = ["SKU", "Description", "Qty", "Unit net", "VAT %", "Disc %", "Line total"]
    colw = [230, 400, 80, 120, 90, 90, 150]
    x0 = 40
    row_h = 42
    header_y = y
    d.rectangle([x0, header_y, x0 + sum(colw), header_y + row_h], outline=LINE, width=2)
    hx = x0
    for c, w in zip(cols, colw):
        d.text((hx + 10, header_y + 10), c, font=F_MED, fill=GRAY)
        hx += w
    y += row_h

    rows = [
        ("MAT.OESX.02", "Ergonomic Office Chair", "1", "570.00", "19", "0", "570.00"),
        ("MAT.TBL.01", "Standing Desk", "2", "350.00", "19", "5", "665.00"),
    ]
    for r in rows:
        d.rectangle([x0, y, x0 + sum(colw), y + row_h], outline=LINE, width=2)
        hx = x0
        for val, w in zip(r, colw):
            d.text((hx + 10, y + 10), val, font=F_MED, fill=INK)
            hx += w
        y += row_h
    y += 14

    # --- totals ---
    totals = [
        ("Net total", "1235.00"),
        ("VAT (19%)", "234.65"),
        ("Total", "1469.65"),
    ]
    for k, v in totals:
        d.text((40, y), f"{k}: EUR {v}", font=F_MED, fill=INK); y += 28

    img.save(OUT)
    print("wrote", os.path.abspath(OUT))

if __name__ == "__main__":
    main()
