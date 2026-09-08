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
    readonly ComboBox mode = new ComboBox(), mouseButton = new ComboBox();
    readonly Label inputHint = new Label(), inputLabel = new Label();
    bool mouseMode;
    uint mouseDown;
    INPUT heldInput; bool held; long releaseAt; IntPtr target;
    bool active; long next, sent; ushort selected; bool extended; bool startHotkey, stopHotkey;

    public AutoKey()
    {
        Text = "AutoKey 3 — Keyboard & Mouse"; ClientSize = new Size(560, 690);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 10);
        AutoScaleMode=AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(18, 23, 34); ForeColor=Color.FromArgb(233,238,248);
        Label heading = LabelAt("AutoKey", 28, 22, 360, 49);
        heading.Font = new Font("Segoe UI", 28, FontStyle.Bold);
        Label badge=LabelAt("KEYBOARD + MOUSE",350,40,185,25); badge.ForeColor=Color.FromArgb(115,211,193); badge.Font=new Font("Segoe UI",9,FontStyle.Bold);
        LabelAt("Your repetitive actions, on a timer.", 30, 81, 480, 27).ForeColor=Color.FromArgb(159,173,195);
        LabelAt("01   CHOOSE YOUR ACTION",30,130,480,26).ForeColor=Color.FromArgb(115,211,193);
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
        inputHint.SetBounds(30,256,498,45); inputHint.ForeColor=Color.FromArgb(159,173,195); inputHint.Font=new Font("Segoe UI",9);
        inputHint.Text="Keys go to the active window. F8 and F9 are reserved."; Controls.Add(inputHint);
        mode.SelectedIndexChanged+=delegate { key.Visible=mode.SelectedIndex==0; mouseButton.Visible=!key.Visible; inputLabel.Text=key.Visible ? "Keyboard key" : "Mouse button"; inputHint.Text=key.Visible ? "Keys go to the active window. F8 and F9 are reserved." : "Clicks happen at your cursor. Move it onto the target after Start."; };
        LabelAt("02   SET YOUR TIMING",30,304,480,26).ForeColor=Color.FromArgb(115,211,193);
        AddNumber("Interval (seconds)", interval, 343, 0.05m, 86400, 1, 2);
        AddNumber("Start countdown (seconds)", delay, 387, 1, 60, 3, 0);
        AddNumber("Action limit (0 = unlimited)", limit, 431, 0, 1000000, 0, 0);
        AddNumber("Hold duration (milliseconds)", hold, 475, 20, 5000, 100, 0);
        start.Text = "Start   /   F8"; start.SetBounds(30, 532, 242, 48);
        start.BackColor = Color.FromArgb(115,211,193); start.ForeColor = Color.FromArgb(18,23,34); start.FlatStyle=FlatStyle.Flat; start.FlatAppearance.BorderSize=0;
        stop.Text = "Stop   /   F9"; stop.SetBounds(286, 532, 242, 48); stop.Enabled=false;
        stop.FlatStyle=FlatStyle.Flat; stop.FlatAppearance.BorderColor=Color.FromArgb(70,83,105); stop.BackColor=Color.FromArgb(30,39,55);
        Controls.Add(start); Controls.Add(stop);
        status.SetBounds(30, 596, 498, 44); status.Text="Ready when you are."; Controls.Add(status);
        shortcuts.SetBounds(30, 648, 498, 36); shortcuts.Font = new Font("Segoe UI", 8); shortcuts.ForeColor=Color.FromArgb(159,173,195); Controls.Add(shortcuts);
        foreach(ComboBox c in new ComboBox[]{key,mode,mouseButton}) { c.FlatStyle=FlatStyle.Flat; c.BackColor=Color.FromArgb(30,39,55); c.ForeColor=ForeColor; }
        start.Click += delegate { Begin(); }; stop.Click += delegate { End("Stopped."); };
        timer.Interval=15; timer.Tick += Tick;
        FormClosing += delegate { End("Stopped."); };
    }
    Label LabelAt(string text, int x, int y, int w, int h) { Label l=new Label(); l.Text=text; l.SetBounds(x,y,w,h); Controls.Add(l); return l; }
    void AddNumber(string label, NumericUpDown n, int y, decimal min, decimal max, decimal value, int decimals) {
        LabelAt(label,30,y+3,260,30); n.SetBounds(310,y,218,30); n.Minimum=min; n.Maximum=max; n.DecimalPlaces=decimals; n.Increment=decimals>0 ? 0.05m : 1; n.Value=value; n.BackColor=Color.FromArgb(30,39,55); n.ForeColor=ForeColor; n.BorderStyle=BorderStyle.FixedSingle; Controls.Add(n);
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
        sent=0; next=(long)(delay.Value*1000); active=true;
        mode.Enabled=mouseButton.Enabled=key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=start.Enabled=false; stop.Enabled=true;
        clock.Restart(); timer.Start(); status.Text="Starting in " + delay.Value + " seconds. Switch to your target app.";
    }
    void End(string message) {
        ReleaseKey();
        active=false; timer.Stop(); clock.Stop(); mode.Enabled=mouseButton.Enabled=key.Enabled=interval.Enabled=delay.Enabled=limit.Enabled=hold.Enabled=true;
        start.Enabled=stopHotkey && !held; stop.Enabled=held; status.Text=held ? "Release failed. Press and release the key/button manually, then F9." : message+"  Actions sent: "+sent;
    }
    void ReleaseKey() {
        if(!held) return;
        INPUT up=ReleaseInput(heldInput);
        if(SendInput(1,new INPUT[]{up},Marshal.SizeOf(typeof(INPUT)))==1) held=false;
    }
    static uint MouseFlag(int index) { return index==0 ? 2u : index==1 ? 8u : 32u; }
    static INPUT ReleaseInput(INPUT down) { if(down.type==0) down.data.mouse.flags <<= 1; else down.data.keyboard.flags|=2; return down; }
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
        if(mouseMode && Bounds.Contains(Cursor.Position)) { status.Text="Paused. Move your cursor outside AutoKey to click."; return; }
        INPUT down=new INPUT(); down.type=1; down.data.keyboard.scan=(ushort)MapVirtualKey(selected,0); down.data.keyboard.flags=8u | (extended ? 1u : 0u);
        if(mouseMode) { down=new INPUT(); down.type=0; down.data.mouse.flags=mouseDown; }
        target=GetForegroundWindow();
        uint result=SendInput(1,new INPUT[]{down},Marshal.SizeOf(typeof(INPUT)));
        if(result!=1) { End("Windows rejected input. Check the target's permissions."); return; }
        heldInput=down; held=true; releaseAt=now+(long)hold.Value;
        sent++; next=now+Math.Max((long)(interval.Value*1000),(long)hold.Value+20);
        status.Text="Running: "+(mouseMode ? mouseButton.Text : key.Text)+" every "+interval.Value+" s. Actions: "+sent;
    }
    [STAThread] public static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--self-test") {
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


