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

    static readonly Color Bg = Color.FromArgb(14, 18, 28), Panel2 = Color.FromArgb(24, 31, 46), Accent = Color.FromArgb(52, 152, 255), AccentHi = Color.FromArgb(92, 178, 255);
    const int BannerH = 230;
    Banner banner = new Banner();
    Image bannerImg;

    // Pannello con double buffering: immagine del gioco (cover-fit) + sfumatura + titolo.
    class Banner : Panel
    {
        public Image Img;
        public Banner() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var r = ClientRectangle;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(r, Color.FromArgb(40, 90, 170), Color.FromArgb(120, 60, 160), 35f)) g.FillRectangle(br, r);
            if (Img != null)
            {
                float k = Math.Max((float)r.Width / Img.Width, (float)r.Height / Img.Height);
                float w = Img.Width * k, h = Img.Height * k;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(Img, (r.Width - w) / 2, (r.Height - h) / 2, w, h);
            }
            // sfumatura scura in basso per far leggere il testo e raccordare col resto della finestra
            var fade = new Rectangle(0, r.Height / 4, r.Width, r.Height - r.Height / 4 + 1);
            using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(fade, Color.FromArgb(0, Bg), Color.FromArgb(255, Bg), 90f)) g.FillRectangle(br, fade);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var f1 = new Font("Segoe UI", 11f, FontStyle.Bold)) using (var f2 = new Font("Segoe UI Semibold", 26f, FontStyle.Bold))
            {
                g.DrawString("PATCH ITALIANA", f1, new SolidBrush(Color.FromArgb(220, AccentHi)), 30, r.Height - 118);
                g.DrawString("ANIIMO", f2, new SolidBrush(Color.FromArgb(120, 0, 0, 0)), 30, r.Height - 96);
                g.DrawString("ANIIMO", f2, Brushes.White, 28, r.Height - 98);
            }
        }
    }

    static void StyleButton(Button b, Color back, Color hover)
    {
        b.FlatStyle = FlatStyle.Flat; b.BackColor = back; b.ForeColor = Color.White; b.Cursor = Cursors.Hand;
        b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = hover; b.FlatAppearance.MouseDownBackColor = back;
        b.Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
    }

    Launcher()
    {
        Text = "Aniimo - Patch Italiana";
        ClientSize = new Size(760, 500);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg; ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        DoubleBuffered = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        banner.Location = new Point(0, 0); banner.Size = new Size(760, BannerH);
        lblVer.AutoSize = true; lblVer.BackColor = Color.Transparent; lblVer.ForeColor = Color.FromArgb(200, 220, 240);
        lblVer.Location = new Point(30, BannerH - 34); lblVer.Font = new Font("Segoe UI", 9.5f);
        banner.Controls.Add(lblVer);

        var lblP = new Label { Text = "Cartella Aniimo_Data del gioco", AutoSize = true, Location = new Point(30, BannerH + 6), ForeColor = Color.FromArgb(150, 165, 185), Font = new Font("Segoe UI", 9f) };
        txtPath.Location = new Point(32, BannerH + 30); txtPath.Width = 586; txtPath.Font = new Font("Segoe UI", 10.5f);
        txtPath.BackColor = Panel2; txtPath.ForeColor = Color.White; txtPath.BorderStyle = BorderStyle.FixedSingle;
        btnBrowse.Text = "Sfoglia..."; btnBrowse.Location = new Point(630, BannerH + 28); btnBrowse.Size = new Size(100, 29);
        lblStatus.Location = new Point(30, BannerH + 72); lblStatus.Size = new Size(700, 44); lblStatus.ForeColor = Color.FromArgb(215, 225, 238);
        prog.Location = new Point(32, BannerH + 122); prog.Size = new Size(698, 8); prog.Style = ProgressBarStyle.Continuous;
        btnInstall.Text = "Installa / Aggiorna"; btnInstall.Location = new Point(32, BannerH + 150); btnInstall.Size = new Size(260, 52);
        btnPlay.Text = "▶  Avvia Aniimo"; btnPlay.Location = new Point(304, BannerH + 150); btnPlay.Size = new Size(260, 52);
        btnRestore.Text = "Ripristina originale"; btnRestore.Location = new Point(576, BannerH + 150); btnRestore.Size = new Size(154, 52);
        StyleButton(btnBrowse, Color.FromArgb(44, 56, 78), Color.FromArgb(60, 76, 104));
        StyleButton(btnInstall, Color.FromArgb(32, 120, 210), AccentHi);
        StyleButton(btnPlay, Color.FromArgb(34, 150, 90), Color.FromArgb(52, 180, 112));
        StyleButton(btnRestore, Color.FromArgb(44, 56, 78), Color.FromArgb(60, 76, 104));
        btnBrowse.Font = btnRestore.Font = new Font("Segoe UI", 9.5f);
        // stato disabilitato leggibile (il tema Flat altrimenti lascia testo grigio su grigio)
        foreach (var b in new[] { btnBrowse, btnInstall, btnPlay, btnRestore })
            b.EnabledChanged += delegate { b.Cursor = b.Enabled ? Cursors.Hand : Cursors.Default; b.Invalidate(); };
        Controls.AddRange(new Control[] { banner, lblP, txtPath, btnBrowse, lblStatus, prog, btnInstall, btnPlay, btnRestore });

        LoadBannerLocal();
        btnBrowse.Click += delegate { Browse(); };
        btnInstall.Click += async delegate { await Install(); };
        btnRestore.Click += async delegate { await Restore(); };
        btnPlay.Click += delegate { Play(); };
        txtPath.Leave += delegate { SaveCfg(); RefreshState(); };
        Shown += async delegate { await Startup(); };
    }

    // ---------- immagine del gioco ----------
    // Ordine: risorsa incorporata (banner.jpg/png al momento della build) -> banner.* accanto all'exe -> copia in cache -> scaricata dal repo (banner.jpg accanto al manifest).
    static Image LoadImg(byte[] b) { try { return Image.FromStream(new MemoryStream(b)); } catch { return null; } }
    void SetBanner(Image img) { if (img == null) return; bannerImg = img; banner.Img = img; banner.Invalidate(); }

    void LoadBannerLocal()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (var n in asm.GetManifestResourceNames())
                if (n.StartsWith("banner", StringComparison.OrdinalIgnoreCase))
                    using (var s = asm.GetManifestResourceStream(n)) using (var ms = new MemoryStream()) { s.CopyTo(ms); SetBanner(LoadImg(ms.ToArray())); return; }
        }
        catch { }
        foreach (var dir in new[] { AppDomain.CurrentDomain.BaseDirectory, AppDir })
            foreach (var ext in new[] { "jpg", "png" })
            {
                try { var f = Path.Combine(dir, "banner." + ext); if (File.Exists(f)) { SetBanner(LoadImg(File.ReadAllBytes(f))); if (bannerImg != null) return; } } catch { }
            }
    }

    async Task LoadBannerRemote()
    {
        if (bannerImg != null || manifestUrl == null || !manifestUrl.StartsWith("http")) return;
        try
        {
            byte[] data = await Task.Run(() => { using (var wc = new WebClient()) return wc.DownloadData(new Uri(new Uri(manifestUrl), "banner.jpg")); });
            var img = LoadImg(data);
            if (img == null) return;
            SetBanner(img);
            try { Directory.CreateDirectory(AppDir); File.WriteAllBytes(Path.Combine(AppDir, "banner.jpg"), data); } catch { }
        }
        catch { }
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
        await LoadBannerRemote();
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
