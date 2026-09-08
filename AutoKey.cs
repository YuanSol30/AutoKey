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
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
    readonly ComboBox key = new ComboBox();
    readonly NumericUpDown interval = new NumericUpDown(), delay = new NumericUpDown(), limit = new NumericUpDown();
    readonly Button start = new Button(), stop = new Button();
    readonly Label status = new Label(), shortcuts = new Label();
    readonly Timer timer = new Timer();
    readonly NumericUpDown hold = new NumericUpDown();
    readonly ComboBox mode = new ComboBox(), mouseButton = new ComboBox();
    readonly Label inputHint = new Label(), inputLabel = new Label();
    bool mouseMode;
    uint mouseDown;
    INPUT heldInput; bool held;
    bool active; long sent; ushort selected; bool extended; bool startHotkey, stopHotkey;
    readonly System.Threading.ManualResetEvent cancel = new System.Threading.ManualResetEvent(false);
    System.Threading.Thread worker;
    volatile string progress="Ready.";
    volatile bool finished;

    public AutoKey()
    {
        Text = "AutoKey 4"; ClientSize = new Size(560, 690);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        AutoScaleMode=AutoScaleMode.Dpi;
        BackColor = Color.White; ForeColor=Color.FromArgb(35,35,35);
        Label heading = LabelAt("AutoKey", 28, 22, 360, 49);
        heading.Font = new Font("Segoe UI", 24, FontStyle.Regular);
        LabelAt("Keyboard and mouse, simplified.", 30, 81, 480, 27).ForeColor=Color.DimGray;
        LabelAt("Action",30,130,480,26).Font=new Font("Segoe UI",10,FontStyle.Bold);
        LabelAt("Action type",30,173,240,28);
        mode.SetBounds(290,170,238,30); mode.DropDownStyle=ComboBoxStyle.DropDownList;
        mode.Items.AddRange(new object[]{"Keyboard press","Mouse click"}); mode.SelectedIndex=0; Controls.Add(mode);
        inputLabel.Text="Keyboard key"; inputLabel.SetBounds(30,218,240,28); Controls.Add(inputLabel);
        key.SetBounds(290,215,238,30); key.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string name in new string[] { "Space", "Enter", "Tab", "Backspace", "Escape", "Up", "Down", "Left", "Right", "Home", "End", "PageUp", "PageDown", "Insert", "Delete" }) key.Items.Add(name);
        for (char c='A'; c<='Z'; c++) key.Items.Add(c.ToString());
        for (int i=0; i<=9; i++) key.Items.Add(i.ToString());
        for (int i=1; i<=12; i++) if (i!=8 && i!=9) key.Items.Add("F"+i);
        key.SelectedIndex=0; Controls.Add(key);
        mouseButton.Bounds=key.Bounds; mouseButton.DropDownStyle=ComboBoxStyle.DropDownList;
        mouseButton.Items.AddRange(new object[]{"Left button","Right button","Middle button"}); mouseButton.SelectedIndex=0; mouseButton.Visible=false; Controls.Add(mouseButton);
        inputHint.SetBounds(30,256,498,45); inputHint.ForeColor=Color.DimGray; inputHint.Font=new Font("Segoe UI",9);
        inputHint.Text="Keys go to the active window. F8 and F9 are reserved."; Controls.Add(inputHint);
        mode.SelectedIndexChanged+=delegate { key.Visible=mode.SelectedIndex==0; mouseButton.Visible=!key.Visible; inputLabel.Text=key.Visible ? "Keyboard key" : "Mouse button"; inputHint.Text=key.Visible ? "Keys go to the active window. F8 and F9 are reserved." : "Clicks happen at your cursor. Move it onto the target after Start."; };
        LabelAt("Timing",30,304,480,26).Font=new Font("Segoe UI",10,FontStyle.Bold);
        AddNumber("Interval (milliseconds)", interval, 343, 1, 86400000, 100, 0);
        AddNumber("Start countdown (seconds)", delay, 387, 1, 60, 3, 0);
        AddNumber("Action limit (0 = unlimited)", limit, 431, 0, 1000000, 0, 0);
        AddNumber("Hold duration (ms, maximum)", hold, 475, 1, 5000, 30, 0);
        start.Text = "Start   /   F8"; start.SetBounds(30, 532, 242, 48);
        start.BackColor = Color.FromArgb(35,35,35); start.ForeColor = Color.White; start.FlatStyle=FlatStyle.Flat; start.FlatAppearance.BorderSize=0;
        stop.Text = "Stop   /   F9"; stop.SetBounds(286, 532, 242, 48); stop.Enabled=false;
        stop.FlatStyle=FlatStyle.Flat; stop.FlatAppearance.BorderColor=Color.LightGray; stop.BackColor=Color.White;
        Controls.Add(start); Controls.Add(stop);
        status.SetBounds(30, 596, 498, 44); status.Text="Ready when you are."; Controls.Add(status);
        shortcuts.SetBounds(30, 648, 498, 36); shortcuts.Font = new Font("Segoe UI", 8); shortcuts.ForeColor=Color.DimGray; Controls.Add(shortcuts);
        foreach(ComboBox c in new ComboBox[]{key,mode,mouseButton}) { c.FlatStyle=FlatStyle.Flat; c.BackColor=Color.WhiteSmoke; c.ForeColor=ForeColor; }
        start.Click += delegate { Begin(); }; stop.Click += delegate { End("Stopped."); };
        timer.Interval=50; timer.Tick += Tick;
        FormClosing += delegate { End("Stopped."); };
    }
    Label LabelAt(string text, int x, int y, int w, int h) { Label l=new Label(); l.Text=text; l.SetBounds(x,y,w,h); Controls.Add(l); return l; }
    void AddNumber(string label, NumericUpDown n, int y, decimal min, decimal max, decimal value, int decimals) {
        LabelAt(label,30,y+3,275,30); n.SetBounds(310,y,218,30); n.Minimum=min; n.Maximum=max; n.DecimalPlaces=decimals; n.Increment=decimals>0 ? 0.05m : 1; n.Value=value; n.BackColor=Color.White; n.ForeColor=ForeColor; n.BorderStyle=BorderStyle.FixedSingle; Controls.Add(n);
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
        if(held) { status.Text="Release the previous input with F9 before starting again."; return; }
        mouseMode=mode.SelectedIndex==1; mouseDown=MouseFlag(mouseButton.SelectedIndex);
        selected=KeyCode(key.Text);
        if(!mouseMode && MapVirtualKey(selected,0)==0) { status.Text="This key has no scan code on your keyboard."; return; }
        extended=selected>=33 && selected<=46;
        sent=0; active=true;
        mode.Enabled=mouseButton.Enabled=key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=start.Enabled=false; stop.Enabled=true;
        int period=(int)interval.Value, duration=EffectiveHold((int)hold.Value,period), countdown=(int)delay.Value*1000;
        long maximum=(long)limit.Value; IntPtr ownWindow=Handle;
        INPUT down=new INPUT(); down.type=1; down.data.keyboard.scan=(ushort)MapVirtualKey(selected,0); down.data.keyboard.flags=8u | (extended ? 1u : 0u);
        if(mouseMode) { down=new INPUT(); down.data.mouse.flags=mouseDown; }
        cancel.Reset(); finished=false; progress="Starting in " + delay.Value + " seconds. Switch to your target app.";
        worker=new System.Threading.Thread(delegate() { RunInput(down,ownWindow,period,duration,countdown,maximum); }); worker.IsBackground=true; worker.Start(); timer.Start(); status.Text=progress;
    }
    void End(string message) {
        cancel.Set(); if(worker!=null && worker.IsAlive) worker.Join(); worker=null;
        ReleaseKey();
        active=false; timer.Stop(); mode.Enabled=mouseButton.Enabled=key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=true;
        start.Enabled=stopHotkey && !held; stop.Enabled=held; status.Text=held ? "Release failed. Press and release the key/button manually, then F9." : message+"  Actions sent: "+sent;
    }
    void ReleaseKey() {
        if(!held) return;
        INPUT up=ReleaseInput(heldInput);
        if(SendInput(1,new INPUT[]{up},Marshal.SizeOf(typeof(INPUT)))==1) held=false;
    }
    static uint MouseFlag(int index) { return index==0 ? 2u : index==1 ? 8u : 32u; }
    static INPUT ReleaseInput(INPUT down) { if(down.type==0) down.data.mouse.flags <<= 1; else down.data.keyboard.flags|=2; return down; }
    static int EffectiveHold(int requested,int period) { return Math.Min(requested,Math.Max(1,period/2)); }
    static bool IsOwnTarget(IntPtr foreground,IntPtr underPointer,IntPtr own,bool mouse) { return foreground==own || (mouse && underPointer==own); }
    void RunInput(INPUT down,IntPtr own,int period,int duration,int countdown,long maximum) {
        bool resolution=timeBeginPeriod(1)==0;
        try {
            Stopwatch timing=Stopwatch.StartNew(); long due=countdown;
            while(!cancel.WaitOne(0)) {
                long now=timing.ElapsedMilliseconds;
                if(now<due) {
                    if(sent==0) progress="Starting in "+Math.Ceiling((due-now)/1000.0)+" seconds. Focus your target.";
                    cancel.WaitOne((int)Math.Min(10,due-now)); continue;
                }
                Point pointer; IntPtr foreground=GetForegroundWindow();
                IntPtr underPointer=IntPtr.Zero;
                if(down.type==0) {
                    if(!GetCursorPos(out pointer)) { progress="Unable to read cursor position."; break; }
                    underPointer=GetAncestor(WindowFromPoint(pointer),2);
                }
                if(IsOwnTarget(foreground,underPointer,own,down.type==0)) {
                    progress="Paused. Focus the target app and move your cursor there.";
                    cancel.WaitOne(20); continue;
                }
                if(SendInput(1,new INPUT[]{down},Marshal.SizeOf(typeof(INPUT)))!=1) { progress="Windows rejected input. Check the target application's permissions."; break; }
                heldInput=down; held=true;
                cancel.WaitOne(duration);
                ReleaseKey();
                if(held) { progress="Input release failed. Press and release the key/button manually."; break; }
                sent++;
                progress="Running  |  "+period+" ms interval  |  "+sent+" actions sent";
                if(maximum>0 && sent>=maximum) { progress="Completed. "+sent+" actions sent."; break; }
                // Do not burst to catch up after a slow or paused application.
                due=Math.Max(now+period,timing.ElapsedMilliseconds+1);
            }
        } finally { ReleaseKey(); if(resolution) timeEndPeriod(1); finished=true; }
    }
    void Tick(object sender, EventArgs e) {
        if(!active) return;
        status.Text=progress;
        if(finished) { string result=progress; End("Stopped."); if(!held) status.Text=result; }
    }
    [STAThread] public static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--self-test") {
            IntPtr own=new IntPtr(100), game=new IntPtr(200);
            if(IsOwnTarget(game,game,own,true) || !IsOwnTarget(game,own,own,true) || !IsOwnTarget(own,game,own,false)) return 4;
            if(EffectiveHold(30,10)!=5 || EffectiveHold(30,100)!=30 || EffectiveHold(30,1)!=1) return 5;
            for(int i=0;i<3;i++) { INPUT down=new INPUT(); down.type=0; down.data.mouse.flags=MouseFlag(i); INPUT up=ReleaseInput(down); if(up.type!=0 || up.data.mouse.flags!=(i==0 ? 4u : i==1 ? 16u : 64u)) return 2; }
            INPUT keyboard=new INPUT(); keyboard.type=1; keyboard.data.keyboard.flags=9; keyboard.data.keyboard.scan=72;
            INPUT released=ReleaseInput(keyboard); if(released.data.keyboard.flags!=11 || released.data.keyboard.scan!=72) return 3;
            return Marshal.SizeOf(typeof(INPUT))==(IntPtr.Size==8 ? 40 : 28) && KeyCode("Space")==32 && KeyCode("Enter")==13 && KeyCode("A")==65 && KeyCode("0")==48 && KeyCode("F12")==123 && MapVirtualKey(32,0)==57 && MapVirtualKey(13,0)==28 ? 0 : 1;
        }
        if(args.Length==2 && args[0]=="--preview") {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(AutoKey form=new AutoKey()) { form.mode.SelectedIndex=1; form.ShowInTaskbar=false; form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-10000,-10000); form.Show(); Application.DoEvents(); using(Bitmap bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save(args[1]); } }
            return 0;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new AutoKey()); return 0;
    }
}



