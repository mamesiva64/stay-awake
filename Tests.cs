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
    public bool FailRead, FailDc, IgnoreWrite, Hibernate = true;
    public Guid Active() { return Plan; }
    public bool CanHibernate() { return Hibernate; }
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
    public int DisplayOffs;
    public bool Set(bool awake, bool display) { Awake = awake; Display = display; return !Fail; }
    public void DisplayOff(IntPtr window) { DisplayOffs++; }
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
            service.Set(state, false, 0);
            Check(fake.Ac == 0 && fake.Dc == 2 && fake.Activations == 1, "Change plugged-in only; battery untouched");
            service.Set(service.Read(), true, 3);
            Check(fake.Ac == 0 && fake.Dc == 3, "Change battery separately to any Windows action");
            service.Set(service.Read(), false, 1);
            Check(fake.Ac == 1 && fake.Dc == 3, "Set Sleep for one power source only");
            state = service.Read(); fake.Plan = Guid.NewGuid(); int writes = fake.Writes;
            MustFail(delegate { service.Set(state, false, 0); }, "Reject a changed power plan");
            Check(fake.Writes == writes, "No writes after stale plan confirmation");
            state = service.Read(); fake.Dc = 2;
            MustFail(delegate { service.Set(state, false, 0); }, "Reject changed OS values during confirmation");
            fake.FailDc = true;
            MustFail(delegate { service.Set(service.Read(), true, 0); }, "Report write failure");
            Check(service.Read().Ac == 1 && service.Read().Dc == 2, "Failed write leaves both power sources as Windows has them");
            fake.FailDc = false; fake.IgnoreWrite = true;
            MustFail(delegate { service.Set(service.Read(), false, 0); }, "Verify settings after applying");

            var errors = new List<string>();
            fake = new FakeLidPower(); var awake = new FakeAwakePower();
            bool accept = false; string prompt = "";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            using (var form = new MainForm(fake, awake, delegate(string text) { prompt = text; return accept; }, errors.Add, delegate { return true; }))
            {
                Check(!form.Display.Checked && awake.Awake && !awake.Display, "Keep-screen-on is off by default");
                Check(fake.Writes == 0 && MainForm.Selected(form.LidAc) == 1 && MainForm.Selected(form.LidDc) == 2,
                    "Startup shows each power source's Windows value; never auto-modifies lid settings");
                Check(form.LidAc.Items.Count == 4 && form.LidAc.Items[0].ToString() == "何もしない" && form.LidAc.Items[3].ToString() == "シャットダウン",
                    "Offer the same four actions as Windows");
                form.ChangeLid(false, 1);
                Check(fake.Writes == 0 && prompt == "", "Choosing the current value asks nothing and writes nothing");
                form.LidAc.SelectedIndex = 0; form.ChangeLid(false, 0);
                Check(fake.Writes == 0 && MainForm.Selected(form.LidAc) == 1
                    && prompt.Contains("カバーを閉じたときの動作（電源接続時）") && prompt.Contains("現在：スリープ") && prompt.Contains("変更後：何もしない"),
                    "No puts the list back to the Windows value; prompt uses Windows wording");
                accept = true; form.ChangeLid(false, 0);
                Check(MainForm.Selected(form.LidAc) == 0 && fake.Ac == 0 && fake.Dc == 2, "Plugged-in change affects only the plugged-in setting");
                form.ChangeLid(true, 3);
                Check(MainForm.Selected(form.LidDc) == 3 && fake.Ac == 0 && fake.Dc == 3
                    && prompt.Contains("バッテリー駆動時") && prompt.Contains("変更後：シャットダウン"), "Battery change affects only the battery setting");
                fake.Ac = 2; fake.Dc = 1; form.RefreshLid();
                Check(MainForm.Selected(form.LidAc) == 2 && MainForm.Selected(form.LidDc) == 1, "Reflect Windows changes made outside the app");
                fake.FailRead = true; form.RefreshLid();
                Check(!form.LidAc.Enabled && !form.LidDc.Enabled, "Unavailable settings disable lid editing");
                fake.FailRead = false; form.RefreshLid();
                Check(form.LidAc.Enabled && form.LidDc.Enabled, "Recover automatically when OS settings become readable");
                form.ToggleScreenOff();
                for (int i = 0; i < 4; i++) form.ScreenOffTick();
                Check(awake.DisplayOffs == 0 && form.ScreenOff.Text.Contains("1"), "Screen-off counts down before acting");
                form.ScreenOffTick();
                Check(awake.DisplayOffs == 1 && form.ScreenOffCountdown == 0 && awake.Awake, "Screen turns off after 5 seconds; sleep prevention stays on");
                form.ToggleScreenOff(); form.ToggleScreenOff();
                for (int i = 0; i < 5; i++) form.ScreenOffTick();
                Check(awake.DisplayOffs == 1, "Clicking again cancels screen-off");
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
            using (var form = new MainForm(fake, awake, delegate(string text) { prompt = text; return false; }, errors.Add, delegate { return true; }))
            {
                form.ChangeLid(false, 0);
                Check(form.Text.Contains("Prevent sleep") && prompt.Contains("When I close the lid (plugged in)")
                    && prompt.Contains("previous setting is not saved") && form.ExitMessage().Contains("lid-close setting stays"),
                    "English window and confirmations");
            }
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            fake = new FakeLidPower(); awake = new FakeAwakePower();
            bool allowExit = false; string exitPrompt = "";
            using (var form = new MainForm(fake, awake, delegate { return false; }, errors.Add, delegate(string text) { exitPrompt = text; return allowExit; }))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-3000, -3000);
                form.Show(); form.Close();
                Check(!form.IsDisposed && awake.Awake && exitPrompt.Contains("スリープ防止は解除") && exitPrompt.Contains("バッテリー駆動時：休止状態"),
                    "Closing asks first; Cancel keeps the app running");
                allowExit = true; form.Close();
                Check(form.IsDisposed && !awake.Awake && fake.Writes == 0, "OK closes and releases prevention without touching lid settings");
            }
            fake = new FakeLidPower { Hibernate = false };
            using (var form = new MainForm(fake, new FakeAwakePower(), delegate { return false; }, errors.Add, delegate { return true; }))
                Check(form.LidAc.Items.Count == 3 && form.LidDc.Items.Count == 4 && MainForm.Selected(form.LidDc) == 2,
                    "Hide Hibernate when unavailable, but still show it when Windows is set to it");
            fake = new FakeLidPower(); awake = new FakeAwakePower { Fail = true };
            using (var form = new MainForm(fake, awake, delegate { return false; }, errors.Add, delegate { return true; }))
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
            using (var form = new MainForm(new WindowsLidPower(), new FakeAwakePower(), delegate { return false; }, delegate(string error) { throw new Exception(error); }, delegate { return true; }))
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
