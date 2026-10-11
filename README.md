# 2048 Puzzle

A colourful 2048 game for Android with a permanent ad banner that players remove with a one-time purchase.
Written in C# with .NET MAUI, so it opens, builds and runs from Visual Studio.

![Logo](docs/screenshots/logo.png)

<img src="docs/screenshots/01_classic.png" width="200"> <img src="docs/screenshots/02_combo.png" width="200"> <img src="docs/screenshots/03_time_attack.png" width="200"> <img src="docs/screenshots/04_result_daily.png" width="200">

_These images are drawn by the game's own scene renderer (`tools/Puzzle2048.Preview`). The grey strip is where the ad banner sits._

## What is in the game

- **Classic** on 4x4, 5x5 and 6x6 boards with undo, a tile-removal power-up and a saved game
- **Daily Challenge**: one puzzle per UTC day, the same tile sequence and goal for everyone, unlimited retries, best score of the day, a daily streak and a shareable result
- **Time Attack**: 60 seconds, every merge adds time (up to 90)
- **Sprint**: reach 512 as fast as you can
- **Combos**: merge on consecutive moves for up to +200% bonus points
- **Levels**: every point you score fills a level bar; completing a daily goal adds bonus points
- **Daily stars**: one to three stars for each daily challenge, shown on the menu
- **Game feel**: candy style tiles, bouncy slides and pops, particle bursts, floating points, praise words for combos and big tiles, confetti, screen shake, a level-up celebration and an animated backdrop
- **Sound**: bubble pops that rise in pitch with the tile value, plus jingles for combos, wins and level-ups. Can be switched off in Settings
- **Daily reminder**: optional, one evening notification, only if the daily challenge has not been played yet
- **Ads and purchase**: Google AdMob banner at the bottom of every screen, removed by a one-time Google Play purchase (`remove_ads`)
- English only, portrait, Android 7.0 (API 24) and newer, 64-bit devices

## Build it with Visual Studio

You need **Visual Studio 2026** (version 18 or newer). The project targets .NET 10, which Microsoft supports only from Visual Studio 2026.

1. In the Visual Studio Installer, install the **.NET Multi-platform App UI development** workload. Make sure the Android SDK and Java options of that workload are ticked.
2. Open `Puzzle2048.sln`.
3. Set **Puzzle2048.App** as the startup project, pick **Android Emulator** or your USB-connected phone, and press **Run**. Debug builds always show Google's test ads.

### Make an APK or a Play Store bundle

Two different files, for two different jobs:

| File | For | How |
| --- | --- | --- |
| **APK** (`.apk`) | Installing on your own phone, or sharing | Release build with `-p:UseApk=true` |
| **AAB** (`.aab`) | Uploading to Google Play | Release build (the default) |

**Pick the file whose name ends in `-Signed`.** Each build also leaves an unsigned copy next to it, and a phone or Google Play will not accept that one.

**In Visual Studio:** right-click **Puzzle2048.App**, choose **Publish** or **Archive** (the name depends on your version), then **Distribute**. Choose the ad hoc option for an APK or the Google Play option for an AAB. The wizard can create your signing key.

**From a command line (Windows PowerShell):**

```powershell
# Once: create your signing key. Use the SAME password for the store and the key.
keytool -genkeypair -v -keystore puzzle2048.keystore -alias puzzle -keyalg RSA -keysize 2048 -validity 10000

$env:KEY_PASS = "your-password"
$env:STORE_PASS = "your-password"

# APK for your own phone
dotnet publish src/Puzzle2048.App -f net10.0-android -c Release -p:UseApk=true -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=puzzle2048.keystore -p:AndroidSigningKeyAlias=puzzle -p:AndroidSigningKeyPass=env:KEY_PASS -p:AndroidSigningStorePass=env:STORE_PASS

# AAB for Google Play
dotnet publish src/Puzzle2048.App -f net10.0-android -c Release -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=puzzle2048.keystore -p:AndroidSigningKeyAlias=puzzle -p:AndroidSigningKeyPass=env:KEY_PASS -p:AndroidSigningStorePass=env:STORE_PASS
```

The files appear in `src\Puzzle2048.App\bin\Release\net10.0-android\publish\`:
`com.emeraldwall.puzzle2048-Signed.apk` and `com.emeraldwall.puzzle2048-Signed.aab`.

- **Only for your own phone**, you can leave out every signing option. The `-Signed.apk` is then signed with a debug key, which is fine to install but never to publish.
- **Google Play** needs a bundle signed with your own key (Play App Signing keeps your key as the "upload key"). Raise `ApplicationVersion` for every upload.
- If the build stops with a `jarsigner` error, the store password and key password probably differ. Use the same password for both.
- **Keep `puzzle2048.keystore` and its passwords safe and backed up.** Without them you cannot publish updates. Never commit them (`.gitignore` already skips `*.keystore`).
- Install the APK by copying it to the phone and opening it (allow "install unknown apps"), or with `adb install`. The remove-ads purchase only works in a build installed from Google Play, so test that on an internal testing track.

## Before you publish

1. **Support email**: set `SupportEmail` in `src/Puzzle2048.App/Services/AppConstants.cs`.
2. **Version**: raise `ApplicationVersion` in `Puzzle2048.App.csproj` for every upload to Google Play.
3. **AdMob**: the release build uses the live ids in `AppConstants.cs` (`AdConfig`). After the app is live, link it to its Play listing in AdMob. Do not tap live ads yourself.
4. **Play Console**: create a one-time in-app product with id `remove_ads`. Purchases only work in a build installed from Google Play, for example from an internal test track.
5. **Children**: every ad request is tagged child-directed with a G rating, so ads are non-personalized and there is no consent form. Make the target audience, ads and advertising ID answers in Play Console match. The Google ads library adds the `AD_ID` permission to the manifest, so review that answer carefully.
6. **Privacy policy**: publish one that mentions AdMob and the purchase.

## Project layout

| Folder | What it holds |
| --- | --- |
| `src/Puzzle2048.Core` | Game rules, modes, scoring, daily challenge, progress. No UI, fully tested |
| `src/Puzzle2048.Rendering` | Draws the whole game screen (`GameScene`): board, header, buttons, effects and result cards. Uses vector text, so it looks the same on every phone |
| `src/Puzzle2048.App` | The Android app: screens, ads, billing, reminders |
| `tests` | Unit tests for the rules, progress and rendering helpers |
| `tools/Puzzle2048.Preview` | Draws game screens to PNG files, to check the look without a phone |
| `tools/generate_glyphs.py` | Rebuilds the vector font data from the Fredoka font |
| `tools/generate_sounds.py` | Rebuilds the sound effects in `Resources/Raw/sfx` (pure Python, no samples, so no third-party audio rights) |

Run the tests with `dotnet test tests/Puzzle2048.Tests` or from Test Explorer.

## Licenses and credits

- Font: Fredoka Bold by the Fredoka Project Authors, SIL Open Font License 1.1 (`src/Puzzle2048.App/Resources/Raw/Fredoka-OFL.txt`, shown in the app under About). Keep that file with the app.
- Libraries: .NET MAUI, Google Mobile Ads (AdMob) and Plugin.InAppBilling, under their own licenses.
- This is proprietary, closed-source software. All rights reserved, see [LICENSE](LICENSE). Keep this repository private.
