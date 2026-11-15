using System.Collections.Generic;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    Dictionary<string,Texture2D> reforgedIcons;
    Texture2D HdIcon(Texture2D original) {
        if(!original||!options.hdTextures)return original;
        if(reforgedIcons==null){reforgedIcons=new Dictionary<string,Texture2D>();foreach(var icon in ContentResources.LoadAll<Texture2D>("ReforgedIcons"))reforgedIcons[icon.name]=icon;Debug.Log("CF_REFORGED_ICONS loaded="+reforgedIcons.Count);}
        return reforgedIcons.TryGetValue(original.name,out var replacement)?replacement:original;
    }
}
}
