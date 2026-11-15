using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CastleFightLauncher {
// GDI+ artwork stays separate from the updater and account protocol.
static class LauncherTheme {
    internal static readonly Color Gold=Color.FromArgb(226,194,126), Ink=Color.FromArgb(13,20,25), Text=Color.FromArgb(239,235,222), Muted=Color.FromArgb(174,185,186);
    static readonly PrivateFontCollection fonts=new PrivateFontCollection();
    // AddMemoryFont may retain the buffer; keep it pinned for this process-wide collection.
    static GCHandle fontMemory;
    static LauncherTheme(){
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherHeading")){
            if(stream==null)return;byte[] bytes=new byte[(int)stream.Length];int offset=0;
            while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read==0)throw new System.IO.EndOfStreamException();offset+=read;}
            fontMemory=GCHandle.Alloc(bytes,GCHandleType.Pinned);fonts.AddMemoryFont(fontMemory.AddrOfPinnedObject(),bytes.Length);
        }
    }
    internal static Font Heading(float size){return new Font(fonts.Families.Length>0?fonts.Families[0]:new FontFamily("Georgia"),size,FontStyle.Regular,GraphicsUnit.Pixel);}
    internal static void Line(Graphics g,Color color,float x,float y,float width){using(var pen=new Pen(color))g.DrawLine(pen,x,y,x+width,y);}
    internal static void Diamond(Graphics g,float x,float y,float radius,Color color){using(var brush=new SolidBrush(color))g.FillPolygon(brush,new[]{new PointF(x,y-radius),new PointF(x+radius,y),new PointF(x,y+radius),new PointF(x-radius,y)});}
    internal static void Words(Graphics g,string text,Font font,Color color,RectangleF area,StringAlignment align){using(var brush=new SolidBrush(color))using(var format=new StringFormat{Alignment=align,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(text,font,brush,area,format);}
    internal static void Shield(Graphics g,float x,float y,float size){
        using(var pen=new Pen(Gold,1.4f)){
            g.DrawPolygon(pen,new[]{new PointF(x,y-size),new PointF(x+size*.74f,y-size*.66f),new PointF(x+size*.62f,y+size*.35f),new PointF(x,y+size),new PointF(x-size*.62f,y+size*.35f),new PointF(x-size*.74f,y-size*.66f)});
            g.DrawLine(pen,x-size*.34f,y+size*.4f,x+size*.34f,y-size*.42f);g.DrawLine(pen,x-size*.34f,y-size*.42f,x+size*.34f,y+size*.4f);
        }
    }
}
sealed class ForgeButton:Button {
    internal bool Primary, Chrome;
    internal string Symbol="";
    bool hover,pressed;
    public ForgeButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;UseVisualStyleBackColor=false;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float scale=Height/(Primary?62f:42f);
        var r=new Rectangle(0,0,Width-1,Height-1);
        Color top=Primary?Color.FromArgb(219,181,104):Color.FromArgb(29,40,45),bottom=Primary?Color.FromArgb(151,111,52):Color.FromArgb(18,28,33);
        if(!Enabled){top=Color.FromArgb(45,49,47);bottom=Color.FromArgb(27,34,36);}else if(pressed){top=Primary?Color.FromArgb(161,119,56):Color.FromArgb(39,51,54);bottom=top;}else if(hover){top=Primary?Color.FromArgb(241,208,140):Color.FromArgb(43,58,62);}
        using(var brush=new LinearGradientBrush(r,top,bottom,90f))g.FillRectangle(brush,r);
        Color border=Enabled?(Primary?Color.FromArgb(247,220,160):Color.FromArgb(111,101,75)):Color.FromArgb(68,76,76);
        using(var pen=new Pen(border))g.DrawRectangle(pen,r);
        if(Primary){using(var pen=new Pen(Color.FromArgb(95,255,237,192)))g.DrawRectangle(pen,3,3,Width-7,Height-7);}
        Color text=Enabled?(Primary?Color.FromArgb(35,28,18):LauncherTheme.Text):Color.FromArgb(151,160,158);
        if(Chrome){
            using(var pen=new Pen(text,1.4f)){float cx=Width/2f,cy=Height/2f;if(Symbol=="close"){g.DrawLine(pen,cx-5,cy-5,cx+5,cy+5);g.DrawLine(pen,cx+5,cy-5,cx-5,cy+5);}else g.DrawLine(pen,cx-6,cy+3,cx+6,cy+3);}
        }else{
            LauncherTheme.Words(g,Text,Font,text,new RectangleF(12,0,Width-24,Height-1),StringAlignment.Center);
            if(Primary){using(var pen=new Pen(Color.FromArgb(Enabled?140:70,text),1.4f)){float x=Width-27*scale,y=Height/2f;g.DrawLine(pen,x-10*scale,y,x,y);g.DrawLine(pen,x-5*scale,y-5*scale,x,y);g.DrawLine(pen,x-5*scale,y+5*scale,x,y);}}
        }
        if(Focused&&ShowFocusCues){using(var pen=new Pen(Primary?Color.FromArgb(67,47,22):LauncherTheme.Gold,2)){pen.DashStyle=DashStyle.Dot;g.DrawRectangle(pen,6,6,Width-13,Height-13);}}
    }
}
sealed class ForgeProgress:Control {
    int value;internal int Maximum=1000;
    internal int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(Maximum,value));Invalidate();}}
    public ForgeProgress(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AccessibleRole=AccessibleRole.ProgressBar;TabStop=false;}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.Clear(Color.FromArgb(28,37,41));int width=(int)((Width-2)*(Value/(float)Maximum));
        if(width>0)using(var b=new LinearGradientBrush(new Rectangle(1,1,Math.Max(1,Width-2),Height-2),Color.FromArgb(132,94,45),LauncherTheme.Gold,0f))g.FillRectangle(b,1,1,width,Height-2);
        using(var p=new Pen(Color.FromArgb(89,84,66)))g.DrawRectangle(p,0,0,Width-1,Height-1);
        AccessibleDescription=(Value*100/Maximum)+"%";
    }
}
}
