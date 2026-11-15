using System;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Reflection;
namespace CastleFightLauncher {
static class Program {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateHardLink(string file,string existing,IntPtr reserved);
    public static string Home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CastleFight");
    [STAThread]static int Main(string[] args){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);Application.EnableVisualStyles();
        try{
            if(args.Length>0&&args[0]=="--core-test"){
                var core=new UpdateCore(args[2],true);var release=core.Parse(File.ReadAllText(args[1]));var result=core.Apply(release,delegate(string text,float value){},CancellationToken.None);Console.WriteLine(new JavaScriptSerializer().Serialize(result));return 0;
            }
            if(args.Length>0&&args[0]=="--seed"){
                var core=new UpdateCore(Path.Combine(Home,"Game"));var release=core.Parse(File.ReadAllText(args[1]));Directory.CreateDirectory(core.Root);
                foreach(var row in release.windows.files){string target=core.Target(row.path),source=Path.Combine(args[2],row.path.Replace('/',Path.DirectorySeparatorChar));if(UpdateCore.Hash(source)!=row.sha256)throw new InvalidDataException("Seed hash mismatch");Directory.CreateDirectory(Path.GetDirectoryName(target));if(!File.Exists(target)&&!CreateHardLink(target,source,IntPtr.Zero))File.Copy(source,target);}
                core.Apply(release,delegate(string text,float value){},CancellationToken.None);return 0;
            }
            if(args.Length==2&&args[0]=="--auth-test"){
                Uri uri=new Uri(args[1]);if(!uri.IsLoopback||uri.Scheme!="ws")throw new InvalidOperationException("Test endpoint must be local");
                using(var form=new LauncherForm()){var result=Task.Run(delegate{return form.Authenticate(Environment.GetEnvironmentVariable("CF_QA_LOGIN"),Environment.GetEnvironmentVariable("CF_QA_PASSWORD"),args[1]);}).GetAwaiter().GetResult();if(!result.ContainsKey("token"))throw new Exception("Missing token");Console.WriteLine("LAUNCHER_AUTH_PASS");}return 0;
            }
            Application.Run(new LauncherForm(args.Length>=2&&args[0]=="--preview"?args[1]:null,args.Length>=3?args[2]:"ready"));return 0;
        }catch(Exception e){if(args.Length>0){Console.Error.WriteLine(e.GetBaseException().Message);return 1;}MessageBox.Show(e.Message,"Castle Fight");return 1;}
    }
}
static class SessionHandoff {
    internal static string Create(string home,bool guest,string token,string nickname){
        string folder=Path.Combine(home,"Launcher","Sessions");Directory.CreateDirectory(folder);string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");
        using(File.Create(path)){}
        var access=new FileSecurity();access.SetAccessRuleProtection(true,false);access.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));File.SetAccessControl(path,access);
        File.WriteAllText(path,new JavaScriptSerializer().Serialize(new{endpoint="wss://45-15-156-101.fatmetal.net/play",token=guest?"":token,nickname=guest?"Гость":nickname,guest=guest}));return path;
    }
}
sealed class LauncherForm:Form {
    const string Endpoint="wss://45-15-156-101.fatmetal.net/play";
    readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=1024*1024};readonly UpdateCore core=new UpdateCore(Path.Combine(Program.Home,"Game"));
    readonly Label status=new Label(),account=new Label();readonly ForgeProgress progress=new ForgeProgress();readonly ForgeButton play=new ForgeButton{Primary=true},guest=new ForgeButton(),check=new ForgeButton(),cancel=new ForgeButton(),login=new ForgeButton(),logout=new ForgeButton();readonly TextBox name=new TextBox(),password=new TextBox();
    CancellationTokenSource stopping;Release ready;string token="",nickname="";bool busy;
    Image backdrop;float ui=1;string phase="ПОДГОТОВКА К БИТВЕ";bool accountError;
    readonly ForgeButton minimize=new ForgeButton{Chrome=true,Symbol="minimize"},close=new ForgeButton{Chrome=true,Symbol="close"};
    readonly Label percent=new Label();Label loginCaption,passwordCaption;
    readonly ToolTip tips=new ToolTip{AutoPopDelay=15000,InitialDelay=500};
    [DllImport("user32.dll")]static extern bool ReleaseCapture();
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
    string AccountFile{get{return Path.Combine(Program.Home,"Launcher","account.dat");}}
    int U(float n){return (int)Math.Round(n*ui);}
    Rectangle R(int x,int y,int w,int h){return new Rectangle(U(x),U(y),U(w),U(h));}
    Font Body(float pixels,FontStyle style=FontStyle.Regular){return new Font("Segoe UI",pixels*ui,style,GraphicsUnit.Pixel);}
    Label LabelAt(string text,int x,int y,int w,int h,float pixels,Color color){var label=new Label{Text=text,Bounds=R(x,y,w,h),Font=Body(pixels),ForeColor=color,BackColor=Color.Transparent,AutoEllipsis=true};Controls.Add(label);return label;}
    public LauncherForm(string preview=null,string previewState="ready"){
        AutoScaleMode=AutoScaleMode.None;
        using(var graphics=CreateGraphics())ui=graphics.DpiX/96f;
        var area=Screen.FromControl(this).WorkingArea;ui=Math.Min(ui,Math.Min((area.Width-32)/1120f,(area.Height-32)/720f));
        if(preview!=null&&previewState=="compact")ui=.8f;
        if(preview!=null&&previewState=="dpi150")ui=1.5f;
        Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);Text="Castle Fight";ClientSize=new Size(U(1120),U(720));FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=LauncherTheme.Ink;ForeColor=LauncherTheme.Text;Font=Body(15);DoubleBuffered=true;
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherBackdrop"))if(source!=null)backdrop=new Bitmap(source);
        Style(minimize,"",1016,12,40,32);minimize.AccessibleName="Свернуть";minimize.TabStop=false;minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};tips.SetToolTip(minimize,"Свернуть");
        Style(close,"",1064,12,40,32);close.AccessibleName="Закрыть лаунчер";close.TabStop=false;close.Click+=delegate{Close();};tips.SetToolTip(close,"Закрыть");
        LabelAt("ВАШ АККАУНТ",772,116,280,24,17,LauncherTheme.Gold);
        LabelAt("Войдите для сетевой игры.\nИли отправляйтесь в бой гостем.",772,155,280,46,14,LauncherTheme.Muted);
        loginCaption=LabelAt("Логин",772,212,280,22,13,LauncherTheme.Muted);
        Input(name,772,238,280,"Логин",0);name.MaxLength=40;
        passwordCaption=LabelAt("Пароль",772,292,280,22,13,LauncherTheme.Muted);
        Input(password,772,318,280,"Пароль",1);password.UseSystemPasswordChar=true;password.MaxLength=128;
        Style(login,"Войти в аккаунт",772,378,280,42);login.TabIndex=2;login.Click+=async delegate{await Login();};
        Style(logout,"Сменить аккаунт",772,378,280,42);logout.TabIndex=2;logout.Click+=delegate{
            try{if(File.Exists(AccountFile))File.Delete(AccountFile);token=nickname="";accountError=false;account.Text="Можно войти в аккаунт или играть гостем.";RefreshAccount();name.Focus();}
            catch(Exception){accountError=true;account.Text="Не удалось выйти. Попробуйте ещё раз.";RefreshAccount();}
        };
        account.Bounds=R(772,436,280,54);account.Font=Body(13);account.BackColor=Color.Transparent;account.ForeColor=LauncherTheme.Muted;account.Text="Один аккаунт для лаунчера и игры.\nПароль не сохраняется.";account.AutoEllipsis=true;Controls.Add(account);
        status.Bounds=R(40,594,648,44);status.Font=Body(14);status.ForeColor=LauncherTheme.Muted;status.BackColor=Color.Transparent;status.Text="Проверяем обновления и целостность файлов…";status.AutoEllipsis=true;Controls.Add(status);
        progress.Bounds=R(40,649,648,8);Controls.Add(progress);
        percent.Bounds=R(622,564,66,25);percent.Text="0%";percent.TextAlign=ContentAlignment.MiddleRight;percent.Font=Body(16);percent.ForeColor=LauncherTheme.Gold;percent.BackColor=Color.Transparent;Controls.Add(percent);
        Style(check,"Проверить файлы",40,669,170,30);check.TabIndex=5;check.Click+=async delegate{await UpdateGame();};
        Style(cancel,"Отложить загрузку",226,669,188,30);cancel.TabIndex=6;cancel.Enabled=false;cancel.Visible=false;cancel.Click+=delegate{if(stopping!=null)stopping.Cancel();};
        Style(play,"ИГРАТЬ",756,566,324,62);play.Font=LauncherTheme.Heading(27*ui);play.TabIndex=3;play.Enabled=false;play.Click+=delegate{Launch(false);};
        Style(guest,"Играть гостем",756,640,324,42);guest.TabIndex=4;guest.Enabled=false;guest.Click+=delegate{Launch(true);};
        AcceptButton=play;
        tips.SetToolTip(guest,"Запустить без сетевого аккаунта. Сохранённый вход останется в лаунчере.");
        name.KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Login();}};
        password.KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Login();}};
        if(preview==null)LoadAccount();RefreshAccount();
        Shown+=async delegate{
            if(preview!=null){
                // Deterministic offline visual fixtures never read or write real account data in the image.
                token="";nickname="";name.Text="";password.Text="";accountError=false;account.Text="Один аккаунт для лаунчера и игры.\nПароль не сохраняется.";
                status.Text="Версия 0.31.0 установлена. Всё готово к новой битве.";phase="ИГРА ГОТОВА";progress.Value=1000;percent.Text="100%";play.Enabled=guest.Enabled=true;
                if(previewState=="loading"){busy=true;phase="ОБНОВЛЯЕМ ИГРУ";status.Text="Загружаем модели и текстуры · 248 МБ из 620 МБ";progress.Value=400;percent.Text="40%";play.Enabled=guest.Enabled=check.Enabled=false;cancel.Visible=cancel.Enabled=true;}
                if(previewState=="error"){phase="ЗАГРУЗКА ПРИОСТАНОВЛЕНА";status.Text="Не удалось подключиться к серверу обновлений. Проверьте соединение и нажмите «Проверить файлы».";progress.Value=370;percent.Text="37%";play.Enabled=guest.Enabled=false;accountError=true;account.Text="Неверный логин или пароль.\nПроверьте данные и попробуйте снова.";}
                if(previewState=="account"){token="preview-only";nickname="Повелитель Очень Длинного Имени";name.Text="CastleCommander";account.Text="Вход сохранён на этом компьютере.\nМожно отправляться в сетевую игру.";}
                RefreshAccount();Invalidate();Update();
                using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(preview);}
                Close();
            }else await UpdateGame();
        };
        FormClosing+=delegate{if(stopping!=null)stopping.Cancel();};
    }
    void Input(TextBox box,int x,int y,int width,string accessible,int tab){
        box.Bounds=R(x+13,y+11,width-26,25);box.BorderStyle=BorderStyle.None;box.Font=Body(17);box.BackColor=Color.FromArgb(14,23,29);box.ForeColor=LauncherTheme.Text;box.AccessibleName=accessible;box.TabIndex=tab;Controls.Add(box);
        box.Enter+=delegate{Invalidate();};box.Leave+=delegate{Invalidate();};
    }
    void RefreshAccount(){
        bool signed=token!="";loginCaption.Visible=passwordCaption.Visible=name.Visible=password.Visible=!signed;login.Visible=!signed;logout.Visible=signed;name.Enabled=password.Enabled=!signed;
        tips.SetToolTip(logout,signed?"Аккаунт: "+nickname:"");
        account.ForeColor=accountError?Color.FromArgb(245,173,142):LauncherTheme.Muted;tips.SetToolTip(account,account.Text);Invalidate();
    }
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&e.Y<U(536)){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}}
    protected override bool ProcessCmdKey(ref Message message,Keys key){
        if(key==Keys.Escape){Close();return true;}
        if(key==Keys.Enter&&(name.Focused||password.Focused)){if(login.Enabled)login.PerformClick();return true;}
        return base.ProcessCmdKey(ref message,key);
    }
    protected override void OnPaintBackground(PaintEventArgs e){
        var g=e.Graphics;g.Clear(LauncherTheme.Ink);g.ScaleTransform(ui,ui);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if(backdrop!=null){float factor=Math.Max(1120f/backdrop.Width,536f/backdrop.Height);g.DrawImage(backdrop,new RectangleF(0,0,backdrop.Width*factor,backdrop.Height*factor));}
        using(var shade=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,0,1120,536),Color.FromArgb(36,5,12,22),Color.FromArgb(175,7,15,22),0f))g.FillRectangle(shade,0,0,1120,536);
        using(var shade=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,0,744,536),Color.FromArgb(85,5,11,17),Color.Transparent,0f))g.FillRectangle(shade,0,0,744,536);
        using(var shade=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,370,1120,166),Color.Transparent,Color.FromArgb(245,13,20,25),90f))g.FillRectangle(shade,0,370,1120,166);
        using(var brush=new SolidBrush(Color.FromArgb(205,8,15,21)))g.FillRectangle(brush,0,0,1120,56);
        LauncherTheme.Line(g,Color.FromArgb(75,226,194,126),24,55,1072);
        using(var f=Body(12/ui))LauncherTheme.Words(g,"CASTLE FIGHT  /  LAUNCHER",f,LauncherTheme.Gold,new RectangleF(40,13,380,28),StringAlignment.Near);
        LauncherTheme.Shield(g,63,124,22);
        using(var f=Body(12/ui))LauncherTheme.Words(g,"ПОСТРОЙТЕ СВОЮ ПОБЕДУ",f,LauncherTheme.Gold,new RectangleF(104,111,500,28),StringAlignment.Near);
        using(var title=new Font("Georgia",69,FontStyle.Bold,GraphicsUnit.Pixel)){
            using(var shadow=new SolidBrush(Color.FromArgb(160,0,0,0))){g.DrawString("CASTLE",title,shadow,40,169);g.DrawString("FIGHT",title,shadow,40,242);}
            using(var ink=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(40,166,520,164),Color.FromArgb(255,245,215),Color.FromArgb(199,158,81),90f)){g.DrawString("CASTLE",title,ink,38,166);g.DrawString("FIGHT",title,ink,38,239);}
        }
        LauncherTheme.Line(g,Color.FromArgb(135,226,194,126),44,345,66);LauncherTheme.Diamond(g,119,345,3,LauncherTheme.Gold);LauncherTheme.Line(g,Color.FromArgb(65,226,194,126),129,345,159);
        using(var f=LauncherTheme.Heading(24))LauncherTheme.Words(g,"Каждая постройка меняет ход битвы.",f,LauncherTheme.Text,new RectangleF(42,366,648,38),StringAlignment.Near);
        using(var f=new Font("Segoe UI",15,GraphicsUnit.Pixel))LauncherTheme.Words(g,"Выберите расу. Соберите армию. Удержите замок.",f,Color.FromArgb(207,215,213),new RectangleF(44,413,644,28),StringAlignment.Near);
        using(var f=new Font("Segoe UI",12,GraphicsUnit.Pixel))LauncherTheme.Words(g,"БИТВЫ 1 × 1     /     ИГРА С БОТАМИ     /     СЕТЕВЫЕ СРАЖЕНИЯ",f,LauncherTheme.Gold,new RectangleF(44,468,650,28),StringAlignment.Near);
        // Obsidian account panel with an inset frame and restrained corner details.
        using(var brush=new SolidBrush(Color.FromArgb(235,10,18,25)))g.FillRectangle(brush,744,86,336,418);
        using(var pen=new Pen(Color.FromArgb(131,117,80)))g.DrawRectangle(pen,744,86,336,418);
        using(var pen=new Pen(Color.FromArgb(36,226,194,126)))g.DrawRectangle(pen,750,92,324,406);
        LauncherTheme.Diamond(g,912,86,4,LauncherTheme.Gold);
        if(token=="")foreach(var box in new[]{name,password}){var frame=new RectangleF(box.Left/ui-13,box.Top/ui-11,box.Width/ui+26,46);using(var brush=new SolidBrush(Color.FromArgb(14,23,29)))g.FillRectangle(brush,frame);using(var pen=new Pen(box.Focused?LauncherTheme.Gold:Color.FromArgb(72,86,91),box.Focused?2:1))g.DrawRectangle(pen,frame.X,frame.Y,frame.Width,frame.Height);}
        if(token!=""){
            using(var brush=new SolidBrush(Color.FromArgb(246,10,18,25)))g.FillRectangle(brush,760,201,304,160);
            LauncherTheme.Shield(g,795,244,17);
            using(var f=new Font("Segoe UI",12,GraphicsUnit.Pixel))LauncherTheme.Words(g,"ВХОД ВЫПОЛНЕН",f,LauncherTheme.Gold,new RectangleF(828,218,218,22),StringAlignment.Near);
            using(var f=LauncherTheme.Heading(23))using(var brush=new SolidBrush(LauncherTheme.Text))using(var format=new StringFormat{Trimming=StringTrimming.EllipsisCharacter})g.DrawString(nickname,f,brush,new RectangleF(772,276,280,74),format);
        }
        using(var brush=new SolidBrush(LauncherTheme.Ink))g.FillRectangle(brush,0,536,1120,184);
        LauncherTheme.Line(g,Color.FromArgb(115,226,194,126),24,536,1072);LauncherTheme.Diamond(g,560,536,4,LauncherTheme.Gold);
        using(var f=new Font("Segoe UI",15,FontStyle.Bold,GraphicsUnit.Pixel))LauncherTheme.Words(g,phase,f,LauncherTheme.Text,new RectangleF(40,562,560,28),StringAlignment.Near);
        using(var f=new Font("Segoe UI",11,GraphicsUnit.Pixel))LauncherTheme.Words(g,"Аккаунт сохранится при входе гостем",f,LauncherTheme.Muted,new RectangleF(756,688,324,22),StringAlignment.Center);
        using(var pen=new Pen(Color.FromArgb(111,99,69)))g.DrawRectangle(pen,0,0,1119,719);
        g.ResetTransform();
    }
    protected override void Dispose(bool disposing){if(disposing){tips.Dispose();if(backdrop!=null){backdrop.Dispose();backdrop=null;}}base.Dispose(disposing);}
    void Style(ForgeButton b,string text,int x,int y,int width,int height){b.Text=text;b.Bounds=R(x,y,width,height);b.Font=Body(14);b.BackColor=LauncherTheme.Ink;b.ForeColor=ForeColor;Controls.Add(b);}
    void ShowProgress(string text,float fraction){
        if(IsDisposed||Disposing||!IsHandleCreated)return;
        try{BeginInvoke((Action)delegate{if(IsDisposed)return;status.Text=text;tips.SetToolTip(status,text);progress.Value=Math.Max(0,Math.Min(1000,(int)(fraction*1000)));percent.Text=(progress.Value/10)+"%";});}catch(InvalidOperationException){}
    }
    async Task UpdateGame(){
        if(busy)return;busy=true;phase="ОБНОВЛЯЕМ ИГРУ";Invalidate();check.Enabled=play.Enabled=guest.Enabled=false;cancel.Enabled=true;cancel.Visible=true;progress.Maximum=1000;stopping=new CancellationTokenSource();ready=null;
        try{
            bool failed=false;
            try{ready=await Task.Run(delegate{var release=core.Fetch();core.Apply(release,ShowProgress,stopping.Token);return release;});status.Text="Версия "+ready.version+" установлена. Всё готово к новой битве.";phase="ИГРА ГОТОВА";progress.Value=1000;percent.Text="100%";play.Enabled=true;}
            catch(Exception e){failed=true;phase="ЗАГРУЗКА ПРИОСТАНОВЛЕНА";status.Text=e is OperationCanceledException?"Загрузка отложена. Проверка продолжится при следующем запуске.":"Не удалось обновить игру: "+e.GetBaseException().Message;}
            if(failed)try{var installed=await Task.Run(delegate{return core.Installed();});if(installed!=null){await Task.Run(delegate{core.Apply(installed,delegate(string t,float p){},CancellationToken.None);});ready=installed;play.Enabled=true;phase="ДОСТУПНА СОХРАНЁННАЯ ВЕРСИЯ";status.Text+=" Сохранённая версия доступна.";}}catch(Exception){}
        }finally{busy=false;check.Enabled=true;guest.Visible=true;guest.Enabled=play.Enabled;cancel.Enabled=false;cancel.Visible=false;stopping.Dispose();stopping=null;tips.SetToolTip(status,status.Text);Invalidate();}
    }
    static Dictionary<string,object> Object(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
    static string Field(Dictionary<string,object> value,string key){object found;return value.TryGetValue(key,out found)?Convert.ToString(found):"";}
    async Task Login(){
        if(login.Enabled==false)return;string user=name.Text.Trim(),secret=password.Text;password.Text="";if(user==""||secret==""){accountError=true;account.Text="Введите логин и пароль сетевого аккаунта.";RefreshAccount();return;}login.Enabled=false;accountError=false;account.Text="Входим в аккаунт…";RefreshAccount();
        try{
            var response=await Authenticate(user,secret);token=Field(response,"token");object profile;nickname=response.TryGetValue("profile",out profile)?Field(Object(profile),"name"):user;
            if(token=="")throw new Exception("Сервер не выдал токен входа");Directory.CreateDirectory(Path.GetDirectoryName(AccountFile));var payload=Encoding.UTF8.GetBytes(json.Serialize(new{token=token,nickname=nickname,login=user}));File.WriteAllBytes(AccountFile,ProtectedData.Protect(payload,null,DataProtectionScope.CurrentUser));account.Text="Выполнен вход: "+nickname;
        }catch(Exception e){accountError=true;account.Text=e.GetBaseException().Message;}finally{secret=null;login.Enabled=true;RefreshAccount();}
    }
    internal async Task<Dictionary<string,object>> Authenticate(string user,string secret,string endpoint=Endpoint){
        using(var socket=new ClientWebSocket())using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(18))){
            await socket.ConnectAsync(new Uri(endpoint),deadline.Token);await Send(socket,new{type="browse",protocol=1,build="0.6.19",name=user,heartbeat=true},deadline.Token);bool sent=false;
            while(true){var packet=await Receive(socket,deadline.Token);string type=Field(packet,"type");
                if(type=="directory"&&!sent){await Send(socket,new{type="profile_auth",action="login",name=user,password=secret},deadline.Token);sent=true;}
                if(type=="profile"&&Field(packet,"request_type")=="profile_auth"){object ok;if(!packet.TryGetValue("ok",out ok)||!Convert.ToBoolean(ok))throw new Exception(Field(packet,"message"));return packet;}
                if(type=="error")throw new Exception(Field(packet,"message"));
            }
        }
    }
    async Task Send(ClientWebSocket socket,object packet,CancellationToken cancelToken){byte[] data=Encoding.UTF8.GetBytes(json.Serialize(packet));await socket.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Text,true,cancelToken);}
    async Task<Dictionary<string,object>> Receive(ClientWebSocket socket,CancellationToken cancelToken){using(var message=new MemoryStream()){var buffer=new byte[16384];WebSocketReceiveResult reply;do{reply=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),cancelToken);if(reply.MessageType==WebSocketMessageType.Close)throw new IOException("Сервер закрыл соединение");if(message.Length+reply.Count>1024*1024)throw new InvalidDataException("Ответ сервера слишком большой");message.Write(buffer,0,reply.Count);}while(!reply.EndOfMessage);return json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(message.ToArray()));}}
    void LoadAccount(){try{if(!File.Exists(AccountFile))return;var data=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(AccountFile),null,DataProtectionScope.CurrentUser)));token=Field(data,"token");nickname=Field(data,"nickname");name.Text=Field(data,"login");account.Text="Аккаунт: "+nickname;}catch(Exception){account.Text="Войдите в аккаунт повторно.";}}
    void Launch(bool asGuest){
        if(ready==null||busy)return;
        try{
            string args="";if(token!=""||asGuest){
                string path=SessionHandoff.Create(Program.Home,asGuest,token,nickname);args="--launcher-session=\""+path+"\"";
            }
            Process.Start(new ProcessStartInfo(core.Target(ready.windows.entryPoint),args){WorkingDirectory=core.Root,UseShellExecute=false});Close();
        }catch(Exception e){status.Text="Не удалось запустить игру: "+e.GetBaseException().Message;}
    }
}
}
