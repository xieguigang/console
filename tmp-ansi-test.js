// Temporary verification harness for the terminal parser + screen model.
const fs = require('fs');
const path = require('path');
const vm = require('vm');

const dir = path.join(__dirname, 'console', 'WebView2', 'wwwroot');
const sandbox = { window: {}, Math, Set, Array, console };
sandbox.window.Math = Math;
vm.createContext(sandbox);

for (const f of ['ansi-parser.js', 'terminal-screen.js']) {
    vm.runInContext(fs.readFileSync(path.join(dir, f), 'utf8'), sandbox, { filename: f });
}

const { AnsiParser, TerminalScreen, AnsiPalette } = sandbox.window;

let failures = 0;
function check(name, actual, expected) {
    const a = JSON.stringify(actual);
    const e = JSON.stringify(expected);
    if (a !== e) {
        console.log(`FAIL ${name}\n  expected ${e}\n  actual   ${a}`);
        failures++;
    } else {
        console.log(`ok   ${name}`);
    }
}

function rowText(screen, r) {
    return screen.rowText(screen.grid[r]);
}

// --- palette parity with AnsiEscapeRenderer ---
check('xterm256 16 -> cube origin', AnsiPalette.xterm256(16), '#000000');
check('xterm256 231 -> cube white', AnsiPalette.xterm256(231), '#ffffff');
check('xterm256 232 -> grey 8', AnsiPalette.xterm256(232), '#080808');
check('xterm256 255 -> grey 238', AnsiPalette.xterm256(255), '#eeeeee');
check('xterm256 9 -> standard red', AnsiPalette.xterm256(9), '#ff0000');
check('xterm256 21 cube', AnsiPalette.xterm256(21), '#0000ff');

// --- basic text + CRLF ---
{
    const s = new TerminalScreen(20, 5);
    const p = new AnsiParser(s);
    p.feed('Hello\r\nWorld');
    check('plain text row0', rowText(s, 0), 'Hello');
    check('plain text row1', rowText(s, 1), 'World');
    check('cursor after write', [s.cursorRow, s.cursorCol], [1, 5]);
}

// --- SGR colours ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[31mR\x1b[0mN');
    check('sgr red fg', s.grid[0][0].attrs.fg, '#8b0000');
    check('sgr reset clears attrs', s.grid[0][1].attrs, null);
}

// --- compound reset-then-set in one SGR run (regression) ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[1m\x1b[0;31mZ');
    check('reset then set in one run', s.grid[0][0].attrs.fg, '#8b0000');
    check('reset then set drops bold', s.grid[0][0].attrs.bold, false);
}

// --- truecolor + 256 ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[38;2;10;20;30mX\x1b[38;5;196mY');
    check('truecolor fg', s.grid[0][0].attrs.fg, '#0a141e');
    check('256 fg', s.grid[0][1].attrs.fg, '#ff0000');
}

// --- split escape sequence across feeds (the SSH packetisation case) ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('A\x1b[3');       // incomplete CSI
    check('pendingTail held', p.pendingTail, '\x1b[3');
    check('nothing spurious written', rowText(s, 0), 'A');
    p.feed('1mB');           // completes -> SGR 31
    check('split sequence applied', s.grid[0][1].attrs.fg, '#8b0000');
    check('split sequence text', rowText(s, 0), 'AB');
    check('pendingTail cleared', p.pendingTail, '');
}

// --- split across ESC alone ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('X\x1b');
    check('lone ESC held', p.pendingTail, '\x1b');
    p.feed('[32mG');
    check('lone ESC resumed', s.grid[0][1].attrs.fg, '#006400');
}

// --- cursor positioning + erase ---
{
    const s = new TerminalScreen(10, 4);
    const p = new AnsiParser(s);
    p.feed('abcdefghij');
    p.feed('\x1b[1;4H');     // row 1, col 4 (1-based)
    check('CUP position', [s.cursorRow, s.cursorCol], [0, 3]);
    p.feed('\x1b[K');        // erase to end of line
    check('EL0', rowText(s, 0), 'abc');
}

// --- ED 2 clears whole screen ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('one\r\ntwo\r\nthree');
    p.feed('\x1b[2J');
    check('ED2 row0', rowText(s, 0), '');
    check('ED2 row2', rowText(s, 2), '');
}

// --- backspace / tab ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('abc\b \b');
    check('backspace erases', rowText(s, 0), 'ab');
    const s2 = new TerminalScreen(20, 3);
    new AnsiParser(s2).feed('a\tb');
    check('tab stops at 8', s2.grid[0][8].ch, 'b');
}

// --- autowrap + deferred wrap ---
{
    const s = new TerminalScreen(5, 3);
    const p = new AnsiParser(s);
    p.feed('12345');
    check('no premature wrap', [s.cursorRow, s.wrapPending], [0, true]);
    p.feed('6');
    check('wrap on next glyph', rowText(s, 1), '6');
}

// --- scroll + scrollback ---
{
    const s = new TerminalScreen(10, 2);
    const p = new AnsiParser(s);
    p.feed('l1\r\nl2\r\nl3');
    check('scrolled view row0', rowText(s, 0), 'l2');
    check('scrolled view row1', rowText(s, 1), 'l3');
    check('scrollback captured', s.rowText(s.scrollback[0]), 'l1');
}

// --- scroll region ---
{
    const s = new TerminalScreen(10, 5);
    const p = new AnsiParser(s);
    p.feed('\x1b[1;1Ha\r\n\x1b[2;1Hb\r\n\x1b[3;1Hc');
    p.feed('\x1b[2;3r');   // region rows 2..3
    check('region set', [s.scrollTop, s.scrollBottom], [1, 2]);
    check('cursor homed to region', [s.cursorRow, s.cursorCol], [1, 0]);
}

// --- insert/delete lines and chars ---
{
    const s = new TerminalScreen(6, 3);
    const p = new AnsiParser(s);
    p.feed('abcdef');
    p.feed('\x1b[1;2H\x1b[2P');   // delete 2 chars at col 2
    check('DCH', rowText(s, 0), 'adef');
    p.feed('\x1b[1;1H\x1b[2@');   // insert 2 blanks, row is 6 wide so 'f' survives
    check('ICH', rowText(s, 0), '  adef');
}

// --- alternate buffer round trip ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('primary');
    p.feed('\x1b[?1049h');
    check('alt buffer blank', rowText(s, 0), '');
    p.feed('alt');
    check('alt content', rowText(s, 0), 'alt');
    p.feed('\x1b[?1049l');
    check('primary restored', rowText(s, 0), 'primary');
    check('alt not in scrollback', s.scrollback.length, 0);
}

// --- cursor save/restore ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[2;5H\x1b7\x1b[1;1H\x1b8');
    check('DECSC/DECRC', [s.cursorRow, s.cursorCol], [1, 4]);
}

// --- OSC title is swallowed, not printed ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b]0;my title\x07done');
    check('OSC swallowed', rowText(s, 0), 'done');
}

// --- OSC split across feeds ---
{
    const s = new TerminalScreen(20, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b]0;par');
    check('OSC held', p.pendingTail.startsWith('\x1b]0;par'), true);
    p.feed('tial\x07ok');
    check('OSC resumed', rowText(s, 0), 'ok');
}

// --- DECTCEM cursor hide ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[?25l');
    check('cursor hidden', s.cursorVisible, false);
    p.feed('\x1b[?25h');
    check('cursor shown', s.cursorVisible, true);
}

// --- resize keeps content ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('hello');
    s.resize(20, 5);
    check('resize preserves text', rowText(s, 0), 'hello');
    check('resize grid dims', [s.grid.length, s.grid[0].length], [5, 20]);
}

// --- unbounded garbage does not wedge the parser ---
{
    const s = new TerminalScreen(10, 3);
    const p = new AnsiParser(s);
    p.feed('\x1b[' + '1'.repeat(5000));
    check('runaway sequence not buffered', p.pendingTail.length, 0);
}

// --- scrollback cap ---
{
    const s = new TerminalScreen(5, 1);
    s.maxScrollback = 10;
    const p = new AnsiParser(s);
    for (let i = 0; i < 50; i++) p.feed('x\r\n');
    check('scrollback capped', s.scrollback.length <= 10, true);
}

console.log(failures === 0 ? '\nALL PASS' : `\n${failures} FAILURE(S)`);
process.exit(failures === 0 ? 0 : 1);
