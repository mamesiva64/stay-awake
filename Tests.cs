using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;

sealed class FakeLidPower : ILidPower
{
    public Guid Plan = Guid.NewGuid();
    public uint Ac = 1, Dc = 2;
    public int Writes, Activations;
    public bool FailRead, FailDc, IgnoreWrite;
    public Guid Active() { return Plan; }
    public uint Read(Guid plan, bool battery)
    {
        if (FailRead) throw new InvalidOperationException("Read denied");
        return battery ? Dc : Ac;
    }
    public void Write(Guid plan, bool battery, uint value)
    {
        if (battery && FailDc) throw new InvalidOperationException("Write denied");
        Writes++;
        if (!IgnoreWrite) { if (battery) Dc = value; else Ac = value; }
    }
    public void Activate(Guid plan) { Activations++; }
}
sealed class FakeAwakePower : IAwakePower
{
    public bool Awake, Display, Fail;
    public bool Set(bool awake, bool display) { Awake = awake; Display = display; return !Fail; }
}

static class Tests
{
    static int count;
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        count++; Console.WriteLine("PASS: " + name);
    }
    static void MustFail(Action action, string name)
    {
        try { action(); } catch (InvalidOperationException) { Check(true, name); return; }
        throw new Exception("Expected failure: " + name);
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 2 && args[0] == "--screenshots") { Screenshots(args[1]); return 0; }
            var fake = new FakeLidPower(); var service = new LidSettings(fake);
            LidState state = service.Read();
            Check(state.Ac == 1 && state.Dc == 2 && fake.Writes == 0, "Read the OS without writing");
            service.Set(state, true);
            Check(fake.Ac == 0 && fake.Dc == 0 && fake.Activations == 1, "Enable both power modes");
            service.Set(service.Read(), false);
            Check(fake.Ac == 1 && fake.Dc == 1, "Disable explicitly sets Sleep, not a remembered value");
            state = service.Read(); fake.Plan = Guid.NewGuid(); int writes = fake.Writes;
            MustFail(delegate { service.Set(state, true); }, "Reject a changed power plan");
            Check(fake.Writes == writes, "No writes after stale plan confirmation");
            state = service.Read(); fake.Dc = 3;
            MustFail(delegate { service.Set(state, true); }, "Reject changed OS values during confirmation");
            fake.FailDc = true;
            MustFail(delegate { service.Set(service.Read(), true); }, "Report partial write failure");
            Check(service.Read().Ac == 0 && service.Read().Dc == 3, "Read actual partial result; do not pretend it was restored");
            fake.FailDc = false; fake.IgnoreWrite = true;
            MustFail(delegate { service.Set(service.Read(), true); }, "Verify settings after applying");

            var errors = new List<string>();
            fake = new FakeLidPower(); var awake = new FakeAwakePower();
            bool accept = false; string prompt = "";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            using (var form = new MainForm(fake, awake, delegate(string text) { prompt = text; return accept; }, errors.Add))
            {
                Check(fake.Writes == 0 && form.Lid.CheckState == CheckState.Unchecked, "Startup reflects OS; never auto-modifies lid settings");
                form.ChangeLid();
                Check(fake.Writes == 0 && prompt.Contains("何もしない"), "Cancel enable leaves OS unchanged");
                accept = true; form.ChangeLid();
                Check(form.Lid.Checked && fake.Ac == 0 && fake.Dc == 0, "Confirmed enable updates checkbox from OS");
                accept = false; form.ChangeLid();
                Check(form.Lid.Checked && prompt.Contains("『スリープ』"), "Cancel disable preserves current setting");
                accept = true; form.ChangeLid();
                Check(!form.Lid.Checked && fake.Ac == 1 && fake.Dc == 1, "Confirmed disable sets Sleep");
                fake.Ac = 0; fake.Dc = 2; form.RefreshLid();
                Check(form.Lid.CheckState == CheckState.Indeterminate && form.LidDescription.Text.Contains("休止状態"), "Show mixed AC/battery OS settings");
                fake.FailRead = true; form.RefreshLid();
                Check(!form.Lid.Enabled, "Unavailable settings disable lid editing");
                fake.FailRead = false; form.RefreshLid();
                Check(form.Lid.Enabled, "Recover automatically when OS settings become readable");
                writes = fake.Writes;
                form.Duration.SelectedIndex = 1;
                form.Pulse(DateTime.UtcNow.AddHours(1));
                Check(!awake.Awake && fake.Writes == writes, "Timer expiry releases idle-sleep prevention without touching lid settings");
                form.Duration.SelectedIndex = 2;
                Check(!awake.Awake && !form.Deadline.HasValue, "Timer selection does not silently reactivate disabled prevention");
                form.Dispose();
                Check(fake.Writes == writes && !awake.Awake, "Exit releases execution request without restoring OS settings");
            }
            Check(errors.Count == 0, "Normal UI flows have no errors");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR");
            Check(Ui.T("日本語", "English") == "English", "Non-Japanese languages fall back to English");
            fake = new FakeLidPower(); awake = new FakeAwakePower();
            using (var form = new MainForm(fake, awake, delegate(string text) { prompt = text; return false; }, errors.Add))
            {
                form.ChangeLid();
                Check(form.Text.Contains("Prevent sleep") && prompt.Contains("Previous settings will not be saved"), "English window and confirmation");
            }
            fake = new FakeLidPower(); awake = new FakeAwakePower { Fail = true };
            using (var form = new MainForm(fake, awake, delegate { return false; }, errors.Add))
                Check(!awake.Awake && errors.Count == 1, "Do not claim prevention is enabled after execution-state failure");
            Console.WriteLine("Passed " + count + " checks. No real Windows settings were changed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
    static void Screenshots(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string language in new[] { "ja", "en" })
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(language);
            using (var form = new MainForm(new WindowsLidPower(), new FakeAwakePower(), delegate { return false; }, delegate(string error) { throw new Exception(error); }))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-3000, -3000);
                form.Show(); Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                    bitmap.Save(Path.Combine(folder, "stay-awake-" + language + ".png"));
                }
                form.Close();
            }
        }
        Console.WriteLine("Captured Japanese and English UI using read-only OS settings.");
    }
}
