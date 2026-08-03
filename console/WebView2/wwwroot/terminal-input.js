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

    // Keys that translate directly into a fixed escape sequence.
    var SPECIAL_KEYS = {
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

        // Current (not yet submitted) input line and the history ring.
        this.buffer = '';
        this.history = [];
        this.historyIndex = 0;

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

        var mapping = this.findMapping(e);
        if (mapping) {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                // Mapped chords are control signals: they must arrive verbatim,
                // with no line terminator appended.
                this.onRaw(mapping.data);
                if (mapping.data === ETX) {
                    this.buffer = '';
                }
            }
            return;
        }

        if (e.key === 'Enter') {
            e.preventDefault();
            this.submit();
            return;
        }

        if (e.key === 'Backspace') {
            e.preventDefault();
            if (this.buffer.length > 0) {
                this.buffer = this.buffer.substring(0, this.buffer.length - 1);
                this.onEcho('\b \b');
            }
            return;
        }

        if (e.key === 'ArrowUp' || e.key === 'ArrowDown') {
            e.preventDefault();
            this.navigateHistory(e.key === 'ArrowUp' ? -1 : 1);
            return;
        }

        if (e.key === 'Tab') {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.onRaw('\t');
            }
            return;
        }

        if (e.ctrlKey && !e.altKey && e.key.length === 1) {
            var upper = e.key.toUpperCase();
            if (upper >= 'A' && upper <= 'Z') {
                e.preventDefault();
                if (this.sendKeysToProcess) {
                    this.onRaw(String.fromCharCode(upper.charCodeAt(0) - 64));
                }
                return;
            }
        }

        var special = SPECIAL_KEYS[e.key];
        if (special) {
            e.preventDefault();
            if (this.sendKeysToProcess) {
                this.onRaw(special);
            }
            return;
        }

        // Printable characters fall through to the `input` event so that dead
        // keys and IME composition keep working.
    };

    TerminalInput.prototype.typeText = function (text) {
        if (!this.canType() || text.length === 0) {
            return;
        }

        var printable = '';
        for (var i = 0; i < text.length; i++) {
            var code = text.charCodeAt(i);
            if (code === 10 || code === 13) {
                this.buffer += printable;
                printable = '';
                this.submit();
                continue;
            }
            if (code >= 0x20 && code !== 0x7f) {
                printable += text.charAt(i);
            }
        }

        if (printable.length > 0) {
            this.buffer += printable;
            this.onEcho(printable);
        }
    };

    TerminalInput.prototype.paste = function (text) {
        // Normalise line endings so a multi-line paste submits cleanly.
        this.typeText(text.replace(/\r\n/g, '\n').replace(/\r/g, '\n'));
    };

    TerminalInput.prototype.submit = function () {
        var line = this.buffer;
        this.buffer = '';

        this.onEcho('\r\n');

        if (line.length > 0) {
            // Collapse consecutive duplicates the way a shell history does.
            if (this.history.length === 0 || this.history[this.history.length - 1] !== line) {
                this.history.push(line);
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
        var erase = '';
        for (var i = 0; i < this.buffer.length; i++) {
            erase += '\b \b';
        }

        this.buffer = text;
        this.onEcho(erase + text);
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
        this.historyIndex = this.history.length;
    };

    global.TerminalInput = TerminalInput;
})(window);
