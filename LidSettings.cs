using System;
using System.Runtime.InteropServices;

// This service only reads/writes Windows settings. It never stores a backup on disk.
interface ILidPower
{
    Guid Active();
    uint Read(Guid plan, bool battery);
    void Write(Guid plan, bool battery, uint value);
    void Activate(Guid plan);
}

sealed class WindowsLidPower : ILidPower
{
    static Guid subgroup = new Guid("4f971e89-eebd-4455-a8de-9e59040e7347");
    static Guid setting = new Guid("5ca83367-6e45-459f-a27b-476b1d01c936");
    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr plan);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root, ref Guid plan, ref Guid sub, ref Guid key, out uint value);
    [DllImport("powrprof.dll")] static extern uint PowerReadDCValueIndex(IntPtr root, ref Guid plan, ref Guid sub, ref Guid key, out uint value);
    [DllImport("powrprof.dll")] static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid plan, ref Guid sub, ref Guid key, uint value);
    [DllImport("powrprof.dll")] static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid plan, ref Guid sub, ref Guid key, uint value);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid plan);
    static void Check(uint result)
    {
        if (result != 0) throw new InvalidOperationException(Ui.T(
            "Windows の電源設定を操作できませんでした。エラー: ",
            "Windows could not access the power setting. Error: ") + result);
    }
    public Guid Active()
    {
        IntPtr p;
        Check(PowerGetActiveScheme(IntPtr.Zero, out p));
        try { return (Guid)Marshal.PtrToStructure(p, typeof(Guid)); }
        finally { LocalFree(p); }
    }
    public uint Read(Guid plan, bool battery)
    {
        uint value;
        Check(battery ? PowerReadDCValueIndex(IntPtr.Zero, ref plan, ref subgroup, ref setting, out value)
                      : PowerReadACValueIndex(IntPtr.Zero, ref plan, ref subgroup, ref setting, out value));
        return value;
    }
    public void Write(Guid plan, bool battery, uint value)
    {
        Check(battery ? PowerWriteDCValueIndex(IntPtr.Zero, ref plan, ref subgroup, ref setting, value)
                      : PowerWriteACValueIndex(IntPtr.Zero, ref plan, ref subgroup, ref setting, value));
    }
    public void Activate(Guid plan) { Check(PowerSetActiveScheme(IntPtr.Zero, ref plan)); }
}

sealed class LidState
{
    public readonly Guid Plan;
    public readonly uint Ac, Dc;
    public LidState(Guid plan, uint ac, uint dc) { Plan = plan; Ac = ac; Dc = dc; }
    public bool SameAs(LidState other) { return other != null && Plan == other.Plan && Ac == other.Ac && Dc == other.Dc; }
}

sealed class LidSettings
{
    readonly ILidPower power;
    public LidSettings(ILidPower power) { this.power = power; }
    public LidState Read()
    {
        Guid plan = power.Active();
        var state = new LidState(plan, power.Read(plan, false), power.Read(plan, true));
        if (power.Active() != plan) throw new InvalidOperationException(Ui.T(
            "電源プランが切り替わりました。もう一度お試しください。",
            "The power plan changed. Please try again."));
        return state;
    }
    public void Set(LidState confirmed, bool keepRunning)
    {
        // The confirmation applies only to the plan and values the user actually saw.
        if (!Read().SameAs(confirmed)) throw new InvalidOperationException(Ui.T(
            "確認中にOSの設定が変わりました。表示を確認してもう一度操作してください。",
            "Windows settings changed during confirmation. Review the current values and try again."));
        uint value = keepRunning ? 0u : 1u;
        // No saved previous state or automatic restore. On partial failure, the UI re-reads Windows.
        power.Write(confirmed.Plan, false, value);
        power.Write(confirmed.Plan, true, value);
        if (power.Active() != confirmed.Plan) throw new InvalidOperationException(Ui.T(
            "変更中に電源プランが切り替わりました。変更対象のプランと現在の設定をWindowsで確認してください。",
            "The power plan changed while applying settings. Check the affected plan in Windows."));
        power.Activate(confirmed.Plan);
        LidState actual = Read();
        if (actual.Plan != confirmed.Plan || actual.Ac != value || actual.Dc != value)
            throw new InvalidOperationException(Ui.T("設定の反映を確認できませんでした。", "The setting could not be verified."));
    }
}
