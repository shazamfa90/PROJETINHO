// Harness SO PARA TESTE: abre a janela com estados forcados (sem rede) para tirar screenshot.
// Compilado junto com src/ (menos Program.cs). Uso:  Preview.exe <escala> <estado> <segundos>
using System;
using System.Threading;
using System.Windows.Forms;
using Necess;

static class Preview
{
    [STAThread]
    static void Main(string[] a)
    {
        float scale = a.Length > 0 ? float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture) : 1f;
        string state = a.Length > 1 ? a[1] : "mixed";
        int secs = a.Length > 2 ? int.Parse(a[2]) : 8;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        string dir = a.Length > 3 ? a[3] : @"C:\Users\Nome\Desktop\Necess\Downloads";
        var f = new MainForm(dir, scale);

        if (state == "mixed")          // concluido, baixando 42%, falha, ocioso, baixando sem tamanho
        {
            Set(f, 1, JobState.Done, 100, "");
            Set(f, 2, JobState.Done, 100, "");
            Set(f, 3, JobState.Downloading, 42, "");
            Set(f, 4, JobState.Failed, -1, "Servidor recusou (403)");
            Set(f, 5, JobState.Downloading, -1, "", 3200000);
        }
        else if (state == "alldone")
        {
            foreach (Row r in f.Rows) if (r.Job.Def.Kind == ItemKind.Download) { r.Job.State = JobState.Done; r.Job.Percent = 100; }
        }
        else if (state == "zipfail")
        {
            f.ZipJob.State = JobState.Failed; f.ZipJob.Message = "Sem conexão";
        }
        else if (state == "focus")
        {
            f.Shown += delegate { f.Rows[1].Button.Focus(); };
        }
        f.SyncUi();

        var t = new System.Windows.Forms.Timer { Interval = secs * 1000 };
        t.Tick += delegate { f.Close(); };
        t.Start();
        Application.Run(f);
    }

    static void Set(MainForm f, int idx, JobState s, int pct, string msg, long bytes = 0)
    {
        Job j = f.Rows[idx].Job;
        j.State = s; j.Percent = pct; j.Message = msg; j.Bytes = bytes;
    }
}
