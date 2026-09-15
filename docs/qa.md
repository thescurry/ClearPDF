# Graphics / Steve QA gates

Standing visual checks for polish. Version **1.1.0** is set together in csproj / AssemblyInfo, Inno `AppVersion`, About (`ClearPDF 1.1.0`), and tag `v1.1.0`.

## Empty Recent (release blocker)

Gate: empty/open screen (`EmptyView` in `MainWindow.xaml`, mock `docs/mocks/chrome-arc-empty.png`).

- [ ] Empty Recent: ≤12 rows; filenames never clip leading chars; timestamps fully readable; Open CTA uses accent pill.

Layout notes: empty column is **520** wide (was 300 with a 320-wide Recent list, which clipped both ends). Filename column is left-aligned with `TextTrimming=CharacterEllipsis`. RelativeTime column is `Width=Auto` / `MinWidth=112` with `TextTrimming=None`. `OpenHeroButton` is a filled `#5A7FA6` pill.

`RecentFilesService.MaxEntries` stays **12**. Unit test: add 13 unique paths → `Load().Count == 12` (oldest dropped).

## v1.2 page-selection chrome (does not replace the empty-state gate)

Ship version stays **1.1.0** until Chip’s bump. Graphics mocks: `docs/mocks/v12-01-multiselect.png`, `v12-02-context-menu.png`, `v12-03-save-as-choice.png`.

- One accent `#5A7FA6`. Selected thumb **border** is primary; gray page index; **checks only on selected thumbs** (beside the number, not a second fill on the page).
- Tiny **2-line** thumb context menu: Save pages as... / Print selected.
- Save As with multi-select: quiet in-window card — radios **Selected pages (n) | Entire document**, Cancel + accent Save. No title-bar modal sprawl.
- Steal notes: [`docs/mocks/v12-STEAL.md`](mocks/v12-STEAL.md).
- Empty Recent gate above is unchanged (MaxEntries=12, no leading clip, accent Open pill).

## Deferred (later 1.1.x)

Do **not** build the first-open Welcome card from `docs/mocks/chrome-arc-intro.png` in this pass. Empty-state stays Open + Recent. Track Welcome for a later 1.1.x note.
