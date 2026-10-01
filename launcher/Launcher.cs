// Aniimo - Patch Italiana :: Launcher standalone (.NET Framework 4.x, nessuna dipendenza).
// Scarica i dati aggiornati della patch da un manifest online e li applica al gioco.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

class Launcher : Form
{
    // URL del manifest: sostituito da build_launcher.bat oppure da manifest_url.txt accanto all'exe.
    const string DefaultManifestUrl = "__MANIFEST_URL__";
    static readonly string AppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AniimoPatchITA");
    static readonly string CfgFile = Path.Combine(AppDir, "config.json");
    static readonly string BackupDir = Path.Combine(AppDir, "backup");

    TextBox txtPath = new TextBox();
    Label lblStatus = new Label(), lblVer = new Label();
    ProgressBar prog = new ProgressBar();
    Button btnBrowse = new Button(), btnInstall = new Button(), btnPlay = new Button(), btnRestore = new Button();
    Dictionary<string, object> manifest;
    Dictionary<string, object> cfg = new Dictionary<string, object>();
    string manifestUrl;

    static bool cli;   // modalita' da riga di comando: Launcher.exe --install <cartella Aniimo_Data> (senza finestra)

    [STAThread]
    static int Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
        if (args.Length >= 2 && args[0] == "--install")
        {
            cli = true;
            var l = new Launcher();
            l.txtPath.Text = args[1];
            l.manifestUrl = args.Length >= 3 ? args[2] : DefaultManifestUrl;
            Task.Run(() => l.FetchManifest()).Wait();
            if (l.manifest == null) return 2;
            Task.Run(() => l.Install()).Wait();
            return l.InstalledVersion() == l.RemoteVersion() ? 0 : 1;
        }
        Application.EnableVisualStyles();
        Application.Run(new Launcher());
        return 0;
    }

    Launcher()
    {
        Text = "Aniimo - Patch Italiana";
        ClientSize = new Size(640, 330);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(16, 20, 24); ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);

        var title = new Label { Text = "PATCH ITALIANA PER ANIIMO", Font = new Font("Segoe UI", 16f, FontStyle.Bold), AutoSize = true, Location = new Point(24, 18) };
        lblVer.Location = new Point(26, 56); lblVer.AutoSize = true; lblVer.ForeColor = Color.FromArgb(126, 200, 255);
        var lblP = new Label { Text = "Cartella Aniimo_Data del gioco:", AutoSize = true, Location = new Point(26, 96) };
        txtPath.Location = new Point(28, 122); txtPath.Width = 480;
        btnBrowse.Text = "Sfoglia..."; btnBrowse.Location = new Point(520, 120); btnBrowse.Width = 92;
        lblStatus.Location = new Point(26, 166); lblStatus.Size = new Size(590, 52);
        prog.Location = new Point(28, 226); prog.Size = new Size(584, 14);
        btnInstall.Text = "Installa / Aggiorna"; btnInstall.Location = new Point(28, 262); btnInstall.Size = new Size(190, 42);
        btnPlay.Text = "Avvia Aniimo"; btnPlay.Location = new Point(228, 262); btnPlay.Size = new Size(190, 42);
        btnRestore.Text = "Ripristina originale"; btnRestore.Location = new Point(428, 262); btnRestore.Size = new Size(184, 42);
        foreach (var b in new[] { btnBrowse, btnInstall, btnPlay, btnRestore })
        { b.FlatStyle = FlatStyle.Flat; b.BackColor = Color.FromArgb(40, 52, 66); b.ForeColor = Color.White; b.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 130); }
        btnInstall.BackColor = Color.FromArgb(32, 110, 190);
        txtPath.BackColor = Color.FromArgb(28, 34, 42); txtPath.ForeColor = Color.White; txtPath.BorderStyle = BorderStyle.FixedSingle;
        Controls.AddRange(new Control[] { title, lblVer, lblP, txtPath, btnBrowse, lblStatus, prog, btnInstall, btnPlay, btnRestore });

        btnBrowse.Click += delegate { Browse(); };
        btnInstall.Click += async delegate { await Install(); };
        btnRestore.Click += async delegate { await Restore(); };
        btnPlay.Click += delegate { Play(); };
        txtPath.Leave += delegate { SaveCfg(); RefreshState(); };
        Shown += async delegate { await Startup(); };
    }

    // ---------- util ----------
    void Status(string s) { if (cli) { Console.WriteLine(s); return; } if (InvokeRequired) { Invoke(new Action<string>(Status), s); return; } lblStatus.Text = s; }
    void Progress(int v) { if (cli) return; if (InvokeRequired) { Invoke(new Action<int>(Progress), v); return; } prog.Value = Math.Max(0, Math.Min(100, v)); }
    void Busy(bool b) { if (cli) return; if (InvokeRequired) { Invoke(new Action<bool>(Busy), b); return; } btnInstall.Enabled = btnRestore.Enabled = btnBrowse.Enabled = !b; }
    static JavaScriptSerializer Js() { var j = new JavaScriptSerializer(); j.MaxJsonLength = int.MaxValue; return j; }
    string GameDir { get { return txtPath.Text.Trim().Trim('"'); } }
    string LuaDir { get { return Path.Combine(GameDir, @"cvs\res\lua"); } }
    string Xdf { get { return Path.Combine(LuaDir, "LuaScripts.xdf"); } }
    bool GameOk() { return GameDir.Length > 0 && File.Exists(Xdf); }

    void LoadCfg()
    {
        try { if (File.Exists(CfgFile)) cfg = Js().Deserialize<Dictionary<string, object>>(File.ReadAllText(CfgFile)); } catch { }
        object p; if (cfg.TryGetValue("gamePath", out p) && p != null) txtPath.Text = (string)p;
    }
    void SaveCfg()
    {
        try { Directory.CreateDirectory(AppDir); cfg["gamePath"] = GameDir; File.WriteAllText(CfgFile, Js().Serialize(cfg)); } catch { }
    }
    string Cfg(string k) { object o; return cfg.TryGetValue(k, out o) && o != null ? o.ToString() : null; }

    static string FindGameDir()
    {
        var c = new List<string>();
        try
        {
            string steam = null;
            foreach (var key in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", @"HKEY_CURRENT_USER\Software\Valve\Steam" })
            {
                var v = Registry.GetValue(key, "InstallPath", null) ?? Registry.GetValue(key, "SteamPath", null);
                if (v != null) { steam = v.ToString(); break; }
            }
            if (steam != null)
            {
                var libs = new List<string> { steam };
                var vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                foreach (var l in libs) c.Add(Path.Combine(l, @"steamapps\common\Aniimo\Aniimo_Data"));
            }
        }
        catch { }
        c.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Aniimo\Aniimo_Data");
        foreach (var d in c) if (File.Exists(Path.Combine(d, @"cvs\res\lua\LuaScripts.xdf"))) return d;
        return null;
    }

    void Browse()
    {
        using (var fd = new FolderBrowserDialog { Description = "Seleziona la cartella Aniimo_Data del gioco" })
            if (fd.ShowDialog() == DialogResult.OK) { txtPath.Text = fd.SelectedPath; SaveCfg(); RefreshState(); }
    }

    void Play()
    {
        var root = Directory.GetParent(GameDir);
        var exe = root == null ? null : Path.Combine(root.FullName, "Aniimo.exe");
        if (exe != null && File.Exists(exe)) { Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = root.FullName }); Close(); }
        else MessageBox.Show("Non trovo Aniimo.exe accanto alla cartella Aniimo_Data.", "Aniimo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    bool GameRunning() { return Process.GetProcessesByName("Aniimo").Length > 0; }

    // ---------- avvio / stato ----------
    async Task Startup()
    {
        LoadCfg();
        manifestUrl = DefaultManifestUrl;
        try { var f = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifest_url.txt"); if (File.Exists(f)) manifestUrl = File.ReadAllText(f).Trim(); } catch { }
        if (!GameOk())
        {
            var d = FindGameDir();
            if (d != null) { txtPath.Text = d; SaveCfg(); }
        }
        Busy(true); Status("Controllo aggiornamenti...");
        await FetchManifest();
        Busy(false); RefreshState();
    }

    async Task FetchManifest()
    {
        manifest = null;
        if (manifestUrl == null || manifestUrl.StartsWith("__")) { Status("Launcher non configurato (URL del manifest mancante)."); return; }
        try
        {
            string json = await Task.Run(() =>
            {
                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                {
                    wc.Headers["Cache-Control"] = "no-cache";
                    return wc.DownloadString(manifestUrl.StartsWith("http") ? manifestUrl + (manifestUrl.Contains("?") ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks : manifestUrl);
                }
            });
            manifest = Js().Deserialize<Dictionary<string, object>>(json);
        }
        catch (Exception ex) { Status("Impossibile contattare il server della patch: " + ex.Message); }
    }

    int InstalledVersion() { int v; return int.TryParse(Cfg("installedVersion"), out v) && Cfg("installedFor") == GameDir ? v : 0; }
    int RemoteVersion() { return manifest == null ? 0 : Convert.ToInt32(manifest["version"]); }

    void RefreshState()
    {
        bool ok = GameOk();
        btnPlay.Enabled = ok;
        btnRestore.Enabled = ok && File.Exists(Path.Combine(BackupDir, "LuaScripts.xdf"));
        int inst = InstalledVersion(), rem = RemoteVersion();
        lblVer.Text = manifest == null ? "Versione online: n/d" : "Versione online: v" + rem + " (" + manifest["date"] + ")   |   Installata: " + (inst > 0 ? "v" + inst : "nessuna");
        if (!ok) { btnInstall.Enabled = false; Status("Indica la cartella Aniimo_Data del gioco (quella che contiene cvs\\res\\lua\\LuaScripts.xdf)."); return; }
        btnInstall.Enabled = manifest != null;
        if (manifest == null) return;
        if (inst == rem) { btnInstall.Text = "Reinstalla"; Status("Patch aggiornata. Premi \"Avvia Aniimo\" e scegli la lingua \"Italiano\" nel menu."); }
        else { btnInstall.Text = inst == 0 ? "Installa" : "Aggiorna a v" + rem; Status(inst == 0 ? "Patch non ancora installata." : "Nuova versione disponibile: v" + rem + "."); }
    }

    // ---------- installazione ----------
    static string Sha256(byte[] b) { using (var s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLower(); }
    static byte[] Gunzip(byte[] b) { using (var ms = new MemoryStream(b)) using (var gz = new GZipStream(ms, CompressionMode.Decompress)) using (var o = new MemoryStream()) { gz.CopyTo(o); return o.ToArray(); } }

    async Task Install()
    {
        if (GameRunning()) { MessageBox.Show("Chiudi Aniimo prima di installare la patch.", "Aniimo", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        Busy(true); Progress(0);
        try
        {
            var payloads = new Dictionary<string, byte[]>();
            var pl = ((System.Collections.IEnumerable)manifest["payloads"]).Cast<Dictionary<string, object>>().ToList();
            for (int i = 0; i < pl.Count; i++)
            {
                var p = pl[i]; string id = (string)p["id"], rel = (string)p["url"];
                Status("Scarico " + id + " (" + (i + 1) + "/" + pl.Count + ")...");
                string url = new Uri(new Uri(manifestUrl), rel).ToString();
                byte[] raw = await Task.Run(() => { using (var wc = new WebClient()) return wc.DownloadData(url); });
                byte[] data = rel.EndsWith(".gz") ? Gunzip(raw) : raw;
                if (Sha256(data) != (string)p["sha256"]) throw new Exception("Verifica fallita per " + id + " (file corrotto o incompleto).");
                payloads[id] = data; Progress(10 + 40 * (i + 1) / pl.Count);
            }
            Status("Backup dei file originali..."); await Task.Run(() => Backup());
            Progress(60);
            Status("Applico la patch (può richiedere qualche secondo)...");
            await Task.Run(() => Apply(payloads));
            cfg["installedVersion"] = RemoteVersion().ToString(); cfg["installedFor"] = GameDir; SaveCfg();
            Progress(100); if (!cli) RefreshState();
            Status("Fatto! Patch v" + RemoteVersion() + " installata. Avvia Aniimo e scegli \"Italiano\" nel menu lingua.");
        }
        catch (Exception ex) { Status("Errore: " + ex.Message); if (!cli) MessageBox.Show(ex.ToString(), "Errore installazione", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        Busy(false);
    }

    List<string[]> Targets()   // [payload, file sciolto relativo a cvs\res\lua, voce dentro l'xdf]
    {
        var l = new List<string[]>();
        foreach (Dictionary<string, object> a in (System.Collections.IEnumerable)manifest["apply"])
            l.Add(new[] { (string)a["payload"], a.ContainsKey("loose") ? (string)a["loose"] : null, a.ContainsKey("xdfEntry") ? (string)a["xdfEntry"] : null });
        return l;
    }

    void Backup()
    {
        Directory.CreateDirectory(BackupDir);
        string mark = Path.Combine(BackupDir, "for.txt");
        if (File.Exists(mark) && File.ReadAllText(mark) == GameDir && File.Exists(Path.Combine(BackupDir, "LuaScripts.xdf"))) return; // backup gia' fatto
        File.Copy(Xdf, Path.Combine(BackupDir, "LuaScripts.xdf"), true);
        foreach (var t in Targets()) if (t[1] != null)
        {
            var src = Path.Combine(LuaDir, t[1]);
            if (File.Exists(src)) { var dst = Path.Combine(BackupDir, "loose", t[1]); Directory.CreateDirectory(Path.GetDirectoryName(dst)); File.Copy(src, dst, true); }
        }
        File.WriteAllText(mark, GameDir);
    }

    void Apply(Dictionary<string, byte[]> payloads)
    {
        var t = Targets();
        foreach (var x in t) if (x[1] != null)
        {
            var dst = Path.Combine(LuaDir, x[1]); Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.WriteAllBytes(dst, payloads[x[0]]);
        }
        using (var za = ZipFile.Open(Xdf, ZipArchiveMode.Update))
            foreach (var x in t) if (x[2] != null)
            {
                var old = za.GetEntry(x[2]); if (old != null) old.Delete();
                var e = za.CreateEntry(x[2], CompressionLevel.Optimal);
                using (var s = e.Open()) s.Write(payloads[x[0]], 0, payloads[x[0]].Length);
            }
    }

    async Task Restore()
    {
        if (GameRunning()) { MessageBox.Show("Chiudi Aniimo prima di ripristinare.", "Aniimo", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        if (MessageBox.Show("Ripristinare i file del gioco com'erano prima dell'installazione con questo launcher?", "Aniimo", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Busy(true);
        try
        {
            await Task.Run(() =>
            {
                File.Copy(Path.Combine(BackupDir, "LuaScripts.xdf"), Xdf, true);
                var lb = Path.Combine(BackupDir, "loose");
                if (Directory.Exists(lb)) foreach (var f in Directory.GetFiles(lb, "*", SearchOption.AllDirectories)) File.Copy(f, Path.Combine(LuaDir, f.Substring(lb.Length + 1)), true);
            });
            cfg["installedVersion"] = "0"; SaveCfg(); RefreshState(); Status("File originali ripristinati.");
        }
        catch (Exception ex) { Status("Errore: " + ex.Message); }
        Busy(false);
    }
}
