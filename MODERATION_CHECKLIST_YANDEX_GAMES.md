Yandex Games moderation checklist for this project

Current code status
- SDK integration is present via PluginYG2.
- `LoadingAPI.ready()` is expected through PluginYG2 `autoGRA`.
- Auto-pause/audio pause is enabled through PluginYG2.
- Guest play exists.
- Manual Yandex auth button exists.
- Local save exists and cloud save exists.
- Rewarded ads and interstitials are called through Yandex SDK only.
- Language auto-detection via SDK exists.

Code changes already applied
- Cloud save no longer requests elevated auth scopes automatically.
- Auth CTA explains why sign-in is needed.
- Rewarded ad button explicitly says it shows an ad and what reward is given.
- Language switcher supports all Yandex language codes with fallback logic.
- Interstitial ads now have a minimum session delay and cooldown.

Draft settings to choose carefully
- Platforms: select only `desktop` and `mobile` unless TV support is fully implemented.
- Orientation: select only the orientation your game actually supports well.
- Languages: for now, safest is `ru`, `en`, `tr`.
- Cloud saves: mark `yes`, because the game saves to cloud and local storage.
- In-app purchases: mark `no` unless you actually wire up Yandex payments UI and product flow.

Before submission
- Fill `How to play` with real controls for desktop and mobile.
- Fill `About the game` without duplicated text from other fields.
- Add support email in the draft.
- Set the correct age rating.
- Verify the title is unique in all selected languages.
- Upload real gameplay screenshots, not mockups.
- Make sure icon and cover are not raw screenshots.

Manual test plan
- Launch on desktop Chrome, Yandex Browser, Firefox.
- Launch on Android Chrome and Yandex app.
- Verify no browser scrollbars appear.
- Verify long tap does not open context menu or text selection.
- Verify audio pauses when the tab is hidden and on ad open.
- Verify guest progress survives page refresh.
- Verify Yandex-authorized progress sync works across devices.
- Verify rewarded ad is optional and game remains playable without it.
- Verify interstitial appears only after meaningful gameplay and not too often.
- Verify resize/orientation changes do not break UI.
- Verify all visible gameplay-critical texts are translated for selected draft languages.

High-risk moderation mistakes to avoid
- Do not select languages in the draft that are not actually translated in the game description fields.
- Do not enable TV platform in the draft.
- Do not add external links in game texts, promo, or UI.
- Do not publish with editor/debug/dev UI visible.
- Do not rely on “works on my device”; test resize, pause, ads, refresh, auth, and mobile gestures.

Recommended release decision for this build
- Publish as `desktop + mobile`.
- Use languages `ru + en + tr`.
- Mark cloud saves enabled.
- Keep in-app purchases disabled unless implemented end-to-end.
