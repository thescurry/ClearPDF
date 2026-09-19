# v1.2 page-selection — steal from Graphics mocks

Locked look: `v12-01-multiselect.png`, `v12-02-context-menu.png`, `v12-03-save-as-choice.png`.

One accent only: **`#5A7FA6`**. Do not invent a second color (no system-blue radios, no green checks, no orange pills).

## Multi-select thumbs (`v12-01`)

- **Selected border is primary** — 2px `#5A7FA6` around the page image.
- Page index stays a **gray** circle under the thumb (not accent-filled).
- **Checks only on selected thumbs** — a simple accent check beside the number, not a filled badge on the page.
- Unselected: gray border, gray number, no check.

## Tiny context menu (`v12-02`)

Two lines, no icons, no gutter:

1. Save pages as...
2. Print selected

White rounded card, hairline border, light shadow.

## Save as choice (`v12-03`)

Quiet compact **in-window card** (no title-bar modal sprawl):

- Title: Save as
- One row of radios: **Selected pages (n)** | **Entire document** (accent dots)
- Cancel + accent Save
- Light dim over the reader; Escape / dim click dismisses

Explicit **Save pages as...** (toolbar / menu / thumb item) skips this card and extracts.

## Out of scope

Acrobat edit, forms, annotations, OCR. Empty Recent gate unchanged.
