using System;

namespace CastleFight {
internal static class CastleLoadingMotion {
    internal const int LoopSeconds=6;
    internal const int FramesPerSecond=25;
    internal const int PuffCount=14;
    internal const int WindowCount=7;
    internal const int MaskSize=64;
    const double Tau=2*Math.PI;

    internal struct Sprite {
        public float x,y,width,height,opacity;
        public Sprite(double x,double y,double width,double height,double opacity){
            this.x=(float)x;this.y=(float)y;this.width=(float)width;this.height=(float)height;this.opacity=(float)opacity;
        }
    }

    // Coordinates are relative to the unchanging first tile of the castle atlas.
    static readonly double[,] Windows={
        {.233,.429,.018,.069}, {.273,.439,.017,.063}, {.465,.350,.012,.049},
        {.506,.338,.013,.058}, {.583,.428,.016,.059}, {.614,.417,.017,.065},
        {.811,.460,.016,.058}
    };

    internal static Sprite SmokePuff(int index,double seconds){
        int trail=index%2,number=index/2;
        double phase=seconds/LoopSeconds+number/7.0+trail*.21;
        double age=phase-Math.Floor(phase);
        double swell=Math.Sin(Math.PI*age);
        double originX=trail==0?.267:.547,originY=trail==0?.301:.243;
        double plumeX=originX+age*(trail==0?.26:.34)+.038*swell*Math.Sin(age*6+number*3.1);
        double plumeY=originY-age*(trail==0?.51:.46)+.013*swell*Math.Sin(number*2+age*7);
        double width=.10+.36*age;
        return new Sprite(plumeX,plumeY,width,width*.78,
            Math.Pow(swell,1.7)*(trail==0?.49:.57));
    }

    internal static Sprite WindowGlow(int index,double seconds){
        double phase=Tau*seconds/LoopSeconds,indexPhase=index*.94;
        double flame=.52+.24*Math.Sin(3*phase+indexPhase)+.16*Math.Sin(7*phase+indexPhase*2.1)
            +.06*Math.Sin(11*phase-indexPhase);
        return new Sprite(Windows[index,0],Windows[index,1],Windows[index,2],Windows[index,3],flame);
    }

    static double Clamp01(double value){return Math.Max(0,Math.Min(1,value));}

    // The mask is built once and shared by all puffs, so drawing never creates particle textures.
    internal static byte SmokeMask(int column,int row){
        double x=(column+.5)*2/MaskSize-1,y=(row+.5)*2/MaskSize-1;
        double radius=x*x+y*y;
        if(radius>=1)return 0;
        double variation=.55+.17*Math.Sin(x*12+y*3)+.15*Math.Sin(y*13-x*6)+
            .10*Math.Sin(x*23+y*19)+.05*Math.Sin(y*39+x*32);
        return (byte)Math.Round(255*Clamp01(Math.Pow(1-radius,1.9)*variation));
    }

    internal static byte GlowMask(int column,int row){
        double x=(column+.5)*2/MaskSize-1,y=(row+.5)*2/MaskSize-1;
        double radius=x*x+y*y;
        return (byte)Math.Round(255*Math.Pow(Clamp01(1-radius),2.1));
    }
}
}
