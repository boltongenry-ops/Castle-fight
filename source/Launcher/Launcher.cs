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
            Application.Run(new LauncherForm(args.Length==2&&args[0]=="--preview"?args[1]:null));return 0;
        }catch(Exception e){if(args.Length>0){Console.Error.WriteLine(e.GetBaseException().Message);return 1;}MessageBox.Show(e.Message,"Castle Fight");return 1;}
    }
}
sealed class LauncherForm:Form {
    const string Endpoint="wss://45-15-156-101.fatmetal.net/play";
    readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=1024*1024};readonly UpdateCore core=new UpdateCore(Path.Combine(Program.Home,"Game"));
    readonly Label status=new Label(),account=new Label();readonly ProgressBar progress=new ProgressBar();readonly Button play=new Button(),guest=new Button(),check=new Button(),cancel=new Button(),login=new Button(),logout=new Button();readonly TextBox name=new TextBox(),password=new TextBox();
    CancellationTokenSource stopping;Release ready;string token="",nickname="";bool busy;
    Image backdrop;
    string AccountFile{get{return Path.Combine(Program.Home,"Launcher","account.dat");}}
    public LauncherForm(string preview=null){
        Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);Text="Castle Fight · Launcher";ClientSize=new Size(850,540);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(15,23,27);ForeColor=Color.FromArgb(231,220,190);Font=new Font("Segoe UI",11);DoubleBuffered=true;
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherBackdrop"))if(source!=null)backdrop=new Bitmap(source);
        var title=new Label{Text="CASTLE FIGHT",Font=new Font("Georgia",33,FontStyle.Bold),ForeColor=Color.FromArgb(221,185,102),Location=new Point(35,30),Size=new Size(780,65)};Controls.Add(title);
        Controls.Add(new Label{Text="Мир готовится к следующей битве",Location=new Point(39,104),Size=new Size(760,30)});
        Controls.Add(new Label{Text="Сетевой аккаунт",Location=new Point(40,145),Size=new Size(300,25)});
        name.SetBounds(40,196,330,30);name.ForeColor=Color.White;name.BackColor=Color.FromArgb(29,41,46);name.MaxLength=40;Controls.Add(name);
        password.SetBounds(390,196,270,30);password.UseSystemPasswordChar=true;password.BackColor=name.BackColor;password.ForeColor=Color.White;password.MaxLength=128;Controls.Add(password);
        var hint=new Label{Text="Логин",Location=new Point(40,173),Size=new Size(330,23),Font=new Font(Font.FontFamily,9)};Controls.Add(hint);
        Controls.Add(new Label{Text="Пароль",Location=new Point(390,173),Size=new Size(270,23),Font=hint.Font});
        Style(login,"Войти",680,193,125,36);login.Click+=async delegate{await Login();};Style(logout,"Выйти",680,240,125,32);logout.Click+=delegate{token=nickname="";if(File.Exists(AccountFile))File.Delete(AccountFile);account.Text="Без входа · доступна игра с ботами";};
        account.SetBounds(40,243,620,35);account.Text="Без входа · доступна игра с ботами";Controls.Add(account);
        status.SetBounds(40,315,760,50);status.Text="Проверяем обновления…";Controls.Add(status);progress.SetBounds(40,372,765,13);Controls.Add(progress);
        Style(check,"Проверить файлы",40,414,225,45);check.Click+=async delegate{await UpdateGame();};Style(cancel,"Отложить",280,414,155,45);cancel.Enabled=false;cancel.Visible=false;cancel.Click+=delegate{if(stopping!=null)stopping.Cancel();};
        Style(guest,"Играть гостем",280,414,220,55);guest.Enabled=false;guest.Click+=delegate{Launch(true);};
        Style(play,"ИГРАТЬ",525,414,280,55);play.Font=new Font(Font.FontFamily,15,FontStyle.Bold);play.Enabled=false;play.Click+=delegate{Launch(false);};
        var footer=new Label{Text="GitHub · boltongenry-ops / Castle-fight     |     Аккаунт общий с игрой",Location=new Point(40,496),Size=new Size(765,24),Font=hint.Font};Controls.Add(footer);
        foreach(Control child in Controls)if(child is Label)child.BackColor=Color.Transparent;
        LoadAccount();Shown+=async delegate{if(preview!=null){status.Text="Версия 0.31.0 готова. Можно играть.";progress.Maximum=1000;progress.Value=1000;play.Enabled=guest.Enabled=true;using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(preview);}Close();}else await UpdateGame();};FormClosing+=delegate{if(stopping!=null)stopping.Cancel();};
    }
    protected override void OnPaintBackground(PaintEventArgs e){base.OnPaintBackground(e);if(backdrop!=null){e.Graphics.DrawImage(backdrop,ClientRectangle);using(var shade=new SolidBrush(Color.FromArgb(213,10,17,21)))e.Graphics.FillRectangle(shade,ClientRectangle);}}
    void Style(Button b,string text,int x,int y,int width,int height){b.Text=text;b.SetBounds(x,y,width,height);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(140,116,67);b.BackColor=Color.FromArgb(47,44,33);b.ForeColor=ForeColor;Controls.Add(b);}
    void ShowProgress(string text,float fraction){if(IsDisposed||!IsHandleCreated)return;BeginInvoke((Action)delegate{status.Text=text;progress.Value=Math.Max(0,Math.Min(1000,(int)(fraction*1000)));});}
    async Task UpdateGame(){
        if(busy)return;busy=true;check.Enabled=play.Enabled=guest.Enabled=false;guest.Visible=false;cancel.Enabled=true;cancel.Visible=true;progress.Maximum=1000;stopping=new CancellationTokenSource();ready=null;
        try{
            bool failed=false;
            try{ready=await Task.Run(delegate{var release=core.Fetch();core.Apply(release,ShowProgress,stopping.Token);return release;});status.Text="Версия "+ready.version+" готова. Можно играть.";play.Enabled=true;}
            catch(Exception e){failed=true;status.Text=e is OperationCanceledException?"Загрузка отложена. Проверка продолжится при следующем запуске.":"Не удалось обновить игру: "+e.GetBaseException().Message;}
            if(failed)try{var installed=await Task.Run(delegate{return core.Installed();});if(installed!=null){await Task.Run(delegate{core.Apply(installed,delegate(string t,float p){},CancellationToken.None);});ready=installed;play.Enabled=true;status.Text+=" Сохранённая версия доступна.";}}catch(Exception){}
        }finally{busy=false;check.Enabled=true;guest.Visible=true;guest.Enabled=play.Enabled;cancel.Enabled=false;cancel.Visible=false;stopping.Dispose();stopping=null;}
    }
    static Dictionary<string,object> Object(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
    static string Field(Dictionary<string,object> value,string key){object found;return value.TryGetValue(key,out found)?Convert.ToString(found):"";}
    async Task Login(){
        if(login.Enabled==false)return;string user=name.Text.Trim(),secret=password.Text;password.Text="";if(user==""||secret==""){account.Text="Введите логин и пароль сетевого аккаунта.";return;}login.Enabled=false;account.Text="Входим в аккаунт…";
        try{
            var response=await Authenticate(user,secret);token=Field(response,"token");object profile;nickname=response.TryGetValue("profile",out profile)?Field(Object(profile),"name"):user;
            if(token=="")throw new Exception("Сервер не выдал токен входа");Directory.CreateDirectory(Path.GetDirectoryName(AccountFile));var payload=Encoding.UTF8.GetBytes(json.Serialize(new{token=token,nickname=nickname,login=user}));File.WriteAllBytes(AccountFile,ProtectedData.Protect(payload,null,DataProtectionScope.CurrentUser));account.Text="Выполнен вход: "+nickname;
        }catch(Exception e){account.Text=e.GetBaseException().Message;}finally{secret=null;login.Enabled=true;}
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
                string folder=Path.Combine(Program.Home,"Launcher","Sessions");Directory.CreateDirectory(folder);string path=Path.Combine(folder,Guid.NewGuid().ToString("N")+".json");File.WriteAllText(path,json.Serialize(new{endpoint=Endpoint,token=asGuest?"":token,nickname=asGuest?"Гость":nickname,guest=asGuest}));
                var access=new FileSecurity();access.SetAccessRuleProtection(true,false);access.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));File.SetAccessControl(path,access);args="--launcher-session=\""+path+"\"";
            }
            Process.Start(new ProcessStartInfo(core.Target(ready.windows.entryPoint),args){WorkingDirectory=core.Root,UseShellExecute=false});Close();
        }catch(Exception e){status.Text="Не удалось запустить игру: "+e.GetBaseException().Message;}
    }
}
}
