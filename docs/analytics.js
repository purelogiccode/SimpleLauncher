/*
 * Cookie-free analytics loader and download-click event helper.
 *
 * Supports GoatCounter (open source, hosted or self-hosted) and Plausible. The provider
 * is configured in analytics-config.js. When no provider is configured, this file makes
 * no network requests and exposes a no-op window.slAnalytics.track().
 *
 * Privacy:
 *   - no cookies, localStorage identifiers or fingerprinting;
 *   - Do Not Track / Global Privacy Control are honoured by default;
 *   - download events contain only the platform, architecture and format.
 */
(function () {
    'use strict';

    var config = window.SL_ANALYTICS_CONFIG || {};
    var provider = String(config.provider || 'none').toLowerCase();
    var site = String(config.site || '').trim();
    var respectDnt = config.respectDoNotTrack !== false;

    var dnt = navigator.doNotTrack === '1' || navigator.doNotTrack === 'yes' ||
        window.doNotTrack === '1' || window.doNotTrack === 'yes' ||
        navigator.msDoNotTrack === '1' ||
        navigator.globalPrivacyControl === true;

    var disabled = provider === 'none' || !site || (respectDnt && dnt);
    var queue = [];
    var goatReady = false;

    function noop() { }

    window.slAnalytics = {
        enabled: false,
        provider: provider,
        track: noop
    };

    if (disabled) return;

    function loadScript(src, attributes, onLoad) {
        var script = document.createElement('script');
        script.async = true;
        script.src = src;
        if (attributes) {
            Object.keys(attributes).forEach(function (name) {
                script.setAttribute(name, attributes[name]);
            });
        }
        script.onload = onLoad || noop;
        script.onerror = noop;
        document.head.appendChild(script);
    }

    function send(event) {
        if (provider === 'goatcounter') {
            if (!goatReady || !window.goatcounter || typeof window.goatcounter.count !== 'function') {
                queue.push(event);
                return;
            }
            var path = '/event/' + event.name;
            if (event.props && event.props.platform) path += '/' + event.props.platform;
            if (event.props && event.props.arch) path += '/' + event.props.arch;
            if (event.props && event.props.format) path += '/' + event.props.format;
            window.goatcounter.count({
                path: path,
                title: event.title || event.name,
                event: true,
                no_session: true
            });
            return;
        }

        if (provider === 'plausible') {
            if (typeof window.plausible === 'function') {
                window.plausible(event.name, { props: event.props || {} });
            } else {
                queue.push(event);
            }
        }
    }

    function flush() {
        if (provider === 'goatcounter' && !goatReady) return;
        if (provider === 'plausible' && typeof window.plausible !== 'function') return;
        while (queue.length) send(queue.shift());
    }

    if (provider === 'goatcounter') {
        loadScript(
            'https://gc.zgo.at/count.js',
            { 'data-goatcounter': 'https://' + site + '.goatcounter.com/count' },
            function () {
                goatReady = true;
                flush();
            }
        );

        // count.js is async; wait until its public API is available before flushing.
        var attempts = 0;
        var timer = window.setInterval(function () {
            if (window.goatcounter && typeof window.goatcounter.count === 'function') {
                goatReady = true;
                window.clearInterval(timer);
                flush();
            } else if (++attempts > 50) {
                window.clearInterval(timer);
            }
        }, 100);
    } else if (provider === 'plausible') {
        // Plausible supports a queue before its script loads.
        window.plausible = window.plausible || function () {
            (window.plausible.q = window.plausible.q || []).push(arguments);
        };
        loadScript('https://plausible.io/js/script.js', { 'data-domain': site }, flush);
    } else {
        return;
    }

    window.slAnalytics = {
        enabled: true,
        provider: provider,
        track: function (name, props) {
            if (!name) return;
            send({
                name: name,
                title: props && props.title ? props.title : name,
                props: props || {}
            });
        }
    };

    // Flush anything that was tracked between the loader starting and the provider
    // becoming ready. The interval above handles GoatCounter; Plausible's queue handles
    // itself, but a short retry keeps both providers predictable.
    window.setTimeout(flush, 1200);
})();
