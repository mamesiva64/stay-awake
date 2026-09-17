using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("StayAwake")]
[assembly: AssemblyDescription("Prevent idle sleep on Windows")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

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

interface IAwakePower { bool Set(bool awake, bool display); }
sealed class WindowsAwakePower : IAwakePower
{
    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);
    public bool Set(bool awake, bool display)
    {
        uint flags = 0x80000000;
        if (awake) flags |= 1u | (display ? 2u : 0u);
        return SetThreadExecutionState(flags) != 0;
    }
}

class MainForm : Form
{
    static readonly int[] Minutes = { 0, 30, 60, 120, 240, 480 };
    readonly LidSettings lid;
    readonly IAwakePower power;
    readonly Func<string, bool> confirm;
    readonly Action<string> reportError;
    readonly Panel status = new Panel();
    readonly Label heading = new Label();
    readonly Label subtitle = new Label();
    internal readonly Button Toggle = new Button();
    internal readonly ComboBox Duration = new ComboBox();
    internal readonly CheckBox Display = new CheckBox();
    internal readonly CheckBox Lid = new CheckBox();
    internal readonly Label LidDescription = new Label();
    readonly Label remaining = new Label();
    readonly System.Windows.Forms.Timer tick = new System.Windows.Forms.Timer();
    bool awake = true;
    internal DateTime? Deadline { get; private set; }
    int ticks;
    public MainForm() : this(new WindowsLidPower(), new WindowsAwakePower(), null, null) { }
    internal MainForm(ILidPower lidPower, IAwakePower awakePower, Func<string, bool> confirmation, Action<string> error)
    {
        lid = new LidSettings(lidPower);
        power = awakePower;
        confirm = confirmation ?? delegate(string message) {
            return MessageBox.Show(this, message, Ui.T("OSの電源設定を変更", "Change Windows power settings"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        };
        reportError = error ?? delegate(string message) {
            MessageBox.Show(this, message, "StayAwake", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };
        Font = new Font(Ui.Japanese ? "Yu Gothic UI" : "Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(600, 490);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(246, 248, 250);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        status.SetBounds(0, 0, 600, 100);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        heading.SetBounds(20, 20, 560, 38);
        heading.Font = new Font(Font.FontFamily, 19F, FontStyle.Bold);
        heading.TextAlign = ContentAlignment.MiddleCenter;
        heading.ForeColor = Color.White;
        subtitle.SetBounds(20, 60, 560, 28);
        subtitle.TextAlign = ContentAlignment.MiddleCenter;
        subtitle.ForeColor = Color.White;
        status.Controls.Add(heading); status.Controls.Add(subtitle); Controls.Add(status);

        Toggle.SetBounds(28, 120, 544, 48);
        Toggle.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
        Toggle.UseVisualStyleBackColor = true;
        Toggle.Click += delegate { awake = !awake; SetDeadline(); ApplyAwake(); UpdateUi(); };
        Controls.Add(Toggle);

        AddLabel(Ui.T("タイマーで解除：", "Turn off after:"), 28, 185, 144, 28);
        Duration.SetBounds(174, 184, 126, 28);
        Duration.DropDownStyle = ComboBoxStyle.DropDownList;
        Duration.Items.AddRange(new object[] {
            Ui.T("無期限", "Never"), Ui.T("30分", "30 minutes"), Ui.T("1時間", "1 hour"),
            Ui.T("2時間", "2 hours"), Ui.T("4時間", "4 hours"), Ui.T("8時間", "8 hours") });
        Duration.SelectedIndex = 0;
        Duration.SelectedIndexChanged += delegate { SetDeadline(); UpdateUi(); };
        Controls.Add(Duration);
        remaining.SetBounds(314, 186, 258, 28);
        remaining.ForeColor = Color.FromArgb(87, 96, 106);
        Controls.Add(remaining);

        Label options = AddLabel(Ui.T("オプション", "Options"), 28, 232, 544, 28);
        options.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
        Display.SetBounds(28, 266, 544, 32);
        Display.Text = Ui.T("画面もつけたままにする（動画視聴、監視画面など）", "Keep the screen on (videos, monitoring, etc.)");
        Display.Checked = true;
        Display.CheckedChanged += delegate { ApplyAwake(); UpdateUi(); };
        Controls.Add(Display);

        Lid.SetBounds(28, 308, 544, 46);
        Lid.Text = Ui.T("ふたを閉じても動作を続ける（ノートPC用 ※電源設定を変更）",
            "Keep running when the lid is closed\n(laptops; changes Windows power settings)");
        Lid.ThreeState = true;
        Lid.AutoCheck = false;
        Lid.Click += delegate { ChangeLid(); };
        Controls.Add(Lid);
        LidDescription.SetBounds(48, 358, 524, 62);
        LidDescription.ForeColor = Color.FromArgb(87, 96, 106);
        Controls.Add(LidDescription);
        AddLabel(Ui.T("終了するとスリープ抑止は解除されます。\nふたの電源設定は、タイマー解除・アプリ終了後も残ります。",
            "Closing the app releases idle-sleep prevention.\nLid settings remain after the timer ends or the app closes."), 28, 433, 544, 48)
            .ForeColor = Color.FromArgb(87, 96, 106);

        tick.Interval = 1000;
        tick.Tick += delegate { Pulse(DateTime.UtcNow); if (++ticks % 3 == 0) RefreshLid(); };
        Activated += delegate { RefreshLid(); };
        ApplyAwake(); RefreshLid(); UpdateUi(); tick.Start();
    }
    Label AddLabel(string text, int x, int y, int width, int height)
    {
        var label = new Label { Text = text };
        label.SetBounds(x, y, width, height); Controls.Add(label); return label;
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
            reportError(Ui.T("スリープ抑止を設定できませんでした。", "Could not set sleep prevention."));
        }
    }
    void UpdateUi()
    {
        status.BackColor = awake ? Color.FromArgb(35, 134, 54) : Color.FromArgb(110, 118, 129);
        heading.Text = awake ? Ui.T("スリープを防止しています", "Keeping your PC awake") : Ui.T("通常のスリープ設定です", "Normal sleep settings");
        subtitle.Text = awake ? (Display.Checked ? Ui.T("画面もつけたままにします", "The screen stays on too") : Ui.T("画面はOSの設定に従います", "The screen follows Windows settings"))
            : Ui.T("無操作によるスリープを許可しています", "Idle sleep is allowed");
        Toggle.Text = awake ? Ui.T("スリープ抑止を解除する", "Turn off sleep prevention") : Ui.T("スリープを防止する", "Prevent sleep");
        remaining.Text = !awake ? "" : Deadline.HasValue
            ? Ui.T("残り ", "Remaining ") + TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((Deadline.Value - DateTime.UtcNow).TotalSeconds))).ToString(@"hh\:mm\:ss")
            : Ui.T("手動で解除するまで", "Until turned off");
        Text = "StayAwake — " + Ui.T("スリープを防止", "Prevent sleep");
    }
    internal void RefreshLid()
    {
        try
        {
            LidState state = lid.Read();
            Lid.Enabled = true;
            Lid.CheckState = state.Ac == 0 && state.Dc == 0 ? CheckState.Checked
                : (state.Ac == 0 || state.Dc == 0 ? CheckState.Indeterminate : CheckState.Unchecked);
            LidDescription.Text = Ui.T("OSの設定：電源接続時 ", "Windows: Plugged in — ") + Ui.ActionName(state.Ac)
                + Ui.T(" ／ バッテリー時 ", " / On battery — ") + Ui.ActionName(state.Dc)
                + "\n" + Ui.T("変更前の設定は保存しません。", "Previous settings are not saved.");
        }
        catch (Exception)
        {
            Lid.Enabled = false; Lid.CheckState = CheckState.Indeterminate;
            LidDescription.Text = Ui.T("OSのふたの設定を取得できません。\n非対応のPC、または権限・ポリシーの制限が考えられます。",
                "Lid settings are unavailable. This PC may not support them,\nor access may be restricted by permissions or policy.");
        }
    }
    internal void ChangeLid()
    {
        try
        {
            LidState state = lid.Read();
            bool enable = !(state.Ac == 0 && state.Dc == 0);
            string message = Ui.T("OSの設定を変更することになります。問題ないですか？", "This will change Windows power settings. Do you want to continue?")
                + "\n\n" + Ui.T("現在：電源接続時 ", "Current: Plugged in — ") + Ui.ActionName(state.Ac)
                + Ui.T(" ／ バッテリー時 ", " / On battery — ") + Ui.ActionName(state.Dc)
                + "\n\n" + (enable
                    ? Ui.T("ふたを閉じたときの動作を、電源接続時・バッテリー時とも『何もしない』にします。",
                        "Set the lid-close action to 'Do nothing' for both plugged-in and battery power.")
                    : Ui.T("ふたを閉じたときの動作を、電源接続時・バッテリー時とも『スリープ』にします。",
                        "Set the lid-close action to 'Sleep' for both plugged-in and battery power."))
                + "\n\n" + Ui.T("変更前の設定は保存しません。変更はタイマー解除・アプリ終了後も残ります。",
                    "Previous settings will not be saved. Changes remain after the timer ends or the app closes.");
            if (confirm(message)) lid.Set(state, enable);
        }
        catch (Exception ex)
        {
            reportError(ex.Message + "\n\n" + Ui.T(
                "一部のみ変更された可能性があります。OSの現在値を再取得します。権限や会社のポリシーも確認してください。",
                "Settings may have changed partially. Current Windows values will be refreshed. Check permissions or company policy."));
        }
        finally { RefreshLid(); }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel) { tick.Stop(); power.Set(false, false); }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tick.Dispose(); power.Set(false, false); }
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
                MessageBox.Show(Ui.T("StayAwake は既に起動しています。", "StayAwake is already running."), "StayAwake");
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            GC.KeepAlive(mutex);
        }
    }
}
