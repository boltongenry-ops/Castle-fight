using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using CastleFight;

[assembly: AssemblyTitle("Castle Fight Portable")]
[assembly: AssemblyProduct("Castle Fight")]
[assembly: AssemblyVersion("0.20.0.0")]

internal static class PortableLauncher
{
    static LoadingWindow loading;
    delegate bool WindowVisitor(IntPtr window,IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] struct WindowRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowVisitor visitor,IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out WindowRect rectangle);
    static bool PlayerWindowVisible(int processId){
        bool found=false;
        EnumWindows(delegate(IntPtr window,IntPtr parameter){
            uint owner;GetWindowThreadProcessId(window,out owner);WindowRect rect;
            if(owner==(uint)processId&&IsWindowVisible(window)&&GetWindowRect(window,out rect)&&rect.Right-rect.Left>=320&&rect.Bottom-rect.Top>=180){found=true;return false;}
            return true;
        },IntPtr.Zero);
        return found;
    }
    static void CloseLoading(){if(loading!=null){loading.CancelPreparation=null;loading.Close();loading.Dispose();loading=null;}}
    static string Arg(string[] args,string key){return args.Where(a=>a.StartsWith(key+"=",StringComparison.Ordinal)).Select(a=>a.Substring(key.Length+1)).FirstOrDefault()??"";}
    const string Magic = "CFPORTABLE-V1!!!"; // 16 bytes; footer = offset + length + SHA256 + magic.
    [STAThread]
    static int Main(string[] args)
    {
        AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
        AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
        Application.EnableVisualStyles();
        try
        {
            string self = Assembly.GetExecutingAssembly().Location;
            loading=new LoadingWindow();
            string gifFrames=Arg(args,"--castle-frames");
            if(gifFrames!=""){loading.SaveCastleFrames(gifFrames);return 0;}
            string preview=Arg(args,"--launcher-preview");
            if(preview!=""){
                loading.SaveFrame(preview,.35);
                loading.SaveFrame(preview+".second.png",4.1);
                return 0;
            }
            loading.Show();
            long offset, length;
            byte[] expected;
            using (var file = File.OpenRead(self))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 64) throw new InvalidDataException("Incomplete portable file.");
                file.Position = file.Length - 64;
                offset = reader.ReadInt64(); length = reader.ReadInt64(); expected = reader.ReadBytes(32);
                if (Encoding.ASCII.GetString(reader.ReadBytes(16)) != Magic || offset < 1 || length < 1 || offset > file.Length - 64 || length != file.Length - 64 - offset)
                    throw new InvalidDataException("Incomplete portable payload.");
            }
            string hash = BitConverter.ToString(expected).Replace("-", "").ToLowerInvariant();
            string cache = Path.Combine(Path.GetDirectoryName(self), "CastleFight-Portable.files");
            Directory.CreateDirectory(cache);
            string destination = Path.Combine(cache, hash.Substring(0, 16));
            string player = Path.Combine(destination, "CastleFightMap.exe");
            using (var cancellation = new CancellationTokenSource())
            {
                loading.CancelPreparation = cancellation.Cancel;
                var preparation = Task.Run(() => Prepare(self, offset, length, expected, cache, destination, hash, cancellation.Token));
                while (!preparation.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(16);
                }
                preparation.GetAwaiter().GetResult();
                loading.CancelPreparation = null;
            }
            if (args.Contains("--portable-extract-only")) return 0;
            string ready=Path.Combine(cache,".ready-"+Guid.NewGuid().ToString("N"));
            var forwarded=args.Where(a=>a!="--portable-wait"&&(!a.StartsWith("--launcher-",StringComparison.Ordinal)||a.StartsWith("--launcher-session=",StringComparison.Ordinal))).Concat(new[]{"--startup-ready="+ready}).ToArray();
            loading.Stage="Готовим карту и модели";loading.Percent=-1;
            var start=new ProcessStartInfo(player,String.Join(" ",forwarded.Select(Quote))){WorkingDirectory=destination,UseShellExecute=false};
            using(var process=Process.Start(start)){
                var until=DateTime.UtcNow.AddMinutes(4);
                while(!process.HasExited&&!File.Exists(ready)&&DateTime.UtcNow<until){
                    // Unity owns its loading screen as soon as its real window appears.
                    // Readiness still controls the marker lifetime, never a second overlay.
                    if(loading!=null&&PlayerWindowVisible(process.Id))CloseLoading();
                    Application.DoEvents();Thread.Sleep(16);
                }
                CloseLoading();
                RemoveReadinessMarker(ready);
                if(args.Contains("--portable-wait")){process.WaitForExit();return process.ExitCode;}
            }
            return 0;
        }
        catch (OperationCanceledException) { return 0; }
        catch (Exception e)
        {
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CastleFight-launch-error.log"), e.ToString()); } catch { }
            if (Arg(args,"--launcher-preview")==""&&Arg(args,"--castle-frames")==""&&!args.Contains("--portable-wait")&&!args.Contains("--portable-extract-only")) MessageBox.Show("Не удалось запустить Castle Fight.\n\n" + e.Message + "\n\nПоложите EXE в доступную для записи папку или пересоберите портативную версию.", "Castle Fight", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally {CloseLoading();}
    }
    static void Report(string stage, int percent)
    {
        loading.BeginInvoke(new Action(() => {
            if (loading == null) return;
            loading.Stage = stage;
            loading.Percent = percent;
            loading.Invalidate();
        }));
    }

    static void Prepare(string self, long offset, long length, byte[] expected, string cache,
        string destination, string hash, CancellationToken cancellation)
    {
        Report("Проверяем файлы", -1);
        using (var mutex = new Mutex(false, "Local\\CastleFightPortable-" + hash))
        {
            bool owned = false;
            try
            {
                try { owned = WaitHandle.WaitAny(new WaitHandle[] { mutex, cancellation.WaitHandle }, TimeSpan.FromMinutes(5)) == 0; }
                catch (AbandonedMutexException error) { owned = error.MutexIndex == 0; }
                cancellation.ThrowIfCancellationRequested();
                if (!owned) throw new IOException("Another launch is still preparing the game. Try again shortly.");
                if (File.Exists(Path.Combine(destination, ".complete"))) return;

                string staging = Path.Combine(cache, ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(staging);
                try
                {
                    using (var file = File.OpenRead(self))
                    using (var payload = new PayloadStream(file, offset, length))
                    {
                        var buffer = new byte[1024 * 1024];
                        using (var sha = SHA256.Create())
                        {
                            int size;
                            while ((size = payload.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                sha.TransformBlock(buffer, 0, size, buffer, 0);
                            }
                            sha.TransformFinalBlock(buffer, 0, 0);
                            if (!sha.Hash.SequenceEqual(expected)) throw new InvalidDataException("Portable file checksum mismatch. Rebuild or copy the EXE again.");
                        }
                        Report("Распаковываем мир", 0);
                        payload.Position = 0;
                        using (var zip = new ZipArchive(payload, ZipArchiveMode.Read))
                        {
                            int completed = 0;
                            string prefix = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                            foreach (var entry in zip.Entries)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                string target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                                if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid archive path.");
                                if (String.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(target);
                                else
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                                    using (var input = entry.Open())
                                    using (var output = File.Create(target))
                                    {
                                        int size;
                                        while ((size = input.Read(buffer, 0, buffer.Length)) > 0)
                                        {
                                            cancellation.ThrowIfCancellationRequested();
                                            output.Write(buffer, 0, size);
                                        }
                                    }
                                }
                                Report("Распаковываем мир", ++completed * 100 / zip.Entries.Count);
                            }
                        }
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if (!File.Exists(Path.Combine(staging, "CastleFightMap.exe")) || !File.Exists(Path.Combine(staging, "UnityPlayer.dll")))
                        throw new InvalidDataException("Game files are missing.");
                    File.WriteAllText(Path.Combine(staging, ".complete"), hash);
                    MovePreparedDirectory(staging, destination, cancellation);
                }
                finally
                {
                    string absoluteStaging = Path.GetFullPath(staging);
                    if (String.Equals(Path.GetDirectoryName(absoluteStaging), Path.GetFullPath(cache), StringComparison.OrdinalIgnoreCase)
                        && Directory.Exists(absoluteStaging))
                    {
                        try { Directory.Delete(absoluteStaging, true); }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                }
            }
            finally { if (owned) mutex.ReleaseMutex(); }
        }
    }

    sealed class PayloadStream : Stream
    {
        readonly Stream file;
        readonly long start, length;
        public PayloadStream(Stream file, long start, long length)
        {
            this.file = file;
            this.start = start;
            this.length = length;
            Position = 0;
        }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return length; } }
        public override long Position
        {
            get { return file.Position - start; }
            set
            {
                if (value < 0 || value > length) throw new IOException("Invalid payload position.");
                file.Position = start + value;
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return file.Read(buffer, offset, (int)Math.Min(count, length - Position));
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = checked(offset + (origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? Position : length));
            return Position;
        }
        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
    }

    static void RemoveReadinessMarker(string path)
    {
        // The player may still own the just-created marker. Cleanup must not fail a successful launch.
        for(int attempt=0;attempt<10;attempt++){
            try {File.Delete(path);return;}
            catch(IOException){Thread.Sleep(20);}
            catch(UnauthorizedAccessException){return;}
        }
    }
    static void MovePreparedDirectory(string staging, string destination, CancellationToken cancellation)
    {
        // Scanners may briefly retain a handle on a just-extracted file on Windows.
        for (int attempt = 0; ; attempt++)
        {
            try { Directory.Move(staging, destination); return; }
            catch (IOException) { if (attempt >= 39) throw; }
            catch (UnauthorizedAccessException) { if (attempt >= 39) throw; }
            if (cancellation.WaitHandle.WaitOne(250)) cancellation.ThrowIfCancellationRequested();
        }
    }
    // Windows CommandLineToArgvW escaping, including paths with spaces and trailing backslashes.
    static string Quote(string value)
    {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); }
            else { result.Append('\\', slashes); result.Append(c); }
            slashes = 0;
        }
        result.Append('\\', slashes * 2); result.Append('"'); return result.ToString();
    }
}

internal sealed class LoadingWindow : Form {
    bool cleaned;Bitmap background,castleBody,smokeMask,windowMask;
    readonly ImageAttributes spriteTint=new ImageAttributes();
    readonly ColorMatrix spriteAlpha=new ColorMatrix();
    Rectangle castle;readonly PrivateFontCollection fonts=new PrivateFontCollection();IntPtr fontMemory;
    Font titleFont,loadingFont,detailFont;readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer();readonly Stopwatch clock=Stopwatch.StartNew();
    public string Stage="Проверяем файлы";public int Percent=-1;
    public Action CancelPreparation;
    double? previewTime;
    static Image ResourceImage(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)return null;using(var image=Image.FromStream(stream))return new Bitmap(image);}}
    public LoadingWindow(){
        Text="Castle Fight — Загрузка";
        FormBorderStyle=FormBorderStyle.FixedSingle;
        MinimizeBox=true;MaximizeBox=false;ShowInTaskbar=true;
        StartPosition=FormStartPosition.CenterScreen;
        var desktop=Screen.PrimaryScreen.WorkingArea;
        float scale=Math.Min(1,Math.Min((desktop.Width-40)/1120f,(desktop.Height-80)/630f));
        ClientSize=new Size((int)(1120*scale),(int)(630*scale));
        DoubleBuffered=true;BackColor=Color.FromArgb(10,17,13);
        CacheArtwork();
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LoadingFont")){
            if(stream!=null){byte[] bytes=new byte[stream.Length];stream.Read(bytes,0,bytes.Length);fontMemory=Marshal.AllocCoTaskMem(bytes.Length);Marshal.Copy(bytes,0,fontMemory,bytes.Length);fonts.AddMemoryFont(fontMemory,bytes.Length);}
        }
        FontFamily family=fonts.Families.Length>0?fonts.Families[0]:FontFamily.GenericSerif;
        titleFont=new Font(family,24,FontStyle.Regular,GraphicsUnit.Pixel);loadingFont=new Font(family,38,FontStyle.Regular,GraphicsUnit.Pixel);detailFont=new Font(family,16,FontStyle.Regular,GraphicsUnit.Pixel);
        animation.Interval=40;animation.Tick+=delegate{if(WindowState!=FormWindowState.Minimized)Invalidate();};animation.Start();
    }
    static Bitmap CropArtwork(Image source,Size size,RectangleF crop){
        var cached=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(cached)){
            g.CompositingMode=CompositingMode.SourceCopy;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            g.DrawImage(source,new RectangleF(PointF.Empty,size),crop,GraphicsUnit.Pixel);
        }
        return cached;
    }
    void CacheArtwork(){
        using(var source=ResourceImage("SiegeBackdrop")){
            if(source!=null){
                float height=source.Width*630f/1120;
                background=CropArtwork(source,ClientSize,new RectangleF(0,(source.Height-height)*.5f,source.Width,height));
            }
        }
        float x=ClientSize.Width/1120f,y=ClientSize.Height/630f;
        castle=new Rectangle((int)(713*x),(int)(342*y),(int)(369*x),(int)(246*y));
        castleBody=LoadCastleBody(castle.Size);
        smokeMask=MakeCastleMask(true);
        windowMask=MakeCastleMask(false);
    }
    static Bitmap LoadCastleBody(Size size){
        using(var body=ResourceImage("CastleBody")){
            return body==null?null:CropArtwork(body,size,new RectangleF(0,0,body.Width,body.Height));
        }
    }
    static Bitmap MakeCastleMask(bool smoke){
        const int size=CastleLoadingMotion.MaskSize;
        var mask=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            mask.SetPixel(x,y,smoke?Color.FromArgb(CastleLoadingMotion.SmokeMask(x,y),143,136,123):
                Color.FromArgb(CastleLoadingMotion.GlowMask(x,y),255,205,112));
        return mask;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason==CloseReason.UserClosing && CancelPreparation!=null)
        {
            CancelPreparation();
            e.Cancel=true;
        }
        base.OnFormClosing(e);
    }
    void DrawCastleSprite(Graphics g,Bitmap texture,Rectangle area,CastleLoadingMotion.Sprite sprite){
        if(sprite.opacity<=0)return;
        float width=area.Width*sprite.width,height=area.Height*sprite.height;
        float x=area.X+sprite.x*area.Width-width*.5f,y=area.Y+sprite.y*area.Height-height*.5f;
        spriteAlpha.Matrix33=sprite.opacity;
        spriteTint.SetColorMatrix(spriteAlpha);
        // Float destination points avoid pixel snapping as the smoke rises.
        g.DrawImage(texture,new[]{new PointF(x,y),new PointF(x+width,y),new PointF(x,y+height)},
            new RectangleF(0,0,texture.Width,texture.Height),GraphicsUnit.Pixel,spriteTint);
    }
    void DrawLoadingCastle(Graphics g,Bitmap body,Rectangle area,double time){
        if(body==null)return;
        for(int i=0;i<CastleLoadingMotion.PuffCount;i++)
            DrawCastleSprite(g,smokeMask,area,CastleLoadingMotion.SmokePuff(i,time));
        g.DrawImageUnscaled(body,area.Location);
        for(int i=0;i<CastleLoadingMotion.WindowCount;i++)
            DrawCastleSprite(g,windowMask,area,CastleLoadingMotion.WindowGlow(i,time));
    }
    public void SaveCastleFrames(string directory){
        Directory.CreateDirectory(directory);
        var area=new Rectangle(12,100,360,240);
        using(var body=LoadCastleBody(area.Size))
        using(var canvas=new Bitmap(384,344,PixelFormat.Format32bppArgb))
        using(var g=Graphics.FromImage(canvas)){
            if(body==null)throw new InvalidDataException("Missing loading castle");
            g.InterpolationMode=InterpolationMode.Bilinear;
            for(int frame=0;frame<CastleLoadingMotion.LoopSeconds*CastleLoadingMotion.FramesPerSecond;frame++){
                g.Clear(Color.Transparent);
                DrawLoadingCastle(g,body,area,(double)frame/CastleLoadingMotion.FramesPerSecond);
                canvas.Save(Path.Combine(directory,frame.ToString("D3")+".png"),ImageFormat.Png);
            }
        }
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);PaintLoading(e.Graphics);
    }
    void PaintLoading(Graphics g){
        if(background==null)g.Clear(BackColor);else g.DrawImageUnscaled(background,0,0);
        g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        double time=previewTime??clock.Elapsed.TotalSeconds;
        g.InterpolationMode=InterpolationMode.Bilinear;
        DrawLoadingCastle(g,castleBody,castle,time);
        g.ScaleTransform(ClientSize.Width/1120f,ClientSize.Height/630f);
        using(var ink=new SolidBrush(Color.FromArgb(235,8,19,13)))g.FillRectangle(ink,42,388,645,190);
        using(var gold=new SolidBrush(Color.FromArgb(220,181,109))){
            g.DrawString("Castle Fight",titleFont,gold,52,47);
            g.FillRectangle(gold,53,91,155,1);g.FillRectangle(gold,42,388,645,1);
            const int barX=65,barWidth=597;
            using(var rail=new SolidBrush(Color.FromArgb(55,57,42)))g.FillRectangle(rail,barX,531,barWidth,5);
            if(Percent>=0){
                g.FillRectangle(gold,barX,531,barWidth*Math.Min(100,Percent)/100,5);
                g.DrawString(Percent+"%",detailFont,gold,613,500);
            }else{
                float phase=(float)(time%3)/1.5f;
                float shift=(barWidth-119)*(1-Math.Abs(1-phase));
                g.FillRectangle(gold,barX+shift,531,119,5);
            }
        }
        using(var paper=new SolidBrush(Color.FromArgb(247,231,191)))g.DrawString("Подготовка к битве",loadingFont,paper,61,405);
        using(var muted=new SolidBrush(Color.FromArgb(201,202,179)))g.DrawString(Stage,detailFont,muted,new RectangleF(65,476,540,41));
        using(var pen=new Pen(Color.FromArgb(120,159,124,65),1))g.DrawRectangle(pen,0,0,1119,629);
    }
    public void SaveFrame(string path,double time){
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        previewTime=time;
        try{using(var bitmap=new Bitmap(ClientSize.Width,ClientSize.Height)){
            using(var graphics=Graphics.FromImage(bitmap))PaintLoading(graphics);
            bitmap.Save(path,ImageFormat.Png);
        }}
        finally{previewTime=null;}
    }
    protected override void Dispose(bool disposing){if(disposing&&!cleaned){cleaned=true;animation.Stop();animation.Dispose();if(background!=null)background.Dispose();if(castleBody!=null)castleBody.Dispose();if(smokeMask!=null)smokeMask.Dispose();if(windowMask!=null)windowMask.Dispose();spriteTint.Dispose();titleFont.Dispose();loadingFont.Dispose();detailFont.Dispose();fonts.Dispose();if(fontMemory!=IntPtr.Zero)Marshal.FreeCoTaskMem(fontMemory);}base.Dispose(disposing);}
}
