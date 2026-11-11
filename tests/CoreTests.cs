// Testes do nucleo (sem UI). Offline por padrao; com --online tambem bate nos servidores reais
// e baixa de verdade com o Downloader.
//   mcs -target:exe -out:CoreTests.exe tests/CoreTests.cs src/Core.cs && mono CoreTests.exe [--online]
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Necess;

static class CoreTests
{
    static int pass, fail;

    static void Check(bool ok, string name)
    {
        if (ok) pass++; else fail++;
        Console.WriteLine((ok ? "  ok    " : "  FALHOU ") + name);
    }

    static int Main(string[] args)
    {
        Http.Init();
        Console.WriteLine("== offline");
        Offline();
        if (Array.IndexOf(args, "--online") >= 0)
        {
            Console.WriteLine("== online (servidores reais)");
            Online();
        }
        Console.WriteLine(fail == 0 ? "TUDO OK (" + pass + " verificacoes)" : fail + " FALHA(S) de " + (pass + fail));
        return fail == 0 ? 0 : 1;
    }

    static byte[] B(params int[] v) { var b = new byte[v.Length]; for (int i = 0; i < v.Length; i++) b[i] = (byte)v[i]; return b; }
    static string K(byte[] b) { return FileKinds.Detect(b, b.Length); }

    static void Offline()
    {
        Check(K(B(0x4D, 0x5A, 0x90, 0x00)) == "exe", "MZ -> exe");
        Check(K(B(0x50, 0x4B, 0x03, 0x04)) == "zip", "PK -> zip");
        Check(K(B(0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00)) == "rar", "Rar! -> rar (RAR5)");
        Check(K(B(0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C)) == "7z", "7z");
        Check(K(Encoding.ASCII.GetBytes("<!DOCTYPE html><html>")) == "html", "<!DOCTYPE -> html");
        Check(K(Encoding.UTF8.GetBytes("﻿  \r\n<html lang=pt>")) == "html", "BOM + espacos + <html -> html");
        Check(K(B(1, 2, 3, 4, 5)) == "unknown", "lixo -> unknown");
        Check(K(new byte[0]) == "unknown", "vazio -> unknown");

        Check(FileKinds.Matches(Expect.Exe, "exe") && !FileKinds.Matches(Expect.Exe, "html"), "Expect.Exe aceita exe, recusa html");
        Check(FileKinds.Matches(Expect.Archive, "rar") && FileKinds.Matches(Expect.Archive, "zip") && FileKinds.Matches(Expect.Archive, "7z"), "Expect.Archive aceita rar/zip/7z");
        Check(!FileKinds.Matches(Expect.Archive, "exe") && !FileKinds.Matches(Expect.Archive, "html"), "Expect.Archive recusa exe/html");
        Check(FileKinds.Matches(Expect.Zip, "zip") && !FileKinds.Matches(Expect.Zip, "rar"), "Expect.Zip so aceita zip");

        // pagina REAL de "Virus scan warning" do Drive (a que quebrava o Defender Control)
        string html = File.ReadAllText(Path.Combine(Dir(), "fixtures", "drive_warning.html"));
        string url = Drive.ConfirmUrl(html);
        Check(url != null, "Drive.ConfirmUrl acha o formulario na pagina real");
        if (url != null)
        {
            Check(url.StartsWith("https://drive.usercontent.google.com/download?"), "  URL de confirmacao e do dominio do Google");
            Check(url.Contains("id=1jxmKjN820qP_cLZLgbeBi-aP5DUbROle"), "  leva o id do arquivo");
            Check(url.Contains("export=download") && url.Contains("confirm=t") && url.Contains("uuid="), "  leva export, confirm e uuid");
        }
        Check(Drive.ConfirmUrl("<html><body>qualquer outra pagina</body></html>") == null, "HTML qualquer -> null");
        Check(Drive.ConfirmUrl("<form id=\"download-form\" action=\"https://evil.example/x\" method=\"get\"><input type=\"hidden\" name=\"id\" value=\"1\"></form>") == null,
              "formulario apontando para fora do Google -> null (nao segue)");
        string enc = Drive.ConfirmUrl("<form id=\"download-form\" action=\"https://drive.usercontent.google.com/download\"><input type=\"hidden\" name=\"a\" value=\"x&amp;y z\"></form>");
        Check(enc != null && enc.EndsWith("?a=x%26y%20z"), "valores sao HTML-decodificados e escapados na URL");

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cat = true;
        foreach (ItemDef d in Catalog.Items)
        {
            cat &= d.Url.StartsWith("https://") && !string.IsNullOrEmpty(d.Name) && !string.IsNullOrEmpty(d.Desc) && !string.IsNullOrEmpty(d.Source);
            if (d.Kind == ItemKind.Download) cat &= !string.IsNullOrEmpty(d.File) && d.Expect != Expect.None && files.Add(d.File);
        }
        Check(cat, "catalogo: tudo https, com nome/descricao/fonte, downloads com arquivo unico e Expect");
        Check(Catalog.Zip.Url.Contains("/HEAD/"), "Necess.zip aponta para HEAD (branch padrao), nao para um branch de sessao");
        Check(Regex.IsMatch("href=\"/rar/winrar-x64-723br.exe\"", Catalog.Items[3].LatestPattern) &&
              !Regex.IsMatch("href=\"/rar/winrar-x64-730b2br.exe\"", Catalog.Items[3].LatestPattern),
              "padrao do WinRAR pega a estavel (723br) e ignora a beta (730b2br)");
    }

    static string Dir() { return Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0])) is string d && Directory.Exists(Path.Combine(d, "fixtures")) ? d : "tests"; }

    static void Online()
    {
        string w = Catalog.ResolveUrl(Catalog.Items[3]);
        Check(Regex.IsMatch(w, @"^https://www\.rarlab\.com/rar/winrar-x64-\d+br\.exe$"), "WinRAR: URL mais nova resolvida na pagina oficial -> " + w);

        var all = new List<ItemDef>(Catalog.Items); all.Add(Catalog.Zip);
        foreach (ItemDef d in all)
        {
            if (d.Kind != ItemKind.Download) continue;
            try
            {
                string u = Catalog.ResolveUrl(d);
                byte[] head = Http.ReadHead(u, 4096, 25000);
                string kind = FileKinds.Detect(head, head.Length);
                if (kind == "html")
                {
                    string next = Drive.ConfirmUrl(Encoding.UTF8.GetString(head));
                    if (next != null) { head = Http.ReadHead(next, 4096, 25000); kind = FileKinds.Detect(head, head.Length) + " (via Drive)"; }
                }
                Check(FileKinds.Matches(d.Expect, kind.Split(' ')[0]), "link entrega o tipo certo: " + d.Name + " -> " + kind);
            }
            catch (Exception ex) { Check(false, "link " + d.Name + ": " + ex.Message); }
        }

        // ---- Downloader de ponta a ponta, num diretorio temporario ----
        string tmp = Path.Combine(Path.GetTempPath(), "necess-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        var dl = new Downloader(tmp);
        var jobs = new List<Job>();
        foreach (int i in new[] { 2, 4, 5 }) jobs.Add(new Job(Catalog.Items[i]));              // dxwebsetup, Wub, Defender Control
        jobs.Add(new Job(Catalog.Zip));
        var notFound = new Job(new ItemDef { Name = "404", Kind = ItemKind.Download, Expect = Expect.Exe, File = "x.exe",
                                             Url = "https://raw.githubusercontent.com/shazamfa90/PROJETINHO/HEAD/nao-existe.exe" });
        var htmlPage = new Job(new ItemDef { Name = "pagina", Kind = ItemKind.Download, Expect = Expect.Exe, File = "y.exe",
                                             Url = "https://playvalorant.com/pt-br/download/" });
        jobs.Add(notFound); jobs.Add(htmlPage);

        List<Job> ok = null, bad = null;
        int batches = 0;
        var done = new ManualResetEvent(false);
        dl.BatchFinished += delegate (List<Job> o, List<Job> b) { ok = o; bad = b; batches++; done.Set(); };
        dl.StartAll(jobs);
        bool finished = done.WaitOne(TimeSpan.FromSeconds(240));
        Thread.Sleep(300);
        dl.Stop();

        Check(finished, "lote terminou (BatchFinished disparou)");
        Check(batches == 1, "BatchFinished disparou UMA vez (abre a pasta uma vez so)");
        Check(ok != null && ok.Count == 4 && bad != null && bad.Count == 2, "4 concluidos e 2 falhos");
        Check(jobs[0].State == JobState.Done && File.Exists(Path.Combine(tmp, "dxwebsetup.exe")) && FileKinds.DetectFile(Path.Combine(tmp, "dxwebsetup.exe")) == "exe", "dxwebsetup.exe baixado e e um exe");
        Check(jobs[1].State == JobState.Done && File.Exists(Path.Combine(tmp, "Wub.zip")) && FileKinds.DetectFile(Path.Combine(tmp, "Wub.zip")) == "zip", "Wub.zip baixado e e um zip");
        string rar = Path.Combine(tmp, "dControl.rar");
        Check(jobs[2].State == JobState.Done && File.Exists(rar) && FileKinds.DetectFile(rar) == "rar", "Defender Control: o fluxo do Drive entregou o .rar de verdade (nao o HTML de 3 KB)");
        Check(File.Exists(rar) && new FileInfo(rar).Length > 100000, "  .rar com tamanho real: " + (File.Exists(rar) ? new FileInfo(rar).Length : 0) + " bytes");
        Check(!File.Exists(Path.Combine(tmp, "dControl.zip")), "  nao existe mais o dControl.zip falso");
        Check(jobs[3].State == JobState.Done && FileKinds.DetectFile(Path.Combine(tmp, "Necess.zip")) == "zip", "Necess.zip baixado de HEAD e e um zip");
        Check(notFound.State == JobState.Failed && notFound.Message == "Servidor recusou (404)", "404 -> falha com causa: '" + notFound.Message + "'");
        Check(htmlPage.State == JobState.Failed && htmlPage.Message.StartsWith("Veio uma p"), "pagina HTML no lugar de .exe -> falha com causa: '" + htmlPage.Message + "'");
        Check(Directory.GetFiles(tmp, "*.part").Length == 0, "nenhum .part sobrando");
        Check(!File.Exists(Path.Combine(tmp, "x.exe")) && !File.Exists(Path.Combine(tmp, "y.exe")), "falhas nao deixam arquivo (nem pagina HTML com nome de .exe)");
        try { Directory.Delete(tmp, true); } catch { }
    }
}
