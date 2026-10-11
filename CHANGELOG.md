# Changelog

## 1.1.0
- Fixed Headpat/Boop syncing: each now uses a single sync parameter (`Utility/HeadpatSync`, `Utility/BoopSync`). The unused `AD/HeadpatSync` and `AD/BoopSync` parameters were removed.
- Added Manual Gestures: an optional `ManualGesture` int with a configurable number of states. At 0 the normal gesture/Avatar Dynamics logic runs; any higher value forces the matching `Manual#` state.

## 1.0.0
- Initial release.
