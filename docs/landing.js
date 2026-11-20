/*
 * Landing page behaviour:
 *   - detect the visitor's OS/architecture and recommend the matching release asset;
 *   - fill the download cards and the primary button from the latest GitHub release;
 *   - emit a cookie-free "download-click" analytics event (via analytics.js).
 *
 * If the GitHub API is unavailable, every link falls back to the release page.
 */
(function () {
    'use strict';

    var RELEASE_API = 'https://api.github.com/repos/purelogiccode/SimpleLauncher/releases/latest';
    var RELEASE_PAGE = 'https://github.com/purelogiccode/SimpleLauncher/releases/latest';

    var primary = document.getElementById('primary-download');
    var primaryLabel = document.getElementById('primary-download-label');
    var detectionNote = document.getElementById('detection-note');
    var cards = Array.prototype.slice.call(document.querySelectorAll('.download-card[data-asset]'));

    var patterns = {
        // Windows prefers the per-user installer; older releases only have the ZIP.
        'win-x64': [
            /^SimpleLauncher-.+-win-x64-setup\.exe$/i,
            /^release_.+_win-x64\.zip$/i
        ],
        'win-arm64': [
            /^SimpleLauncher-.+-win-arm64-setup\.exe$/i,
            /^release_.+_win-arm64\.zip$/i
        ],
        'linux-x64': [
            /^SimpleLauncher-.+-linux-x64\.AppImage$/i,
            /^SimpleLauncher-.+-linux-x64\.flatpak$/i,
            /^release_.+_linux-x64\.zip$/i
        ],
        'linux-arm64': [
            /^SimpleLauncher-.+-linux-arm64\.AppImage$/i,
            /^SimpleLauncher-.+-linux-arm64\.flatpak$/i,
            /^release_.+_linux-arm64\.zip$/i
        ],
        'osx-arm64': [/^SimpleLauncher-.+-osx-arm64\.dmg$/i]
    };

    var detected = detectPlatform();
    var availableAssets = [];

    function detectPlatform() {
        var ua = navigator.userAgent || '';
        var platform = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || '';

        if (/Win/i.test(platform) || /Windows/i.test(ua)) {
            return {
                os: 'windows',
                arch: /ARM64|aarch64/i.test(ua) ? 'arm64' : 'x64',
                key: /ARM64|aarch64/i.test(ua) ? 'win-arm64' : 'win-x64'
            };
        }

        if (/Mac/i.test(platform) || /Mac OS X/i.test(ua)) {
            // The macOS build is Apple Silicon only, so there is no Intel variant to detect.
            return { os: 'macos', arch: 'arm64', key: 'osx-arm64' };
        }

        if (/Linux|X11/i.test(platform) || /Linux/i.test(ua)) {
            return {
                os: 'linux',
                arch: /aarch64|arm64/i.test(ua) ? 'arm64' : 'x64',
                key: /aarch64|arm64/i.test(ua) ? 'linux-arm64' : 'linux-x64'
            };
        }

        return { os: 'unknown', arch: '', key: '' };
    }

    function platformName(platform) {
        if (platform.os === 'windows') return 'Windows ' + (platform.arch === 'arm64' ? 'ARM64' : 'x64');
        if (platform.os === 'linux') return 'Linux ' + (platform.arch === 'arm64' ? 'ARM64' : 'x64');
        if (platform.os === 'macos') return 'macOS (Apple Silicon)';
        return 'your platform';
    }

    function findAsset(key, assets) {
        if (!key || !patterns[key] || !assets || !assets.length) return null;
        var candidates = patterns[key];
        for (var c = 0; c < candidates.length; c++) {
            for (var i = 0; i < assets.length; i++) {
                if (candidates[c].test(assets[i].name)) return assets[i];
            }
        }
        return null;
    }

    function formatSize(bytes) {
        if (!bytes || bytes < 1024) return '';
        var mb = bytes / (1024 * 1024);
        return mb >= 1024 ? (mb / 1024).toFixed(1) + ' GB' : Math.round(mb) + ' MB';
    }

    function setLink(link, asset, fallback) {
        if (!link) return;
        if (asset && asset.browser_download_url) {
            link.href = asset.browser_download_url;
            link.setAttribute('data-resolved', 'true');
        } else {
            link.href = fallback;
            link.removeAttribute('data-resolved');
        }
    }

    function formatLabel(asset) {
        var name = (asset && asset.name) || '';
        if (/\.AppImage$/i.test(name)) return 'AppImage · self-contained';
        if (/\.flatpak$/i.test(name)) return 'Flatpak · self-contained';
        if (/\.dmg$/i.test(name)) return 'DMG · unsigned, not notarized';
        if (/win-.*-setup\.exe$/i.test(name)) return 'Installer · per-user, .NET 11 bootstrapped';
        if (/linux-.*\.zip$/i.test(name)) return 'Portable ZIP · .NET 11 runtime';
        if (/win-.*\.zip$/i.test(name)) return 'Portable ZIP · .NET 11 Desktop Runtime';
        return '';
    }

    function updateCards(assets) {
        cards.forEach(function (card) {
            var key = card.getAttribute('data-asset');
            var asset = findAsset(key, assets);
            setLink(card, asset, RELEASE_PAGE);

            var format = card.querySelector('.download-format');
            if (!format) return;
            if (!format.getAttribute('data-default')) {
                format.setAttribute('data-default', format.textContent);
            }
            if (!asset) {
                format.textContent = format.getAttribute('data-default');
                return;
            }

            var label = formatLabel(asset) || format.getAttribute('data-default');
            var size = formatSize(asset.size);
            format.textContent = label + (size ? ' · ' + size : '');
        });
    }

    function applyRecommendation(assets) {
        if (!primary || !primaryLabel) return;

        var key = detected.key;
        var asset = findAsset(key, assets);
        var installer = asset && /-setup\.exe$/i.test(asset.name);
        var label = key
            ? 'Download' + (installer ? ' installer' : '') + ' for ' + platformName(detected)
            : 'Download the latest release';

        primaryLabel.textContent = label;
        primary.setAttribute('data-platform', detected.os);
        primary.setAttribute('data-arch', detected.arch);
        setLink(primary, asset, RELEASE_PAGE);

        cards.forEach(function (card) {
            card.classList.toggle('is-recommended', card.getAttribute('data-asset') === key);
        });

        if (!detectionNote) return;

        if (!key) {
            detectionNote.textContent = 'Pick the download that matches your platform below.';
        } else if (asset) {
            detectionNote.textContent = 'Recommended for you: ' + platformName(detected) + ' · ' + asset.name;
        } else {
            detectionNote.textContent = 'Recommended for you: ' + platformName(detected) +
                ' · the direct file is not in the latest release yet, so the button opens the release page.';
        }
    }

    function refineArchitecture() {
        if (!navigator.userAgentData || typeof navigator.userAgentData.getHighEntropyValues !== 'function') return;

        navigator.userAgentData.getHighEntropyValues(['architecture', 'bitness']).then(function (values) {
            if (values && values.architecture === 'arm' && detected.os === 'windows') {
                detected = { os: 'windows', arch: 'arm64', key: 'win-arm64' };
                applyRecommendation(availableAssets);
            }
        }).catch(function () {
            // Keep the user-agent based guess.
        });
    }

    function loadRelease() {
        applyRecommendation([]);
        if (detectionNote) {
            detectionNote.textContent = 'Finding the latest release for ' + platformName(detected) + '…';
        }

        var controller = typeof AbortController === 'function' ? new AbortController() : null;
        var timeout = window.setTimeout(function () {
            if (controller) controller.abort();
        }, 8000);

        fetch(RELEASE_API, {
            headers: { Accept: 'application/vnd.github+json' },
            signal: controller ? controller.signal : undefined
        })
            .then(function (response) {
                if (!response.ok) throw new Error('GitHub API returned ' + response.status);
                return response.json();
            })
            .then(function (release) {
                availableAssets = (release && release.assets) || [];
                updateCards(availableAssets);
                applyRecommendation(availableAssets);
            })
            .catch(function () {
                if (detectionNote) {
                    detectionNote.textContent = 'Direct links are unavailable right now; every button opens the GitHub release page.';
                }
            })
            .then(function () {
                window.clearTimeout(timeout);
            });
    }

    if (primary) {
        primary.addEventListener('click', function () {
            primary.setAttribute('data-download', detected.key || 'primary');
        });
    }

    document.addEventListener('click', function (event) {
        var link = event.target.closest ? event.target.closest('a[data-download]') : null;
        if (!link || !window.slAnalytics || typeof window.slAnalytics.track !== 'function') return;

        var key = link.getAttribute('data-download') || 'unknown';
        var platform = link.getAttribute('data-platform') || '';
        var arch = link.getAttribute('data-arch') || '';
        if (key === 'primary') {
            platform = detected.os;
            arch = detected.arch;
            key = detected.key || 'unknown';
        }

        // Distinguish the Windows installer from the portable ZIP in the event.
        if (/-setup\.exe(\?|$)/i.test(link.getAttribute('href') || '')) {
            key += '-setup';
        }

        var downloadTitle = platform || 'unknown';
        if (arch) downloadTitle += ' ' + arch;

        window.slAnalytics.track('download-click', {
            platform: platform || 'unknown',
            arch: arch || 'unknown',
            format: key,
            title: 'Download: ' + downloadTitle
        });
    });

    var video = document.querySelector('.tour-video video');
    if (video) {
        video.addEventListener('play', function () {
            if (video.getAttribute('data-tracked')) return;
            video.setAttribute('data-tracked', 'true');
            if (window.slAnalytics && typeof window.slAnalytics.track === 'function') {
                window.slAnalytics.track('demo-play', { title: '20-second tour' });
            }
        });
    }

    applyRecommendation([]);
    refineArchitecture();
    loadRelease();
})();
