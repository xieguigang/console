/*
    Bootstrap and message router.

    Wires the parser, screen, renderer and input handler together and bridges
    them to the WinForms host over the WebView2 message channel.

    Inbound messages from the host are queued and drained on an animation frame:
    the host already batches at ~60 Hz, but a burst that spans several ticks
    still collapses into a single reparse and repaint here.
*/
(function (global) {
    'use strict';

    var host = global.chrome && global.chrome.webview ? global.chrome.webview : null;

    var elements = {
        viewport: document.getElementById('viewport'),
        screen: document.getElementById('screen'),
        measure: document.getElementById('measure'),
        keyboard: document.getElementById('keyboard')
    };

    var screen = new global.TerminalScreen(80, 24);
    var renderer = new global.TerminalRenderer(screen, {
        viewport: elements.viewport,
        screenElement: elements.screen,
        measureElement: elements.measure
    });
    var parser = new global.AnsiParser(screen);

    var pendingChunks = [];
    var drainScheduled = false;
    var reportedCols = 0;
    var reportedRows = 0;

    function post(message) {
        if (host) {
            host.postMessage(JSON.stringify(message));
        }
    }

    function showFatal(text) {
        var banner = document.getElementById('fatal');
        if (!banner) {
            banner = document.createElement('div');
            banner.id = 'fatal';
            document.body.appendChild(banner);
        }
        banner.textContent = text;
    }

    // ---- Output pipeline ----

    function drain() {
        drainScheduled = false;

        if (pendingChunks.length === 0) {
            return;
        }

        // One join, one parse pass: feeding chunk-by-chunk would re-enter the
        // parser's split-sequence bookkeeping for no benefit.
        var text = pendingChunks.join('');
        pendingChunks.length = 0;

        parser.feed(text);
        renderer.schedule();
    }

    function scheduleDrain() {
        if (drainScheduled) {
            return;
        }
        drainScheduled = true;
        global.requestAnimationFrame(drain);
    }

    function write(text) {
        if (!text) {
            return;
        }
        pendingChunks.push(text);
        scheduleDrain();
    }

    // ---- Sizing ----

    function applyGridSize(force) {
        var size = renderer.computeGridSize();

        if (!force && size.cols === reportedCols && size.rows === reportedRows) {
            return;
        }

        screen.resize(size.cols, size.rows);
        renderer.schedule();

        reportedCols = size.cols;
        reportedRows = size.rows;

        post({ type: 'resize', cols: size.cols, rows: size.rows });
    }

    // ---- Input plumbing ----

    var input = new global.TerminalInput({
        element: elements.keyboard,
        viewport: elements.viewport,
        screen: screen,
        renderer: renderer,
        onLine: function (line) {
            post({ type: 'input', data: line });
        },
        onRaw: function (data, line, cursor) {
            // The pending line travels with the key so the host can implement
            // completion against what is actually on screen.
            post({
                type: 'raw',
                data: data,
                line: line || '',
                cursor: typeof cursor === 'number' ? cursor : 0
            });
        },
        onEcho: function (text) {
            /*
                Local echo goes through the same parser as process output so that
                the cursor, wrapping and scrollback stay consistent; the terminal
                has no separate "input zone" the way the RichTextBox did.
            */
            write(text);
            renderer.scrollToBottom();
        }
    });

    screen.onBell = function () {
        post({ type: 'bell' });
    };

    // ---- Host message handling ----

    function handleHostMessage(raw) {
        var message;

        try {
            message = typeof raw === 'string' ? JSON.parse(raw) : raw;
        } catch (e) {
            return;
        }

        if (!message || !message.type) {
            return;
        }

        switch (message.type) {
            case 'output':
                write(message.data);
                break;

            case 'style':
                renderer.applyStyle(message);
                // Cell metrics changed, so the grid almost certainly did too.
                applyGridSize(true);
                break;

            case 'config':
                input.configure(message);
                break;

            case 'clear':
                parser.reset();
                screen.fullReset();
                input.clear();
                renderer.clear();
                break;

            case 'scrollback':
                if (typeof message.lines === 'number' && message.lines > 0) {
                    screen.maxScrollback = message.lines;
                }
                break;

            case 'focus':
                input.focus();
                renderer.setFocused(true);
                break;

            case 'setLine':
                input.setLine(message.data || '');
                break;

            default:
                break;
        }
    }

    if (host) {
        host.addEventListener('message', function (e) {
            // WebView2 delivers PostWebMessageAsString payloads in `data`.
            handleHostMessage(e.data);
        });
    }

    // Also expose a direct entry point so the host can fall back to
    // ExecuteScriptAsync if the message channel is unavailable.
    global.terminalHostMessage = handleHostMessage;

    // ---- Lifecycle ----

    if (global.ResizeObserver) {
        var observer = new global.ResizeObserver(function () {
            applyGridSize(false);
        });
        observer.observe(elements.viewport);
    } else {
        global.addEventListener('resize', function () {
            applyGridSize(false);
        });
    }

    // Web fonts can settle after first paint and shift the cell metrics.
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(function () {
            applyGridSize(true);
        });
    }

    global.addEventListener('error', function (e) {
        showFatal('Terminal renderer error: ' + (e.message || 'unknown'));
    });

    // Suppress zooming so the measured cell size stays authoritative.
    global.addEventListener('wheel', function (e) {
        if (e.ctrlKey) {
            e.preventDefault();
        }
    }, { passive: false });

    document.addEventListener('keydown', function (e) {
        if (e.ctrlKey && (e.key === '+' || e.key === '-' || e.key === '0')) {
            e.preventDefault();
        }
    });

    applyGridSize(true);
    input.focus();
    renderer.setFocused(true);
    renderer.schedule();

    // Tells the host the renderer is live; it replies by flushing everything it
    // queued while WebView2 was still initialising.
    post({ type: 'ready', cols: reportedCols, rows: reportedRows });
})(window);
