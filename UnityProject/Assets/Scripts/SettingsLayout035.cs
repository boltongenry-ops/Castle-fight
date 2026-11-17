using UnityEngine;
namespace CastleFight {
public static class SettingsLayout035 {
    public static bool PreviewMobile;
    public static bool Mobile=>Application.isMobilePlatform||PreviewMobile;
    public static Rect Control(int row)=>new Rect(610,116+44*row,365,38);
    public static Rect Downloads=>new Rect(290,565,420,38);
    public static Rect Updates=>new Rect(730,565,245,38);
    public static Rect Cancel=>new Rect(290,622,220,48);
    public static Rect Apply=>new Rect(660,622,315,48);
}
}
