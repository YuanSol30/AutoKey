using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class AutoKey : Form
{
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }
    [StructLayout(LayoutKind.Explicit)] struct UNION { [FieldOffset(0)] public KEYBDINPUT keyboard; [FieldOffset(0)] public MOUSEINPUT mouse; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int x, y; public uint data, flags, time; public UIntPtr extra; }
    [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    readonly ComboBox key = new ComboBox();
    readonly NumericUpDown interval = new NumericUpDown(), delay = new NumericUpDown(), limit = new NumericUpDown();
    readonly Button start = new Button(), stop = new Button();
    readonly Label status = new Label(), shortcuts = new Label();
    readonly Timer timer = new Timer();
    readonly Stopwatch clock = new Stopwatch();
    readonly NumericUpDown hold = new NumericUpDown();
    INPUT heldInput; bool held; long releaseAt; IntPtr target;
    bool active; long next, sent; ushort selected; bool extended; bool startHotkey, stopHotkey;

    public AutoKey()
    {
        Text = "AutoKey v2 — Keyboard Repeater"; ClientSize = new Size(480, 510);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 251);
        Label heading = LabelAt("Automatic key presses", 24, 20, 430, 34);
        heading.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        LabelAt("Sends a key to whichever window is active.", 24, 61, 430, 26);
        LabelAt("Keyboard key", 24, 109, 210, 27);
        key.SetBounds(250, 106, 202, 30); key.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string name in new string[] { "Space", "Enter", "Tab", "Backspace", "Escape", "Up", "Down", "Left", "Right", "Home", "End", "PageUp", "PageDown", "Insert", "Delete" }) key.Items.Add(name);
        for (char c='A'; c<='Z'; c++) key.Items.Add(c.ToString());
        for (int i=0; i<=9; i++) key.Items.Add(i.ToString());
        for (int i=1; i<=12; i++) if (i!=8 && i!=9) key.Items.Add("F"+i);
        key.SelectedIndex=0; Controls.Add(key);
        AddNumber("Interval (seconds)", interval, 153, 0.05m, 86400, 1, 2);
        AddNumber("Start countdown (seconds)", delay, 200, 1, 60, 3, 0);
        AddNumber("Press limit (0 = unlimited)", limit, 247, 0, 1000000, 0, 0);
        start.Text = "Start  •  F8"; start.SetBounds(24, 300, 208, 44);
        start.BackColor = Color.FromArgb(35, 93, 220); start.ForeColor = Color.White; start.FlatStyle=FlatStyle.Flat;
        stop.Text = "Stop  •  F9"; stop.SetBounds(244, 300, 208, 44); stop.Enabled=false;
        Controls.Add(start); Controls.Add(stop);
        status.SetBounds(24, 360, 428, 40); status.Text="Ready. Choose a key and interval."; Controls.Add(status);
        shortcuts.SetBounds(24, 407, 428, 44); shortcuts.Font = new Font("Segoe UI", 9); Controls.Add(shortcuts);
        AddNumber("Hold key (milliseconds)", hold, 291, 20, 5000, 100, 0);
        start.Top+=50; stop.Top+=50; status.Top+=50; shortcuts.Top+=50;
        start.Click += delegate { Begin(); }; stop.Click += delegate { End("Stopped."); };
        timer.Interval=15; timer.Tick += Tick;
        FormClosing += delegate { End("Stopped."); };
    }
    Label LabelAt(string text, int x, int y, int w, int h) { Label l=new Label(); l.Text=text; l.SetBounds(x,y,w,h); Controls.Add(l); return l; }
    void AddNumber(string label, NumericUpDown n, int y, decimal min, decimal max, decimal value, int decimals) {
        LabelAt(label,24,y+3,225,30); n.SetBounds(250,y,202,30); n.Minimum=min; n.Maximum=max; n.DecimalPlaces=decimals; n.Increment=decimals>0 ? 0.05m : 1; n.Value=value; Controls.Add(n);
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        startHotkey=RegisterHotKey(Handle,1,0x4000,(uint)Keys.F8);
        stopHotkey=RegisterHotKey(Handle,2,0x4000,(uint)Keys.F9);
        shortcuts.Text=stopHotkey ? (startHotkey ? "F8 starts / stops. F9 always stops, even in another app." : "F8 unavailable. Use Start; F9 stops from any app.") : "F9 is in use by another app. Close that app and reopen AutoKey.";
        start.Enabled=stopHotkey;
    }
    protected override void OnHandleDestroyed(EventArgs e) { UnregisterHotKey(Handle,1); UnregisterHotKey(Handle,2); base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x0312) { if(m.WParam.ToInt32()==2 || active) End("Stopped."); else Begin(); }
        base.WndProc(ref m);
    }
    static ushort KeyCode(string name) { if(name.Length==1) return (ushort)name[0]; return (ushort)(Keys)Enum.Parse(typeof(Keys),name); }
    void Begin() {
        if(active || !stopHotkey) return;
        selected=KeyCode(key.Text);
        if(MapVirtualKey(selected,0)==0) { status.Text="This key has no scan code on your keyboard."; return; }
        extended=selected>=33 && selected<=46;
        sent=0; next=(long)(delay.Value*1000); active=true;
        key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=start.Enabled=false; stop.Enabled=true;
        clock.Restart(); timer.Start(); status.Text="Starting in " + delay.Value + " seconds. Switch to your target app.";
    }
    void End(string message) {
        ReleaseKey();
        active=false; timer.Stop(); clock.Stop(); key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=true;
        start.Enabled=stopHotkey; stop.Enabled=false; status.Text=message+"  Presses sent: "+sent;
    }
    void ReleaseKey() {
        if(!held) return;
        INPUT up=heldInput; up.data.keyboard.flags|=2;
        if(SendInput(1,new INPUT[]{up},Marshal.SizeOf(typeof(INPUT)))==1) held=false;
    }
    void Tick(object sender, EventArgs e) {
        if(!active) return;
        long now=clock.ElapsedMilliseconds;
        if(held) {
            if(now<releaseAt && GetForegroundWindow()==target) return;
            ReleaseKey();
            if(held) { End("Key release failed. Press and release the selected key manually."); return; }
            if(limit.Value>0 && sent>=(long)limit.Value) { End("Completed."); return; }
            return;
        }
        if(now<next) { if(sent==0) status.Text="Starting in "+Math.Ceiling((next-now)/1000.0)+" seconds. Switch to your target app."; return; }
        if(GetForegroundWindow()==Handle) { status.Text="Paused while AutoKey is active. Switch to your target app."; return; }
        INPUT down=new INPUT(); down.type=1; down.data.keyboard.scan=(ushort)MapVirtualKey(selected,0); down.data.keyboard.flags=8u | (extended ? 1u : 0u);
        target=GetForegroundWindow();
        uint result=SendInput(1,new INPUT[]{down},Marshal.SizeOf(typeof(INPUT)));
        if(result!=1) { End("Windows rejected input. Check the target's permissions."); return; }
        heldInput=down; held=true; releaseAt=now+(long)hold.Value;
        sent++; next=now+Math.Max((long)(interval.Value*1000),(long)hold.Value+20);
        status.Text="Running: "+key.Text+" every "+interval.Value+" s. Presses sent: "+sent;
    }
    [STAThread] public static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--self-test") {
            return Marshal.SizeOf(typeof(INPUT))==(IntPtr.Size==8 ? 40 : 28) && KeyCode("Space")==32 && KeyCode("Enter")==13 && KeyCode("A")==65 && KeyCode("0")==48 && KeyCode("F12")==123 && MapVirtualKey(32,0)==57 && MapVirtualKey(13,0)==28 ? 0 : 1;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new AutoKey()); return 0;
    }
}

