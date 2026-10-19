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
- **Account**: optional Google Play Games sign-in with a cloud save of the level, streaks, records and stars, six leaderboards and ten achievements. "Delete my data" in Settings wipes the progress from the phone and the cloud. The game works fully without signing in
- **Ads and purchase**: Google AdMob banner at the bottom of every screen, removed by a one-time Google Play purchase (`remove_ads`)
- **Smooth on fast screens**: frames follow the display (60, 90, 120 or 144 Hz) and the game asks for the screen's highest refresh rate while you play. Swipes are read from raw touches and act as soon as the direction is clear
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
6. **Privacy policy**: publish one that mentions AdMob, the purchase and Google Play Games (sign-in, cloud save, leaderboards and achievements).
7. **Play Games account** (optional, see below). Until it is set up, every account feature stays hidden.

## Set up the Play Games account

The sign-in, cloud save, leaderboards and achievements need a Play Games Services project. Without one the game simply hides them.

1. In Play Console open your app, then **Grow users** (or **Grow**) **> Play Games Services > Setup and management > Configuration**, and create a Play Games Services project. Google's guide: [Set up Google Play Games Services](https://developer.android.com/games/pgs/console/setup).
2. Add **credentials** for Android. Use the SHA-1 fingerprint of your **app signing key** (Play Console > Test and release > App integrity) and, for builds you install yourself, of your upload key: `keytool -list -v -keystore puzzle2048.keystore -alias puzzle2048`.
3. Turn on **Saved Games** in the project's properties (**Edit properties** on the Configuration page).
4. Create these **leaderboards**. Higher is better for all of them except Sprint:
   - Level points, Classic 4x4, Classic 5x5, Classic 6x6, Time Attack
   - Sprint: a time leaderboard, smaller is better
5. Create these **achievements**:
   - Tiles: make a 256, 512, 1024, 2048 and 4096 tile
   - Streaks: daily streak of 3, 7 and 30 days
   - Levels: reach level 10 and level 25
6. Paste every id into `src/Puzzle2048.App/Platforms/Android/Resources/values/playgames.xml`. The project id comes from the Configuration page. Leaderboards and Achievements each have a **Get resources** button that lists their ids. An id you leave empty is skipped.
7. Add your Google account as a **tester** under Play Games Services until you publish the project, then publish it from the same page.

Children whose Google account is supervised with Family Link need a parent's approval for a Play Games profile. Without one they still play normally, just without the account features.

## Project layout

| Folder | What it holds |
| --- | --- |
| `src/Puzzle2048.Core` | Game rules, modes, scoring, daily challenge, progress. No UI, fully tested |
| `src/Puzzle2048.Rendering` | Draws the whole game screen (`GameScene`): board, header, buttons, effects and result cards. Uses vector text, so it looks the same on every phone |
| `src/Puzzle2048.App` | The Android app: screens, ads, billing, reminders |
| `tests` | Unit tests for the rules, progress and rendering helpers |
| `tools/Puzzle2048.Preview` | Draws game screens to PNG files, to check the look without a phone. `--sprites` draws through the sprite cache like the app; `--bench` times frames while tiles move |
| `tools/generate_glyphs.py` | Rebuilds the vector font data from the Fredoka font |
| `tools/generate_sounds.py` | Rebuilds the sound effects in `Resources/Raw/sfx` (pure Python, no samples, so no third-party audio rights) |

Run the tests with `dotnet test tests/Puzzle2048.Tests` or from Test Explorer.

## How it stays smooth

- **Vsync loop**: the game screen redraws from Android's Choreographer, once per display refresh, and every animation reads the frame's own time. When nothing moves it drops to about 15 frames a second for the drifting background.
- **High refresh rate**: on Android 15 and newer the game view asks for a high frame rate; on older versions the window picks the display's fastest mode. The phone goes back to its normal rate when you leave the game.
- **Sprite cache** (`Rendering/Sprites.cs`): tiles, the board, letters, candy panels, icons, particles and the background are drawn once into bitmaps and then stamped, so a frame is a few dozen texture draws and creates no Android objects. The preview tool's `--sprites` mode checks that the cached look matches the vector drawing.
- **Raw touch**: a swipe triggers after 14 dp of movement while the finger is still down, a quick flick of 7 dp counts when it lifts, and buttons sink the moment they are touched.

## Licenses and credits

- Font: Fredoka Bold by the Fredoka Project Authors, SIL Open Font License 1.1 (`src/Puzzle2048.App/Resources/Raw/Fredoka-OFL.txt`, shown in the app under About). Keep that file with the app.
- Libraries: .NET MAUI, Google Mobile Ads (AdMob) and Plugin.InAppBilling, under their own licenses.
- This is proprietary, closed-source software. All rights reserved, see [LICENSE](LICENSE). Keep this repository private.
