# Play internal testing

The Android application id is `com.missionwinning.app`. Internal testing uses an Android App Bundle
(`.aab`), signed with an upload key that stays off the repository.

## Upload key

Create the keystore somewhere outside this repo. Do not commit it, and do not put the passwords in a
tracked file.

```sh
keytool -genkeypair -v \
  -keystore "$HOME/keys/mission-winning-upload.keystore" \
  -alias upload \
  -keyalg RSA -keysize 2048 -validity 10000
```

Tell Gradle where it is. Either export these before the build, or put the same names in
`~/.gradle/gradle.properties` (your user file, not `app/android/gradle.properties`):

```
ANDROID_UPLOAD_STORE_FILE=/absolute/path/mission-winning-upload.keystore
ANDROID_UPLOAD_STORE_PASSWORD=...
ANDROID_UPLOAD_KEY_ALIAS=upload
ANDROID_UPLOAD_KEY_PASSWORD=...
```

`app/plugins/android-signing.js` reads each value from a Gradle project property first, then from the
environment. When `ANDROID_UPLOAD_STORE_FILE` is unset, `bundleRelease` and `assembleRelease` sign with
the debug keystore so a local build still finishes. Play Console rejects a debug-signed bundle. Use the
upload key for anything you upload.

## Build the bundle

From `app/`:

```sh
npx expo prebuild --platform android
cd android
./gradlew bundleRelease
```

The bundle is `android/app/build/outputs/bundle/release/app-release.aab`.

`./gradlew assembleRelease` writes an APK at `android/app/build/outputs/apk/release/app-release.apk`.
Internal testing wants the AAB. The config plugin sets Gradle to `-Xmx4g -XX:MaxMetaspaceSize=1536m`,
which is what R8 needs to finish minifying this app.

A clean native project (`npx expo prebuild --platform android --clean`) picks up signing changes. The
generated `android/` directory is not committed.

## Internal testing

1. In Play Console, open the app, then Testing → Internal testing → Create new release.
2. Upload `app-release.aab`.
3. Add the release notes and review the release. Internal testing can run while the app is still in
   draft, before production review.
4. Testers opt in from the link on the Internal testing page. The Google account on the phone has to
   be on the tester list.

The first upload enrolls the app in Play App Signing. Google holds the app signing key after that.
The keystore above is only the upload key, which you keep and reuse for later releases.
