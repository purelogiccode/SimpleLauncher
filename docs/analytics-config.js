/*
 * Simple Launcher website analytics configuration.
 *
 * The landing page only uses cookie-free, privacy-friendly analytics. No third-party
 * script is loaded while provider is "none".
 *
 * To enable:
 *   1. Create a free GoatCounter site (https://www.goatcounter.com/) and copy its code,
 *      or add the domain to Plausible (https://plausible.io/).
 *   2. Set provider and site below.
 *   3. Commit and redeploy the docs.
 *
 * GoatCounter:  provider: 'goatcounter', site: 'yourcode'
 * Plausible:    provider: 'plausible',   site: 'purelogiccode.github.io'
 */
window.SL_ANALYTICS_CONFIG = {
    provider: 'none',
    site: '',
    respectDoNotTrack: true
};
