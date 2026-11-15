using System;using System.IO;using System.Linq;using System.Reflection;using System.Drawing;using System.Windows.Forms;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Security.Cryptography;using System.Diagnostics;
namespace CastleFightLauncher {static class LauncherDesignChecks {
static readonly List<string> checks=new List<string>();static Type type;
static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
static object Get(object instance,string name){return type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance);}
static void Set(object instance,string name,object value){type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(instance,value);}
static object Call(object instance,string name,params object[] values){return type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(instance,values);}
static bool OwnVisible(Control c){return (bool)typeof(Control).GetMethod("GetState",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{2});}
[STAThread]static int Main(string[] args){try{
 Application.EnableVisualStyles();var assembly=Assembly.LoadFrom(args[0]);type=assembly.GetType("CastleFightLauncher.LauncherForm");string home=Path.GetFullPath(args[1]);Directory.CreateDirectory(home);assembly.GetType("CastleFightLauncher.Program").GetField("Home").SetValue(null,home);
 foreach(string mode in new[]{"ready","compact","dpi150"})using(var form=(Form)Activator.CreateInstance(type,new object[]{"offline-preview",mode})){
   foreach(Control c in form.Controls)Check(form.ClientRectangle.Contains(c.Bounds),mode+": control inside window: "+c.GetType().Name+" "+c.Text);
   var controls=new[]{"name","password","login","play","guest","check","close","minimize"}.Select(n=>(Control)Get(form,n)).ToArray();
   for(int i=0;i<controls.Length;i++)for(int j=i+1;j<controls.Length;j++)Check(!controls[i].Bounds.IntersectsWith(controls[j].Bounds),mode+": interactive controls do not overlap "+i+"/"+j);
   Check(((TextBox)Get(form,"password")).UseSystemPasswordChar,"Password remains masked at "+mode);
   Check(((Control)Get(form,"name")).TabIndex==0&&((Control)Get(form,"password")).TabIndex==1&&((Control)Get(form,"login")).TabIndex==2&&((Control)Get(form,"play")).TabIndex==3&&((Control)Get(form,"guest")).TabIndex==4,"Logical keyboard order at "+mode);
   Check(!((Control)Get(form,"play")).Enabled&&!((Control)Get(form,"guest")).Enabled,"Launch disabled before resource validation at "+mode);
   Set(form,"token","fixture-token");Set(form,"nickname","Очень длинное имя игрока");Call(form,"RefreshAccount");
   Check(!OwnVisible((Control)Get(form,"name"))&&!OwnVisible((Control)Get(form,"password"))&&!OwnVisible((Control)Get(form,"loginCaption"))&&!OwnVisible((Control)Get(form,"passwordCaption"))&&!OwnVisible((Control)Get(form,"login"))&&OwnVisible((Control)Get(form,"logout")),"Saved account hides input labels and shows account switch at "+mode);
   Set(form,"token","");Call(form,"RefreshAccount");Check(OwnVisible((Control)Get(form,"name"))&&OwnVisible((Control)Get(form,"password"))&&OwnVisible((Control)Get(form,"login"))&&!OwnVisible((Control)Get(form,"logout")),"Logout restores inputs at "+mode);
   Call(form,"Login");Check(((Label)Get(form,"account")).Text.Contains("Введите логин"),"Empty login gets local inline validation at "+mode);
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));Check(image.Width==form.Width,"Real form renders at "+mode);}
 }
 // The actual production Launch method starts a harmless fixture player, for both account and guest.
 string game=Path.Combine(home,"Game");Directory.CreateDirectory(game);File.Copy(args[3],Path.Combine(game,"CastleFightMap.exe"),true);
 string saved=Path.Combine(home,"Launcher","account.dat");Directory.CreateDirectory(Path.GetDirectoryName(saved));byte[] privateAccount=ProtectedData.Protect(new byte[]{1,2,3},null,DataProtectionScope.CurrentUser);File.WriteAllBytes(saved,privateAccount);
 foreach(bool guest in new[]{false,true})using(var form=(Form)Activator.CreateInstance(type,new object[]{"offline-preview","ready"})){
   var serializer=new JavaScriptSerializer();string manifest=serializer.Serialize(new{schema=1,version="0.31.0",windows=new{url="https://example.com/game.exe",size=1,entryPoint="CastleFightMap.exe",files=new[]{new{path="CastleFightMap.exe",size=1,sha256=new string('0',64),offset=0,compressedSize=1,method=0}}}});
   var core=Get(form,"core");object release=core.GetType().GetMethod("Parse").Invoke(core,new object[]{manifest});Set(form,"ready",release);Set(form,"token","fixture-account-token");Set(form,"nickname","FixtureAccount");
   string captured=Path.Combine(game,"fixture-arguments.txt");if(File.Exists(captured))File.Delete(captured);Call(form,"Launch",guest);
   var clock=Stopwatch.StartNew();while(!File.Exists(captured)&&clock.ElapsedMilliseconds<5000)System.Threading.Thread.Sleep(20);Check(File.Exists(captured),"Actual launch starts fixture player: guest="+guest);
   string argument=File.ReadAllText(captured);Check(argument.StartsWith("--launcher-session="),"Actual launch forwards session path: guest="+guest);string path=argument.Substring("--launcher-session=".Length).Trim('"');var session=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
   Check((string)session["token"]==(guest?"":"fixture-account-token")&&(string)session["nickname"]==(guest?"Гость":"FixtureAccount"),"Actual account and guest token isolation: guest="+guest);
   Check(File.ReadAllBytes(saved).SequenceEqual(privateAccount),"Launch preserves DPAPI account: guest="+guest);Check(File.GetAccessControl(path).AreAccessRulesProtected,"Protected session ACL: guest="+guest);
 }
 File.WriteAllText(args[2],new JavaScriptSerializer().Serialize(new{status="PASS",actualCompiledLauncher=true,checks=checks}));Console.WriteLine("LAUNCHER_DESIGN_PASS "+checks.Count);return 0;
}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}}
