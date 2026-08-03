/*
    Keyboard and mouse input.

    Because the WebView owns the focus, all key handling happens here rather than
    in WinForms. Keys are translated into the byte sequences a terminal would
    emit and handed back to the host, which forwards them to the process
    interface. Line-oriented editing (the visible input line and its history) is
    also maintained here so the behaviour matches the RichTextBox control.
*/
(function (global) {
    'use strict';

    var ETX = '\x03';
    var ESC = '\x1b';

    // Keys that translate directly into a fixed escape sequence.
    var SPECIAL_KEYS = {
        Escape: ESC,
        ArrowUp: '\x1b[A',
        ArrowDown: '\x1b[B',
        ArrowRight: '\x1b[C',
        ArrowLeft: '\x1b[D',
        Home: '\x1b[H',
        End: '\x1b[F',
        Insert: '\x1b[2~',
        Delete: '\x1b[3~',
        PageUp: '\x1b[5~',
        PageDown: '\x1b[6~',
        F1: '\x1bOP',
        F2: '\x1bOQ',
        F3: '\x1bOR',
        F4: '\x1bOS',
        F5: '\x1b[15~',
        F6: '\x1b[17~',
        F7: '\x1b[18~',
        F8: '\x1b[19~',
        F9: '\x1b[20~',
        F10: '\x1b[21~',
        F11: '\x1b[23~',
        F12: '\x1b[24~'
    };

    function repeat(text, count) {
        var out = '';
        for (var i = 0; i < count; i++) {
            out += text;
        }
        return out;
    }

    function TerminalInput(options) {
        this.element = options.element;
        this.viewport = options.viewport;
        this.screen = options.screen;
        this.renderer = options.renderer;

        // Callbacks supplied by the app layer.
        this.onLine = options.onLine || function () { };
        this.onRaw = options.onRaw || function () { };
        this.onEcho = options.onEcho || function () { };
        this.onFocusChange = options.onFocusChange || function () { };

        this.inputEnabled = true;
        this.readOnly = false;
        this.sendKeysToProcess = false;
        this.keyMappings = [];

        // Current (not yet submitted) input line, the caret offset within it and
        // the history ring.
        this.buffer = '';
        this.cursor = 0;
        this.history = [];
        this.historyIndex = 0;
        this.maxHistory = 500;

        this.composing = false;

        this.attach();
    }

    TerminalInput.prototype.configure = function (config) {
        if (typeof config.inputEnabled === 'boolean') {
            this.inputEnabled = config.inputEnabled;
        }
        if (typeof config.readOnly === 'boolean') {
            this.readOnly = config.readOnly;
        }
        if (typeof config.sendKeysToProcess === 'boolean') {
            this.sendKeysToProcess = config.sendKeysToProcess;
        }
        if (Array.isArray(config.keyMappings)) {
            this.keyMappings = config.keyMappings;
        }
    };

    TerminalInput.prototype.canType = function () {
        return this.inputEnabled && !this.readOnly;
    };

    TerminalInput.prototype.focus = function () {
        try {
            this.element.focus({ preventScroll: true });
        } catch (e) {
            this.element.focus();
        }
    };

    TerminalInput.prototype.attach = function () {
        var self = this;
        var element = this.element;

        element.addEventListener('keydown', function (e) {
            self.handleKeyDown(e);
        });

        element.addEventListener('compositionstart', function () {
            self.composing = true;
        });

        element.addEventListener('compositionend', function (e) {
            self.composing = false;
            if (e.data) {
                self.typeText(e.data);
            }
            element.value = '';
        });

        // `input` catches IME output and anything the browser inserts that
        // keydown did not already consume.
        element.addEventListener('input', function (e) {
            if (self.composing) {
                return;
            }
            var value = element.value;
            element.value = '';
            if (value.length > 0) {
                self.typeText(value);
            }
        });

        element.addEventListener('focus', function () {
            self.renderer.setFocused(true);
            self.onFocusChange(true);
        });

        element.addEventListener('blur', function () {
            self.renderer.setFocused(false);
            self.onFocusChange(false);
        });

        element.addEventListener('paste', function (e) {
            e.preventDefault();
            var text = (e.clipboardData || global.clipboardData).getData('text');
            if (text) {
                self.paste(text);
            }
        });

        // Clicking anywhere hands focus back to the hidden textarea, unless the
        // user is selecting text (in which case stealing focus would clear it).
        this.viewport.addEventListener('mousedown', function (e) {
            if (e.button === 2) {
                return;
            }
            global.setTimeout(function () {
                if (String(global.getSelection()) === '') {
                    self.focus();
                }
            }, 0);
        });

        // Select-to-copy, matching the RichTextBox control's MouseUp behaviour.
        this.viewport.addEventListener('mouseup', function (e) {
            if (e.button !== 0) {
                return;
            }
            var selection = String(global.getSelection());
            if (selection.length > 0) {
                self.copy(selection);
            }
        });

        // Right-click pastes; the native menu is suppressed.
        this.viewport.addEventListener('contextmenu', function (e) {
            e.preventDefault();
            self.requestPaste();
        });

        document.addEventListener('contextmenu', function (e) {
            e.preventDefault();
        });

        // Last line of defence: if the document is given the focus but it settles
        // on the body rather than the sink - which is what happens when the host
        // calls WebView2.Focus() - pull it back, otherwise no key ever reaches
        // the handlers above and the terminal looks unresponsive.
        global.addEventListener('focus', function () {
            if (document.activeElement !== element) {
                self.focus();
            }
        });

        document.addEventListener('mousedown', function () {
            global.setTimeout(function () {
                if (document.activeElement === document.body &&
                    String(global.getSelection()) === '') {
                    self.focus();
                }
            }, 0);
        });
    };

    /**
     * Looks for a host-supplied mapping matching the current key chord.
     * Mappings carry the exact byte string the RichTextBox control would send.
     */
    TerminalInput.prototype.findMapping = function (e) {
        for (var i = 0; i < this.keyMappings.length; i++) {
            var m = this.keyMappings[i];
            if (!!m.ctrl === e.ctrlKey &&
                !!m.alt === e.altKey &&
                !!m.shift === e.shiftKey &&
                m.key === e.key) {
                return m;
            }
        }
        return null;
    };

    TerminalInput.prototype.handleKeyDown = function (e) {
        if (this.composing) {
            return;
        }

        // Let the browser handle explicit copy so native selection copy works.
        if (e.ctrlKey && !e.altKey && (e.key === 'c' || e.key === 'C')) {
            if (String(global.getSelection()) !== '') {
                return;
            }
        }

        if (e.ctrlKey && !e.altKey && (e.key === 'v' || e.key === 'V')) {
            return; // handled by the paste event
        }

        // Scrollback navigation is a viewer concern, not process input.
        if (e.shiftKey && (e.key === 'PageUp' || e.key === 'PageDown')) {
            var delta = this.viewport.clientHeight * (e.key === 'PageUp' ? -1 : 1);
            this.viewport.scrollTop += delta;
            e.preventDefault();
            return;
        }

        if (!this.canType()) {
            return;
        }

        // Host-declared chords are control signals rather than text, so they are
        // forwarded in both modes. In line-edit mode the back-end is the only
        // party that can act on them meaningfully (tab completion needs the
        // command table, Ctrl+C needs to abandon the pending line), and dropping
        // them here is what previously made Tab and Ctrl+C dead keys in the
        // local shell.
        var mapping = this.findMapping(e);
        if (mapping) {
            e.preventDefault();
            this.emitRaw(mapping.data);
            if (mapping.data === ETX) {
                this.buffer = '';
                this.cursor = 0;
            }
            return;
        }

        if (e.key === 'Enter') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                // The remote PTY assembles lines itself; sending a submitted line
                // as well would duplicate every command.
                this.emitRaw('\r');
            } else {
                this.submit();
            }
            return;
        }

        if (e.key === 'Backspace') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.emitRaw('\x7f');
            } else {
                this.deleteBackward();
            }
            return;
        }

        if (e.key === 'Delete' && !this.sendKeysToProcess) {
            e.preventDefault();
            this.deleteForward();
            return;
        }

        if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.emitRaw(SPECIAL_KEYS[e.key]);
            } else {
                this.navigateHistory(e.key === 'ArrowUp' ? -1 : 1);
            }
            return;
        }

        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.emitRaw(SPECIAL_KEYS[e.key]);
            } else {
                this.moveCursor(e.key === 'ArrowLeft' ? -1 : 1);
            }
            return;
        }

        if (e.key === 'Home' || e.key === 'End') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.emitRaw(SPECIAL_KEYS[e.key]);
            } else {
                this.setCursor(e.key === 'Home' ? 0 : this.buffer.length);
            }
            return;
        }

        if (e.key === 'Tab') {
            e.preventDefault();
            // Always forwarded: completion is the back-end's business in both
            // modes.
            this.emitRaw('\t');
            return;
        }

        if (e.key === 'Escape') {
            // preventDefault matters here: in a WebView the browser still acts on
            // Escape (cancelling page load, leaving full screen), and Escape is
            // not a printable character, so without an explicit branch it reaches
            // neither the `input` event nor the process and is silently dropped -
            // which is what stopped vim from leaving insert mode.
            e.preventDefault();
            // Forwarded in both modes, like Tab: Escape is a control signal, not
            // text, and only the back-end can act on it.
            this.emitRaw(ESC);
            if (!this.sendKeysToProcess) {
                // Line-edit mode has no remote reader to interpret the escape, so
                // give it the local meaning of abandoning the pending input line.
                this.replaceBuffer('');
            }
            return;
        }

        if (e.ctrlKey && !e.altKey && e.key.length === 1) {
            var upper = e.key.toUpperCase();
            if (upper >= 'A' && upper <= 'Z') {
                e.preventDefault();
                this.emitRaw(String.fromCharCode(upper.charCodeAt(0) - 64));
                return;
            }
        }

        var special = SPECIAL_KEYS[e.key];
        if (special) {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.emitRaw(special);
            }
            return;
        }

        // Printable characters fall through to the `input` event so that dead
        // keys and IME composition keep working.
    };

    /**
     * Sends a raw key sequence to the host along with the line being edited, so
     * back-ends can implement completion without shadowing the renderer's state.
     */
    TerminalInput.prototype.emitRaw = function (data) {
        this.onRaw(data, this.buffer, this.cursor);
    };

    TerminalInput.prototype.typeText = function (text) {
        if (!this.canType() || text.length === 0) {
            return;
        }

        // In raw mode the back-end owns the echo, so characters go straight out.
        if (this.sendKeysToProcess) {
            this.emitRaw(text.replace(/\n/g, '\r'));
            return;
        }

        var printable = '';
        for (var i = 0; i < text.length; i++) {
            var code = text.charCodeAt(i);
            if (code === 10 || code === 13) {
                this.insert(printable);
                printable = '';
                this.submit();
                continue;
            }
            if (code >= 0x20 && code !== 0x7f) {
                printable += text.charAt(i);
            }
        }

        this.insert(printable);
    };

    /**
     * Inserts `text` at the caret and repaints the tail of the line.
     */
    TerminalInput.prototype.insert = function (text) {
        if (!text || text.length === 0) {
            return;
        }

        var tail = this.buffer.substring(this.cursor);

        this.buffer = this.buffer.substring(0, this.cursor) + text + tail;
        this.cursor += text.length;

        // Reprint the remainder, then walk the caret back over it so the visible
        // caret matches this.cursor.
        this.onEcho(text + tail + repeat('\b', tail.length));
    };

    /**
     * Backspace: removes the character before the caret.
     */
    TerminalInput.prototype.deleteBackward = function () {
        if (this.cursor === 0) {
            return;
        }

        var tail = this.buffer.substring(this.cursor);

        this.buffer = this.buffer.substring(0, this.cursor - 1) + tail;
        this.cursor -= 1;

        // Step back over the doomed glyph, redraw the tail, blank the now-stale
        // trailing cell, then return the caret.
        this.onEcho('\b' + tail + ' ' + repeat('\b', tail.length + 1));
    };

    /**
     * Delete: removes the character under the caret.
     */
    TerminalInput.prototype.deleteForward = function () {
        if (this.cursor >= this.buffer.length) {
            return;
        }

        var tail = this.buffer.substring(this.cursor + 1);

        this.buffer = this.buffer.substring(0, this.cursor) + tail;

        this.onEcho(tail + ' ' + repeat('\b', tail.length + 1));
    };

    TerminalInput.prototype.moveCursor = function (delta) {
        this.setCursor(this.cursor + delta);
    };

    TerminalInput.prototype.setCursor = function (position) {
        var target = Math.max(0, Math.min(this.buffer.length, position));

        if (target === this.cursor) {
            return;
        }

        if (target < this.cursor) {
            this.onEcho(repeat('\b', this.cursor - target));
        } else {
            this.onEcho(this.buffer.substring(this.cursor, target));
        }

        this.cursor = target;
    };

    TerminalInput.prototype.paste = function (text) {
        // Normalise line endings so a multi-line paste submits cleanly.
        this.typeText(text.replace(/\r\n/g, '\n').replace(/\r/g, '\n'));
    };

    TerminalInput.prototype.submit = function () {
        var line = this.buffer;

        // Park the caret past the tail before breaking the line, otherwise the
        // remainder of a mid-line submit would be overwritten.
        this.setCursor(line.length);

        this.buffer = '';
        this.cursor = 0;

        this.onEcho('\r\n');

        if (line.length > 0) {
            // Collapse consecutive duplicates the way a shell history does.
            if (this.history.length === 0 || this.history[this.history.length - 1] !== line) {
                this.history.push(line);
                if (this.history.length > this.maxHistory) {
                    this.history.shift();
                }
            }
        }
        this.historyIndex = this.history.length;

        this.onLine(line);
    };

    TerminalInput.prototype.navigateHistory = function (direction) {
        if (this.history.length === 0) {
            return;
        }

        var next = this.historyIndex + direction;

        if (next < 0) {
            next = 0;
        }
        if (next > this.history.length) {
            next = this.history.length;
        }
        if (next === this.historyIndex) {
            return;
        }

        this.historyIndex = next;

        var replacement = next < this.history.length ? this.history[next] : '';
        this.replaceBuffer(replacement);
    };

    /**
     * Swaps the visible input line for `text` by erasing what was echoed before.
     */
    TerminalInput.prototype.replaceBuffer = function (text) {
        var replacement = text || '';

        // Rewind to the start of the line before erasing, so the caret does not
        // have to be at the end for this to work.
        var prefix = repeat('\b', this.cursor);
        var blank = repeat(' ', this.buffer.length);
        var rewind = repeat('\b', this.buffer.length);

        this.buffer = replacement;
        this.cursor = replacement.length;

        this.onEcho(prefix + blank + rewind + replacement);
    };

    /**
     * Applies a line rewritten by the host - tab completion, for instance - so
     * the screen and this buffer stay in agreement.
     */
    TerminalInput.prototype.setLine = function (text) {
        this.replaceBuffer(text || '');
    };

    TerminalInput.prototype.copy = function (text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text)['catch'](function () {
                /* Clipboard access can be denied; silently ignore. */
            });
        }
    };

    TerminalInput.prototype.requestPaste = function () {
        var self = this;

        if (navigator.clipboard && navigator.clipboard.readText) {
            navigator.clipboard.readText().then(function (text) {
                if (text) {
                    self.paste(text);
                }
            })['catch'](function () {
                /* Denied or empty; nothing to paste. */
            });
        }
    };

    TerminalInput.prototype.clear = function () {
        this.buffer = '';
        this.cursor = 0;
        this.historyIndex = this.history.length;
    };

    global.TerminalInput = TerminalInput;
})(window);
