using System.Runtime.CompilerServices;
namespace CastleFight {
// Retain the published 0.33.2 managed type; new per-draft settings live outside serialized Options.
static class SoftFogSetting {public static bool Enabled;}
public static class FogOption034 {
    sealed class Mode {public bool enabled;}
    static readonly ConditionalWeakTable<PcGame.Options,Mode> modes=new ConditionalWeakTable<PcGame.Options,Mode>();
    public static bool Get(PcGame.Options options)=>modes.GetValue(options,_=>new Mode()).enabled;
    public static void Set(PcGame.Options options,bool value)=>modes.GetValue(options,_=>new Mode()).enabled=value;
}
}
