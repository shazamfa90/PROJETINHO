// Botao plano desenhado a mao: raio pequeno, sem sombra/glow, foco visivel, estados claros.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Necess
{
    sealed class FlatButton : Button
    {
        /// <summary>Botao principal (preenchido com o acento). Um por tela.</summary>
        public bool Primary;
        bool hot, down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            UseVisualStyleBackColor = false;
            BackColor = Theme.Bg;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { down = true; Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs pe)
        {
            Graphics g = pe.Graphics;
            g.Clear(Theme.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill, border, ink;
            if (!Enabled)
            {
                fill = Primary ? Theme.Line : Theme.Bg; border = Theme.Line; ink = Theme.Ink3;
            }
            else if (Primary)
            {
                fill = down ? Theme.AccentDown : hot ? Theme.AccentHover : Theme.Accent;
                border = fill; ink = Theme.AccentInk;
            }
            else
            {
                fill = down ? Theme.Pressed : hot ? Theme.Raised : Theme.Bg;
                border = hot || down ? Theme.Ink2 : Theme.Border;
                ink = Theme.Ink;
            }

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.RoundRect(r, Theme.S(Theme.Radius)))
            using (var b = new SolidBrush(fill))
            using (var p = new Pen(border, 1f))
            {
                g.FillPath(b, path);
                g.DrawPath(p, path);
            }

            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            if (Focused && ShowFocusCues)
            {
                int inset = Theme.S(3);
                var fr = new Rectangle(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
                using (var pen = new Pen(Primary ? Theme.AccentInk : Theme.Accent, Math.Max(1.5f, Theme.Scale * 1.5f)))
                    g.DrawRectangle(pen, fr);
            }
        }
    }
}
