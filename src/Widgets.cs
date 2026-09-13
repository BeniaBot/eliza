// Widgets.cs -- the controls the shell is built from.
//
// The shell used to be one window that painted every pixel of itself inside a
// single OnPaint, and remembered which rectangles were clickable by filling a
// list as it drew. That is the cause of nearly everything that was wrong with
// it, and the faults were not independent:
//
//   * it was slow, because moving the wheel one notch repainted the whole
//     page -- every row, every paragraph of the About text, measuring its own
//     text and building its own Font as it went
//   * scrolling stuttered, because there was no scrolling: there was a number
//     added to a coordinate and a full repaint
//   * clicks kept landing a row out, because the rectangle a click was tested
//     against had been computed during the previous paint, and anything that
//     moved between the two put them out of step
//   * there was no keyboard, no focus, no pressed state and no scrollbar,
//     because none of those exist unless something makes them
//
// So: real controls. Each row is a Control with its own bounds, its own
// hover and focus, and its own painting; Windows does the hit-testing, the
// tab order and the scrolling. Layout happens when the size changes rather
// than on every frame, and a control repaints only when something about it
// actually changed.
//
// They are still drawn by hand, because the program has a look and the stock
// controls do not have it. Drawn by hand is fine. Hit-tested by hand was not.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace ElizaApp
{
    /*  A scrolling page.

        AutoScroll gives the real thing: a scrollbar, the wheel behaving the
        way it does everywhere else on the machine, keyboard paging, and the
        smoothness that comes of Windows blitting what it already has instead
        of asking for it again.

        WS_EX_COMPOSITED double-buffers the whole tree of children at once.
        Without it a panel full of child controls tears as it scrolls, and no
        amount of double-buffering the children individually fixes it. */
    /*  A thin scrollbar, drawn rather than asked for.

        Windows' own is the one from 1995: seventeen pixels of grey with a
        raised bevel and an arrow button at each end, and it is the single
        thing that makes a window look its age fastest. It is also wrong for a
        page like this one, where it sits over the reading and takes the eye.

        This is the shape everything has used for fifteen years: a narrow
        track that is only there when the content overflows, a thumb that
        fades up when the pointer is near, and no buttons at all. */
    class Slim : Control
    {
        public ScrollBox Owner;
        public float Zoom = 1f;

        bool over, dragging;
        int grabbedAt, wasAt;

        public Slim()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Default;
        }

        int S(int v) { return (int)Math.Round(v * Zoom); }

        // Where the thumb sits, and how tall it is, for the content as it is.
        bool Measure(out int top, out int tall)
        {
            top = tall = 0;
            if (Owner == null) return false;
            int content = Owner.DisplayRectangle.Height;
            int window = Owner.ClientSize.Height;
            if (content <= window || window <= 0) return false;

            int least = S(28);
            tall = Math.Max(least, window * window / content);
            int room = window - tall;
            int scrolled = -Owner.AutoScrollPosition.Y;
            int most = content - window;
            top = most <= 0 ? 0 : room * scrolled / most;
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);

            int top, tall;
            if (!Measure(out top, out tall)) return;

            // Narrow at rest, wider under the pointer, the way a scrollbar
            // that is trying not to be in the way behaves.
            int wide = over || dragging ? S(8) : S(5);
            int x = (Width - wide) / 2;
            var thumb = new Rectangle(x, top, wide, tall);

            /*  Visible at rest, because a bar nobody can see is a page that
                looks as though it ends where the window does. */
            Color ink = dragging ? Theme.Text : over ? Theme.Muted
                      : Color.FromArgb(200, Theme.Muted);
            using (var b = new SolidBrush(ink))
            using (var path = Round(thumb, wide / 2))
                g.FillPath(b, path);
        }

        protected override void OnMouseEnter(EventArgs e) { over = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { over = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || Owner == null) return;
            int top, tall;
            if (!Measure(out top, out tall)) return;

            if (e.Y < top || e.Y > top + tall)
            {
                // Clicking the track jumps a page, which is what a track does.
                int by = e.Y < top ? -Owner.ClientSize.Height : Owner.ClientSize.Height;
                Owner.ScrollTo(-Owner.AutoScrollPosition.Y + by);
                Invalidate();
                return;
            }

            dragging = true;
            grabbedAt = e.Y;
            wasAt = -Owner.AutoScrollPosition.Y;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            /*  A drag ends when the button is up, however it got there.
                It was cleared only in OnMouseUp, so Alt+Tab in the middle of
                one -- which takes the capture away without a mouse-up -- left
                the page following a bare hover afterwards. */
            if ((Control.MouseButtons & MouseButtons.Left) == 0) dragging = false;
            if (!dragging || Owner == null) { base.OnMouseMove(e); return; }
            int top, tall;
            if (!Measure(out top, out tall)) return;

            int window = Owner.ClientSize.Height;
            int room = window - tall;
            int most = Owner.DisplayRectangle.Height - window;
            if (room <= 0 || most <= 0) return;

            Owner.ScrollTo(wasAt + (e.Y - grabbedAt) * most / room);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            dragging = false;
            base.OnMouseCaptureChanged(e);
        }

        /*  The wheel, over the bar itself.

            As a child of the panel the bar passed the wheel up to it without
            being asked. It is a sibling now -- it had to be, or the page
            disposed it on every rebuild -- and a sibling bubbles to the
            window, not to the panel. So the twelve pixels of the page a
            pointer is most likely to be resting on were the twelve where the
            wheel did nothing. */
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (Owner == null) { base.OnMouseWheel(e); return; }
            Owner.WheelBy(e.Delta);
        }

        public void Refresh2()
        {
            int top, tall;
            bool needed = Measure(out top, out tall);
            if (Visible != needed) Visible = needed;
            if (needed) Invalidate();
        }

        static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /*  A scrolling page.

        AutoScroll gives the real thing: the wheel behaving the way it does
        everywhere else on the machine, keyboard paging, and the smoothness
        that comes of Windows blitting what it already has instead of asking
        for it again. What it also gives is the 1995 scrollbar, and that is
        hidden and replaced by Slim.

        WS_EX_COMPOSITED double-buffers the whole tree of children at once.
        Without it a panel full of child controls tears as it scrolls, and no
        amount of double-buffering the children individually fixes it. */
    class ScrollBox : Panel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr window, int bar, bool show);

        const int SB_VERT = 1, SB_BOTH = 3;
        const int WM_NCCALCSIZE = 0x0083;

        public readonly Slim Bar = new Slim();

        public ScrollBox()
        {
            AutoScroll = true;
            DoubleBuffered = true;

            /*  Panel turns Selectable off in its own constructor, so a panel
                cannot take the keyboard -- and the wheel goes to whatever has
                the keyboard. Every Focus() written to make this page scroll
                was therefore a no-op, and arriving on a page and turning the
                wheel moved nothing at all. */
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.Selectable, true);
            TabStop = false;          // reachable by click, not in the tab walk

            Bar.Owner = this;
            /*  NOT a child of this panel.

                It was, and two things followed. The page rebuilds itself by
                sweeping its own Controls collection and disposing what it
                finds -- which disposed the bar, after which every call on it
                silently did nothing and the program had no scrollbar at all,
                anywhere, with no exception to say so. And a child of an
                AutoScroll panel scrolls with the content, so the bar would
                have slid off the top the moment it worked.

                It belongs to the panel's parent, floated over the panel's
                edge, and PlaceBar puts it there. */
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null && Bar.Parent != Parent)
            {
                Parent.Controls.Add(Bar);
                Bar.BringToFront();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;      // WS_EX_COMPOSITED
                return cp;
            }
        }

        /*  AutoScroll puts the native bar back on every layout, so it has to
            be taken away again at the moment Windows asks how much room the
            frame needs. Handling it here rather than after the fact is what
            stops it flickering into view. */
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && IsHandleCreated)
                ShowScrollBar(Handle, SB_BOTH, false);
            base.WndProc(ref m);
        }

        public void PlaceBar(int wide)
        {
            if (Bar.IsDisposed) return;
            Bar.Zoom = wide / 10f;

            // In the parent's coordinates, along the edge of this panel.
            Bar.Bounds = Theme.Mirrored
                ? new Rectangle(Left, Top, wide, Height)
                : new Rectangle(Right - wide, Top, wide, Height);
            Bar.BringToFront();
            Bar.Refresh2();
        }

        /*  The wheel, handed up from a child that would otherwise have
            eaten it.

            A Win32 edit control answers WM_MOUSEWHEEL itself. The reader on
            the transcript page is as tall as the whole conversation and has
            no bar of its own, so it scrolled nothing, returned, and the page
            never saw the message: a saved conversation could be moved only by
            dragging the bar. */
        public void WheelBy(int delta)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines <= 0) lines = 3;
            int step = (int)Math.Round(22 * Bar.Zoom);
            if (step < 12) step = 12;
            ScrollTo(-AutoScrollPosition.Y - delta * lines * step / 120);
        }

        /*  Raised however the page moved. Scroll alone is not enough: it comes
            from the scrollbar, and setting AutoScrollPosition in code is
            silent -- so Page Down moved the page and the chapter bar went on
            pointing at the first chapter. */
        public event EventHandler Moved;

        void Stirred()
        {
            if (Moved != null) Moved(this, EventArgs.Empty);
        }

        public void ScrollTo(int y)
        {
            int most = Math.Max(0, DisplayRectangle.Height - ClientSize.Height);
            AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(most, y)));
            Bar.Refresh2();
            Stirred();
        }

        protected override void OnScroll(ScrollEventArgs e)
        {
            base.OnScroll(e);
            Bar.Refresh2();
            Stirred();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            /*  Only if nothing inside has it. Tab onto the text-size stepper,
                turn the wheel one notch to see the rest of the page, press
                Up: the arrow moved the page instead of the setting, and the
                focus ring was gone. The wheel had already arrived here; the
                Focus call was not what made it scroll. */
            if (!ContainsFocus) Focus();
            Bar.Refresh2();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Bar.Refresh2();
        }

        // Page Up, Page Down, Home and End, which a scrolling page has.
        protected override bool IsInputKey(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.PageUp: case Keys.PageDown:
                case Keys.Home: case Keys.End:
                case Keys.Up: case Keys.Down:
                    return true;
            }
            return base.IsInputKey(key);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = ClientSize.Height - 60;
            int by = 0;
            switch (e.KeyCode)
            {
                case Keys.PageDown: by = step; break;
                case Keys.PageUp: by = -step; break;
                case Keys.Down: by = 60; break;
                case Keys.Up: by = -60; break;
                case Keys.Home: by = -DisplayRectangle.Height; break;
                case Keys.End: by = DisplayRectangle.Height; break;
                default: base.OnKeyDown(e); return;
            }
            ScrollTo(-AutoScrollPosition.Y + by);
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Clicking the empty part of a page gives it the keyboard, so the
            // wheel and the paging keys have somewhere to arrive.
            Focus();
            base.OnMouseDown(e);
        }
    }

    /*  Anything that can be pointed at.

        Hover, press and keyboard focus, in one place, so that no control has
        to remember to draw them and none of them can disagree about what
        "hot" means. */
    abstract class Widget : Control
    {
        protected bool Hot, Held;
        public float Zoom = 1f;

        protected Widget()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
        }

        /*  What a screen reader says about this control.

            A control drawn by hand is, to anything that is not looking at the
            screen, a blank rectangle. Windows will read out a role and a name
            if it is given them, and nothing at all if it is not -- so every
            control here says what it is, what it is called, and what it is
            currently set to. */
        public void Announce(string name, AccessibleRole role, string state)
        {
            AccessibleName = name;
            AccessibleRole = role;
            AccessibleDescription = state;
        }

        public int S(int v) { return (int)Math.Round(v * Zoom); }

        /*  Which part of this control the pointer is over.

            Every one of these draws its hover state by asking where the mouse
            is while it paints -- and a control only paints when something
            asks it to. Entering a segmented strip over one choice and sliding
            to the next left the first one lit and the second one dark until
            something else forced a repaint. Tracking the move is the whole
            fix, and repainting only when the answer changes is what keeps it
            from being expensive. */
        protected int Under = -1;

        protected virtual int PartAt(Point where) { return -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int now = PartAt(e.Location);
            if (now != Under) { Under = now; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Hot = true; Invalidate(); base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Hot = Held = false; Under = -1; Invalidate(); base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (ByKeyboard) { ByKeyboard = false; Invalidate(); }
            Held = true; Focus(); Invalidate(); base.OnMouseDown(e);
        }

        protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
        {
            if (!ByKeyboard) { ByKeyboard = true; Invalidate(); }
            base.OnPreviewKeyDown(e);
        }

        /*  And only the left button finishes one.

            The guard was put on the window's own hotspots and not on a single
            real control, so right-clicking a switch flipped it and
            right-clicking the red button opened the erase dialog. Every
            control inherits the check from here rather than remembering it. */
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Clicked(e);
            base.OnMouseClick(e);
        }

        protected virtual void Clicked(MouseEventArgs e) { }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            Held = false; Invalidate(); base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        // Space and Enter do what a click does. Every control on Windows
        // behaves this way and a person who works from the keyboard expects it.
        protected override bool IsInputKey(Keys key)
        {
            if (key == Keys.Left || key == Keys.Right || key == Keys.Space ||
                key == Keys.Up || key == Keys.Down) return true;
            return base.IsInputKey(key);
        }

        protected static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /*  Where the keyboard is, and only when the keyboard is driving.

            A dotted rectangle that appears the moment you click something is
            the single most dated thing an interface can do -- it is Windows
            95, and it is also wrong: the ring is there to tell somebody
            working from the keyboard where they are, and somebody who has just
            clicked a thing knows perfectly well where they are. So it follows
            the keyboard, and it is a clean rounded line rather than a dotted
            one. */
        protected static bool ByKeyboard;

        protected void FocusRing(Graphics g, Rectangle r, int radius)
        {
            if (!Focused || !ByKeyboard) return;
            /*  Inside the shape, not outside it. It inflated by three,
                and every caller passes a rectangle whose X is zero -- so the
                ring was drawn at x = -5, and a control's painting is clipped
                to its own client rectangle. The left-hand cap of every focus
                ring in the program was cut off. */
            using (var p = new Pen(Theme.Accent, S(2)))
            using (var path = Rounded(Rectangle.Inflate(r, -S(1), -S(1)),
                                      Math.Max(1, radius - S(1))))
                g.DrawPath(p, path);
        }

        protected Font Face(float points, FontStyle style)
        {
            return Fonts.Get(points, style);
        }
    }

    /*  Fonts, kept rather than made.

        A Font is a handle to a GDI object, and the old code built one for
        every line of every paragraph on every frame. They are shared now and
        live as long as the program does, which is the right lifetime for a
        thing there are fourteen distinct values of.

        The sizes asked for here are points, and they are NOT multiplied
        by the screen scale, however much that looks like an oversight
        beside S(), which is. A point is a physical size: Windows turns
        it into pixels through the screen’s own dpi, so nine points is
        already half as tall again on a screen at 150 per cent. Scaling
        it as well grew the letters as the square of the scale while
        every rectangle around them grew once -- and a rectangle too
        short for its text does not overflow, it cuts. On a machine at
        150 per cent the words under the screen read "עורר" where the
        program had written "ענבר". */
    /*  A model, on the page that is only models.

        Deliberately not the card ELIZA's own scripts sit on. They are not the
        same kind of thing and a heading over two rows of identical cards says
        they are: a model is a character, and what you are choosing is which
        room you walk into. So this is a wide row rather than a tile, with a
        round mark in the colours of that room and one object out of it, and a
        round button rather than a square one.

        And it is a real control, so the keyboard reaches it -- the opening
        screen's cards are painted rectangles with a list of hit boxes, and
        Tab has never found one of those. */
    /*  One row of the navigation strip.

        It was painted by the window, as a rectangle with a hotspot remembered
        beside it. That works for a pointer and for nothing else: to anything
        not looking at the screen the whole strip was blank, the keyboard could
        not reach it, and the only way to know that Ctrl+1 went anywhere was to
        read the source. Five real controls cost five handles and answer to
        Tab, to Space, to Narrator and to the hit test Windows already has. */
    class NavRow : Widget
    {
        public string Label = "", Hint = "";
        public bool Active, Mirror;
        public event EventHandler Pressed;

        public NavRow() { TabStop = true; }

        protected override void Clicked(MouseEventArgs e) { Fire(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            { Fire(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Fire() { if (Pressed != null) Pressed(this, EventArgs.Empty); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // The strip's own colour, because this sits on the strip.
            using (var b = new SolidBrush(Theme.Panel))
                g.FillRectangle(b, ClientRectangle);

            var row = new Rectangle(0, 0, Width - 1, Height - 1);
            if (Active || Hot || Focused)
            {
                using (var b = new SolidBrush(Theme.PanelHot))
                using (var path = Rounded(row, S(8)))
                    g.FillPath(b, path);
            }
            if (Active)
            {
                /*  The mark that says which page you are on. In high contrast
                    it is the only thing that says it, because there the fill
                    above is the window colour like everything else. */
                var tick = Mirror
                    ? new Rectangle(row.Right - S(3), row.Y + S(10), S(3), S(20))
                    : new Rectangle(row.X, row.Y + S(10), S(3), S(20));
                using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, tick);
            }

            var text = new Rectangle(row.X + S(16), row.Y, row.Width - S(32), row.Height);
            TextRenderer.DrawText(g, Label,
                Face(10f, Active ? FontStyle.Bold : FontStyle.Regular), text,
                Active ? Theme.Text : Theme.Muted,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
                (Mirror ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                        : TextFormatFlags.Left));

            /*  The shortcut, beside the thing it is a shortcut for. Ctrl+1 to
                Ctrl+5 had worked since the nav was written and the string
                "Ctrl" appeared nowhere in the program, so the only way to know
                was to read the source. */
            var hint = new Rectangle(row.X + S(16), row.Y, row.Width - S(28), row.Height);
            TextRenderer.DrawText(g, Hint, Face(8f, FontStyle.Regular), hint,
                Active ? Theme.Muted : Theme.Faint,
                TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
                (Mirror ? TextFormatFlags.Left : TextFormatFlags.Right));

            FocusRing(g, row, S(8));
        }
    }

    /*  A square button with a drawing on it instead of a word.

        Three of them, and all three are drawn here in lines rather than set
        in a font: this program carries no images and no icon font, and a
        glyph borrowed from whatever face happens to be installed is a glyph
        that looks different on the next machine.

        The arrow follows the reading. In Hebrew, back is to the right. */
    class IconWidget : Widget
    {
        public enum Mark { Back, Copy, Bin }

        public Mark Glyph;
        public bool Mirror;
        public event EventHandler Pressed;

        /*  What the copy button does when it has copied. A button that
            answers nothing leaves you pressing it twice to be sure. */
        bool done;
        readonly Timer settle = new Timer();

        public IconWidget()
        {
            TabStop = true;
            settle.Interval = 1400;
            settle.Tick += (s, e) => { settle.Stop(); done = false; Invalidate(); };
        }

        public void Confirm() { done = true; Invalidate(); settle.Stop(); settle.Start(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) settle.Dispose();
            base.Dispose(disposing);
        }

        protected override void Clicked(MouseEventArgs e) { Fire(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            { Fire(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Fire() { if (Pressed != null) Pressed(this, EventArgs.Empty); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);

            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = S(10);
            using (var b = new SolidBrush(Hot || Focused ? Theme.PanelHot : Theme.Panel))
            using (var path = Rounded(box, radius))
                g.FillPath(b, path);
            using (var p = new Pen(Hot || Focused ? Theme.Accent : Theme.Line,
                                   Hot || Focused ? 1.5f : 1f))
            using (var path = Rounded(box, radius))
                g.DrawPath(p, path);

            var ink = new Pen(done ? Theme.Accent : Theme.Text, Math.Max(1.4f, S(2) * 0.8f));
            ink.StartCap = LineCap.Round;
            ink.EndCap = LineCap.Round;
            ink.LineJoin = LineJoin.Round;
            float cx = Width / 2f, cy = Height / 2f, u = Math.Min(Width, Height) / 2f;

            using (ink)
            {
                if (done)
                {
                    // A tick, whatever the button is: it means "that happened".
                    g.DrawLines(ink, new[]
                    {
                        new PointF(cx - u * 0.42f, cy + u * 0.02f),
                        new PointF(cx - u * 0.10f, cy + u * 0.34f),
                        new PointF(cx + u * 0.46f, cy - u * 0.36f),
                    });
                }
                else if (Glyph == Mark.Back)
                {
                    float dir = Mirror ? -1f : 1f;      // in Hebrew, back points right
                    g.DrawLine(ink, cx - u * 0.46f * dir, cy, cx + u * 0.44f * dir, cy);
                    g.DrawLines(ink, new[]
                    {
                        new PointF(cx - u * 0.10f * dir, cy - u * 0.34f),
                        new PointF(cx - u * 0.46f * dir, cy),
                        new PointF(cx - u * 0.10f * dir, cy + u * 0.34f),
                    });
                }
                else if (Glyph == Mark.Copy)
                {
                    // Two sheets, one behind the other.
                    var back = new RectangleF(cx - u * 0.46f, cy - u * 0.50f,
                                              u * 0.66f, u * 0.80f);
                    var front = new RectangleF(cx - u * 0.16f, cy - u * 0.22f,
                                               u * 0.66f, u * 0.80f);
                    using (var path = RoundedF(back, u * 0.16f)) g.DrawPath(ink, path);
                    using (var b2 = new SolidBrush(Hot || Focused ? Theme.PanelHot : Theme.Panel))
                    using (var path = RoundedF(front, u * 0.16f))
                    { g.FillPath(b2, path); g.DrawPath(ink, path); }
                }
                else
                {
                    // A bin: lid, body, and the two lines down the inside.
                    g.DrawLine(ink, cx - u * 0.46f, cy - u * 0.34f, cx + u * 0.46f, cy - u * 0.34f);
                    g.DrawLine(ink, cx - u * 0.18f, cy - u * 0.52f, cx + u * 0.18f, cy - u * 0.52f);
                    g.DrawLines(ink, new[]
                    {
                        new PointF(cx - u * 0.34f, cy - u * 0.34f),
                        new PointF(cx - u * 0.26f, cy + u * 0.52f),
                        new PointF(cx + u * 0.26f, cy + u * 0.52f),
                        new PointF(cx + u * 0.34f, cy - u * 0.34f),
                    });
                    g.DrawLine(ink, cx - u * 0.08f, cy - u * 0.14f, cx - u * 0.04f, cy + u * 0.30f);
                    g.DrawLine(ink, cx + u * 0.08f, cy - u * 0.14f, cx + u * 0.04f, cy + u * 0.30f);
                }
            }

            FocusRing(g, box, radius);
        }

        static GraphicsPath RoundedF(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class ModelCard : Widget
    {
        public string Title2 = "", Note = "", Go = "";
        public bool Mirror;                 // the model's own language, not the shell's
        public Scene Scene;
        public event EventHandler Pressed;

        public ModelCard() { Height = 104; }

        protected override void Clicked(MouseEventArgs e) { Fire(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Fire(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Fire() { if (Pressed != null) Pressed(this, EventArgs.Empty); }

        TextFormatFlags Reading
        {
            get
            {
                return TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis |
                    (Mirror ? TextFormatFlags.Right | TextFormatFlags.RightToLeft
                            : TextFormatFlags.Left);
            }
        }

        /*  Where the parts sit across the card, at whatever width it is being
            given. Worked out in one place because two things need it: the
            paint, and the measurement that decides how tall the card has to
            be. When they were worked out twice they disagreed, and the second
            line of three of the six sentences was cut in half. */
        void Columns(int width, out bool tight, out int disc, out int edge,
                     out int from, out int wide)
        {
            /*  Six of these stand side by side now, so a card has to work
                narrow as well as wide. Narrow, the round button goes: the
                whole card has been the button all along -- it lights up under
                the pointer, it answers Space, and it tells a screen reader it
                is one -- and the sentence needs the room more than the word
                does. Wide, nothing changed. */
            tight = width < S(420);
            disc = tight ? S(46) : S(62);
            edge = tight ? S(14) : S(20);
            int step = tight ? S(12) : S(18);
            int far = width - 1;

            int ringLeft = Mirror ? far - edge - disc : edge;
            int buttonLeft = Mirror ? edge : far - edge - S(104);

            from = Mirror ? (tight ? edge : buttonLeft + S(104) + step)
                          : ringLeft + disc + step;
            wide = (Mirror ? ringLeft : tight ? far - edge : buttonLeft)
                   - from - (tight ? 0 : step);
            if (wide < S(60)) wide = S(60);
        }

        int TitleTop { get { return Width < S(420) ? S(14) : S(26); } }

        /*  Tall enough for the letters that hang below the line.

            It was a fixed twenty-two, and at twenty-two the bottom of a final
            nun is cut off -- so השדכן was drawn on his own card as השדכו, and
            stayed that way until somebody read it. ך, ן, ף, ץ and ק all
            descend; so do the Latin p and q, and the box has to hold them.
            Measured from the font rather than guessed, because the font can
            be changed from the settings. */
        int TitleTall
        {
            get
            {
                var font = Face(Width < S(420) ? 12f : 13f, FontStyle.Bold);
                int line = TextRenderer.MeasureText("ןקp", font).Height;
                return Math.Max(Width < S(420) ? S(22) : S(26), line + S(2));
            }
        }

        /*  How tall it has to be at that width. The sentence is the part that
            varies -- one line for some of them, two for others -- so it is
            measured. */
        public int Wants(int width)
        {
            bool tight; int disc, edge, from, wide;
            Columns(width, out tight, out disc, out edge, out from, out wide);
            int top = tight ? S(14) : S(26);
            int tall = tight ? S(22) : S(26);
            int said = TextRenderer.MeasureText(Note, Face(9.5f, FontStyle.Regular),
                new Size(wide, S(200)), Reading | TextFormatFlags.WordBreak).Height;
            int wants = top + tall - S(2) + said + edge;
            int floor = disc + edge * 2;
            return Math.Max(Math.Max(wants, floor), tight ? S(78) : S(104));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);

            var card = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = S(20);
            using (var b = new SolidBrush(Hot || Focused ? Theme.PanelHot : Theme.Panel))
            using (var path = Rounded(card, radius))
                g.FillPath(b, path);
            using (var p = new Pen(Hot || Focused ? Theme.Accent : Theme.Line,
                                   Hot || Focused ? 1.5f : 1f))
            using (var path = Rounded(card, radius))
                g.DrawPath(p, path);

            bool tight; int disc, edge, from, wide;
            Columns(Width, out tight, out disc, out edge, out from, out wide);

            // The mark: a disc in the room's own colour, with one object from
            // it drawn inside.
            var ring = new Rectangle(
                Mirror ? card.Right - edge - disc : edge,
                (Height - disc) / 2, disc, disc);
            if (Scene != null)
            {
                using (var b = new SolidBrush(Scene.Mark))
                    g.FillEllipse(b, ring);
                using (var p = new Pen(Color.FromArgb(60, Theme.Text)))
                    g.DrawEllipse(p, ring);
                var inside = new RectangleF(ring.X + disc * 0.16f, ring.Y + disc * 0.16f,
                                            disc * 0.68f, disc * 0.68f);
                var was = g.Clip;
                g.SetClip(ring);
                Scene.Emblem(g, inside, Zoom);
                g.Clip = was;
            }

            int goW = S(104), goH = S(38);
            var button = new Rectangle(
                Mirror ? edge : card.Right - edge - goW,
                (Height - goH) / 2, goW, goH);

            var name = new Rectangle(from, TitleTop, wide, TitleTall);
            TextRenderer.DrawText(g, Title2, Face(tight ? 12f : 13f, FontStyle.Bold), name,
                Theme.Text, Reading | TextFormatFlags.VerticalCenter);

            var note = new Rectangle(from, name.Bottom - S(2), wide,
                                     Math.Max(S(30), Height - name.Bottom - S(6)));
            TextRenderer.DrawText(g, Note, Face(9.5f, FontStyle.Regular), note,
                Theme.Muted, Reading | TextFormatFlags.WordBreak);

            if (!tight)
            {
                // A round button. Every other button in this program has corners.
                using (var b = new SolidBrush(Hot || Focused ? Theme.Accent : Theme.Line))
                using (var path = Rounded(button, goH / 2))
                    g.FillPath(b, path);
                TextRenderer.DrawText(g, Go, Face(9.5f, FontStyle.Bold), button,
                    Hot || Focused ? Theme.Back : Theme.Text,
                    TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    (Mirror ? TextFormatFlags.RightToLeft : 0));
            }

            FocusRing(g, card, radius);
        }
    }

    /*  A card: a rounded, bordered surface to put controls on.

        WinForms controls are rectangles, so anything that wants a rounded
        card behind it has to have one painted. */
    class CardPanel : Panel
    {
        public float Zoom = 1f;

        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back))
                g.FillRectangle(b, ClientRectangle);

            int radius = (int)Math.Round(12 * Zoom);
            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(Theme.Panel))
            using (var path = Corner(box, radius))
                g.FillPath(b, path);
            using (var p = new Pen(Theme.Line))
            using (var path = Corner(box, radius))
                g.DrawPath(p, path);
        }

        static GraphicsPath Corner(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /*  A saved conversation, laid out to be read.

        It was a borderless TextBox with the file dropped into it: one colour,
        one weight, no space between turns, the banner in the same type as the
        talking, and the echo marker still at the head of every line you had
        typed. On the screen it was a wall of text with dots down one side.

        A RichTextBox costs nothing more and gives the page the two things it
        wanted -- her turns and yours told apart, and air between them -- while
        keeping the one thing a transcript is actually for: selecting a
        sentence out of it and copying it. */
    class ReaderBox : RichTextBox
    {
        const int WM_MOUSEWHEEL = 0x020A;

        public ReaderBox()
        {
            BorderStyle = BorderStyle.None;
            ReadOnly = true;
            Multiline = true;
            WordWrap = true;
            ScrollBars = RichTextBoxScrollBars.None;
            DetectUrls = false;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                var page = Owner();
                if (page != null)
                {
                    page.WheelBy((short)((((long)m.WParam) >> 16) & 0xFFFF));
                    m.Result = IntPtr.Zero;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        ScrollBox Owner()
        {
            for (Control c = Parent; c != null; c = c.Parent)
                if (c is ScrollBox) return (ScrollBox)c;
            return null;
        }

        /*  The file, turned into a conversation again.

            The first lines, down to the first blank one, are the banner the
            program printed when the conversation opened; they are a heading,
            not talk. After that a line beginning with the echo marker is
            something the person typed and everything else is something she
            said. */
        public void Fill(string transcript, bool rtl, float zoom)
        {
            var hers = Fonts.Get(10.5f, FontStyle.Regular);
            var yours = Fonts.Get(10.5f, FontStyle.Bold);
            var banner = Fonts.Get(8.5f, FontStyle.Regular);
            var air = Fonts.Get(4f, FontStyle.Regular);

            var align = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;

            Clear();
            var lines = transcript.Replace("\u00A0", " ")
                                  .Replace("\r\n", "\n").Split('\n');
            bool heading = true;
            int wasSpeaker = -1;

            foreach (var raw in lines)
            {
                string line = raw.Trim('\uFEFF').TrimEnd();
                if (heading && line.Length == 0) { heading = false; continue; }

                bool mine = line.StartsWith("\u00B7");
                if (mine) line = line.Substring(1).TrimStart();
                int speaker = heading ? 2 : (mine ? 1 : 0);

                // A gap where the turn changes, and nowhere else.
                if (!heading && wasSpeaker >= 0 && speaker != wasSpeaker)
                {
                    SelectionAlignment = align;
                    SelectionFont = air;
                    SelectionColor = Theme.Back;
                    AppendText("\n");
                }

                SelectionAlignment = align;
                SelectionFont = heading ? banner : (mine ? yours : hers);
                SelectionColor = heading ? Theme.Faint
                                         : (mine ? Theme.Accent : Theme.Text);
                AppendText(line + "\n");
                if (line.Length > 0) wasSpeaker = speaker;
            }

            Select(0, 0);
        }
    }

    static class Fonts
    {
        static readonly Dictionary<string, Font> kept = new Dictionary<string, Font>();

        public static Font Get(float points, FontStyle style)
        {
            string key = points.ToString("0.##") + "/" + (int)style;
            Font f;
            if (kept.TryGetValue(key, out f)) return f;
            f = new Font("Segoe UI", points, style);
            kept[key] = f;
            return f;
        }
    }

    // ---- the controls themselves -------------------------------------------

    /*  Two states, and it looks like a thing with two states. */
    class SwitchWidget : Widget
    {
        public bool On;
        public bool Live = true;
        public event EventHandler Changed;

        public SwitchWidget() { Size = new Size(48, 28); }

        /*  Where the switch actually is.

            It is drawn 44 wide inside a control 60 wide and always at x = 0,
            and Clicked tested nothing -- so sixteen pixels of bare background
            worked the switch, and in a Hebrew layout that slack was on the
            reading side, where the pointer comes from. */
        Rectangle Shape
        {
            get
            {
                int h = S(24), w = S(44);
                return new Rectangle(Theme.Mirrored ? 0 : Width - w,
                                     (Height - h) / 2, w, h);
            }
        }

        protected override void Clicked(MouseEventArgs e)
        {
            if (Shape.Contains(e.Location)) Flip();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Flip(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Flip()
        {
            if (!Live) return;
            On = !On;
            AccessibleDescription = On ? OnWord : OffWord;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public string OnWord = "on", OffWord = "off";

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            int h = S(24), w = S(44);
            var track = Shape;
            Color fill = !Live ? Theme.Line : On ? Theme.Accent : Theme.PanelHot;
            using (var b = new SolidBrush(fill))
            using (var path = Rounded(track, h / 2))
                g.FillPath(b, path);
            if (!On || !Live)
                using (var p = new Pen(Hot && Live ? Theme.Muted : Theme.Line))
                using (var path = Rounded(track, h / 2))
                    g.DrawPath(p, path);

            // The knob travels towards the side the reading runs to.
            int knob = h - S(8);
            bool far = Theme.Mirrored ? !On : On;
            var dot = new Rectangle(far ? track.Right - knob - S(4) : track.X + S(4),
                                    track.Y + S(4), knob, knob);
            using (var b = new SolidBrush(!Live ? Theme.Faint
                                        : On ? (Theme.Light ? Color.White : Theme.Back)
                                             : Theme.Muted))
                g.FillEllipse(b, dot);

            FocusRing(g, track, h / 2);
        }
    }

    /*  Two to four choices, all of them in sight.

        The point is not that it is prettier than a row that cycles. It is that
        you can see the option you are not on, and reach it in one click; with
        three values a cycling row can only be gone round, never back. */
    class SegmentWidget : Widget
    {
        public string[] Options = new string[0];
        public int Chosen;
        public event EventHandler Changed;

        readonly List<Rectangle> cells = new List<Rectangle>();

        public SegmentWidget() { Height = 34; }

        protected override void Clicked(MouseEventArgs e)
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].Contains(e.Location)) { Pick(i); return; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Left ? -1 : 0;
            if (step == 0) { base.OnKeyDown(e); return; }
            // Right means "the next one" in the direction the eye is going.
            if (Theme.Mirrored) step = -step;
            Pick(Math.Max(0, Math.Min(Options.Length - 1, Chosen + step)));
            e.Handled = true;
        }

        protected override int PartAt(Point where)
        {
            for (int i = 0; i < cells.Count; i++)
                if (cells[i].Contains(where)) return i;
            return -1;
        }

        void Pick(int i)
        {
            if (i == Chosen || i < 0 || i >= Options.Length) return;
            Chosen = i;
            AccessibleDescription = Options[i];
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);
            if (Options.Length == 0) return;

            int h = S(30);
            var strip = new Rectangle(0, (Height - h) / 2, Width, h);
            using (var b = new SolidBrush(Theme.PanelHot))
            using (var path = Rounded(strip, S(7)))
                g.FillPath(b, path);

            cells.Clear();
            for (int i = 0; i < Options.Length; i++) cells.Add(Rectangle.Empty);

            /*  Each choice gets room in proportion to what it has to say.

                Equal thirds is what a segmented control usually does, and it
                is right when the labels are of a size. These are not: "בערבוב"
                and "מתחלף בין שיחות" in the same strip meant the long one
                spilled out of its own highlight and over its neighbour. */
            var widths = new int[Options.Length];
            int needed = 0;
            for (int i = 0; i < Options.Length; i++)
            {
                widths[i] = TextRenderer.MeasureText(g, Options[i],
                    Face(8.5f, FontStyle.Bold)).Width + S(18);
                needed += widths[i];
            }
            for (int i = 0; i < Options.Length; i++)
                widths[i] = Math.Max(S(40), widths[i] * strip.Width / Math.Max(1, needed));

            var mouse = PointToClient(MousePosition);
            for (int i = 0; i < Options.Length; i++)
            {
                // In a mirrored reading the first option belongs at the far end.
                int slot = Theme.Mirrored ? Options.Length - 1 - i : i;
                int left = strip.X;
                for (int k = 0; k < slot; k++)
                    left += widths[Theme.Mirrored ? Options.Length - 1 - k : k];
                int wide = slot == Options.Length - 1 ? strip.Right - left : widths[i];
                var cell = new Rectangle(left, strip.Y, wide, h);
                cells[i] = cell;

                bool here = i == Chosen;
                if (here || i == Under)
                {
                    var inner = Rectangle.Inflate(cell, -S(3), -S(3));
                    using (var b = new SolidBrush(here ? Theme.Accent : Theme.Panel))
                    using (var path = Rounded(inner, S(5)))
                        g.FillPath(b, path);
                }

                TextRenderer.DrawText(g, Options[i], Face(8.5f, here ? FontStyle.Bold : FontStyle.Regular),
                    cell, here ? (Theme.Light ? Color.White : Theme.Back) : Theme.Muted,
                    Theme.Centre | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            FocusRing(g, strip, S(7));
        }
    }

    /*  Colours, shown as colours.

        They used to be shown as words, and one of them was "לבנים" -- which
        can be read as bricks or as whites and tells you nothing whatever about
        what you are about to choose. */
    class SwatchWidget : Widget
    {
        public int Chosen;
        public event EventHandler Changed;

        readonly List<Rectangle> dots = new List<Rectangle>();

        public SwatchWidget() { Height = 40; }

        protected override void Clicked(MouseEventArgs e)
        {
            for (int i = 0; i < dots.Count; i++)
                if (dots[i].Contains(e.Location)) { Pick(i); return; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Left ? -1 : 0;
            if (step == 0) { base.OnKeyDown(e); return; }
            if (Theme.Mirrored) step = -step;
            Pick((Chosen + step + Theme.Count) % Theme.Count);
            e.Handled = true;
        }

        protected override int PartAt(Point where)
        {
            for (int i = 0; i < dots.Count; i++)
                if (dots[i].Contains(where)) return i;
            return -1;
        }

        void Pick(int i)
        {
            if (i == Chosen) return;
            Chosen = i;
            // Say.Accents exists, fully translated, and nothing read it: the
            // six dots announced a name and an empty value, so pressing Left
            // and Right through them said nothing at all.
            AccessibleDescription = Say.Accents[Math.Min(i, Say.Accents.Length - 1)];
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            int d = S(24), gap = S(12), pad = S(5);
            dots.Clear();
            for (int i = 0; i < Theme.Count; i++) dots.Add(Rectangle.Empty);

            for (int i = 0; i < Theme.Count; i++)
            {
                int slot = Theme.Mirrored ? Theme.Count - 1 - i : i;
                var dot = new Rectangle(pad + slot * (d + gap), (Height - d) / 2, d, d);
                dots[i] = Rectangle.Inflate(dot, S(5), S(5));

                using (var b = new SolidBrush(Theme.AccentAt(i)))
                    g.FillEllipse(b, dot);
                if (i == Chosen)
                    using (var p = new Pen(Theme.Text, S(2)))
                        g.DrawEllipse(p, Rectangle.Inflate(dot, S(4), S(4)));
                else if (i == Under)
                    using (var p = new Pen(Theme.Muted, S(2)))
                        g.DrawEllipse(p, Rectangle.Inflate(dot, S(4), S(4)));
            }

            if (Chosen >= 0 && Chosen < dots.Count)
                FocusRing(g, Rectangle.Inflate(dots[Chosen], S(2), S(2)), S(20));
        }
    }

    // A number with a way up and a way down, which is what a number wants.
    class StepperWidget : Widget
    {
        public int Value, Least = 8, Most = 22;
        public event EventHandler Changed;

        Rectangle less, more;

        public StepperWidget() { Size = new Size(118, 34); }

        protected override void Clicked(MouseEventArgs e)
        {
            if (less.Contains(e.Location)) Step(-1);
            else if (more.Contains(e.Location)) Step(1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right) { Step(Theme.Mirrored ? -1 : 1); e.Handled = true; }
            else if (e.KeyCode == Keys.Left) { Step(Theme.Mirrored ? 1 : -1); e.Handled = true; }
            else if (e.KeyCode == Keys.Up) { Step(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
            else base.OnKeyDown(e);
        }

        protected override int PartAt(Point where)
        {
            if (less.Contains(where)) return 0;
            if (more.Contains(where)) return 1;
            return -1;
        }

        void Step(int by)
        {
            int was = Value;
            Value = Math.Max(Least, Math.Min(Most, Value + by));
            if (Value == was) return;
            AccessibleDescription = Value.ToString();
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            int h = S(30);
            var strip = new Rectangle(0, (Height - h) / 2, S(112), h);
            using (var b = new SolidBrush(Theme.PanelHot))
            using (var path = Rounded(strip, S(7)))
                g.FillPath(b, path);

            /*  The end the number grows towards is the end the reading runs
                to. Wiring minus to the left in both languages put the plus
                under the right hand while the right arrow key subtracted. */
            int side = S(34);
            less = new Rectangle(Theme.Mirrored ? strip.Right - side : strip.X,
                                 strip.Y, side, h);
            more = new Rectangle(Theme.Mirrored ? strip.X : strip.Right - side,
                                 strip.Y, side, h);
            var middle = new Rectangle(strip.X + side, strip.Y, strip.Width - side * 2, h);

            var big = Face(11f, FontStyle.Bold);
            TextRenderer.DrawText(g, "−", big, less,
                Value > Least ? (Under == 0 ? Theme.Accent : Theme.Text) : Theme.Faint,
                Theme.Centre | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, "+", big, more,
                Value < Most ? (Under == 1 ? Theme.Accent : Theme.Text) : Theme.Faint,
                Theme.Centre | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, Value.ToString(), Face(9.5f, FontStyle.Bold), middle,
                Theme.Text, Theme.Centre | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            FocusRing(g, strip, S(7));
        }
    }

    // A button that looks like a button, and a red one when it destroys.
    class PushWidget : Widget
    {
        public string Label = "";
        public string Glyph = "";        // drawn instead of the label
        public bool Danger;
        public event EventHandler Pressed;

        public PushWidget() { Size = new Size(120, 36); }

        // Where the button is, which is not the whole of the control.
        Rectangle Shape
        {
            get
            {
                int h = S(32);
                int wide = Glyph.Length > 0 ? Math.Max(S(26), Width - S(2))
                                            : Math.Min(Width - S(2), S(120));
                return new Rectangle(Theme.Mirrored ? 0 : Width - wide,
                                     (Height - h) / 2, wide, h);
            }
        }

        protected override void Clicked(MouseEventArgs e)
        {
            if (Shape.Contains(e.Location)) Fire();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Fire(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        void Fire() { if (Pressed != null) Pressed(this, EventArgs.Empty); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, ClientRectangle);

            var box = Shape;
            Color face = Danger
                // Those are red-channel values, not alpha: hovering used to
                // brighten the fill and drop the white label to 3.77 to 1, at
                // the moment you decide whether to press it.
                ? Color.FromArgb(Held ? 170 : Hot ? 185 : 205, 74, 58)
                : (Held ? Theme.Line : Hot ? Theme.PanelHot : Theme.Panel);

            using (var b = new SolidBrush(face))
            using (var path = Rounded(box, S(7)))
                g.FillPath(b, path);
            using (var p = new Pen(Danger ? face : Theme.Line))
            using (var path = Rounded(box, S(7)))
                g.DrawPath(p, path);

            if (Glyph == "bin")
                Bin(g, box, Danger ? Color.White : (Hot ? Theme.Text : Theme.Muted));
            else
                TextRenderer.DrawText(g, Label, Face(9f, FontStyle.Bold), box,
                    Danger ? Color.White : Theme.Text,
                    Theme.Centre | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis);

            FocusRing(g, box, S(7));
        }

        /*  A waste bin: a lid with a handle on it, a body that tapers, and
            two ribs. The word "מחיקה" in a pill was the widest thing on a
            row whose actual subject is a date and a sentence, and it read as
            the row's main action rather than as the thing you do to a row you
            no longer want. */
        static void Bin(Graphics g, Rectangle box, Color ink)
        {
            float u = box.Height / 32f;
            float cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f;
            using (var p = new Pen(ink, Math.Max(1f, 1.5f * u)))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;

                float top = cy - 6 * u, bottom = cy + 9 * u;
                g.DrawLine(p, cx - 9 * u, top, cx + 9 * u, top);
                g.DrawLines(p, new[]
                {
                    new PointF(cx - 3.5f * u, top - 3 * u),
                    new PointF(cx - 3.5f * u, top),
                });
                g.DrawLine(p, cx - 3.5f * u, top - 3 * u, cx + 3.5f * u, top - 3 * u);
                g.DrawLine(p, cx + 3.5f * u, top - 3 * u, cx + 3.5f * u, top);
                g.DrawLines(p, new[]
                {
                    new PointF(cx - 7 * u, top + 2 * u),
                    new PointF(cx - 5.5f * u, bottom),
                    new PointF(cx + 5.5f * u, bottom),
                    new PointF(cx + 7 * u, top + 2 * u),
                });
                g.DrawLine(p, cx - 2f * u, top + 5 * u, cx - 2f * u, bottom - 3 * u);
                g.DrawLine(p, cx + 2f * u, top + 5 * u, cx + 2f * u, bottom - 3 * u);
            }
        }
    }

    /*  One setting: what it is, a line saying what it does, and the control.

        The row owns the control rather than the page owning both, so that the
        label, the note and the thing you click move together and cannot get
        out of step with one another. */
    class RowWidget : Control
    {
        public string Label = "", Note = "";
        public Control Widget;
        public float Zoom = 1f;
        public int ControlWidth = 200;

        /*  One width, and all three places use it.

            The width of the control was read separately when the row said how
            tall it wanted to be, when the control was placed, and when the
            label was drawn. Below about 776 pixels the label floored at S(120)
            while the control did not shrink at all, so the two overlapped --
            and the control is a real child window, which means it painted the
            label and its explanation out of existence. */
        int Fits(int width)
        {
            return Math.Min(ControlWidth, Math.Max(S(60), width - S(18) * 3 - S(120)));
        }

        bool hot;

        public RowWidget()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            /*  A plain Control is a tab stop by default, so the backing panels
                filled the tab walk with rectangles that take the focus and
                paint nothing -- seven of the fifteen stops on the first
                settings tab led nowhere at all. */
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
        }

        int S(int v) { return (int)Math.Round(v * Zoom); }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        /*  Leaving the row for the control that sits on it is not leaving.

            Windows sends WM_MOUSELEAVE to a parent the moment the pointer
            moves onto a child, and the setting's control is on top of the
            row -- which is exactly where the pointer goes just before it
            clicks. So the row went dark at the moment of use. */
        protected override void OnMouseLeave(EventArgs e)
        {
            hot = ClientRectangle.Contains(PointToClient(MousePosition));
            Invalidate();
            base.OnMouseLeave(e);
        }

        /*  How tall this row wants to be at a given width. The page asks
            before it places anything, so nothing is measured during painting. */
        public int Wants(Graphics g, int width)
        {
            int pad = S(18);
            int textW = Math.Max(S(120), width - pad * 3 - Fits(width));
            int noteH = string.IsNullOrEmpty(Note) ? 0 : TextRenderer.MeasureText(
                g, Note, Fonts.Get(9f, FontStyle.Regular),
                new Size(textW, int.MaxValue), Theme.Wrap).Height;
            return Math.Max(S(64), S(32) + noteH + S(18));
        }

        public void Place()
        {
            if (Widget == null) return;
            int pad = S(18);
            // Inset, so that the control's own rectangle cannot paint over
            // the rounded edge of the row it sits on.
            Widget.Bounds = Theme.Mirrored
                ? new Rectangle(pad, S(2), Fits(Width), Height - S(4))
                : new Rectangle(Width - pad - Fits(Width), S(2), Fits(Width), Height - S(4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var all = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(Parent == null ? Theme.Back : Theme.Back))
                g.FillRectangle(b, ClientRectangle);
            using (var b = new SolidBrush(hot ? Theme.PanelHot : Theme.Panel))
            using (var path = Round(all, S(8)))
                g.FillPath(b, path);
            if (Theme.Light)
                using (var p = new Pen(Theme.Line))
                using (var path = Round(all, S(8)))
                    g.DrawPath(p, path);

            /*  The control sits on the row and has to be the same colour as
                it, including while the row is lit. Leaving the child at the
                resting colour drew a rectangle of the wrong shade across a
                hovered row, in the shape of its own bounds. */
            if (Widget != null)
            {
                Color want = hot ? Theme.PanelHot : Theme.Panel;
                if (Widget.BackColor != want) Widget.BackColor = want;
            }

            int pad = S(18);
            int textW = Math.Max(S(120), Width - pad * 3 - Fits(Width));
            var labelBox = Theme.Mirrored
                ? new Rectangle(Width - pad - textW, S(14), textW, S(22))
                : new Rectangle(pad, S(14), textW, S(22));
            var noteBox = new Rectangle(labelBox.X, S(36), textW, Height - S(44));

            TextRenderer.DrawText(g, Label, Fonts.Get(10f, FontStyle.Regular),
                labelBox, Theme.Text, Theme.Start);
            if (!string.IsNullOrEmpty(Note))
                TextRenderer.DrawText(g, Note, Fonts.Get(9f, FontStyle.Regular),
                    noteBox, Theme.Faint, Theme.Wrap);
        }

        static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // A heading between groups of rows.
    class HeadingWidget : Control
    {
        public string Label = "";
        public float Zoom = 1f;

        public HeadingWidget()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, false);
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            TabStop = false;
            AccessibleRole = AccessibleRole.Grouping;
            Height = 40;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);
            int pad = (int)Math.Round(22 * Zoom);
            TextRenderer.DrawText(g, Label, Fonts.Get(8.5f, FontStyle.Bold),
                new Rectangle(Theme.Mirrored ? 0 : pad, (int)(14 * Zoom),
                              Width - pad, Height - (int)(16 * Zoom)),
                Theme.Faint, Theme.Start);
        }
    }

    /*  The tabs at the top of the settings.

        Not a stock TabControl: that one draws its own frame and its own
        corners and there is no polite way to stop it. This is the same
        behaviour without the frame -- and, unlike the row of rectangles it
        replaces, the keyboard can reach it. */
    class TabsWidget : Widget
    {
        public string[] Labels = new string[0];
        public int Chosen;
        public bool Wraps;             // the chapter bar has eight of them
        public bool PickedByKey;       // whether the last change came from a key
        public event EventHandler Changed;

        readonly List<Rectangle> boxes = new List<Rectangle>();
        int laidOutAt = -1;
        float fitPoints = 10f;          // the face the labels actually came out at
        int fitPad = 30;

        public TabsWidget() { Height = 44; }

        /*  Work out where each tab goes, and say how tall that makes the strip.

            Eight chapter names at a comfortable padding are wider than the
            window opens at, so the strip put the last two on a line of their
            own: six and two, which reads as something that went wrong rather
            than as a row of chapters. Nothing had tried to make them fit
            first, and nothing had thought about where to break if they would
            not.

            Both now. The comfortable spacing, then a tighter one, then a
            smaller face -- the first that fits on one line wins. Only if none
            of them does it wrap, and then it wraps evenly: four and four, not
            six and two. */
        public int Relayout()
        {
            boxes.Clear();
            if (Labels.Length == 0 || Width < S(80)) return S(46);

            int lineH = S(40), gap = S(2);
            var faces = new float[] { 10f, 10f, 9.5f, 9f };
            var pads = new int[] { 30, 20, 14, 10 };

            using (var g = CreateGraphics())
            {
                int[] wide = null;
                int rows = 1;
                for (int attempt = 0; attempt < faces.Length; attempt++)
                {
                    fitPoints = faces[attempt];
                    fitPad = pads[attempt];
                    wide = Measure(g);
                    rows = RowsFor(wide, gap);
                    if (rows <= 1) break;
                }

                if (!Wraps) rows = 1;
                // An even split of eight over two lines is four, and four may
                // be wider than the line that held six. Give it another line
                // until the split it is about to use actually fits.
                while (rows < Labels.Length && !EvenFits(wide, gap, rows)) rows++;

                int perRow = (Labels.Length + rows - 1) / rows;
                int cx = Theme.Mirrored ? Width : 0, line = 0, onLine = 0;
                foreach (var w in wide)
                {
                    if (onLine == perRow && line + 1 < rows)
                    {
                        line++;
                        onLine = 0;
                        cx = Theme.Mirrored ? Width : 0;
                    }
                    boxes.Add(new Rectangle(Theme.Mirrored ? cx - w : cx,
                                            line * lineH, w, lineH - S(2)));
                    cx = Theme.Mirrored ? cx - w - gap : cx + w + gap;
                    onLine++;
                }

                laidOutAt = Width;
                return (line + 1) * lineH + S(6);
            }
        }

        int[] Measure(Graphics g)
        {
            var wide = new int[Labels.Length];
            var f = Face(fitPoints, FontStyle.Bold);        // the widest it can be
            for (int i = 0; i < Labels.Length; i++)
                wide[i] = TextRenderer.MeasureText(g, Labels[i], f).Width + S(fitPad);
            return wide;
        }

        int RowsFor(int[] wide, int gap)
        {
            int rows = 1, run = 0;
            foreach (var w in wide)
            {
                int next = run + w + (run > 0 ? gap : 0);
                if (next > Width && run > 0) { rows++; run = w; }
                else run = next;
            }
            return rows;
        }

        bool EvenFits(int[] wide, int gap, int rows)
        {
            int perRow = (wide.Length + rows - 1) / rows;
            for (int start = 0; start < wide.Length; start += perRow)
            {
                int run = 0;
                for (int i = start; i < Math.Min(wide.Length, start + perRow); i++)
                    run += wide[i] + (i > start ? gap : 0);
                if (run > Width) return false;
            }
            return true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width != laidOutAt) { Relayout(); Invalidate(); }
        }

        protected override void Clicked(MouseEventArgs e)
        {
            PickedByKey = false;
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Contains(e.Location)) { Pick(i); return; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Left ? -1 : 0;
            if (step == 0) { base.OnKeyDown(e); return; }
            if (Theme.Mirrored) step = -step;
            /*  So that whoever acts on the change knows not to take the
                keyboard away from the strip the arrow key is walking. */
            PickedByKey = true;
            Pick((Chosen + step + Labels.Length) % Labels.Length);
            e.Handled = true;
        }

        protected override int PartAt(Point where)
        {
            for (int i = 0; i < boxes.Count; i++)
                if (boxes[i].Contains(where)) return i;
            return -1;
        }

        void Pick(int i)
        {
            if (i == Chosen || i < 0 || i >= Labels.Length) return;
            Chosen = i;
            AccessibleDescription = Labels[i];
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);
            if (boxes.Count != Labels.Length) Relayout();

            for (int i = 0; i < boxes.Count; i++)
            {
                bool here = i == Chosen;
                var box = boxes[i];

                // What the pointer is on, before it is clicked.
                if (i == Under && !here)
                    using (var b = new SolidBrush(Theme.Line))
                    using (var path = Rounded(Rectangle.Inflate(box, -S(3), -S(6)), S(8)))
                        g.FillPath(b, path);

                TextRenderer.DrawText(g, Labels[i],
                    Face(fitPoints, here ? FontStyle.Bold : FontStyle.Regular), box,
                    here || i == Under ? Theme.Text : Theme.Muted,
                    Theme.Centre | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                if (here)
                {
                    int inset = Math.Max(S(8), S(fitPad) / 2 - S(2));
                    var bar = new Rectangle(box.X + inset, box.Bottom - S(3),
                                            Math.Max(S(12), box.Width - inset * 2), S(3));
                    using (var b = new SolidBrush(Theme.Accent))
                    using (var path = Rounded(bar, S(2)))
                        g.FillPath(b, path);
                }
            }

            // The rule sits under the last line of tabs, wherever that is.
            int bottom = 0;
            foreach (var b2 in boxes) bottom = Math.Max(bottom, b2.Bottom);
            using (var p = new Pen(Theme.Line))
                g.DrawLine(p, 0, bottom, Width, bottom);

            if (Chosen >= 0 && Chosen < boxes.Count)
                FocusRing(g, Rectangle.Inflate(boxes[Chosen], -S(5), -S(5)), S(6));
        }
    }

    /*  A piece of writing, laid out once and painted in pieces.

        The About text is one document of eight chapters, and the old page
        measured and drew all of it on every scroll step -- a hundred and
        twenty blocks, building a Font apiece, for the eight that were on the
        screen. Here the measuring happens when the width changes, the result
        is kept, and painting touches only what the clip rectangle asks for.

        It lives inside a ScrollBox, so scrolling is Windows scrolling: a real
        scrollbar, the wheel behaving the way it does in every other program,
        Page Up and Page Down, and no repaint of anything that merely moved. */
    class ArticleWidget : Control
    {
        class Piece
        {
            public string Kind;          // chapter, heading, quote, link, text
            public string Text, Note, Url;
            public Rectangle Box;
            public int Chapter;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly List<int> chapterTops = new List<int>();
        public float Zoom = 1f;
        int laidOutAt = -1;
        Piece under;

        public event Action<string> LinkPressed;

        public ArticleWidget()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            /*  Reachable from the keyboard: forty-odd links that could only
                be clicked was the largest keyboard dead end in the program. */
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        int chosen = -1;                 // which link the keyboard is on

        protected override bool IsInputKey(Keys key)
        {
            /*  Not Tab. Claiming it stops ProcessDialogKey from running,
                and the keyboard could then neither get past this control nor
                come back out of it -- a trap put here by the change that was
                meant to remove one. */
            switch (key & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.Space: return true;
            }
            return base.IsInputKey(key);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var links = new List<Piece>();
            foreach (var p in pieces) if (p.Kind == "link") links.Add(p);
            if (links.Count == 0) { base.OnKeyDown(e); return; }

            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                int step = e.KeyCode == Keys.Down ? 1 : -1;
                chosen = chosen < 0 ? (step > 0 ? 0 : links.Count - 1)
                                    : (chosen + step + links.Count) % links.Count;
                under = links[chosen];
                if (Parent is ScrollBox)
                    ((ScrollBox)Parent).ScrollTo(Top + under.Box.Y - S(60));
                Invalidate();
                e.Handled = true;
                return;
            }

            if ((e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) &&
                chosen >= 0 && chosen < links.Count)
            {
                if (LinkPressed != null) LinkPressed(links[chosen].Url);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        int S(int v) { return (int)Math.Round(v * Zoom); }

        public int ChapterTop(int i)
        {
            return i >= 0 && i < chapterTops.Count ? chapterTops[i] : 0;
        }

        public int ChapterAt(int y)
        {
            int found = 0;
            for (int i = 0; i < chapterTops.Count; i++)
                if (chapterTops[i] <= y + S(40)) found = i;
            return found;
        }

        /*  The whole of the About text is painted glyphs too, and the control
            was as silent as the conversation. */
        void Announce()
        {
            AccessibleRole = AccessibleRole.Document;
            var sb = new StringBuilder();
            foreach (var p in pieces)
            {
                if (p.Kind == "link") sb.AppendLine(p.Text + " -- " + p.Note);
                else sb.AppendLine(p.Text);
            }
            AccessibleDescription = sb.ToString();
        }

        /*  Measure everything and remember where it goes. Called when the
            width changes and at no other time, which is the whole point. */
        public void Build(string[] chapters, Func<int, IEnumerable<string>> body)
        {
            if (Width == laidOutAt || Width < S(240)) return;
            laidOutAt = Width;
            pieces.Clear();
            chapterTops.Clear();

            int w = Width - S(6);
            using (var g = CreateGraphics())
            {
                int y = 0;
                for (int c = 0; c < chapters.Length; c++)
                {
                    chapterTops.Add(y);
                    y = Put(g, "chapter", chapters[c], null, null, c, y, w);
                    foreach (var raw in body(c))
                    {
                        if (raw.StartsWith("# "))
                            y = Put(g, "heading", raw.Substring(2), null, null, c, y, w);
                        else if (raw.StartsWith("= "))
                            y = Put(g, "lead", raw.Substring(2), null, null, c, y, w);
                        else if (raw.StartsWith("> "))
                            y = Put(g, "quote", raw.Substring(2), null, null, c, y, w);
                        else if (raw.StartsWith("@"))
                        {
                            var bits = raw.Substring(1).Split(PIPE);
                            if (bits.Length >= 3)
                                y = Put(g, "link", bits[1], bits[2], bits[0], c, y, w);
                        }
                        else
                            y = Put(g, "text", raw, null, null, c, y, w);
                    }
                    y += S(16);
                }
                Height = y + S(40);
            }
            Announce();
        }

        static readonly char[] PIPE = new char[] { (char)124 };

        int Put(Graphics g, string kind, string text, string note, string url,
                int chapter, int y, int w)
        {
            var p = new Piece { Kind = kind, Text = text, Note = note, Url = url,
                                Chapter = chapter };
            int h;
            switch (kind)
            {
                case "chapter":
                    y += chapter > 0 ? S(34) : S(6);
                    h = TextRenderer.MeasureText(g, text, Fonts.Get(16f, FontStyle.Bold),
                        new Size(w, int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, h);
                    y += h + S(12);
                    break;
                case "heading":
                    y += S(14);
                    h = TextRenderer.MeasureText(g, text, Fonts.Get(11.5f, FontStyle.Bold),
                        new Size(w, int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, h);
                    y += h + S(10);
                    break;
                /*  The sentence under the chapter's name that says, in
                    words a person who came here by accident can follow, what
                    the chapter is about. Every chapter has one, and a reader
                    who stops after it has still learned the thing. */
                case "lead":
                    h = TextRenderer.MeasureText(g, text, Fonts.Get(11.2f, FontStyle.Regular),
                        new Size(w, int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, h);
                    y += h + S(18);
                    break;
                case "quote":
                    h = TextRenderer.MeasureText(g, text, Fonts.Get(12f, FontStyle.Bold),
                        new Size(w - S(26), int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, h + S(14));
                    y += h + S(32);
                    break;
                case "link":
                    h = TextRenderer.MeasureText(g, note, Fonts.Get(8.5f, FontStyle.Regular),
                        new Size(w - S(34), int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, S(32) + h + S(16));
                    y += p.Box.Height + S(10);
                    break;
                default:
                    h = TextRenderer.MeasureText(g, text, Fonts.Get(9.8f, FontStyle.Regular),
                        new Size(w, int.MaxValue), Theme.Wrap).Height;
                    p.Box = new Rectangle(S(3), y, w, h);
                    y += h + S(14);
                    break;
            }
            pieces.Add(p);
            return y;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Piece was = under;
            under = null;
            foreach (var p in pieces)
                if (p.Kind == "link" && p.Box.Contains(e.Location)) { under = p; break; }
            if (under != was)
            {
                Cursor = under != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (under != null) { under = null; Cursor = Cursors.Default; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            foreach (var p in pieces)
                if (p.Kind == "link" && p.Box.Contains(e.Location))
                {
                    if (LinkPressed != null) LinkPressed(p.Url);
                    return;
                }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, e.ClipRectangle);

            var seen = e.ClipRectangle;
            foreach (var p in pieces)
            {
                if (p.Box.Bottom < seen.Top - S(20) || p.Box.Top > seen.Bottom + S(20))
                    continue;

                switch (p.Kind)
                {
                    case "chapter":
                        if (p.Chapter > 0)
                            using (var pen = new Pen(Theme.Line))
                                g.DrawLine(pen, p.Box.X, p.Box.Y - S(18),
                                           p.Box.Right, p.Box.Y - S(18));
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(16f, FontStyle.Bold),
                            p.Box, Theme.Text, Theme.Wrap);
                        break;

                    case "heading":
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(11.5f, FontStyle.Bold),
                            p.Box, Theme.Accent, Theme.Wrap);
                        break;

                    case "quote":
                        var bar = Theme.Mirrored
                            ? new Rectangle(p.Box.Right - S(3), p.Box.Y, S(3), p.Box.Height)
                            : new Rectangle(p.Box.X, p.Box.Y, S(3), p.Box.Height);
                        using (var b = new SolidBrush(Theme.Accent)) g.FillRectangle(b, bar);
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(12f, FontStyle.Bold),
                            new Rectangle(Theme.Mirrored ? p.Box.X : p.Box.X + S(24),
                                          p.Box.Y + S(6), p.Box.Width - S(26),
                                          p.Box.Height - S(8)),
                            Theme.Text, Theme.Wrap);
                        break;

                    case "lead":
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(11.2f, FontStyle.Regular),
                            p.Box, Theme.Text, Theme.Wrap);
                        break;

                    case "link":
                        bool hot = p == under;
                        using (var b = new SolidBrush(hot ? Theme.PanelHot : Theme.Panel))
                        using (var path = Round(p.Box, S(8)))
                            g.FillPath(b, path);
                        using (var pen = new Pen(hot ? Theme.Accent : Theme.Line))
                        using (var path = Round(p.Box, S(8)))
                            g.DrawPath(pen, path);
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(9.5f, FontStyle.Bold),
                            new Rectangle(p.Box.X + S(16), p.Box.Y + S(11),
                                          p.Box.Width - S(32), S(24)),
                            Theme.Accent, Theme.Start);
                        TextRenderer.DrawText(g, p.Note, Fonts.Get(8.5f, FontStyle.Regular),
                            new Rectangle(p.Box.X + S(16), p.Box.Y + S(34),
                                          p.Box.Width - S(32), p.Box.Height - S(42)),
                            Theme.Faint, Theme.Wrap);
                        break;

                    default:
                        TextRenderer.DrawText(g, p.Text, Fonts.Get(9.8f, FontStyle.Regular),
                            p.Box, Theme.Muted, Theme.Wrap);
                        break;
                }
            }
        }

        static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /*  One saved conversation: when it was, a way to read it, and a way to
        throw it away.

        The bin was missing entirely -- transcripts could be made and never
        removed, which leaves a person with a folder they have to go and find
        in Explorer to tidy. Deleting asks first, because deleting a
        conversation somebody had is not a thing to do on one stray click. */
    class TranscriptRow : Control
    {
        public string Title = "", Path = "", Note = "";
        public float Zoom = 1f;
        public event Action<string> Open;
        public event Action<string> Delete;

        readonly PushWidget bin = new PushWidget();
        bool hot;

        public TranscriptRow()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            /*  Reachable from the keyboard. It was not selectable and the
                only tab stop on the row was its Delete button, so a saved
                conversation could be thrown away without a mouse but not
                opened. */
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
            bin.Pressed += (s, e) => { if (Delete != null) Delete(Path); };
            Controls.Add(bin);

            /*  The row itself opens it. A button that says "open" beside a
                thing whose only purpose is to be opened is a button that
                should not be there; a list of saved conversations opens the
                one you click, the way every list does. */
            Cursor = Cursors.Hand;

            GotFocus += (s, e) => { hot = true; Invalidate(); };
            LostFocus += (s, e) => { hot = false; Invalidate(); };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Space) return;
                if (Open != null) Open(Path);
                e.Handled = true;
            };
        }

        /*  Enter as well as Space.

            Enter is a dialog key: the form takes it before any control sees
            it, so the row's own KeyDown -- written to open the conversation --
            was never reached, and a row could still be deleted from the
            keyboard but not opened. Space worked because Space was already
            claimed here and Enter was not. */
        protected override bool IsInputKey(Keys key)
        {
            switch (key & Keys.KeyCode)
            {
                case Keys.Space: case Keys.Enter: return true;
            }
            return base.IsInputKey(key);
        }

        /*  The left button only. Control.Click fires for all three, so a
            right-click meant to reach a context menu opened the conversation
            instead -- the same fault the rest of the program was gone over
            for. */
        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Open != null) Open(Path);
            base.OnMouseClick(e);
        }

        public void Dress(string binLabel)
        {
            bin.Label = binLabel;
            bin.Glyph = "bin";
            bin.Announce(binLabel, AccessibleRole.PushButton, Title);
            bin.Zoom = Zoom;
            bin.BackColor = Theme.Panel;
            int w = (int)Math.Round(44 * Zoom);
            int pad = (int)Math.Round(12 * Zoom);
            bin.Bounds = Theme.Mirrored
                ? new Rectangle(pad, 0, w, Height)
                : new Rectangle(Width - pad - w, 0, w, Height);
            AccessibleRole = AccessibleRole.ListItem;
            AccessibleName = Title;
            AccessibleDescription = Note;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Theme.Back)) g.FillRectangle(b, ClientRectangle);

            int r = (int)Math.Round(8 * Zoom);
            var all = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var b = new SolidBrush(hot ? Theme.PanelHot : Theme.Panel))
            using (var path = Round(all, r))
                g.FillPath(b, path);
            if (Theme.Light)
                using (var p = new Pen(Theme.Line))
                using (var path = Round(all, r))
                    g.DrawPath(p, path);

            bin.BackColor = hot ? Theme.PanelHot : Theme.Panel;

            /*  The date, and a line out of the conversation under it.

                Four saved conversations from the same afternoon were four
                rows carrying nothing but a timestamp, and no way to tell
                which was which except by opening all four. */
            int pad = (int)Math.Round(18 * Zoom);
            int room = (int)Math.Round(80 * Zoom);
            int wide = Width - room - pad;
            int x = Theme.Mirrored ? room : pad;
            bool two = Note.Length > 0;

            var line1 = new Rectangle(x, two ? (int)Math.Round(9 * Zoom) : 0,
                                      wide, two ? (int)Math.Round(24 * Zoom) : Height);
            TextRenderer.DrawText(g, Title,
                Fonts.Get(10f, two ? FontStyle.Bold : FontStyle.Regular),
                line1, Theme.Text,
                Theme.Start | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (two)
                TextRenderer.DrawText(g, Note, Fonts.Get(9f, FontStyle.Regular),
                    new Rectangle(x, line1.Bottom - (int)Math.Round(2 * Zoom),
                                  wide, (int)Math.Round(22 * Zoom)),
                    Theme.Muted,
                    Theme.Start | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        static GraphicsPath Round(Rectangle r, int radius)
        {
            int d = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            if (r.Width < d || r.Height < d) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
