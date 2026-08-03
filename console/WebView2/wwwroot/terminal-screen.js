/*
    Terminal screen model: the character grid the ANSI parser writes into.

    This is the JS counterpart of the VB TerminalBuffer. It owns cursor state,
    scroll regions, the scrollback history and the alternate screen buffer, and
    tracks which rows changed so the renderer only has to rebuild those.
*/
(function (global) {
    'use strict';

    var TAB_WIDTH = 8;
    var DEFAULT_SCROLLBACK = 5000;

    function defaultAttrs() {
        return {
            fg: null,
            bg: null,
            bold: false,
            dim: false,
            italic: false,
            underline: false,
            strike: false,
            inverse: false,
            hidden: false
        };
    }

    function copyAttrs(a) {
        return {
            fg: a.fg,
            bg: a.bg,
            bold: a.bold,
            dim: a.dim,
            italic: a.italic,
            underline: a.underline,
            strike: a.strike,
            inverse: a.inverse,
            hidden: a.hidden
        };
    }

    /**
     * Cells are compared constantly during rendering, so their styling is
     * flattened into a single interned key string. Comparing keys is a cheap
     * string equality test instead of a nine-field struct comparison.
     */
    function attrKey(a) {
        if (a === null) {
            return '';
        }
        return (a.fg || '') + '|' + (a.bg || '') + '|' +
            (a.bold ? 'b' : '') + (a.dim ? 'd' : '') + (a.italic ? 'i' : '') +
            (a.underline ? 'u' : '') + (a.strike ? 's' : '') +
            (a.inverse ? 'v' : '') + (a.hidden ? 'h' : '');
    }

    function makeCell(ch, attrs, key) {
        return { ch: ch, attrs: attrs, key: key };
    }

    var BLANK_KEY = '';
    function blankCell() {
        return makeCell(' ', null, BLANK_KEY);
    }

    function makeRow(cols) {
        var row = new Array(cols);
        for (var i = 0; i < cols; i++) {
            row[i] = blankCell();
        }
        return row;
    }

    function TerminalScreen(cols, rows) {
        this.cols = Math.max(1, cols || 80);
        this.rows = Math.max(1, rows || 24);

        this.attrs = defaultAttrs();
        this.scrollback = [];
        this.maxScrollback = DEFAULT_SCROLLBACK;

        this.onBell = null;

        this.reset();
    }

    TerminalScreen.prototype.reset = function () {
        this.grid = [];
        for (var i = 0; i < this.rows; i++) {
            this.grid.push(makeRow(this.cols));
        }

        this.cursorRow = 0;
        this.cursorCol = 0;
        this.cursorVisible = true;
        this.autoWrap = true;
        // Set once the cursor writes into the last column; the actual wrap is
        // deferred until the next glyph arrives (DEC "pending wrap" semantics).
        this.wrapPending = false;

        this.scrollTop = 0;
        this.scrollBottom = this.rows - 1;

        this.savedCursor = null;
        this.alternate = null;

        this.attrs = defaultAttrs();
        this.attrsKey = attrKey(this.attrs);

        this.dirtyRows = new Set();
        this.allDirty = true;
    };

    TerminalScreen.prototype.fullReset = function () {
        this.scrollback.length = 0;
        this.reset();
    };

    TerminalScreen.prototype.markDirty = function (row) {
        if (row >= 0 && row < this.rows) {
            this.dirtyRows.add(row);
        }
    };

    TerminalScreen.prototype.markAllDirty = function () {
        this.allDirty = true;
    };

    TerminalScreen.prototype.clearDirty = function () {
        this.dirtyRows.clear();
        this.allDirty = false;
    };

    // ---- Attributes ----

    TerminalScreen.prototype.resetAttrs = function () {
        this.attrs = defaultAttrs();
        this.attrsKey = attrKey(this.attrs);
    };

    /**
     * Returns the styling to stamp onto newly written cells. Cells with default
     * styling store `null` so that the common case costs no memory and compares
     * instantly.
     */
    TerminalScreen.prototype.currentStamp = function () {
        var key = attrKey(this.attrs);
        this.attrsKey = key;
        if (key === BLANK_KEY) {
            return { attrs: null, key: BLANK_KEY };
        }
        return { attrs: copyAttrs(this.attrs), key: key };
    };

    // ---- Text output ----

    TerminalScreen.prototype.writeText = function (text) {
        var stamp = this.currentStamp();

        for (var i = 0; i < text.length; i++) {
            this.writeChar(text.charAt(i), stamp);
        }
    };

    TerminalScreen.prototype.writeChar = function (ch, stamp) {
        if (this.wrapPending) {
            if (this.autoWrap) {
                this.carriageReturn();
                this.lineFeed();
            }
            this.wrapPending = false;
        }

        if (this.cursorCol >= this.cols) {
            this.cursorCol = this.cols - 1;
        }

        var row = this.grid[this.cursorRow];
        row[this.cursorCol] = makeCell(ch, stamp.attrs, stamp.key);
        this.markDirty(this.cursorRow);

        if (this.cursorCol === this.cols - 1) {
            // Defer the wrap: a program that fills the last column and then
            // repositions the cursor must not gain a spurious blank line.
            this.wrapPending = true;
        } else {
            this.cursorCol++;
        }
    };

    // ---- Control characters ----

    TerminalScreen.prototype.carriageReturn = function () {
        this.cursorCol = 0;
        this.wrapPending = false;
    };

    TerminalScreen.prototype.lineFeed = function () {
        this.wrapPending = false;

        if (this.cursorRow === this.scrollBottom) {
            this.scrollUp(1);
        } else if (this.cursorRow < this.rows - 1) {
            this.cursorRow++;
        }
    };

    TerminalScreen.prototype.reverseIndex = function () {
        this.wrapPending = false;

        if (this.cursorRow === this.scrollTop) {
            this.scrollDown(1);
        } else if (this.cursorRow > 0) {
            this.cursorRow--;
        }
    };

    TerminalScreen.prototype.backspace = function () {
        this.wrapPending = false;
        if (this.cursorCol > 0) {
            this.cursorCol--;
        }
    };

    TerminalScreen.prototype.tab = function () {
        this.wrapPending = false;
        var next = (Math.floor(this.cursorCol / TAB_WIDTH) + 1) * TAB_WIDTH;
        this.cursorCol = Math.min(next, this.cols - 1);
    };

    TerminalScreen.prototype.bell = function () {
        if (typeof this.onBell === 'function') {
            this.onBell();
        }
    };

    // ---- Cursor ----

    TerminalScreen.prototype.setCursor = function (row, col) {
        this.cursorRow = Math.min(Math.max(row, 0), this.rows - 1);
        this.cursorCol = Math.min(Math.max(col, 0), this.cols - 1);
        this.wrapPending = false;
    };

    TerminalScreen.prototype.setCursorRow = function (row) {
        this.setCursor(row, this.cursorCol);
    };

    TerminalScreen.prototype.setCursorColumn = function (col) {
        this.setCursor(this.cursorRow, col);
    };

    TerminalScreen.prototype.moveCursor = function (dRow, dCol) {
        this.setCursor(this.cursorRow + dRow, this.cursorCol + dCol);
    };

    TerminalScreen.prototype.saveCursor = function () {
        this.savedCursor = {
            row: this.cursorRow,
            col: this.cursorCol,
            attrs: copyAttrs(this.attrs)
        };
    };

    TerminalScreen.prototype.restoreCursor = function () {
        if (!this.savedCursor) {
            return;
        }
        this.setCursor(this.savedCursor.row, this.savedCursor.col);
        this.attrs = copyAttrs(this.savedCursor.attrs);
        this.attrsKey = attrKey(this.attrs);
    };

    TerminalScreen.prototype.setCursorVisible = function (visible) {
        this.cursorVisible = !!visible;
        this.markDirty(this.cursorRow);
    };

    TerminalScreen.prototype.setAutoWrap = function (enable) {
        this.autoWrap = !!enable;
    };

    // ---- Scrolling ----

    TerminalScreen.prototype.setScrollRegion = function (top, bottom) {
        top = Math.min(Math.max(top, 0), this.rows - 1);
        bottom = Math.min(Math.max(bottom, 0), this.rows - 1);

        if (top >= bottom) {
            // An inverted or degenerate region means "reset to full screen".
            this.scrollTop = 0;
            this.scrollBottom = this.rows - 1;
        } else {
            this.scrollTop = top;
            this.scrollBottom = bottom;
        }

        this.setCursor(this.scrollTop, 0);
    };

    TerminalScreen.prototype.pushScrollback = function (row) {
        // Only the primary buffer contributes history; alternate-screen content
        // (vim, htop) would otherwise pollute the user's scrollback.
        if (this.alternate !== null) {
            return;
        }

        this.scrollback.push(row);

        if (this.scrollback.length > this.maxScrollback) {
            this.scrollback.splice(0, this.scrollback.length - this.maxScrollback);
        }
    };

    TerminalScreen.prototype.scrollUp = function (count) {
        count = Math.max(1, count);
        var fullScreen = this.scrollTop === 0 && this.scrollBottom === this.rows - 1;

        for (var n = 0; n < count; n++) {
            var evicted = this.grid[this.scrollTop];
            this.grid.splice(this.scrollTop, 1);
            this.grid.splice(this.scrollBottom, 0, makeRow(this.cols));

            if (fullScreen) {
                this.pushScrollback(evicted);
            }
        }

        this.markAllDirty();
    };

    TerminalScreen.prototype.scrollDown = function (count) {
        count = Math.max(1, count);

        for (var n = 0; n < count; n++) {
            this.grid.splice(this.scrollBottom, 1);
            this.grid.splice(this.scrollTop, 0, makeRow(this.cols));
        }

        this.markAllDirty();
    };

    // ---- Erasing ----

    TerminalScreen.prototype.eraseInLine = function (mode) {
        var row = this.grid[this.cursorRow];
        var from;
        var to;

        if (mode === 1) {
            from = 0;
            to = this.cursorCol;
        } else if (mode === 2) {
            from = 0;
            to = this.cols - 1;
        } else {
            from = this.cursorCol;
            to = this.cols - 1;
        }

        for (var i = from; i <= to && i < this.cols; i++) {
            row[i] = blankCell();
        }

        this.markDirty(this.cursorRow);
    };

    TerminalScreen.prototype.eraseInDisplay = function (mode) {
        var r;

        if (mode === 1) {
            for (r = 0; r < this.cursorRow; r++) {
                this.grid[r] = makeRow(this.cols);
            }
            this.eraseInLine(1);
        } else if (mode === 2 || mode === 3) {
            for (r = 0; r < this.rows; r++) {
                this.grid[r] = makeRow(this.cols);
            }
            if (mode === 3) {
                this.scrollback.length = 0;
            }
        } else {
            this.eraseInLine(0);
            for (r = this.cursorRow + 1; r < this.rows; r++) {
                this.grid[r] = makeRow(this.cols);
            }
        }

        this.markAllDirty();
    };

    TerminalScreen.prototype.eraseChars = function (count) {
        var row = this.grid[this.cursorRow];
        var end = Math.min(this.cursorCol + Math.max(1, count), this.cols);

        for (var i = this.cursorCol; i < end; i++) {
            row[i] = blankCell();
        }

        this.markDirty(this.cursorRow);
    };

    // ---- Insert / delete ----

    TerminalScreen.prototype.insertLines = function (count) {
        if (this.cursorRow < this.scrollTop || this.cursorRow > this.scrollBottom) {
            return;
        }

        count = Math.max(1, count);

        for (var n = 0; n < count; n++) {
            this.grid.splice(this.scrollBottom, 1);
            this.grid.splice(this.cursorRow, 0, makeRow(this.cols));
        }

        this.markAllDirty();
    };

    TerminalScreen.prototype.deleteLines = function (count) {
        if (this.cursorRow < this.scrollTop || this.cursorRow > this.scrollBottom) {
            return;
        }

        count = Math.max(1, count);

        for (var n = 0; n < count; n++) {
            this.grid.splice(this.cursorRow, 1);
            this.grid.splice(this.scrollBottom, 0, makeRow(this.cols));
        }

        this.markAllDirty();
    };

    TerminalScreen.prototype.insertChars = function (count) {
        var row = this.grid[this.cursorRow];
        count = Math.max(1, count);

        for (var n = 0; n < count; n++) {
            row.pop();
            row.splice(this.cursorCol, 0, blankCell());
        }

        this.markDirty(this.cursorRow);
    };

    TerminalScreen.prototype.deleteChars = function (count) {
        var row = this.grid[this.cursorRow];
        count = Math.max(1, count);

        for (var n = 0; n < count; n++) {
            row.splice(this.cursorCol, 1);
            row.push(blankCell());
        }

        this.markDirty(this.cursorRow);
    };

    // ---- Alternate buffer ----

    TerminalScreen.prototype.setAlternateBuffer = function (enable) {
        if (enable) {
            if (this.alternate !== null) {
                return;
            }

            this.alternate = {
                grid: this.grid,
                cursorRow: this.cursorRow,
                cursorCol: this.cursorCol,
                attrs: copyAttrs(this.attrs),
                scrollTop: this.scrollTop,
                scrollBottom: this.scrollBottom
            };

            this.grid = [];
            for (var i = 0; i < this.rows; i++) {
                this.grid.push(makeRow(this.cols));
            }

            this.cursorRow = 0;
            this.cursorCol = 0;
            this.scrollTop = 0;
            this.scrollBottom = this.rows - 1;
            this.resetAttrs();
        } else {
            if (this.alternate === null) {
                return;
            }

            var saved = this.alternate;
            this.alternate = null;

            this.grid = saved.grid;
            // The primary buffer may have been saved at a different size if the
            // control was resized while the alternate screen was active.
            this.conformGrid();
            this.cursorRow = Math.min(saved.cursorRow, this.rows - 1);
            this.cursorCol = Math.min(saved.cursorCol, this.cols - 1);
            this.attrs = saved.attrs;
            this.attrsKey = attrKey(this.attrs);
            this.scrollTop = Math.min(saved.scrollTop, this.rows - 1);
            this.scrollBottom = Math.min(saved.scrollBottom, this.rows - 1);
        }

        this.wrapPending = false;
        this.markAllDirty();
    };

    // ---- Resize ----

    /** Brings `this.grid` in line with the current cols/rows. */
    TerminalScreen.prototype.conformGrid = function () {
        var r;
        var c;

        for (r = 0; r < this.grid.length; r++) {
            var row = this.grid[r];
            if (row.length > this.cols) {
                row.length = this.cols;
            } else {
                for (c = row.length; c < this.cols; c++) {
                    row.push(blankCell());
                }
            }
        }

        while (this.grid.length < this.rows) {
            this.grid.push(makeRow(this.cols));
        }

        if (this.grid.length > this.rows) {
            /*
                Shrinking: drop rows from the top and preserve them as history,
                which is what a real terminal does and keeps the prompt visible
                instead of scrolling it off the bottom.
            */
            var excess = this.grid.length - this.rows;
            for (var i = 0; i < excess; i++) {
                this.pushScrollback(this.grid[i]);
            }
            this.grid.splice(0, excess);
            this.cursorRow -= excess;
        }
    };

    TerminalScreen.prototype.resize = function (cols, rows) {
        cols = Math.max(1, cols);
        rows = Math.max(1, rows);

        if (cols === this.cols && rows === this.rows) {
            return false;
        }

        var previousRows = this.rows;

        this.cols = cols;
        this.rows = rows;

        this.conformGrid();

        if (this.alternate !== null) {
            // Keep the stashed primary buffer consistent so restoring it later
            // does not produce a ragged grid.
            var alt = this.alternate;
            for (var r = 0; r < alt.grid.length; r++) {
                var row = alt.grid[r];
                if (row.length > cols) {
                    row.length = cols;
                } else {
                    for (var c = row.length; c < cols; c++) {
                        row.push(blankCell());
                    }
                }
            }
        }

        this.cursorRow = Math.min(Math.max(this.cursorRow, 0), this.rows - 1);
        this.cursorCol = Math.min(Math.max(this.cursorCol, 0), this.cols - 1);

        // A resize invalidates any scroll region that referenced the old height.
        if (this.scrollBottom >= previousRows - 1 || this.scrollBottom >= this.rows) {
            this.scrollTop = 0;
            this.scrollBottom = this.rows - 1;
        }

        this.wrapPending = false;
        this.markAllDirty();

        return true;
    };

    // ---- Text extraction (used for clipboard / diagnostics) ----

    TerminalScreen.prototype.rowText = function (row) {
        var text = '';
        for (var i = 0; i < row.length; i++) {
            text += row[i].ch;
        }
        return text.replace(/\s+$/, '');
    };

    global.TerminalScreen = TerminalScreen;
    global.TerminalScreenUtil = {
        blankCell: blankCell,
        attrKey: attrKey,
        defaultAttrs: defaultAttrs
    };
})(window);
