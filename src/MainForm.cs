// Janela principal. Tudo que e texto/filete/progresso e desenhado aqui (sobre o fundo animado);
// so os botoes sao controles reais, para ter foco por teclado, leitor de tela e estados nativos.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Necess
{
    internal sealed class Row
    {
        public Job Job;
        public FlatButton Button;
    }

    internal sealed class MainForm : Form
    {
        // ---- medidas logicas (px @96dpi). Escala de espacamento: 4/8/12/16/24/32/40 ----
        const int PadX = Theme.S7, PadTop = Theme.S6, HeaderH = 56, GapHeader = Theme.S6;
        const int GapList = Theme.S5, BtnH = 36, PadBottom = Theme.S6, ContentW = 840, ClientW = ContentW + PadX * 2;
        const int ColIdx = 0, ColMain = 44, MainW = 396, ColSource = 460, SourceW = 100, ColStatus = 568, StatusW = 150;
        const int BtnW = 112, ChromeFixed = PadTop + HeaderH + GapHeader + GapList + BtnH + PadBottom;   // 212

        readonly Downloader dl;
        readonly string dlDir;
        internal readonly List<Row> Rows = new List<Row>();
        internal readonly Job ZipJob = new Job(Catalog.Zip);
        readonly FlatButton btnFolder, btnZip, btnAll;
        readonly NotifyIcon tray;
        readonly CodeBackdrop backdrop = new CodeBackdrop();
        readonly Timer anim = new Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly bool reduceMotion;
        double lastT;
        string lastFile;
        Bitmap logo;
        Font fTitle, fTag, fName, fDesc, fMono, fStatus, fBtn;
        int rowH, listTop, footerTop;

        public MainForm(string downloadDir, float scaleOverride)
        {
            dlDir = downloadDir;
            Theme.Scale = scaleOverride > 0 ? scaleOverride : DetectScale();
            AutoScaleMode = AutoScaleMode.None;
            Text = "Necess";
            BackColor = Theme.Bg;
            ForeColor = Theme.Ink;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            reduceMotion = SystemReducesMotion();

            LoadAssets();
            fTitle = Theme.Ui(26, true);
            fTag = Theme.Ui(13, false);
            fName = Theme.Ui(14, true);
            fDesc = Theme.Ui(12, false);
            fStatus = Theme.Ui(12, false);
            fBtn = Theme.Ui(12, true);
            fMono = Theme.Mono(11);

            // linhas mais baixas so quando a tela nao comporta (ex.: 1366x768 a 125%)
            int n = Catalog.Items.Length;
            int availLogical = (int)(Screen.PrimaryScreen.WorkingArea.Height / Theme.Scale) - 31 - 12;
            int rowLogical = Math.Max(56, Math.Min(68, (availLogical - ChromeFixed) / n));
            rowH = Theme.S(rowLogical);
            listTop = Theme.S(PadTop + HeaderH + GapHeader);
            footerTop = listTop + n * rowH + Theme.S(GapList);
            ClientSize = new Size(Theme.S(ClientW), footerTop + Theme.S(BtnH + PadBottom));

            // ---- controles (ordem de criacao = ordem do Tab: topo, linhas, rodape) ----
            btnFolder = MakeButton("Abrir pasta", false, "Abrir a pasta de downloads");
            btnFolder.Click += delegate { OpenInFolder(null); };
            foreach (ItemDef d in Catalog.Items)
            {
                var row = new Row { Job = new Job(d), Button = MakeButton("", false, d.Name) };
                row.Button.Click += delegate { OnRowClick(row); };
                Rows.Add(row);
            }
            btnZip = MakeButton("Baixar Necess.zip", false, "Baixar o pacote Necess.zip");
            btnZip.Width = Theme.S(176);
            btnZip.Click += delegate { OnZipClick(); };
            btnAll = MakeButton("Baixar tudo", true, "Baixar todos os itens");
            btnAll.Click += delegate { dl.StartAll(JobsOf(Rows)); };
            PositionControls();

            dl = new Downloader(dlDir);
            dl.JobChanged += delegate { SyncUi(); };
            dl.BatchFinished += OnBatchFinished;

            try
            {
                tray = new NotifyIcon { Icon = Icon, Text = "Necess", Visible = true };
                tray.BalloonTipClicked += delegate { OpenInFolder(lastFile); };
            }
            catch (Exception ex) { Log.Write("tray: " + ex.Message); }

            backdrop.Build(ClientSize.Width);
            anim.Interval = 40;
            anim.Tick += OnTick;
            SyncUi();
        }

        // ---------------------------------------------------------------- montagem
        FlatButton MakeButton(string text, bool primary, string accessibleName)
        {
            var b = new FlatButton
            {
                Text = text, Primary = primary, Font = fBtn,
                Size = new Size(Theme.S(BtnW), Theme.S(BtnH)),
                AccessibleName = accessibleName
            };
            Controls.Add(b);
            return b;
        }

        void PositionControls()
        {
            int px = Theme.S(PadX), right = px + Theme.S(ContentW);
            btnFolder.Location = new Point(right - btnFolder.Width, Theme.S(PadTop + (HeaderH - BtnH) / 2));
            for (int i = 0; i < Rows.Count; i++)
            {
                Button b = Rows[i].Button;
                b.Location = new Point(right - b.Width, listTop + i * rowH + (rowH - b.Height) / 2);
            }
            btnZip.Location = new Point(px, footerTop);
            btnAll.Width = Theme.S(132);
            btnAll.Location = new Point(right - btnAll.Width, footerTop);
        }

        void LoadAssets()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            try
            {
                using (Stream s = asm.GetManifestResourceStream("Necess.png"))
                    if (s != null) using (Image tmp = Image.FromStream(s)) logo = new Bitmap(tmp);
            }
            catch (Exception ex) { Log.Write("logo: " + ex.Message); }

            try
            {
                using (Stream s = asm.GetManifestResourceStream("Necess.ico")) if (s != null) Icon = new Icon(s);
            }
            catch (Exception ex)
            {
                // parser de .ico sem suporte a PNG (ex.: Mono): monta o icone a partir do logo
                Log.Write("icone: " + ex.Message);
                try { if (logo != null) using (var small = new Bitmap(logo, 32, 32)) Icon = Icon.FromHandle(small.GetHicon()); }
                catch { }
            }
        }

        static float DetectScale()
        {
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); }
            catch { return 1f; }
        }

        static IEnumerable<Job> JobsOf(List<Row> rows) { foreach (Row r in rows) yield return r.Job; }

        // ---------------------------------------------------------------- estado -> controles
        internal void SyncUi()
        {
            int done = 0, total = 0;
            bool anyBusy = false;
            foreach (Row r in Rows)
            {
                Job j = r.Job; Button b = r.Button;
                if (j.Def.Kind == ItemKind.Page) { b.Text = "Abrir página"; b.Enabled = true; b.AccessibleName = "Abrir página de " + j.Def.Name; continue; }
                total++;
                switch (j.State)
                {
                    case JobState.Preparing:
                    case JobState.Downloading: anyBusy = true; b.Text = "Baixando…"; b.Enabled = false; break;
                    case JobState.Done: done++; b.Text = "Abrir pasta"; b.Enabled = true; b.AccessibleName = "Mostrar " + j.Def.Name + " na pasta"; break;
                    case JobState.Failed: b.Text = "Tentar de novo"; b.Enabled = true; b.AccessibleName = "Tentar baixar " + j.Def.Name + " de novo"; break;
                    default: b.Text = "Baixar"; b.Enabled = true; b.AccessibleName = "Baixar " + j.Def.Name; break;
                }
            }

            switch (ZipJob.State)
            {
                case JobState.Preparing:
                case JobState.Downloading: btnZip.Text = "Baixando…"; btnZip.Enabled = false; break;
                case JobState.Done: btnZip.Text = "Mostrar o zip"; btnZip.Enabled = true; break;
                case JobState.Failed: btnZip.Text = "Tentar de novo"; btnZip.Enabled = true; break;
                default: btnZip.Text = "Baixar Necess.zip"; btnZip.Enabled = true; break;
            }

            if (anyBusy) { btnAll.Text = "Baixando…"; btnAll.Enabled = false; }
            else if (total > 0 && done == total) { btnAll.Text = "Tudo baixado"; btnAll.Enabled = false; }
            else { btnAll.Text = "Baixar tudo"; btnAll.Enabled = true; }

            Invalidate();
        }

        bool AnyBusy()
        {
            foreach (Row r in Rows) if (r.Job.Busy) return true;
            return ZipJob.Busy;
        }

        void OnRowClick(Row row)
        {
            Job j = row.Job;
            if (j.Def.Kind == ItemKind.Page) { OpenUrl(j.Def.Url); return; }
            if (j.State == JobState.Done) { OpenInFolder(j.Path); return; }
            dl.Start(j);
        }

        void OnZipClick()
        {
            if (ZipJob.State == JobState.Done) { OpenInFolder(ZipJob.Path); return; }
            dl.Start(ZipJob);
        }

        // ---------------------------------------------------------------- fim do lote: notifica e abre a pasta
        void OnBatchFinished(List<Job> okList, List<Job> badList)
        {
            var names = new List<string>();
            foreach (Job b in badList) names.Add(b.Def.Name);
            string bad = string.Join(", ", names.ToArray());

            if (okList.Count == 0)
            {
                Notify("Necess: o download falhou", "Não foi possível baixar: " + bad + ".", ToolTipIcon.Error);
                return;
            }
            string msg = okList.Count == 1
                ? okList[0].Def.Name + " baixado. Abrindo a pasta."
                : okList.Count + " arquivos baixados. Abrindo a pasta.";
            if (badList.Count > 0) msg += " Falhou: " + bad + ".";
            lastFile = okList.Count == 1 ? okList[0].Path : null;
            Notify("Necess: download concluído", msg, ToolTipIcon.Info);
            OpenInFolder(lastFile);
        }

        void Notify(string title, string text, ToolTipIcon icon)
        {
            try { if (tray != null) tray.ShowBalloonTip(8000, title, text, icon); }
            catch (Exception ex) { Log.Write("notificacao: " + ex.Message); }
        }

        void OpenInFolder(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    Process.Start("explorer.exe", "/select,\"" + path + "\"");
                else
                {
                    Directory.CreateDirectory(dlDir);
                    Process.Start("explorer.exe", "\"" + dlDir + "\"");
                }
            }
            catch (Exception ex) { Log.Write("abrir pasta: " + ex.Message); }
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write("abrir url: " + ex.Message); }
        }

        // ---------------------------------------------------------------- animacao
        void OnTick(object sender, EventArgs e)
        {
            double t = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, t - lastT);
            lastT = t;
            if (backdrop.Advance(dt, AnyBusy())) Invalidate();
        }

        // Sem animacao quando minimizada ou quando o Windows esta com "mostrar animacoes" desligado.
        void SyncAnim() { anim.Enabled = !reduceMotion && Visible && WindowState != FormWindowState.Minimized; }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); SyncAnim(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); SyncAnim(); }
        protected override void OnActivated(EventArgs e) { base.OnActivated(e); anim.Interval = 40; }
        protected override void OnDeactivate(EventArgs e) { base.OnDeactivate(e); anim.Interval = 90; }

        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        static bool SystemReducesMotion()
        {
            try
            {
                int on = 1;   // SPI_GETCLIENTAREAANIMATION = 0x1042
                return SystemParametersInfo(0x1042, 0, ref on, 0) && on == 0;
            }
            catch { return false; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try   // barra de titulo escura, da cor do fundo (Windows 10 1809+ / 11); ignora se nao existir
            {
                int one = 1;
                DwmSetWindowAttribute(Handle, 20, ref one, 4);
                DwmSetWindowAttribute(Handle, 19, ref one, 4);
                int cap = Theme.Bg.R | (Theme.Bg.G << 8) | (Theme.Bg.B << 16);
                int txt = Theme.Ink.R | (Theme.Ink.G << 8) | (Theme.Ink.B << 16);
                DwmSetWindowAttribute(Handle, 35, ref cap, 4);
                DwmSetWindowAttribute(Handle, 36, ref txt, 4);
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            anim.Stop();
            dl.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            backdrop.Dispose();
            base.OnFormClosed(e);
        }

        // ---------------------------------------------------------------- pintura
        const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

        static void Txt(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            if (!string.IsNullOrEmpty(s)) TextRenderer.DrawText(g, s, f, r, c, flags | Base);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Bg);
            backdrop.Draw(g, ClientRectangle);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawHeader(g);
            DrawList(g);
            DrawFooter(g);
        }

        void DrawHeader(Graphics g)
        {
            int px = Theme.S(PadX), top = Theme.S(PadTop);
            int logoSz = Theme.S(44);
            if (logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, new Rectangle(px, top + Theme.S(6), logoSz, logoSz));
            }
            int tx = px + logoSz + Theme.S(Theme.S3);
            Txt(g, "Necess", fTitle, new Rectangle(tx, top - Theme.S(2), Theme.S(260), Theme.S(36)), Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            Txt(g, "Essenciais do PC num lugar só.", fTag, new Rectangle(tx, top + Theme.S(36), Theme.S(300), Theme.S(18)), Theme.Ink2, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // onde os arquivos caem (dado -> monoespacada)
            int w = Theme.S(380);
            var pr = new Rectangle(btnFolder.Left - Theme.S(Theme.S3) - w, btnFolder.Top, w, btnFolder.Height);
            Txt(g, dlDir, fMono, pr, Theme.Ink2, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
        }

        void DrawList(Graphics g)
        {
            int px = Theme.S(PadX), cw = Theme.S(ContentW);
            int blockH = Theme.S(19 + 3 + 16);
            using (var line = new Pen(Theme.Line, 1f))
            using (var bar = new SolidBrush(Theme.Accent))
            {
                g.DrawLine(line, px, listTop, px + cw, listTop);
                for (int i = 0; i < Rows.Count; i++)
                {
                    Job j = Rows[i].Job;
                    ItemDef d = j.Def;
                    int top = listTop + i * rowH;
                    int y0 = top + (rowH - blockH) / 2;

                    Txt(g, (i + 1).ToString("00"), fMono, new Rectangle(px + Theme.S(ColIdx), y0, Theme.S(28), Theme.S(19)), Theme.Ink3, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    Txt(g, d.Name, fName, new Rectangle(px + Theme.S(ColMain), y0, Theme.S(MainW), Theme.S(19)), Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    Txt(g, d.Desc, fDesc, new Rectangle(px + Theme.S(ColMain), y0 + Theme.S(22), Theme.S(MainW), Theme.S(16)), Theme.Ink2, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    Txt(g, d.Source, fMono, new Rectangle(px + Theme.S(ColSource), top, Theme.S(SourceW), rowH - 1), Theme.Ink2, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    DrawStatus(g, j, new Rectangle(px + Theme.S(ColStatus), top, Theme.S(StatusW), rowH - 1));

                    g.DrawLine(line, px, top + rowH - 1, px + cw, top + rowH - 1);
                    if (j.State == JobState.Downloading && j.Percent >= 0)
                        g.FillRectangle(bar, px, top + rowH - Theme.S(2), cw * j.Percent / 100, Theme.S(2));
                }
            }
        }

        void DrawStatus(Graphics g, Job j, Rectangle r)
        {
            string text = null;
            Color c = Theme.Ink2;
            switch (j.State)
            {
                case JobState.Preparing: text = "Preparando…"; break;
                case JobState.Downloading:
                    text = j.Percent >= 0 ? "Baixando " + j.Percent + "%"
                         : j.Bytes > 0 ? "Baixando " + Megabytes(j.Bytes) : "Baixando…";
                    c = Theme.Accent; break;
                case JobState.Done:
                    DrawCheck(g, r.X, r.Y + r.Height / 2);
                    r.X += Theme.S(20); r.Width -= Theme.S(20);
                    text = "Concluído"; c = Theme.Ink; break;
                case JobState.Failed: text = j.Message; c = Theme.Err; break;
            }
            Txt(g, text, fStatus, r, c, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        static void DrawCheck(Graphics g, int x, int cy)
        {
            using (var pen = new Pen(Theme.Ok, Math.Max(1.6f, Theme.Scale * 1.7f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[]
                {
                    new Point(x + Theme.S(1), cy),
                    new Point(x + Theme.S(5), cy + Theme.S(4)),
                    new Point(x + Theme.S(12), cy - Theme.S(4))
                });
            }
        }

        static string Megabytes(long b) { return (b / 1048576.0).ToString("0.0") + " MB"; }

        void DrawFooter(Graphics g)
        {
            string text; Color c = Theme.Ink2;
            if (ZipJob.Busy) text = "Necess.zip: baixando" + (ZipJob.Percent >= 0 ? " " + ZipJob.Percent + "%" : "…");
            else if (ZipJob.State == JobState.Failed) { text = "Necess.zip: " + ZipJob.Message; c = Theme.Err; }
            else if (ZipJob.State == JobState.Done) { text = "Necess.zip baixado"; c = Theme.Ink; }
            else
            {
                int done = 0, total = 0;
                foreach (Row r in Rows)
                {
                    if (r.Job.Def.Kind != ItemKind.Download) continue;
                    total++; if (r.Job.State == JobState.Done) done++;
                }
                text = done + " de " + total + " baixados";
            }
            int w = Theme.S(300);
            Txt(g, text, fStatus, new Rectangle(btnAll.Left - Theme.S(Theme.S4) - w, footerTop, w, Theme.S(BtnH)), c, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
