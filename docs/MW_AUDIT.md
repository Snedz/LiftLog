# Mission Winning audit of the LiftLog fork

Audit of this tree as a base for Mission Winning. No product behaviour was changed.
Licence stays AGPL-3.0 (`LICENSE`). Upstream is Liam Morrow's LiftLog.

Commands below were run on this machine on 2026-10-14. Android and the Vitest suite are recorded
with the output they actually produced.

## Architecture

Two runtimes. The phone app does not need the server to log workouts.

### `app/` — Expo ~57, React Native 0.86, React 19

File-based routes live in `app/src/app/` (expo-router). Workouts, plans, and stats are stored in
on-device SQLite through Drizzle (`app/src/db/`, `app/src/drizzle/`). Redux Toolkit holds the
session being edited and preferences; finished sessions are relational rows, not a big JSON blob.
See `docs/Storage.md`.

| Path | Role |
| --- | --- |
| `app/src/app/` | Screens: session, history, stats, feed, settings, AI planner |
| `app/src/components/presentation/` | Presentational UI, including `foundation/` primitives |
| `app/src/components/smart/` | Containers that bind UI to services |
| `app/src/store/` | Redux slices and effects |
| `app/src/services/` | Feed, sessions, SignalR AI chat, health export |
| `app/src/models/` | Domain types and persisted-state migrations |

The native Android project is not committed. `app/.gitignore` ignores `/android/` and `/ios/`.
CI generates it with `npm run prebuild` (`expo prebuild`) and then Gradle. Release signing falls
back to the debug keystore unless `ANDROID_UPLOAD_STORE_FILE` is set
(`app/plugins/android-signing.js`). Application id today is `com.limajuice.liftlog`
(`app/app.json`).

### `backend/` — .NET 10 Web API

`LiftLog.Api` (net10.0) plus `LiftLog.Lib`. It stores feed ciphertext and serves the AI planner.
Postgres or SQLite. It does not store workout logs. Those stay on the device; the feed copies are
end-to-end encrypted (`docs/FeedProcess.md`).

| Route | What it is |
| --- | --- |
| `GET /health` | Liveness, no auth |
| `GET /features` | Which of feed, sharing, AI planner, backup are enabled |
| `POST /user/create`, `GET /user/{id}`, inbox, events | Social feed |
| `POST /backup` | Remote backup upload (needs a sink and an API key) |
| SignalR `/ai-chat` | Planner v1 |
| SignalR `/ai-chat-v2` | Planner v2, the one the app uses |

`site/` is the marketing site, not the Expo app.

## Does it work without the server?

Yes for the training log. The README states that workouts are stored on the device and that no
account is required. Plans, progression, history, and stats read SQLite. Nothing in that path
calls `api.liftlog.online`.

These features do need a reachable LiftLog server:

- **AI planner.** The screen talks only to SignalR. With no connection the chat says it failed to
  reach the server (`app/src/services/ai-chat-service-v2.ts`).
- **Feed.** Friends and the inbox need the API. Local queues exist; they cannot deliver offline.
- **Remote backup.** Off until a backend is assigned, and the upload itself is HTTP.

The built-in backend URL is `https://api.liftlog.online` in release builds and
`http://127.0.0.1:5264` (Android emulator: `http://10.0.2.2:5264`) in dev
(`app/src/services/api-consts.ts`). Pointing Settings → Backends at a self-hosted instance
replaces that for each feature independently (`docs/Backends.md`).

So a fork can ship logging with the server deleted. The planner, feed, and remote backup then
show their existing failure paths.

## How the AI planner is wired

```
Settings → AI planner
  app/src/app/(tabs)/settings/ai/planner.tsx
    → Redux effect app/src/store/ai-planner/effects.ts
      → AiChatServiceV2 (app/src/services/ai-chat-service-v2.ts)
        → SignalR hub {backend}/ai-chat-v2
          → AiWorkoutChatHubV2
            → AiChatDirectoryV2
              → AnthropicChatPlannerV2
                → Anthropic Messages API, model claude-sonnet-4-6
```

### Client

- Service: `app/src/services/ai-chat-service-v2.ts`, constructed in `app/src/services/index.ts`.
- Hub methods: `Introduce`, `SendMessage`, `RestartChat`, `StopInProgress`.
- Server pushes `ReceiveMessage` events. Types are `messageResponse`, `chatPlan`,
  `updateRequired`, and `purchasePro` (`app/src/models/ai-models.ts`).
- A plan is JSON matching `docs/schemas/ai-plan/AiPlan.json` (generated from
  `app/src/models/storage/versions/latest/ai-plan.ts`). The app migrates older plan versions.
- The hosted backend requires a Pro token. In dev that header is `X-API-Key`; in release it is
  `Authorization: Bearer RevenueCat <token>` (`app/src/store/backends/index.ts`). A self-hosted
  backend does not set `requiresPro`.
- There is no model API key in the app. The key lives on the server.

### Server

- Hub: `backend/LiftLog.Api/Hubs/AiWorkoutChatHubV2.cs`, mapped in `Program.cs` only while
  `AiPlanner` is enabled. Disabled hubs return HTTP 423.
- Auth schemes: API key (`X-API-Key`), forward-auth header, or RevenueCat purchase token
  (`AuthSchemes.AiPlanner`).
- Provider: Anthropic. `RegistrationHelpers.AddAnthropicWorkoutPlannerV2` reads
  `AiPlanner:AnthropicApiKey` and optional `AiPlanner:AnthropicModelId` (default
  `claude-sonnet-4-6`). The client is built on first use, so the process can boot without the
  key; the first chat then throws.
- v2 streams tokens from the Anthropic SDK (`AnthropicChatPlannerV2`) and forces a
  `create_workout_plan` tool whose input schema is the embedded `AiPlan.json`
  (`AiPlanToolProvider`).
- v1 (`/ai-chat`, `GenericAiChatWorkoutPlanner`) uses `Microsoft.Extensions.AI` `IChatClient`,
  still backed by the same Anthropic key. The app does not call v1.
- The model is asked to invent a weekly program from the chat. It is not given the device's
  session history, and it does not apply a missed-day deload. Exercise names must match exactly
  because LiftLog joins history on the name string.

### What happens with no server and with no key

| Situation | Logging | AI planner |
| --- | --- | --- |
| Phone offline, no backend | Works | Chat reports that it cannot connect |
| Self-hosted API, no `AiPlanner:AnthropicApiKey` | Works | Hub is up; the first completion throws while constructing the Anthropic client |
| Self-hosted API, key set, `X-API-Key` on the backend | Works | Plans generate, billed to that Anthropic account |
| Hosted `api.liftlog.online` without Pro | Works | Client yields `purchasePro` and never calls the hub |

## Expo web build

`app/app.json` still sets `web.bundler` to `metro` and `web.output` to `single`. The README says
web support was removed. `react-native-web` is not a dependency.

Command, from `app/`:

```
npx expo export --platform web --output-dir /tmp/liftlog-web-export
```

Result: exit 1.

```
CommandError: It looks like you're trying to use web support but don't have the required
dependencies installed.

Install react-native-web@^0.21.2 by running:

npx expo install react-native-web

If you're not using web, please ensure you remove the "web" string from the
platforms array in the project Expo config.
```

The committed tree does not produce a web bundle. This audit did not add `react-native-web`,
because that would change the app.

## Android release build

CI (`.github/workflows/android-build.yml`) uses Node 24, Temurin 17, `npm ci`, `npm run prebuild`,
then `./gradlew build` from `app/android` with ABIs `arm64-v8a,armeabi-v7a`.

This machine had Node 22.14.0 and OpenJDK 21, and no Android SDK. The SDK was installed under
`$HOME/android-sdk` (platforms 35 and 36, build-tools 35 and 36, NDK 27.1.12297006) and was not
committed. Prebuild:

```
cd app && npx expo prebuild --platform android --no-install
```

```
- Creating native directory (./android)
✔ Created native directory
- Updating package.json
✔ Updated package.json | no changes
- Running prebuild
» android: userInterfaceStyle: Install expo-system-ui in your project to enable this feature.
- Running prebuild
✔ Finished prebuild
```

Release APK (one ABI, so it fits this VM; CI builds two). Gradle heap was lowered in the generated
`android/gradle.properties` only. That file is gitignored with the rest of `app/android/`.

```
cd app/android && ./gradlew assembleRelease -PreactNativeArchitectures=arm64-v8a --no-daemon
```

The first attempt set the Gradle daemon to 3 GiB of heap and 512 MiB of metaspace so it would fit next to other work. R8, which minifies the release app, then died:

```
> Task :app:minifyReleaseWithR8
ERROR: R8: java.lang.OutOfMemoryError: Metaspace

> Task :app:minifyReleaseWithR8 FAILED
```

The shell printed `EXIT:0` only because the log was piped through `tail` without `pipefail`. The task itself failed. Native libraries for `arm64-v8a` had already been produced.

The retry used the generated file's original metaspace budget (`-Xmx4g -XX:MaxMetaspaceSize=1536m`) and the same ABI. Cached native tasks were up to date. Result:

```
BUILD SUCCESSFUL in 2m 14s
1507 actionable tasks: 40 executed, 1467 up-to-date
EXIT:0
```

`aapt dump badging` on `app/android/app/build/outputs/apk/release/app-release.apk` (71 MB):

```
package: name='com.limajuice.liftlog' versionCode='1' versionName='1.0.0' platformBuildVersionName='16' platformBuildVersionCode='36' compileSdkVersion='36' compileSdkVersionCodename='16'
sdkVersion:'24'
targetSdkVersion:'36'
```

Release signing used the debug keystore, which is what `android-signing.js` does when `ANDROID_UPLOAD_STORE_FILE` is absent. This was one ABI (`arm64-v8a`). CI builds `arm64-v8a` and `armeabi-v7a` on a larger runner. The release task, including R8, completed.

## App tests

`npm test` is Vitest. The SQLite shim (`app/test/shims/expo-sqlite.ts`) calls
`StatementSync.columns()`. That method is not on Node 22.14.0. It is present on Node 24, which is
what CI uses.

On Node v22.14.0, `CI=1 npm test -- --run` reported:

```
Test Files  26 failed | 66 passed (92)
     Tests  239 failed | 1172 passed | 24 skipped (1435)
```

The failures cascade from `TypeError: statement.columns is not a function` inside the test shim.
That is the runner, not a product bug.

On Node v24.10.0, `CI=1 npx vitest run`:

```
Test Files  1 failed | 91 passed (92)
     Tests  1 failed | 1434 passed (1435)
  Duration  126.37s
EXIT:1
```

The single failure is `src/db/sessions.spec.ts > readPreviousExercises > agrees with the newest performance of each lineage in its history`: fast-check exceeded the 5 second default timeout while the Android release build was using the same CPUs. Run again on its own after that build finished:

```
Test Files  1 passed (1)
     Tests  1 passed | 35 skipped (36)
  Duration  4.33s
EXIT:0
```

The test itself took 1.95s. It is a pre-existing property test, and it passes when the machine is not also compiling native code.

## Backend tests

Not run in this audit. The API targets `net10.0` and the .NET SDK was not part of the image.
The AI planner findings above are from reading the code, not from `dotnet test`.
