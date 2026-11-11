using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Necess
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            // Texto nitido em telas com escala 125%/150% (em vez do Windows esticar a imagem).
            // Precisa vir antes de qualquer janela. Sem manifest: o mcs nao embute um.
            try { SetProcessDPIAware(); } catch { }

            Http.Init();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // eventos do WebClient/Downloader voltam para a thread da interface
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

            // Em vez do dialogo "Ocorreu uma excecao sem tratamento": registra em Necess.log.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Log.Write("excecao: " + e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("fatal: " + e.ExceptionObject);

            Application.Run(new MainForm(AppPaths.DownloadDir(), 0f));
        }
    }
}
