/*
    ANSI escape sequence parser.

    Consumes raw output chunks and drives a TerminalScreen. The parser owns the
    "split sequence" problem: a chunk boundary can fall in the middle of a CSI or
    OSC sequence (routine with SSH packetisation), so any unterminated trailing
    sequence is held back in `pendingTail` and re-parsed once more data arrives.
    This is the JS-side equivalent of the buffering AnsiEscapeRenderer.vb needed
    on the host, and it is why the host no longer has to understand ANSI at all.
*/
(function (global) {
    'use strict';

    var ESC = '\x1b';

    /*
        Standard 16-colour palette. Must stay byte-identical to
        AnsiEscapeRenderer.StandardColors so the WebView2 renderer is a pixel
        drop-in for the RichTextBox one.
    */
    var STANDARD_COLORS = [
        '#000000', // Black
        '#8b0000', // DarkRed
        '#006400', // DarkGreen
        '#ff8c00', // DarkOrange
        '#00008b', // DarkBlue
        '#8b008b', // DarkMagenta
        '#008b8b', // DarkCyan
        '#808080', // Gray
        '#a9a9a9', // DarkGray
        '#ff0000', // Red
        '#008000', // Green
        '#ffff00', // Yellow
        '#0000ff', // Blue
        '#ff00ff', // Magenta
        '#00ffff', // Cyan
        '#ffffff'  // White
    ];

    // xterm 6x6x6 colour-cube levels, matching AnsiEscapeRenderer.Xterm256Color.
    var CUBE_LEVELS = [0, 95, 135, 175, 215, 255];

    function toHex(r, g, b) {
        return '#' +
            ('0' + (r & 255).toString(16)).slice(-2) +
            ('0' + (g & 255).toString(16)).slice(-2) +
            ('0' + (b & 255).toString(16)).slice(-2);
    }

    function xterm256(index) {
        if (index < 0) {
            return STANDARD_COLORS[15];
        }
        if (index < 16) {
            return STANDARD_COLORS[index];
        }
        if (index < 232) {
            var n = index - 16;
            return toHex(
                CUBE_LEVELS[Math.floor(n / 36)],
                CUBE_LEVELS[Math.floor(n / 6) % 6],
                CUBE_LEVELS[n % 6]);
        }
        var v = 8 + (index - 232) * 10;
        return toHex(v, v, v);
    }

    /**
     * Determines whether `text` ends inside an escape sequence that has not yet
     * received its final byte, and if so returns the index at which that
     * incomplete sequence starts. Returns -1 when the chunk can be parsed whole.
     */
    function findIncompleteTail(text) {
        var last = text.lastIndexOf(ESC);
        if (last < 0) {
            return -1;
        }

        var rest = text.substring(last);

        // A lone ESC: we cannot know yet what kind of sequence it introduces.
        if (rest.length === 1) {
            return last;
        }

        var kind = rest.charAt(1);

        if (kind === '[') {
            // CSI: parameter and intermediate bytes, terminated by 0x40-0x7E.
            for (var i = 2; i < rest.length; i++) {
                var code = rest.charCodeAt(i);
                if (code >= 0x40 && code <= 0x7e) {
                    return -1;
                }
            }
            return last;
        }

        if (kind === ']') {
            // OSC: terminated by BEL or ST (ESC \).
            if (rest.indexOf('\x07', 2) >= 0) {
                return -1;
            }
            if (rest.indexOf(ESC + '\\', 2) >= 0) {
                return -1;
            }
            return last;
        }

        if (kind === 'P' || kind === 'X' || kind === '^' || kind === '_') {
            // DCS / SOS / PM / APC: all terminated by ST.
            return rest.indexOf(ESC + '\\', 2) >= 0 ? -1 : last;
        }

        // Two-character escapes (ESC 7, ESC 8, ESC M, ESC c, ...) are complete.
        return -1;
    }

    function parseParams(raw) {
        if (raw.length === 0) {
            return [];
        }
        var parts = raw.split(';');
        var out = new Array(parts.length);
        for (var i = 0; i < parts.length; i++) {
            var n = parseInt(parts[i], 10);
            out[i] = isNaN(n) ? 0 : n;
        }
        return out;
    }

    function param(params, index, fallback) {
        if (index >= params.length) {
            return fallback;
        }
        var v = params[index];
        // A zero/omitted parameter means "use the default" for most CSI verbs.
        return v === 0 ? fallback : v;
    }

    function AnsiParser(screen) {
        this.screen = screen;
        this.pendingTail = '';
    }

    /**
     * Feeds a raw output chunk into the parser, applying it to the screen.
     */
    AnsiParser.prototype.feed = function (chunk) {
        if (!chunk) {
            return;
        }

        var text = this.pendingTail + chunk;
        this.pendingTail = '';

        var cut = findIncompleteTail(text);
        if (cut >= 0) {
            /*
                Guard against a malformed stream that never terminates its
                sequence: without a cap, pendingTail would grow without bound and
                the terminal would appear frozen. 4 KB is far beyond any legal
                sequence length.
            */
            if (text.length - cut > 4096) {
                cut = -1;
            } else {
                this.pendingTail = text.substring(cut);
                text = text.substring(0, cut);
            }
        }

        this.parse(text);
    };

    AnsiParser.prototype.parse = function (text) {
        var screen = this.screen;
        var i = 0;
        var length = text.length;
        var run = '';

        function flushRun() {
            if (run.length > 0) {
                screen.writeText(run);
                run = '';
            }
        }

        while (i < length) {
            var ch = text.charAt(i);
            var code = text.charCodeAt(i);

            if (ch === ESC) {
                flushRun();
                i = this.handleEscape(text, i);
                continue;
            }

            if (code < 0x20 || code === 0x7f) {
                flushRun();
                switch (code) {
                    case 0x07: // BEL
                        screen.bell();
                        break;
                    case 0x08: // BS
                        screen.backspace();
                        break;
                    case 0x09: // HT
                        screen.tab();
                        break;
                    case 0x0a: // LF
                    case 0x0b: // VT
                    case 0x0c: // FF
                        screen.lineFeed();
                        break;
                    case 0x0d: // CR
                        screen.carriageReturn();
                        break;
                    default:
                        // Other C0 controls (and DEL) are not meaningful here.
                        break;
                }
                i++;
                continue;
            }

            run += ch;
            i++;
        }

        flushRun();
    };

    /**
     * Handles the escape sequence starting at `start`. Returns the index of the
     * first character after the sequence.
     */
    AnsiParser.prototype.handleEscape = function (text, start) {
        var screen = this.screen;
        var length = text.length;

        if (start + 1 >= length) {
            return length;
        }

        var kind = text.charAt(start + 1);

        if (kind === '[') {
            return this.handleCsi(text, start);
        }

        if (kind === ']') {
            // OSC: window title and friends. Consumed and discarded.
            var end = start + 2;
            while (end < length) {
                if (text.charCodeAt(end) === 0x07) {
                    return end + 1;
                }
                if (text.charAt(end) === ESC && text.charAt(end + 1) === '\\') {
                    return end + 2;
                }
                end++;
            }
            return length;
        }

        if (kind === 'P' || kind === 'X' || kind === '^' || kind === '_') {
            var st = text.indexOf(ESC + '\\', start + 2);
            return st < 0 ? length : st + 2;
        }

        switch (kind) {
            case '7': // DECSC
                screen.saveCursor();
                break;
            case '8': // DECRC
                screen.restoreCursor();
                break;
            case 'M': // RI - reverse index
                screen.reverseIndex();
                break;
            case 'D': // IND - index
                screen.lineFeed();
                break;
            case 'E': // NEL - next line
                screen.carriageReturn();
                screen.lineFeed();
                break;
            case 'c': // RIS - full reset
                screen.fullReset();
                break;
            case '(': // Character set designation: skip the following byte.
            case ')':
            case '*':
            case '+':
                return start + 3;
            default:
                break;
        }

        return start + 2;
    };

    AnsiParser.prototype.handleCsi = function (text, start) {
        var screen = this.screen;
        var length = text.length;
        var i = start + 2;
        var paramStart = i;

        // Private-mode marker (?, >, <, =) precedes the numeric parameters.
        var privateMarker = '';
        if (i < length) {
            var lead = text.charAt(i);
            if (lead === '?' || lead === '>' || lead === '<' || lead === '=') {
                privateMarker = lead;
                i++;
                paramStart = i;
            }
        }

        while (i < length) {
            var code = text.charCodeAt(i);
            if (code >= 0x40 && code <= 0x7e) {
                break;
            }
            i++;
        }

        if (i >= length) {
            return length;
        }

        var raw = text.substring(paramStart, i);
        // Strip intermediate bytes (0x20-0x2F) such as the '!' of DECSTR.
        var intermediates = '';
        var cleaned = '';
        for (var k = 0; k < raw.length; k++) {
            var c = raw.charCodeAt(k);
            if (c >= 0x20 && c <= 0x2f) {
                intermediates += raw.charAt(k);
            } else {
                cleaned += raw.charAt(k);
            }
        }

        var params = parseParams(cleaned);
        var final = text.charAt(i);

        this.dispatchCsi(final, params, privateMarker, intermediates);

        return i + 1;
    };

    AnsiParser.prototype.dispatchCsi = function (final, params, privateMarker, intermediates) {
        var screen = this.screen;

        switch (final) {
            case 'A': // CUU
                screen.moveCursor(-param(params, 0, 1), 0);
                break;
            case 'B': // CUD
                screen.moveCursor(param(params, 0, 1), 0);
                break;
            case 'C': // CUF
                screen.moveCursor(0, param(params, 0, 1));
                break;
            case 'D': // CUB
                screen.moveCursor(0, -param(params, 0, 1));
                break;
            case 'E': // CNL
                screen.moveCursor(param(params, 0, 1), 0);
                screen.carriageReturn();
                break;
            case 'F': // CPL
                screen.moveCursor(-param(params, 0, 1), 0);
                screen.carriageReturn();
                break;
            case 'G': // CHA
            case '`': // HPA
                screen.setCursorColumn(param(params, 0, 1) - 1);
                break;
            case 'd': // VPA
                screen.setCursorRow(param(params, 0, 1) - 1);
                break;
            case 'H': // CUP
            case 'f': // HVP
                screen.setCursor(param(params, 0, 1) - 1, param(params, 1, 1) - 1);
                break;
            case 'J': // ED
                screen.eraseInDisplay(params.length > 0 ? params[0] : 0);
                break;
            case 'K': // EL
                screen.eraseInLine(params.length > 0 ? params[0] : 0);
                break;
            case 'L': // IL
                screen.insertLines(param(params, 0, 1));
                break;
            case 'M': // DL
                screen.deleteLines(param(params, 0, 1));
                break;
            case 'P': // DCH
                screen.deleteChars(param(params, 0, 1));
                break;
            case '@': // ICH
                screen.insertChars(param(params, 0, 1));
                break;
            case 'X': // ECH
                screen.eraseChars(param(params, 0, 1));
                break;
            case 'S': // SU
                screen.scrollUp(param(params, 0, 1));
                break;
            case 'T': // SD
                screen.scrollDown(param(params, 0, 1));
                break;
            case 'r': // DECSTBM
                if (privateMarker === '') {
                    screen.setScrollRegion(
                        params.length > 0 ? params[0] - 1 : 0,
                        params.length > 1 ? params[1] - 1 : screen.rows - 1);
                }
                break;
            case 's': // SCOSC
                screen.saveCursor();
                break;
            case 'u': // SCORC
                screen.restoreCursor();
                break;
            case 'h': // SM / DECSET
                this.setModes(params, privateMarker, true);
                break;
            case 'l': // RM / DECRST
                this.setModes(params, privateMarker, false);
                break;
            case 'm': // SGR
                if (privateMarker === '') {
                    this.applySgr(params);
                }
                break;
            default:
                // Unsupported verbs (device reports, cursor style, ...) are
                // silently swallowed rather than leaking into the output.
                break;
        }
    };

    AnsiParser.prototype.setModes = function (params, privateMarker, enable) {
        var screen = this.screen;

        for (var i = 0; i < params.length; i++) {
            var mode = params[i];

            if (privateMarker === '?') {
                switch (mode) {
                    case 25: // DECTCEM - cursor visibility
                        screen.setCursorVisible(enable);
                        break;
                    case 1049:
                    case 1047:
                    case 47: // Alternate screen buffer
                        screen.setAlternateBuffer(enable);
                        break;
                    case 7: // DECAWM - auto-wrap
                        screen.setAutoWrap(enable);
                        break;
                    default:
                        break;
                }
            }
        }
    };

    AnsiParser.prototype.applySgr = function (params) {
        var screen = this.screen;

        if (params.length === 0) {
            screen.resetAttrs();
            return;
        }

        var i = 0;
        while (i < params.length) {
            var code = params[i];

            /*
                Re-read on every iteration: resetAttrs() installs a fresh object,
                so a cached reference would leave later codes in the same run
                (e.g. the common "\x1b[0;31m") mutating an orphaned copy.
            */
            var attrs = screen.attrs;

            if (code === 0) {
                screen.resetAttrs();
            } else if (code === 1) {
                attrs.bold = true;
            } else if (code === 2) {
                attrs.dim = true;
            } else if (code === 3) {
                attrs.italic = true;
            } else if (code === 4) {
                attrs.underline = true;
            } else if (code === 7) {
                attrs.inverse = true;
            } else if (code === 8) {
                attrs.hidden = true;
            } else if (code === 9) {
                attrs.strike = true;
            } else if (code === 21 || code === 22) {
                attrs.bold = false;
                attrs.dim = false;
            } else if (code === 23) {
                attrs.italic = false;
            } else if (code === 24) {
                attrs.underline = false;
            } else if (code === 27) {
                attrs.inverse = false;
            } else if (code === 28) {
                attrs.hidden = false;
            } else if (code === 29) {
                attrs.strike = false;
            } else if (code >= 30 && code <= 37) {
                attrs.fg = STANDARD_COLORS[code - 30];
            } else if (code === 38) {
                i = this.applyExtendedColor(params, i, true);
            } else if (code === 39) {
                attrs.fg = null; // default foreground
            } else if (code >= 40 && code <= 47) {
                attrs.bg = STANDARD_COLORS[code - 40];
            } else if (code === 48) {
                i = this.applyExtendedColor(params, i, false);
            } else if (code === 49) {
                attrs.bg = null; // default background
            } else if (code >= 90 && code <= 97) {
                attrs.fg = STANDARD_COLORS[code - 90 + 8];
            } else if (code >= 100 && code <= 107) {
                attrs.bg = STANDARD_COLORS[code - 100 + 8];
            }

            i++;
        }
    };

    /**
     * Handles the 38/48 extended-colour forms. Returns the index of the last
     * parameter consumed, mirroring AnsiEscapeRenderer.ApplyExtendedColor.
     */
    AnsiParser.prototype.applyExtendedColor = function (params, i, isFore) {
        var attrs = this.screen.attrs;
        var idx = i + 1;

        if (idx >= params.length) {
            return i;
        }

        var mode = params[idx];

        if (mode === 5) {
            idx++;
            if (idx < params.length) {
                var color = xterm256(params[idx]);
                if (isFore) {
                    attrs.fg = color;
                } else {
                    attrs.bg = color;
                }
            }
            return idx;
        }

        if (mode === 2) {
            idx++;
            if (idx + 2 < params.length) {
                var rgb = toHex(params[idx], params[idx + 1], params[idx + 2]);
                if (isFore) {
                    attrs.fg = rgb;
                } else {
                    attrs.bg = rgb;
                }
                return idx + 2;
            }
            return params.length - 1;
        }

        return i;
    };

    AnsiParser.prototype.reset = function () {
        this.pendingTail = '';
    };

    global.AnsiParser = AnsiParser;
    global.AnsiPalette = {
        standard: STANDARD_COLORS,
        xterm256: xterm256
    };
})(window);
