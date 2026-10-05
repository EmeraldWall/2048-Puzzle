# 2048 Puzzle

Android 2048 game with a permanent bottom ad banner that players remove with a one-time in-app purchase.

- Boards: 4x4, 5x5, 6x6, undo, tile removal, endless mode, custom background color
- Ads: Google AdMob adaptive banner (bottom of the menu and game screens), EU/UK consent via Google UMP
- Monetization: one-time "Remove ads" product through Google Play Billing, with "Restore purchase" in Settings
- Language: English only

## Before you publish

1. **AdMob**: create an app and a banner unit, then replace the test IDs in `app/build.gradle` (`admobAppId` and `ADMOB_BANNER_UNIT_ID`, release block). Debug builds always use Google's test IDs.
2. **Play Console**: create a one-time in-app product with ID `remove_ads` (or change `REMOVE_ADS_PRODUCT_ID` in `app/build.gradle`). Billing only works from a build uploaded to a Play Console test track, installed from Google Play.
3. **Identity**: change `applicationId` / `namespace` if `com.emeraldwall.puzzle2048` is not the ID you want, replace the launcher icons, and set `support_email` in `app/src/main/res/values/strings.xml`.
4. Publish a privacy policy that mentions AdMob and complete the Play Console Data safety and Ads declarations.

## Build

Requires JDK 17 and the Android SDK.

```
./gradlew assembleDebug
```

## License and credits

MIT, see [LICENSE](LICENSE). This game derives from the open source 2048 code by Gabriele Cirulli and the Android ports built on it, all MIT licensed. Keep the license notices when you distribute the app.
