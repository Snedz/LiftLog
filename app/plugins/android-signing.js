const { withAppBuildGradle } = require('@expo/config-plugins');

const MARKER = 'mission-winning-upload-signing';

// Release signing reads an upload keystore from Gradle properties or the same
// environment variables. Nothing here is a secret, and no keystore is committed.
// With neither set, release builds keep the debug keystore so a local bundle still completes.
const RELEASE_CONFIG = `        // ${MARKER}
        release {
            def uploadStoreFile = project.findProperty('ANDROID_UPLOAD_STORE_FILE') ?: System.getenv('ANDROID_UPLOAD_STORE_FILE')
            def uploadStorePassword = project.findProperty('ANDROID_UPLOAD_STORE_PASSWORD') ?: System.getenv('ANDROID_UPLOAD_STORE_PASSWORD')
            def uploadKeyAlias = project.findProperty('ANDROID_UPLOAD_KEY_ALIAS') ?: System.getenv('ANDROID_UPLOAD_KEY_ALIAS')
            def uploadKeyPassword = project.findProperty('ANDROID_UPLOAD_KEY_PASSWORD') ?: System.getenv('ANDROID_UPLOAD_KEY_PASSWORD')
            if (uploadStoreFile) {
                storeFile file(uploadStoreFile)
                storePassword uploadStorePassword
                keyAlias uploadKeyAlias
                keyPassword uploadKeyPassword
            }
        }
`;

const RELEASE_SIGNING = `signingConfig((project.findProperty('ANDROID_UPLOAD_STORE_FILE') ?: System.getenv('ANDROID_UPLOAD_STORE_FILE')) ? signingConfigs.release : signingConfigs.debug)`;

function insertBeforeClosingBrace(contents, token) {
  const start = contents.indexOf(token);
  if (start < 0) {
    return contents;
  }
  const open = contents.indexOf('{', start);
  let depth = 0;
  for (let index = open; index < contents.length; index++) {
    if (contents[index] === '{') {
      depth++;
    } else if (contents[index] === '}') {
      depth--;
      if (depth === 0) {
        return contents.slice(0, index) + RELEASE_CONFIG + contents.slice(index);
      }
    }
  }
  return contents;
}

module.exports = function withAndroidSigningConfig(config) {
  return withAppBuildGradle(config, (config) => {
    if (config.modResults.contents.includes(MARKER)) {
      return config;
    }
    let contents = insertBeforeClosingBrace(config.modResults.contents, 'signingConfigs {');
    const buildTypes = contents.indexOf('buildTypes {');
    const release = contents.indexOf('release {', buildTypes);
    const signing = contents.indexOf('signingConfig signingConfigs.debug', release);
    if (buildTypes >= 0 && release >= 0 && signing >= 0) {
      contents =
        contents.slice(0, signing) +
        RELEASE_SIGNING +
        contents.slice(signing + 'signingConfig signingConfigs.debug'.length);
    }
    config.modResults.contents = contents;
    return config;
  });
};
