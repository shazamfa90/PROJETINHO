// Tokens de design do Necess (ver DESIGN.md). Medidas em px logicos @96dpi; use S() para escalar.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Necess
{
    static class Theme
    {
        // Neutros quentes (3 tons base + 1 acento). Razoes de contraste conferidas contra Bg.
        public static readonly Color Bg      = Color.FromArgb(0x14, 0x13, 0x11);
        public static readonly Color Raised  = Color.FromArgb(0x1D, 0x1C, 0x19);   // hover de botao
        public static readonly Color Pressed = Color.FromArgb(0x26, 0x24, 0x20);
        public static readonly Color Line    = Color.FromArgb(0x2A, 0x28, 0x24);   // filetes
        public static readonly Color Border  = Color.FromArgb(0x71, 0x6B, 0x60);   // borda de botao, 3.5:1
        public static readonly Color Ink     = Color.FromArgb(0xEC, 0xE8, 0xDF);   // 15.2:1
        public static readonly Color Ink2    = Color.FromArgb(0xA8, 0xA2, 0x94);   // 7.3:1
        public static readonly Color Ink3    = Color.FromArgb(0x7A, 0x75, 0x68);   // 4.0:1 (indices, desabilitado)

        // O unico acento: progresso, botao principal e foco. Em mais nenhum lugar.
        public static readonly Color Accent      = Color.FromArgb(0xE8, 0xA3, 0x3D);   // 8.6:1
        public static readonly Color AccentHover = Color.FromArgb(0xF0, 0xB4, 0x55);
        public static readonly Color AccentDown  = Color.FromArgb(0xCF, 0x8E, 0x2C);
        public static readonly Color AccentInk   = Color.FromArgb(0x1B, 0x12, 0x04);   // 8.6:1 sobre Accent

        // Semanticas, so para estado.
        public static readonly Color Ok  = Color.FromArgb(0x8D, 0xBF, 0x7B);   // 8.7:1
        public static readonly Color Err = Color.FromArgb(0xE4, 0x70, 0x5F);   // 6.0:1

        // Escala de espacamento (px logicos). Apertado dentro do item, folgado entre blocos.
        public const int S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32, S7 = 40;
        public const int Radius = 3;

        public static float Scale = 1f;
        public static int S(int v) { return (int)Math.Round(v * Scale); }

        static HashSet<string> installed;

        static string Pick(params string[] names)
        {
            if (installed == null)
            {
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (FontFamily f in FontFamily.Families) installed.Add(f.Name);
            }
            foreach (string n in names) if (installed.Contains(n)) return n;
            return names[names.Length - 1];
        }

        /// <summary>Fonte da interface: Segoe UI e a nativa do Windows (Semibold para titulos/nomes).</summary>
        public static Font Ui(float px, bool strong)
        {
            string fam = strong ? Pick("Segoe UI Semibold", "Segoe UI", "DejaVu Sans")
                                : Pick("Segoe UI", "DejaVu Sans", "Arial");
            FontStyle st = strong && fam != "Segoe UI Semibold" ? FontStyle.Bold : FontStyle.Regular;
            return new Font(fam, px * Scale, st, GraphicsUnit.Pixel);
        }

        /// <summary>Monoespacada so para dados (caminhos, indices, fonte) e para o codigo do fundo.</summary>
        public static Font Mono(float px)
        {
            return new Font(Pick("Consolas", "DejaVu Sans Mono", "Courier New"), px * Scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = Math.Max(1, radius * 2);
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
