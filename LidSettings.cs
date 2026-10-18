using System;
using System.Runtime.InteropServices;

// This service only reads/writes Windows settings. It never stores a backup on disk.
interface ILidPower
{
    Guid Active();
    uint Read(Guid plan, bool battery);
    void Write(Guid plan, bool battery, uint value);
    void Activate(Guid plan);
    bool CanHibernate();
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
    [DllImport("powrprof.dll")] [return: MarshalAs(UnmanagedType.U1)] static extern bool GetPwrCapabilities(byte[] capabilities);
    static void Check(uint result)
    {
        if (result != 0) throw new InvalidOperationException(Ui.T(
            "Windowsの電源設定を読み書きできませんでした。エラーコード: ",
            "Could not read or change the Windows power setting. Error code: ") + result);
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
    // SYSTEM_POWER_CAPABILITIES: byte 6 = SystemS4, byte 8 = HiberFilePresent. Windows hides Hibernate without both.
    // If the query fails, offer Hibernate rather than hide a valid choice.
    public bool CanHibernate()
    {
        var capabilities = new byte[76];
        return !GetPwrCapabilities(capabilities) || (capabilities[6] != 0 && capabilities[8] != 0);
    }
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
    // Changes only one power source (plugged in or battery); the other is left as Windows has it.
    // value: 0 = Do nothing, 1 = Sleep, 2 = Hibernate, 3 = Shut down.
    public void Set(LidState confirmed, bool battery, uint value)
    {
        // The confirmation applies only to the plan and values the user actually saw.
        if (!Read().SameAs(confirmed)) throw new InvalidOperationException(Ui.T(
            "確認中にWindowsの設定が変わりました。表示を確認して、もう一度操作してください。",
            "Windows settings changed during confirmation. Review the current values and try again."));
        // No saved previous state or automatic restore. On failure, the UI re-reads Windows.
        power.Write(confirmed.Plan, battery, value);
        if (power.Active() != confirmed.Plan) throw new InvalidOperationException(Ui.T(
            "変更中に電源プランが切り替わりました。Windowsの電源オプションで、変更したプランと現在の設定を確認してください。",
            "The power plan changed while applying settings. Check the affected plan in Windows."));
        power.Activate(confirmed.Plan);
        LidState actual = Read();
        if (actual.Plan != confirmed.Plan || (battery ? actual.Dc : actual.Ac) != value)
            throw new InvalidOperationException(Ui.T("変更が反映されたことを確認できませんでした。", "Could not confirm that the change was applied."));
    }
}
