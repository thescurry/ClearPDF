# Graphics / Steve QA gates

Standing visual checks for polish. Chip owns the **1.1.0** version bump — this file is feel/QA only.

## Empty Recent (release blocker)

Gate: empty/open screen (`EmptyView` in `MainWindow.xaml`, mock `docs/mocks/chrome-arc-empty.png`).

- [ ] Empty Recent: ≤12 rows; filenames never clip leading chars; timestamps fully readable; Open CTA uses accent pill.

Layout notes: empty column is **520** wide (was 300 with a 320-wide Recent list, which clipped both ends). Filename column is left-aligned with `TextTrimming=CharacterEllipsis`. RelativeTime column is `Width=Auto` / `MinWidth=112` with `TextTrimming=None`. `OpenHeroButton` is a filled `#5A7FA6` pill.

`RecentFilesService.MaxEntries` stays **12**. Unit test: add 13 unique paths → `Load().Count == 12` (oldest dropped).

## Deferred (later 1.1.x)

Do **not** build the first-open Welcome card from `docs/mocks/chrome-arc-intro.png` in this pass. Empty-state stays Open + Recent. Track Welcome for a later 1.1.x note.
