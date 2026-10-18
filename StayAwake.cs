using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("StayAwake")]
[assembly: AssemblyDescription("Prevent idle sleep on Windows")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]

static class Ui
{
    public static bool Japanese { get { return Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "ja"; } }
    public static string T(string ja, string en) { return Japanese ? ja : en; }
    public static string ActionName(uint action)
    {
        switch (action)
        {
            case 0: return T("何もしない", "Do nothing");
            case 1: return T("スリープ", "Sleep");
            case 2: return T("休止状態", "Hibernate");
            case 3: return T("シャットダウン", "Shut down");
            default: return T("不明", "Unknown") + " (" + action + ")";
        }
    }
}

sealed class LidChoice
{
    public readonly uint Value;
    public LidChoice(uint value) { Value = value; }
    public override string ToString() { return Ui.ActionName(Value); }
}

interface IAwakePower { bool Set(bool awake, bool display); void DisplayOff(IntPtr window); }
sealed class WindowsAwakePower : IAwakePower
{
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    public bool Set(bool awake, bool display)
    {
        uint flags = 0x80000000;
        if (awake) flags |= 1u | (display ? 2u : 0u);
        return SetThreadExecutionState(flags) != 0;
    }
    // WM_SYSCOMMAND / SC_MONITORPOWER / 2 = off. Mouse or keyboard input turns the display back on.
    public void DisplayOff(IntPtr window) { SendMessage(window, 0x0112, (IntPtr)0xF170, (IntPtr)2); }
}

class MainForm : Form
{
    static readonly int[] Minutes = { 0, 30, 60, 120, 240, 480 };
    readonly LidSettings lid;
    readonly IAwakePower power;
    readonly Func<string, bool> confirm;
    readonly Func<string, bool> confirmExit;
    readonly Action<string> reportError;
    readonly Panel status = new Panel();
    readonly Label heading = new Label();
    readonly Label subtitle = new Label();
    internal readonly Button Toggle = new Button();
    internal readonly ComboBox Duration = new ComboBox();
    internal readonly Button ScreenOff = new Button();
    readonly Label screenOffHint = new Label();
    internal readonly CheckBox Display = new CheckBox();
    internal readonly ComboBox LidAc = new ComboBox();
    internal readonly ComboBox LidDc = new ComboBox();
    readonly Label lidStatus = new Label();
    readonly Label remaining = new Label();
    readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer screenOffTimer = new System.Windows.Forms.Timer();
    bool awake = true;
    bool changingLid;
    internal DateTime? Deadline { get; private set; }
    internal int ScreenOffCountdown { get; private set; }
    int ticks;
    public MainForm() : this(new WindowsLidPower(), new WindowsAwakePower(), null, null, null) { }
    internal MainForm(ILidPower lidPower, IAwakePower awakePower, Func<string, bool> confirmation, Action<string> error,
        Func<string, bool> exitConfirmation)
    {
        lid = new LidSettings(lidPower);
        power = awakePower;
        confirm = confirmation ?? delegate(string message) {
            return MessageBox.Show(this, message, Ui.T("Windowsの電源設定を変更", "Change Windows power settings"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        };
        confirmExit = exitConfirmation ?? delegate(string message) {
            return MessageBox.Show(this, message, "StayAwake", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information, MessageBoxDefaultButton.Button1) == DialogResult.OK;
        };
        reportError = error ?? delegate(string message) {
            MessageBox.Show(this, message, "StayAwake", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        Font = new Font(Ui.Japanese ? "Yu Gothic UI" : "Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(420, 372);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 248, 250);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        // Compact status band: state on the first line, screen behavior below it.
        status.SetBounds(0, 0, 420, 70);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        heading.SetBounds(16, 10, 388, 28);
        heading.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        heading.TextAlign = ContentAlignment.MiddleLeft;
        heading.ForeColor = Color.White;
        subtitle.SetBounds(16, 40, 388, 20);
        subtitle.TextAlign = ContentAlignment.MiddleLeft;
        subtitle.ForeColor = Color.White;
        status.Controls.Add(heading); status.Controls.Add(subtitle); Controls.Add(status);

        Toggle.SetBounds(16, 86, 388, 36);
        Toggle.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
        Toggle.UseVisualStyleBackColor = true;
        Toggle.Click += delegate { awake = !awake; SetDeadline(); ApplyAwake(); UpdateUi(); };
        Controls.Add(Toggle);

        AddLabel(this, Ui.T("自動で解除：", "Turn off after:"), 16, 138, 96, 24).TextAlign = ContentAlignment.MiddleLeft;
        Duration.SetBounds(112, 138, 110, 24);
        Duration.DropDownStyle = ComboBoxStyle.DropDownList;
        Duration.Items.AddRange(new object[] {
            Ui.T("しない", "Never"), Ui.T("30分後", "30 minutes"), Ui.T("1時間後", "1 hour"),
            Ui.T("2時間後", "2 hours"), Ui.T("4時間後", "4 hours"), Ui.T("8時間後", "8 hours") });
        Duration.SelectedIndex = 0;
        Duration.SelectedIndexChanged += delegate { SetDeadline(); UpdateUi(); };
        Controls.Add(Duration);
        remaining.SetBounds(232, 138, 172, 24);
        remaining.TextAlign = ContentAlignment.MiddleLeft;
        remaining.ForeColor = Color.FromArgb(87, 96, 106);
        Controls.Add(remaining);

        ScreenOff.SetBounds(16, 172, 150, 28);
        ScreenOff.UseVisualStyleBackColor = true;
        ScreenOff.Click += delegate { ToggleScreenOff(); };
        Controls.Add(ScreenOff);
        screenOffHint.SetBounds(176, 172, 228, 28);
        screenOffHint.TextAlign = ContentAlignment.MiddleLeft;
        screenOffHint.ForeColor = Color.FromArgb(87, 96, 106);
        Controls.Add(screenOffHint);

        // Options sit inside a frame; positions below are relative to the frame.
        var options = new GroupBox { Text = Ui.T("オプション", "Options") };
        options.SetBounds(16, 214, 388, 144);
        options.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        Controls.Add(options);
        Display.SetBounds(12, 24, 364, 24);
        Display.Text = Ui.T("画面もつけたままにする（動画視聴、監視画面など）", "Keep the screen on (videos, monitoring, etc.)");
        Display.Checked = false;
        Display.CheckedChanged += delegate { ApplyAwake(); UpdateUi(); };
        options.Controls.Add(Display);

        AddLabel(options, Ui.T("ノートPCを閉じたときの動作（Windowsの電源設定）",
            "When I close the lid (Windows power setting)"), 12, 56, 364, 20);
        bool hibernate = lidPower.CanHibernate();
        AddLidRow(options, LidAc, Ui.T("電源接続時", "Plugged in"), 80, false, hibernate);
        AddLidRow(options, LidDc, Ui.T("バッテリー駆動時", "On battery"), 110, true, hibernate);
        lidStatus.SetBounds(284, 80, 96, 54);
        lidStatus.ForeColor = Color.FromArgb(87, 96, 106);
        options.Controls.Add(lidStatus);
        // Only the frame caption is bold.
        foreach (Control control in options.Controls) control.Font = Font;

        tick.Interval = 1000;
        tick.Tick += delegate { Pulse(DateTime.UtcNow); if (++ticks % 3 == 0) RefreshLid(); };
        screenOffTimer.Interval = 1000;
        screenOffTimer.Tick += delegate { ScreenOffTick(); };
        Activated += delegate { RefreshLid(); };
        ApplyAwake(); RefreshLid(); UpdateUi(); tick.Start();
    }
    static Label AddLabel(Control parent, string text, int x, int y, int width, int height)
    {
        var label = new Label { Text = text };
        label.SetBounds(x, y, width, height); parent.Controls.Add(label); return label;
    }
    void AddLidRow(Control parent, ComboBox box, string text, int y, bool battery, bool hibernate)
    {
        AddLabel(parent, text, 24, y, 120, 24).TextAlign = ContentAlignment.MiddleLeft;
        box.SetBounds(148, y, 130, 24);
        box.DropDownStyle = ComboBoxStyle.DropDownList;
        // Same choices as Windows Power Options; Hibernate only when this PC supports it.
        foreach (uint value in new uint[] { 0, 1, 2, 3 })
            if (value != 2 || hibernate) box.Items.Add(new LidChoice(value));
        // SelectionChangeCommitted fires only for user changes, not when the app shows the Windows value.
        box.SelectionChangeCommitted += delegate {
            uint? value = Selected(box);
            if (value.HasValue) ChangeLid(battery, value.Value);
        };
        parent.Controls.Add(box);
    }
    internal static uint? Selected(ComboBox box)
    {
        var choice = box.SelectedItem as LidChoice;
        return choice == null ? (uint?)null : choice.Value;
    }
    void SetDeadline()
    {
        int minutes = Minutes[Math.Max(0, Duration.SelectedIndex)];
        Deadline = awake && minutes > 0 ? (DateTime?)DateTime.UtcNow.AddMinutes(minutes) : null;
    }
    internal void Pulse(DateTime now)
    {
        if (awake && Deadline.HasValue && now >= Deadline.Value)
        { awake = false; Deadline = null; ApplyAwake(); }
        UpdateUi();
    }
    void ApplyAwake()
    {
        if (!power.Set(awake, Display.Checked))
        {
            awake = false; Deadline = null;
            power.Set(false, false);
            reportError(Ui.T("スリープ防止を開始できませんでした。", "Could not start sleep prevention."));
        }
    }
    void UpdateUi()
    {
        status.BackColor = awake ? Color.FromArgb(35, 134, 54) : Color.FromArgb(110, 118, 129);
        heading.Text = awake ? Ui.T("スリープを防止しています", "Keeping your PC awake") : Ui.T("スリープを防止していません", "Sleep prevention is off");
        subtitle.Text = awake ? (Display.Checked ? Ui.T("画面もつけたままにします", "The screen stays on too") : Ui.T("画面はWindowsの設定どおりに消えます", "The screen turns off per Windows settings"))
            : Ui.T("Windowsの設定どおりにスリープします", "Your PC sleeps per Windows settings");
        Toggle.Text = awake ? Ui.T("スリープ防止を解除する", "Turn off sleep prevention") : Ui.T("スリープを防止する", "Prevent sleep");
        ScreenOff.Text = ScreenOffCountdown > 0
            ? Ui.T("画面を消します… " + ScreenOffCountdown, "Turning off in " + ScreenOffCountdown + "…")
            : Ui.T("5秒後に画面を消す", "Turn off screen in 5 s");
        screenOffHint.Text = ScreenOffCountdown > 0 ? Ui.T("もう一度クリックで中止", "Click again to cancel")
            : Ui.T("マウスかキーボードの操作で戻ります", "Mouse or keyboard wakes it");
        remaining.Text = awake && Deadline.HasValue
            ? Ui.T("残り ", "Remaining ") + TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((Deadline.Value - DateTime.UtcNow).TotalSeconds))).ToString(@"hh\:mm\:ss")
            : "";
        Text = "StayAwake — " + Ui.T("スリープを防止", "Prevent sleep");
    }
    internal void ToggleScreenOff()
    {
        ScreenOffCountdown = ScreenOffCountdown > 0 ? 0 : 5;
        if (ScreenOffCountdown > 0) screenOffTimer.Start(); else screenOffTimer.Stop();
        UpdateUi();
    }
    internal void ScreenOffTick()
    {
        if (ScreenOffCountdown == 0) return;
        if (--ScreenOffCountdown == 0) screenOffTimer.Stop();
        UpdateUi();
        if (ScreenOffCountdown == 0) power.DisplayOff(Handle);
    }
    // Periodic refresh; skipped while a list is open or a change is being confirmed so it never fights the user.
    internal void RefreshLid()
    {
        if (!changingLid && !LidAc.DroppedDown && !LidDc.DroppedDown) ReadLid();
    }
    void ReadLid()
    {
        try
        {
            LidState state = lid.Read();
            ShowLid(LidAc, state.Ac);
            ShowLid(LidDc, state.Dc);
            lidStatus.Text = "";
        }
        catch (Exception)
        {
            LidAc.Enabled = LidDc.Enabled = false;
            LidAc.SelectedIndex = LidDc.SelectedIndex = -1;
            lidStatus.Text = Ui.T("Windowsの設定を読み取れません", "Can't read the Windows setting");
        }
    }
    // Shows the Windows value as is. A value missing from the list (e.g. Hibernate while unavailable) is added.
    static void ShowLid(ComboBox box, uint action)
    {
        box.Enabled = true;
        if (Selected(box) == action) return;
        int index = 0;
        for (; index < box.Items.Count; index++)
        {
            uint value = ((LidChoice)box.Items[index]).Value;
            if (value == action) { box.SelectedIndex = index; return; }
            if (value > action) break;
        }
        box.Items.Insert(index, new LidChoice(action));
        box.SelectedIndex = index;
    }
    internal void ChangeLid(bool battery, uint value)
    {
        changingLid = true;
        try
        {
            LidState state = lid.Read();
            uint current = battery ? state.Dc : state.Ac;
            if (value == current) return;
            string modeJa = battery ? "バッテリー駆動時" : "電源接続時", modeEn = battery ? "on battery" : "plugged in";
            string message = Ui.T("Windowsの電源設定を変更します。よろしいですか？", "This will change a Windows power setting. Continue?")
                + "\n\n" + Ui.T("カバーを閉じたときの動作（" + modeJa + "）", "When I close the lid (" + modeEn + ")")
                + "\n" + Ui.T("　現在：", "   Current: ") + Ui.ActionName(current)
                + "\n" + Ui.T("　変更後：", "   New: ") + Ui.ActionName(value)
                + "\n\n" + Ui.T((battery ? "電源接続時" : "バッテリー駆動時") + "の設定は変更しません。",
                    "The " + (battery ? "plugged-in" : "on-battery") + " setting is not changed.")
                + "\n" + Ui.T("変更前の設定は保存されず、アプリを終了しても元に戻りません。",
                    "The previous setting is not saved, and closing the app does not restore it.");
            if (confirm(message)) lid.Set(state, battery, value);
        }
        catch (Exception ex)
        {
            reportError(ex.Message + "\n\n" + Ui.T(
                "表示をWindowsの現在の設定に更新します。変更できない場合は、権限や組織のポリシーを確認してください。",
                "The window now shows the current Windows settings. If changes keep failing, check permissions or organization policy."));
        }
        // Always re-read Windows: on No or failure this puts the list back to the actual value.
        finally { changingLid = false; ReadLid(); }
    }
    internal string ExitMessage()
    {
        string message = Ui.T("StayAwakeを終了しますか？", "Close StayAwake?") + "\n";
        if (awake) message += "\n" + Ui.T("・スリープ防止は解除されます。", "• Sleep prevention will stop.");
        message += "\n" + Ui.T("・カバーを閉じたときの動作は、今の設定のまま残ります。", "• The lid-close setting stays as it is.");
        try
        {
            LidState state = lid.Read();
            message += "\n" + Ui.T("　（電源接続時：", "   (Plugged in: ") + Ui.ActionName(state.Ac)
                + Ui.T(" ／ バッテリー駆動時：", " / On battery: ") + Ui.ActionName(state.Dc) + Ui.T("）", ")");
        }
        catch (Exception) { }
        return message;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Ask only when the user closes the window, not on Windows shutdown or sign-out.
        if (e.CloseReason == CloseReason.UserClosing && !confirmExit(ExitMessage())) e.Cancel = true;
        base.OnFormClosing(e);
        if (!e.Cancel) { tick.Stop(); screenOffTimer.Stop(); power.Set(false, false); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tick.Dispose(); screenOffTimer.Dispose(); power.Set(false, false); }
        base.Dispose(disposing);
    }
    [STAThread] static void Main(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg == "--lang=ja") Thread.CurrentThread.CurrentUICulture = new CultureInfo("ja-JP");
            if (arg == "--lang=en") Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
        }
        bool created;
        using (var mutex = new Mutex(true, "Local\\StayAwakeApp", out created))
        {
            if (!created)
            {
                MessageBox.Show(Ui.T("StayAwakeはすでに起動しています。", "StayAwake is already running."), "StayAwake");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            GC.KeepAlive(mutex);
        }
    }
}
