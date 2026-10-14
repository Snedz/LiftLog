const { withGradleProperties } = require('@expo/config-plugins');

const GRADLE_PROPERTIES = {
  // R8 runs out of metaspace at 1g on this app. 1536m is the budget a release build completes with.
  'org.gradle.jvmargs': '-Xmx4g -XX:MaxMetaspaceSize=1536m',
  'org.gradle.caching': 'true',
  'android.enableMinifyInReleaseBuilds': 'true',
  'android.enableShrinkResourcesInReleaseBuilds': 'true',
};

module.exports = function withAndroidGradleProperties(config) {
  return withGradleProperties(config, (config) => {
    for (const [key, value] of Object.entries(GRADLE_PROPERTIES)) {
      const existing = config.modResults.find((item) => item.type === 'property' && item.key === key);
      if (existing) {
        existing.value = value;
      } else {
        config.modResults.push({ type: 'property', key, value });
      }
    }
    return config;
  });
};
