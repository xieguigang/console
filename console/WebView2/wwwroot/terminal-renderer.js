/*
    Dirty-row DOM renderer.

    Each terminal line maps to one <div class="row">; within a row, runs of cells
    that share styling collapse into a single <span>. Only rows the screen model
    flagged as dirty are rebuilt, so a typical frame touches a handful of nodes
    rather than rows x cols of them. Repaints are coalesced onto animation
    frames, which caps the cost of a chatty producer at the display refresh rate.
*/
(function (global) {
    'use strict';

    function escapeHtml(text) {
        var out = '';
        for (var i = 0; i < text.length; i++) {
            var ch = text.charAt(i);
            if (ch === '&') {
                out += '&amp;';
            } else if (ch === '<') {
                out += '&lt;';
            } else if (ch === '>') {
                out += '&gt;';
            } else {
                out += ch;
            }
        }
        return out;
    }

    function TerminalRenderer(screen, options) {
        options = options || {};

        this.screen = screen;
        this.viewport = options.viewport;
        this.container = options.screenElement;
        this.measureElement = options.measureElement;

        this.rowElements = [];
        this.frameHandle = 0;
        this.scheduled = false;

        // Cell metrics, refreshed whenever the font changes.
        this.cellWidth = 0;
        this.cellHeight = 0;

        // Scrollback rows are rendered above the live grid; tracked separately so
        // the live rows keep stable indices in `rowElements`.
        this.renderedScrollback = 0;

        this.followTail = true;
        this.focused = false;

        this.defaultForeground = '#ffffff';
        this.defaultBackground = '#000000';

        var self = this;
        this.viewport.addEventListener('scroll', function () {
            /*
                Pause auto-scroll as soon as the user scrolls away from the
                bottom, otherwise incoming output would repeatedly yank them back
                while they read history.
            */
            var distance = self.viewport.scrollHeight - self.viewport.scrollTop - self.viewport.clientHeight;
            self.followTail = distance <= 4;
        }, { passive: true });

        this.measure();
    }

    /**
     * Recomputes the pixel size of one character cell from the live font.
     */
    TerminalRenderer.prototype.measure = function () {
        var probe = this.measureElement;
        var sample = probe.textContent.length || 1;
        var rect = probe.getBoundingClientRect();

        var width = rect.width / sample;
        var height = rect.height;

        if (width > 0 && height > 0) {
            this.cellWidth = width;
            this.cellHeight = height;
        }

        return { width: this.cellWidth, height: this.cellHeight };
    };

    /**
     * Returns how many whole cells fit in the viewport at the current metrics.
     */
    TerminalRenderer.prototype.computeGridSize = function () {
        this.measure();

        if (this.cellWidth <= 0 || this.cellHeight <= 0) {
            return { cols: 80, rows: 24 };
        }

        var style = global.getComputedStyle(this.viewport);
        var scrollbar = this.viewport.offsetWidth - this.viewport.clientWidth;
        var width = this.viewport.clientWidth -
            parseFloat(style.paddingLeft || 0) - parseFloat(style.paddingRight || 0);
        var height = this.viewport.clientHeight -
            parseFloat(style.paddingTop || 0) - parseFloat(style.paddingBottom || 0);

        if (scrollbar <= 0) {
            // No scrollbar yet, but one will appear as soon as output overflows;
            // reserving room now avoids a reflow-triggered resize storm.
            width -= 12;
        }

        return {
            cols: Math.max(1, Math.floor(width / this.cellWidth)),
            rows: Math.max(1, Math.floor(height / this.cellHeight))
        };
    };

    TerminalRenderer.prototype.applyStyle = function (style) {
        var root = document.documentElement.style;

        if (style.fontFamily) {
            root.setProperty('--term-font', style.fontFamily);
        }
        if (style.fontSize) {
            root.setProperty('--term-size', style.fontSize);
        }
        if (style.foreColor) {
            root.setProperty('--term-fg', style.foreColor);
            this.defaultForeground = style.foreColor;
        }
        if (style.backColor) {
            root.setProperty('--term-bg', style.backColor);
            this.defaultBackground = style.backColor;
        }

        this.measure();
        this.screen.markAllDirty();
        this.schedule();
    };

    TerminalRenderer.prototype.setFocused = function (focused) {
        this.focused = !!focused;
        document.body.classList.toggle('focused', this.focused);
        this.screen.markDirty(this.screen.cursorRow);
        this.schedule();
    };

    TerminalRenderer.prototype.scrollToBottom = function () {
        this.followTail = true;
        this.viewport.scrollTop = this.viewport.scrollHeight;
    };

    TerminalRenderer.prototype.schedule = function () {
        if (this.scheduled) {
            return;
        }

        this.scheduled = true;

        var self = this;
        this.frameHandle = global.requestAnimationFrame(function () {
            self.scheduled = false;
            self.render();
        });
    };

    /**
     * Builds the HTML for a single grid row, merging adjacent cells that share
     * styling into one span. `cursorCol` is -1 when the cursor is not on the row.
     */
    TerminalRenderer.prototype.buildRowHtml = function (cells, cursorCol) {
        var html = '';
        var runText = '';
        var runKey = null;
        var runAttrs = null;

        var self = this;

        function flush() {
            if (runText.length === 0) {
                return;
            }
            html += self.wrapRun(runText, runAttrs, false);
            runText = '';
        }

        for (var i = 0; i < cells.length; i++) {
            var cell = cells[i];

            if (i === cursorCol) {
                flush();
                runKey = null;
                html += this.wrapRun(cell.ch, cell.attrs, true);
                continue;
            }

            if (cell.key !== runKey) {
                flush();
                runKey = cell.key;
                runAttrs = cell.attrs;
            }

            runText += cell.ch;
        }

        flush();

        /*
            An entirely empty row still needs content, otherwise it would
            collapse to zero height and shift every following row upward.
        */
        return html.length > 0 ? html : '<span> </span>';
    };

    TerminalRenderer.prototype.wrapRun = function (text, attrs, isCursor) {
        var classes = '';
        var styles = '';

        if (attrs !== null && attrs !== undefined) {
            if (attrs.bold) {
                classes += ' b';
            }
            if (attrs.dim) {
                classes += ' d';
            }
            if (attrs.italic) {
                classes += ' i';
            }
            if (attrs.underline) {
                classes += ' u';
            }
            if (attrs.strike) {
                classes += ' s';
            }
            if (attrs.hidden) {
                classes += ' h';
            }

            var fg = attrs.fg || this.defaultForeground;
            var bg = attrs.bg || this.defaultBackground;

            if (attrs.inverse) {
                var swap = fg;
                fg = bg;
                bg = swap;
            }

            if (fg !== this.defaultForeground) {
                styles += 'color:' + fg + ';';
            }
            if (bg !== this.defaultBackground) {
                styles += 'background-color:' + bg + ';';
            }
        }

        if (isCursor) {
            classes += ' cursor blink';
        }

        var attrText = '';
        if (classes.length > 0) {
            attrText += ' class="' + classes.substring(1) + '"';
        }
        if (styles.length > 0) {
            attrText += ' style="' + styles + '"';
        }

        return '<span' + attrText + '>' + escapeHtml(text) + '</span>';
    };

    /**
     * Keeps the number of live row elements in sync with the grid height.
     */
    TerminalRenderer.prototype.syncRowElements = function () {
        var needed = this.screen.rows;

        while (this.rowElements.length < needed) {
            var div = document.createElement('div');
            div.className = 'row';
            this.container.appendChild(div);
            this.rowElements.push(div);
        }

        while (this.rowElements.length > needed) {
            var extra = this.rowElements.pop();
            if (extra.parentNode) {
                extra.parentNode.removeChild(extra);
            }
        }
    };

    /**
     * Appends any scrollback rows that have not been rendered yet, and trims the
     * DOM when history is evicted from the model.
     */
    TerminalRenderer.prototype.syncScrollback = function () {
        var history = this.screen.scrollback;

        if (history.length === this.renderedScrollback) {
            return;
        }

        if (history.length < this.renderedScrollback) {
            /*
                History shrank (cleared, or trimmed past what we rendered).
                Rebuilding is simpler and only happens on rare events.
            */
            this.container.textContent = '';
            this.rowElements.length = 0;
            this.renderedScrollback = 0;
            this.screen.markAllDirty();
        }

        var fragment = document.createDocumentFragment();

        for (var i = this.renderedScrollback; i < history.length; i++) {
            var div = document.createElement('div');
            div.className = 'row';
            div.innerHTML = this.buildRowHtml(history[i], -1);
            fragment.appendChild(div);
        }

        // Scrollback rows always precede the live grid.
        if (this.rowElements.length > 0) {
            this.container.insertBefore(fragment, this.rowElements[0]);
        } else {
            this.container.appendChild(fragment);
        }

        this.renderedScrollback = history.length;
    };

    TerminalRenderer.prototype.render = function () {
        var screen = this.screen;

        this.syncScrollback();
        this.syncRowElements();

        var cursorRow = screen.cursorVisible ? screen.cursorRow : -1;
        var cursorCol = screen.cursorVisible ? Math.min(screen.cursorCol, screen.cols - 1) : -1;

        if (screen.allDirty) {
            for (var r = 0; r < screen.rows; r++) {
                this.renderRow(r, cursorRow, cursorCol);
            }
        } else {
            // The cursor may have moved off a row that is otherwise unchanged;
            // repainting the previous row clears the stale cursor cell.
            if (this.lastCursorRow !== undefined && this.lastCursorRow !== cursorRow) {
                screen.markDirty(this.lastCursorRow);
            }
            screen.dirtyRows.add(cursorRow >= 0 ? cursorRow : 0);

            var self = this;
            screen.dirtyRows.forEach(function (index) {
                self.renderRow(index, cursorRow, cursorCol);
            });
        }

        this.lastCursorRow = cursorRow;
        screen.clearDirty();

        if (this.followTail) {
            this.viewport.scrollTop = this.viewport.scrollHeight;
        }
    };

    TerminalRenderer.prototype.renderRow = function (index, cursorRow, cursorCol) {
        if (index < 0 || index >= this.screen.rows) {
            return;
        }

        var element = this.rowElements[index];
        if (!element) {
            return;
        }

        var cells = this.screen.grid[index];
        var html = this.buildRowHtml(cells, index === cursorRow ? cursorCol : -1);

        // Comparing against the previous markup avoids re-parsing HTML for rows
        // the model flagged conservatively (e.g. after a full-screen repaint that
        // actually changed very little).
        if (element.__html !== html) {
            element.innerHTML = html;
            element.__html = html;
        }
    };

    TerminalRenderer.prototype.clear = function () {
        this.container.textContent = '';
        this.rowElements.length = 0;
        this.renderedScrollback = 0;
        this.lastCursorRow = undefined;
        this.followTail = true;
        this.screen.markAllDirty();
        this.schedule();
    };

    global.TerminalRenderer = TerminalRenderer;
})(window);
