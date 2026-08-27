// The one animated moment in the app: when a solve lands, the composition bar draws itself and
// the total cost counts up to the figure the server already rendered. Nothing else on the site
// animates, and nothing here is required for the page to be correct — with this file blocked the
// bar is already full width and the cost already reads ৳231.03, because the CSS collapse and the
// transition are both gated behind classes only this file adds.
//
// Loaded by _Layout on every page. It finds nothing to do on most of them and exits.

(function () {
    'use strict';

    // 40 ms is enough to read the segments as arriving in mix order rather than all at once, and
    // short enough that a twelve-ingredient formulation still finishes inside a second.
    var STAGGER_MS = 40;
    var COUNT_MS = 900;

    // Two of everything on /Experiment, so each bar staggers from its own first segment and each
    // figure counts on its own.
    function bars() {
        return document.querySelectorAll('.fc-mixbar');
    }

    function figures() {
        return document.querySelectorAll('[data-fc-countup]');
    }

    // Someone who has asked their operating system for less motion gets the finished state, which
    // is what the markup already says. The CSS has its own reduced-motion rule as a backstop; this
    // is the half that stops the count-up from running.
    function prefersReducedMotion() {
        return window.matchMedia
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    // Ease-out cubic. The figure should arrive quickly and settle, rather than crawl the last
    // tenth — a linear count reads as a progress bar, which is not what a price is.
    function easeOut(t) {
        var inv = 1 - t;
        return 1 - inv * inv * inv;
    }

    function countUp(figure) {
        var out = figure.querySelector('[data-fc-countup-out]');
        var target = parseFloat(figure.getAttribute('data-fc-countup'));

        // The attribute is written with InvariantCulture on the server precisely so parseFloat can
        // read it. If it still fails, or there is nowhere to write, the server's own text stands.
        if (!out || isNaN(target)) {
            return;
        }

        var started = null;

        function frame(now) {
            if (started === null) {
                started = now;
            }

            var progress = Math.min(1, (now - started) / COUNT_MS);

            if (progress < 1) {
                out.textContent = (target * easeOut(progress)).toFixed(2);
                window.requestAnimationFrame(frame);
            } else {
                // Assigned from the attribute rather than computed, so the figure on screen at the
                // end is character-for-character the one the server sent — no chance of the
                // animation's last frame rounding to a different string.
                out.textContent = figure.getAttribute('data-fc-countup');
            }
        }

        window.requestAnimationFrame(frame);
    }

    function revealBar(bar) {
        var segments = bar.children;

        for (var i = 0; i < segments.length; i++) {
            segments[i].style.transitionDelay = (i * STAGGER_MS) + 'ms';
        }

        // Collapse first, paint, then release. Without the second frame the browser is free to
        // coalesce both class changes into one style recalculation, which means it never observes
        // scaleX(0) and there is no transition to run.
        bar.classList.add('fc-reveal');

        window.requestAnimationFrame(function () {
            window.requestAnimationFrame(function () {
                bar.classList.add('fc-reveal-in');
            });
        });
    }

    function run() {
        if (prefersReducedMotion()) {
            return;
        }

        var mixbars = bars();
        for (var i = 0; i < mixbars.length; i++) {
            revealBar(mixbars[i]);
        }

        var counters = figures();
        for (var k = 0; k < counters.length; k++) {
            countUp(counters[k]);
        }
    }

    // This script is loaded at the end of <body>, so the results are already parsed; the readyState
    // check only covers a future move into <head>.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', run);
    } else {
        run();
    }
})();
