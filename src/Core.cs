// Necess - nucleo sem UI: catalogo, validacao do arquivo baixado, fluxo do Google Drive
// e o Downloader. Nada aqui depende de WinForms, para poder ser testado em console.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Necess
{
    public enum ItemKind { Page, Download }

    /// <summary>O que o arquivo baixado TEM que ser (conferido pelos primeiros bytes).</summary>
    public enum Expect { None, Exe, Zip, Archive }

    public sealed class ItemDef
    {
        public string Name, Source, Desc, Url, File;
        public ItemKind Kind;
        public Expect Expect;
        // Se preenchido, a URL mais nova e procurada nesta pagina oficial; Url e o plano B.
        public string LatestPage, LatestPattern, LatestBase;
    }

    public static class Catalog
    {
        public static readonly ItemDef[] Items =
        {
            new ItemDef { Name = "Visual C++ Runtimes All-in-One", Source = "TechPowerUp",
                Desc = "Todos os runtimes do Visual C++ (jul/2025). Abre a página.",
                Kind = ItemKind.Page, Url = "https://www.techpowerup.com/download/visual-c-redistributable-runtime-package-all-in-one/" },
            new ItemDef { Name = "Visual C++ Redistributable x64", Source = "Microsoft",
                Desc = "Runtime mais recente do VC++, 64 bits.",
                Kind = ItemKind.Download, Expect = Expect.Exe, File = "VC_redist.x64.exe",
                Url = "https://aka.ms/vs/17/release/vc_redist.x64.exe" },
            new ItemDef { Name = "DirectX End-User Runtime", Source = "Microsoft",
                Desc = "Instalador web do DirectX (dxwebsetup).",
                Kind = ItemKind.Download, Expect = Expect.Exe, File = "dxwebsetup.exe",
                Url = "https://download.microsoft.com/download/1/7/1/1718ccc4-6315-4d8e-9543-8e28a4e18c4c/dxwebsetup.exe" },
            new ItemDef { Name = "WinRAR", Source = "RARLAB",
                Desc = "64 bits, português do Brasil. Necessário para o Defender Control.",
                Kind = ItemKind.Download, Expect = Expect.Exe, File = "WinRAR_x64.exe",
                Url = "https://www.rarlab.com/rar/winrar-x64-723br.exe",
                LatestPage = "https://www.rarlab.com/download.htm",
                LatestPattern = "href=\"(/rar/winrar-x64-\\d+br\\.exe)\"",
                LatestBase = "https://www.rarlab.com" },
            new ItemDef { Name = "Windows Update Blocker", Source = "Sordum",
                Desc = "Liga e desliga o Windows Update. Portátil, sem instalação.",
                Kind = ItemKind.Download, Expect = Expect.Archive, File = "Wub.zip",
                Url = "https://www.sordum.org/files/downloads.php?st-windows-update-blocker" },
            new ItemDef { Name = "Defender Control", Source = "Sordum",
                Desc = "Liga e desliga o Defender. Arquivo .rar, senha: sordum.",
                Kind = ItemKind.Download, Expect = Expect.Archive, File = "dControl.rar",
                Url = "https://www.sordum.org/files/downloads.php?st-defender-control" },
            new ItemDef { Name = "Valorant / Riot Client", Source = "Riot Games",
                Desc = "Abre a página oficial para baixar o launcher.",
                Kind = ItemKind.Page, Url = "https://playvalorant.com/pt-br/download/" },
        };

        /// <summary>Pacote do proprio Necess. HEAD = branch padrao do repositorio.</summary>
        public static readonly ItemDef Zip = new ItemDef
        {
            Name = "Necess.zip", Source = "GitHub", Desc = "Pacote do Necess",
            Kind = ItemKind.Download, Expect = Expect.Zip, File = "Necess.zip",
            Url = "https://raw.githubusercontent.com/shazamfa90/PROJETINHO/HEAD/Necess.zip"
        };

        /// <summary>URL final do item: a mais nova da pagina oficial, ou a fixa se falhar.</summary>
        public static string ResolveUrl(ItemDef d)
        {
            if (string.IsNullOrEmpty(d.LatestPage)) return d.Url;
            try
            {
                string html = Http.GetText(d.LatestPage, 8000);
                Match m = Regex.Match(html, d.LatestPattern);
                if (m.Success) return d.LatestBase + m.Groups[1].Value;
            }
            catch (Exception ex) { Log.Write("latest " + d.Name + ": " + ex.Message); }
            return d.Url;
        }
    }

    public static class Http
    {
        public const string UserAgent = "Mozilla/5.0";

        public static void Init()
        {
            try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }
            // padrao e 2 conexoes por host: o "Baixar tudo" ficaria em fila.
            ServicePointManager.DefaultConnectionLimit = 16;
        }

        public static string GetText(string url, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent; req.Timeout = timeoutMs; req.ReadWriteTimeout = timeoutMs;
            using (WebResponse resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
                return sr.ReadToEnd();
        }

        /// <summary>Primeiros bytes de uma URL (segue redirecionamentos). Usado pelos testes.</summary>
        public static byte[] ReadHead(string url, int max, int timeoutMs)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = UserAgent; req.Timeout = timeoutMs; req.ReadWriteTimeout = timeoutMs;
            using (WebResponse resp = req.GetResponse())
            using (Stream st = resp.GetResponseStream())
            {
                var buf = new byte[max];
                int n = 0, r;
                while (n < max && (r = st.Read(buf, n, max - n)) > 0) n += r;
                var outb = new byte[n];
                Array.Copy(buf, outb, n);
                return outb;
            }
        }

        /// <summary>Causa curta e legivel para mostrar na linha (a proxima acao e o botao).</summary>
        public static string FriendlyError(Exception e)
        {
            var we = e as WebException;
            if (we != null)
            {
                switch (we.Status)
                {
                    case WebExceptionStatus.NameResolutionFailure:
                    case WebExceptionStatus.ConnectFailure:
                        return "Sem conexão";
                    case WebExceptionStatus.Timeout:
                        return "Demorou demais";
                    case WebExceptionStatus.TrustFailure:
                    case WebExceptionStatus.SecureChannelFailure:
                        return "Falha de segurança (TLS)";
                    case WebExceptionStatus.ProtocolError:
                        var hr = we.Response as HttpWebResponse;
                        return hr != null ? "Servidor recusou (" + (int)hr.StatusCode + ")" : "Servidor recusou";
                    case WebExceptionStatus.RequestCanceled:
                        return "Cancelado";
                }
                return "Falha na conexão";
            }
            if (e is FileNotFoundException) return "Sumiu após baixar (antivírus?)";
            if (e is IOException || e is UnauthorizedAccessException) return "Não deu para gravar";
            return "Falhou";
        }
    }

    public static class FileKinds
    {
        /// <summary>exe | zip | rar | 7z | html | unknown</summary>
        public static string Detect(byte[] b, int n)
        {
            if (n >= 2 && b[0] == 0x4D && b[1] == 0x5A) return "exe";                       // MZ
            if (n >= 4 && b[0] == 0x50 && b[1] == 0x4B) return "zip";                       // PK
            if (n >= 4 && b[0] == 0x52 && b[1] == 0x61 && b[2] == 0x72 && b[3] == 0x21) return "rar";   // Rar!
            if (n >= 4 && b[0] == 0x37 && b[1] == 0x7A && b[2] == 0xBC && b[3] == 0xAF) return "7z";
            if (n >= 1)
            {
                string t = Encoding.ASCII.GetString(b, 0, Math.Min(n, 512));
                if (Regex.IsMatch(t, @"^[^A-Za-z0-9<]{0,8}<(!doctype|html|head|body|\?xml)", RegexOptions.IgnoreCase))
                    return "html";
            }
            return "unknown";
        }

        public static string DetectFile(string path)
        {
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    var buf = new byte[1024];
                    int n = fs.Read(buf, 0, buf.Length);
                    return Detect(buf, n);
                }
            }
            catch { return "missing"; }
        }

        public static bool Matches(Expect e, string kind)
        {
            switch (e)
            {
                case Expect.Exe: return kind == "exe";
                case Expect.Zip: return kind == "zip";
                case Expect.Archive: return kind == "zip" || kind == "rar" || kind == "7z";
                default: return true;
            }
        }
    }

    public static class Drive
    {
        // Arquivos que o Drive nao consegue escanear (ex.: .rar com senha) nao baixam direto:
        // ele devolve uma pagina "Virus scan warning" com o formulario "Download anyway".
        // Le esse formulario e monta a URL de confirmacao; null se o HTML for outra coisa.
        public static string ConfirmUrl(string html)
        {
            Match act = Regex.Match(html, "<form[^>]*id=\"download-form\"[^>]*action=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!act.Success) return null;
            string baseUrl = WebUtility.HtmlDecode(act.Groups[1].Value);
            if (!Regex.IsMatch(baseUrl, @"^https://(drive\.usercontent\.google\.com|drive\.google\.com|docs\.google\.com)/"))
                return null;
            var pairs = new List<string>();
            foreach (Match m in Regex.Matches(html, "<input[^>]*type=\"hidden\"[^>]*name=\"([^\"]+)\"[^>]*value=\"([^\"]*)\""))
                pairs.Add(m.Groups[1].Value + "=" + Uri.EscapeDataString(WebUtility.HtmlDecode(m.Groups[2].Value)));
            if (pairs.Count == 0) return null;
            return baseUrl + "?" + string.Join("&", pairs.ToArray());
        }
    }

    public static class Log
    {
        public static void Write(string text)
        {
            try
            {
                string f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Necess.log");
                File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine);
            }
            catch { }
        }
    }

    public static class AppPaths
    {
        /// <summary>
        /// Pasta Downloads ao lado do .exe. Usa Downloads\Necess do usuario quando nao da para gravar
        /// ali ou quando o .exe foi aberto de dentro do ZIP (o Explorer o extrai para a pasta temporaria,
        /// e os arquivos baixados sumiriam junto com ela).
        /// </summary>
        public static string DownloadDir()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            if (!IsUnderTemp(appDir))
            {
                string local = Path.Combine(appDir, "Downloads");
                if (CanWrite(local)) return local;
            }
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(Path.Combine(home, "Downloads"), "Necess");
        }

        static bool IsUnderTemp(string dir)
        {
            try
            {
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\', '/');
                string d = Path.GetFullPath(dir);
                return d.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static bool CanWrite(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }
    }

    public enum JobState { Idle, Preparing, Downloading, Done, Failed }

    public sealed class Job
    {
        public readonly ItemDef Def;
        public JobState State = JobState.Idle;
        public string Message = "";          // causa curta quando Failed
        public int Percent = -1;             // -1 = tamanho desconhecido
        public long Bytes;
        public string Path;                  // arquivo final (Done)
        internal string Temp;
        internal bool Retried, Stalled;
        internal int LastProgress;
        internal WebClient Client;
        public Job(ItemDef def) { Def = def; }
        public bool Busy { get { return State == JobState.Preparing || State == JobState.Downloading; } }
    }

    sealed class TimedWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest r = base.GetWebRequest(address);
            if (r != null)
            {
                r.Timeout = 30000;
                var h = r as HttpWebRequest;
                if (h != null) h.ReadWriteTimeout = 30000;
            }
            return r;
        }
    }

    /// <summary>
    /// Baixa para um .part, confere o conteudo (MZ/PK/Rar!), segue a confirmacao do Drive,
    /// renomeia e avisa quando o lote termina. Os eventos saem no SynchronizationContext
    /// de quem criou (a thread da UI); em console eles saem na thread do pool.
    /// </summary>
    public sealed class Downloader
    {
        public event Action<Job> JobChanged;
        public event Action<List<Job>, List<Job>> BatchFinished;   // concluidos, falhos

        readonly SynchronizationContext sync;
        readonly string dir;
        readonly object gate = new object();
        readonly List<Job> ok = new List<Job>(), bad = new List<Job>(), running = new List<Job>();
        readonly System.Threading.Timer watchdog;
        int active;

        public Downloader(string downloadDir)
        {
            dir = downloadDir;
            sync = SynchronizationContext.Current;
            watchdog = new System.Threading.Timer(CheckStalled, null, 5000, 5000);
        }

        public void Stop() { watchdog.Dispose(); }

        void Post(Action a)
        {
            if (sync != null) sync.Post(delegate { a(); }, null); else a();
        }

        void Raise(Job j) { Action<Job> h = JobChanged; if (h != null) h(j); }

        void Set(Job j, JobState s, string msg) { j.State = s; j.Message = msg; Raise(j); }

        public void StartAll(IEnumerable<Job> jobs)
        {
            lock (gate) active++;                 // segura o "fim do lote" ate iniciar todos
            try { foreach (Job j in jobs) if (j.Def.Kind == ItemKind.Download && !j.Busy && j.State != JobState.Done) Start(j); }
            finally { lock (gate) active--; Finish(); }
        }

        public void Start(Job job)
        {
            lock (gate) { if (job.Busy) return; active++; }
            job.Retried = false; job.Path = null; job.Percent = -1; job.Bytes = 0;
            Set(job, JobState.Preparing, "Preparando");
            // a URL "mais nova" (WinRAR) exige uma requisicao: fora da thread da UI
            ThreadPool.QueueUserWorkItem(delegate
            {
                string url;
                try { url = Catalog.ResolveUrl(job.Def); } catch { url = job.Def.Url; }
                Post(delegate { Begin(job, url); });
            });
        }

        void Begin(Job job, string url)
        {
            try
            {
                Directory.CreateDirectory(dir);
                job.Temp = System.IO.Path.Combine(dir, job.Def.File + ".part");
                StartClient(job, url);
            }
            catch (Exception ex) { Fail(job, Http.FriendlyError(ex), ex); }
        }

        void StartClient(Job job, string url)
        {
            var wc = new TimedWebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = Http.UserAgent;
            wc.DownloadProgressChanged += OnProgress;
            wc.DownloadFileCompleted += OnCompleted;
            job.Client = wc; job.Stalled = false; job.LastProgress = Environment.TickCount;
            lock (gate) { if (!running.Contains(job)) running.Add(job); }
            Set(job, JobState.Downloading, "Baixando");
            wc.DownloadFileAsync(new Uri(url), job.Temp, job);
        }

        void OnProgress(object s, DownloadProgressChangedEventArgs e)
        {
            var j = (Job)e.UserState;
            j.LastProgress = Environment.TickCount;
            j.Bytes = e.BytesReceived;
            j.Percent = e.TotalBytesToReceive > 0 ? e.ProgressPercentage : -1;
            Raise(j);
        }

        void OnCompleted(object s, AsyncCompletedEventArgs e)
        {
            var job = (Job)e.UserState;
            try
            {
                if (e.Cancelled) { Fail(job, job.Stalled ? "Sem resposta" : "Cancelado", null); return; }
                if (e.Error != null) { Fail(job, Http.FriendlyError(e.Error), e.Error); return; }

                string kind = FileKinds.DetectFile(job.Temp);

                // Google Drive devolveu a pagina "Virus scan warning": segue o "Download anyway".
                if (kind == "html" && !job.Retried)
                {
                    string next = null;
                    try { next = Drive.ConfirmUrl(File.ReadAllText(job.Temp)); } catch { }
                    if (next != null)
                    {
                        job.Retried = true;
                        TryDelete(job.Temp);
                        StartClient(job, next);
                        return;
                    }
                }

                if (!FileKinds.Matches(job.Def.Expect, kind))
                {
                    Log.Write(job.Def.Name + ": conteudo '" + kind + "' (esperado " + job.Def.Expect + ")");
                    // "missing": o arquivo sumiu depois de baixar - tipicamente o antivirus apagou
                    // (o Defender costuma detectar o Defender Control / Update Blocker como riskware).
                    Fail(job, kind == "missing" ? "Sumiu após baixar (antivírus?)"
                            : kind == "html" ? "Veio uma página, não o arquivo" : "Arquivo inválido", null);
                    return;
                }

                // archive: a extensao vem do conteudo real (.rar, .zip ou .7z)
                string name = job.Def.Expect == Expect.Archive
                    ? System.IO.Path.GetFileNameWithoutExtension(job.Def.File) + "." + kind
                    : job.Def.File;
                string final = System.IO.Path.Combine(dir, name);
                if (File.Exists(final)) File.Delete(final);
                File.Move(job.Temp, final);
                job.Path = final;
                job.Percent = 100;
                lock (gate) { running.Remove(job); ok.Add(job); active--; }
                Set(job, JobState.Done, "Concluído");
                Finish();
            }
            catch (Exception ex) { Fail(job, Http.FriendlyError(ex), ex); }
        }

        void Fail(Job job, string msg, Exception ex)
        {
            if (ex != null) Log.Write(job.Def.Name + ": " + ex.Message);
            TryDelete(job.Temp);
            job.Percent = -1;
            lock (gate) { running.Remove(job); bad.Add(job); active--; }
            Set(job, JobState.Failed, msg);
            Finish();
        }

        void Finish()
        {
            List<Job> o, b;
            lock (gate)
            {
                if (active > 0 || (ok.Count == 0 && bad.Count == 0)) return;
                o = new List<Job>(ok); b = new List<Job>(bad);
                ok.Clear(); bad.Clear();
            }
            Action<List<Job>, List<Job>> h = BatchFinished;
            if (h != null) h(o, b);
        }

        // 45 s sem nenhum byte: cancela (a linha vira "Sem resposta" e da para tentar de novo).
        void CheckStalled(object state)
        {
            List<Job> snap;
            lock (gate) snap = new List<Job>(running);
            foreach (Job j in snap)
            {
                if (j.State == JobState.Downloading && Environment.TickCount - j.LastProgress > 45000)
                {
                    j.Stalled = true;
                    try { j.Client.CancelAsync(); } catch { }
                }
            }
        }

        static void TryDelete(string p)
        {
            try { if (p != null && File.Exists(p)) File.Delete(p); } catch { }
        }
    }
}
