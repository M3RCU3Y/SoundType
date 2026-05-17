# Sample Rejection Notes

Date: 2026-05-17

These notes record source packs the app should not rebundle or recreate unless the user explicitly asks for a new experiment with that source.

## Rejected Typewriter Packs

| Removed folder | Pack id | Display name | Reason |
| --- | --- | --- | --- |
| `assets/packs/Freesound-PortableKeyPair` | `fs-portable-key-pair` | Portable Key Pair | Too low-variation, low-quality, and glitchy sounding. |
| `assets/packs/BSB-HermesTypewriter-Mixed` | `bsb-hermes-mixed-desk` | Hermes Precisa Clean Mix | Annoying/harsh user experience even after cleanup. |
| `assets/packs/BSB-HermesTypewriter-Singles` | `bsb-hermes-singles` | Hermes Precisa Clean Singles | Annoying/harsh user experience and too little key variation. |

## Replacement Rules

- Avoid Hermes Precisa material and the previous portable two-key source pair.
- Prefer packs with at least 24 normal key samples or a source recording that can be cleanly segmented into that many distinct one-shots.
- Include one or more dedicated Enter samples when the source allows it, ideally a margin bell, carriage return, or bell-plus-return.
- Keep Enter samples short and controlled; long ringing tails should be trimmed and faded so repeated Enter presses do not become piercing.
- Record source URL, author, license, and processing notes in each pack's `SOURCE.txt`.
