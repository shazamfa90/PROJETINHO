// Fundo animado de codigo. O texto e o proprio codigo do Necess (download, checagem MZ/PK/Rar!,
// hexdump do cabecalho PE), nao lorem ipsum. Tem funcao: quase parado em repouso e acelera
// enquanto ha download -> o movimento diz "tem trabalho acontecendo". Cada coluna e uma imagem
// pre-renderizada que so desliza (barato: 3 blits por quadro).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace Necess
{
    sealed class CodeBackdrop : IDisposable
    {
        sealed class Column { public Bitmap Strip; public int X, Shown; public double Offset; public double Mult; }

        public const double IdleSpeed = 5, BusySpeed = 38;     // px logicos por segundo
        const int Columns = 3, LineH = 18, ColW = 300, ColChars = 46;

        readonly List<Column> cols = new List<Column>();
        double speed = IdleSpeed;

        static readonly string[] Pool =
        {
            "using System.Net;",
            "var wc = new WebClient();",
            "wc.Headers[\"User-Agent\"] = \"Mozilla/5.0\";",
            "wc.DownloadFileAsync(uri, temp, job);",
            "if (e.Error != null) Fail(job, msg);",
            "byte[] head = new byte[1024];",
            "int n = fs.Read(head, 0, head.Length);",
            "if (head[0] == 0x4D && head[1] == 0x5A)",
            "    return \"exe\";           // MZ",
            "if (head[0] == 0x50 && head[1] == 0x4B)",
            "    return \"zip\";           // PK",
            "if (head[0] == 0x52 && head[1] == 0x61)",
            "    return \"rar\";           // Rar!",
            "if (head[0] == '<') return \"html\";",
            "var m = Regex.Match(html, form);",
            "string next = Drive.ConfirmUrl(html);",
            "File.Move(temp, final);",
            "Process.Start(\"explorer.exe\", args);",
            "tray.ShowBalloonTip(8000, title, msg);",
            "if (job.State == JobState.Done)",
            "    ok.Add(job);",
            "Interlocked.Increment(ref active);",
            "lock (gate) { active--; }",
            "HTTP/1.1 200 OK",
            "Content-Type: application/octet-stream",
            "Content-Length: 25635768",
            "Location: https://aka.ms/vs/17/release/",
            "Invoke-WebRequest $url -OutFile $dest",
            "if (-not (Test-Path $dir)) { mkdir $dir }",
            "Get-FileHash $dest -Algorithm SHA256",
            "sha256: e3b0c44298fc1c149afbf4c8996fb924",
            "vc_redist.x64.exe /install /quiet",
            "dxwebsetup.exe /Q",
            "winget install --id RARLab.WinRAR",
            "return Path.Combine(dir, name + \".part\");",
            "foreach (Job j in jobs) Start(j);",
            "ThreadPool.QueueUserWorkItem(Resolve);",
            "sync.Post(delegate { Begin(job, url); });",
            "catch (WebException ex) { Log(ex.Status); }",
            "job.Percent = e.ProgressPercentage;",
            "Invalidate();",
            "for (int i = 0; i < rows.Count; i++)",
            "    DrawRow(g, rows[i], i);",
            "g.FillRectangle(brush, bar);",
            "if (!Enabled) return;",
            "[STAThread]",
            "Application.Run(new MainForm());",
            "namespace Necess",
            "{",
            "}",
            "// sem closures: estado em Tag/UserState",
            "// o servidor devolveu uma pagina HTML",
            "// confere os bytes antes de renomear",
            "// 45 s sem receber nada: cancela",
        };

        // Primeiros 128 bytes de um .exe: cabecalho DOS ("MZ"), e_lfanew = 0x80 e o stub padrao.
        static readonly byte[] Dos =
        {
            0x4D,0x5A,0x90,0x00,0x03,0x00,0x00,0x00,0x04,0x00,0x00,0x00,0xFF,0xFF,0x00,0x00,
            0xB8,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
            0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x80,0x00,0x00,0x00,
            0x0E,0x1F,0xBA,0x0E,0x00,0xB4,0x09,0xCD,0x21,0xB8,0x01,0x4C,0xCD,0x21,0x54,0x68,
            0x69,0x73,0x20,0x70,0x72,0x6F,0x67,0x72,0x61,0x6D,0x20,0x63,0x61,0x6E,0x6E,0x6F,
            0x74,0x20,0x62,0x65,0x20,0x72,0x75,0x6E,0x20,0x69,0x6E,0x20,0x44,0x4F,0x53,0x20,
            0x6D,0x6F,0x64,0x65,0x2E,0x0D,0x0D,0x0A,0x24,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        };

        static readonly Regex Tok = new Regex(
            @"(?<c>//.*$)|(?<s>""[^""]*""|'[^']*')|(?<n>\b0x[0-9A-Fa-f]+\b|\b\d+\b)|" +
            @"(?<k>\b(?:using|var|new|if|else|return|foreach|for|try|catch|lock|static|public|string|byte|int|namespace|delegate|ref|not|mkdir)\b)",
            RegexOptions.Compiled);

        // cores com alfa embutido: ja saem discretas sobre o fundo
        static readonly Color CPlain = Color.FromArgb(34, Theme.Ink2);
        static readonly Color CComment = Color.FromArgb(22, Theme.Ink2);
        static readonly Color CKeyword = Color.FromArgb(46, Theme.Accent);
        static readonly Color CNumber = Color.FromArgb(36, Theme.Accent);
        static readonly Color CString = Color.FromArgb(46, Theme.Ink);

        public void Build(int clientWidth)
        {
            Dispose();
            int w = Theme.S(ColW), lh = Theme.S(LineH);
            using (Font f = Theme.Mono(11))
            {
                float cw = MeasureChar(f);
                for (int i = 0; i < Columns; i++)
                {
                    var rnd = new Random(11 + i * 7);
                    string[] lines = Compose(rnd, 96);
                    var col = new Column
                    {
                        Strip = Render(lines, w, lh, f, cw),
                        X = Columns == 1 ? 0 : i * (clientWidth - w) / (Columns - 1),
                        Mult = i == 0 ? 0.75 : i == 1 ? 1.0 : 1.3,
                        Offset = rnd.Next(0, 4000)
                    };
                    cols.Add(col);
                }
            }
        }

        static float MeasureChar(Font f)
        {
            using (var bmp = new Bitmap(8, 8))
            using (Graphics g = Graphics.FromImage(bmp))
                return g.MeasureString("MMMMMMMMMM", f, 4000, StringFormat.GenericTypographic).Width / 10f;
        }

        // Mistura trechos do codigo com blocos de hexdump do cabecalho "MZ".
        static string[] Compose(Random rnd, int count)
        {
            var pool = new List<string>(Pool);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                string t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }
            var lines = new List<string>();
            int k = 0;
            while (lines.Count < count)
            {
                for (int n = 0; n < 9 && lines.Count < count; n++) lines.Add(pool[k++ % pool.Count]);
                for (int row = 0; row * 12 < Dos.Length && lines.Count < count; row++)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append((row * 12).ToString("X4")).Append("  ");
                    for (int b = 0; b < 12 && row * 12 + b < Dos.Length; b++)
                        sb.Append(Dos[row * 12 + b].ToString("X2")).Append(b < 11 ? " " : "");
                    lines.Add(sb.ToString());
                }
            }
            return lines.ToArray();
        }

        static Bitmap Render(string[] lines, int w, int lh, Font f, float cw)
        {
            var bmp = new Bitmap(w, lh * lines.Length, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            {
                g.Clear(Color.Transparent);
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Length > ColChars ? lines[i].Substring(0, ColChars) : lines[i];
                    float y = i * lh + (lh - f.Height) / 2f;
                    bool hex = Regex.IsMatch(line, @"^[0-9A-F]{4}  ");
                    if (hex)
                    {
                        Put(g, line.Substring(0, 4), 0, y, f, cw, CComment, sf);
                        Put(g, line.Substring(4), 4, y, f, cw, CPlain, sf);
                        continue;
                    }
                    int pos = 0;
                    foreach (Match m in Tok.Matches(line))
                    {
                        if (m.Index > pos) Put(g, line.Substring(pos, m.Index - pos), pos, y, f, cw, CPlain, sf);
                        Color c = m.Groups["c"].Success ? CComment : m.Groups["s"].Success ? CString
                                : m.Groups["n"].Success ? CNumber : CKeyword;
                        Put(g, m.Value, m.Index, y, f, cw, c, sf);
                        pos = m.Index + m.Length;
                    }
                    if (pos < line.Length) Put(g, line.Substring(pos), pos, y, f, cw, CPlain, sf);
                }
            }
            return bmp;
        }

        static void Put(Graphics g, string text, int charIndex, float y, Font f, float cw, Color c, StringFormat sf)
        {
            using (var b = new SolidBrush(c)) g.DrawString(text, f, b, charIndex * cw, y, sf);
        }

        /// <summary>
        /// Avanca a animacao. busy = ha download em andamento (acelera suavemente).
        /// Retorna true se alguma coluna andou pelo menos 1 px desde o ultimo desenho: em repouso o
        /// fundo anda menos de 1 px por quadro, entao a maioria dos quadros nem precisa ser repintada.
        /// </summary>
        public bool Advance(double dt, bool busy)
        {
            double target = busy ? BusySpeed : IdleSpeed;
            speed += (target - speed) * Math.Min(1.0, dt / 0.6);
            bool moved = false;
            foreach (Column c in cols)
            {
                c.Offset += speed * c.Mult * Theme.Scale * dt;
                if ((int)(c.Offset % c.Strip.Height) != c.Shown) moved = true;
            }
            return moved;
        }

        public void Draw(Graphics g, Rectangle area)
        {
            InterpolationMode oldMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            foreach (Column c in cols)
            {
                int h = c.Strip.Height, w = c.Strip.Width;
                c.Shown = (int)(c.Offset % h);
                int y = -c.Shown;
                for (; y < area.Bottom; y += h)
                    g.DrawImage(c.Strip, new Rectangle(c.X, y, w, h), 0, 0, w, h, GraphicsUnit.Pixel);
            }
            g.InterpolationMode = oldMode;

            // bordas dissolvem no fundo: o texto da interface nunca briga com o codigo la em cima/embaixo
            int fade = Theme.S(96);
            FadeBand(g, new Rectangle(area.X, area.Y, area.Width, fade), 90f);
            FadeBand(g, new Rectangle(area.X, area.Bottom - fade, area.Width, fade), 270f);
        }

        static void FadeBand(Graphics g, Rectangle r, float angle)
        {
            using (var br = new LinearGradientBrush(r, Color.FromArgb(255, Theme.Bg), Color.FromArgb(0, Theme.Bg), angle))
                g.FillRectangle(br, r);
        }

        public void Dispose()
        {
            foreach (Column c in cols) if (c.Strip != null) c.Strip.Dispose();
            cols.Clear();
        }
    }
}
