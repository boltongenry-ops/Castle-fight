using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator VerifyMapArt(string directory){
        if(string.IsNullOrEmpty(BattleLab.Arg("--pc-settings-file")))throw new ArgumentException("Tests require isolated settings");
        Directory.CreateDirectory(directory);
        var art=MapArt;var placements=controls.sceneryRoot.GetComponentsInChildren<NativePlacement>(true);
        int original=placements.Sum(p=>p.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled));
        var ground=controls.sceneryRoot.transform.parent.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Ground 2");
        var classic=ground.sharedMaterial;
        if(!art.Installed)yield return art.Download(controls);
        if(!art.Installed||ground.sharedMaterial!=classic)throw new Exception("Explicit installation failed");
        art.Preferred=true;yield return art.Apply(controls);
        if(ground.sharedMaterial==classic||ground.sharedMaterial.mainTexture.width<1024)throw new Exception("HD ground missing");
        int props=placements.Count(p=>p.transform.Find("Reforged "+p.modelKey));
        if(props<1000)throw new Exception("Expected over 1000 HD placements, got "+props);
        int walls=0;
        foreach(var placement in placements.Where(p=>p.modelKey!=null&&p.modelKey.Contains("ruinswall"))){
            var hd=placement.transform.Find("Reforged "+placement.modelKey).GetComponent<MeshRenderer>();
            var sources=placement.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r!=hd).ToArray();
            var expected=sources[0].bounds;foreach(var r in sources.Skip(1))expected.Encapsulate(r.bounds);
            var actual=hd.bounds;
            if((actual.center-expected.center).magnitude>.025f||(actual.size-expected.size).magnitude>.035f)
                throw new Exception("Wall footprint differs: "+placement.name+" "+actual+" / "+expected);
            walls++;
        }
        if(walls<80)throw new Exception("Missing walls");Debug.Log("CF_MAP_WALLS_PASS "+walls);
        controls.Focus("west");page=Page.Viewer;yield return null;yield return Shot(directory,"map-hd");
        art.Preferred=false;yield return art.Apply(controls);yield return null;
        if(classic!=ground.sharedMaterial||original!=placements.Sum(p=>p.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled)))throw new Exception("Classic restoration failed");
        yield return Shot(directory,"map-classic");art.Remove();if(art.Installed)throw new Exception("Deletion failed");
        Debug.Log("CF_MAP_CHECKS_PASS placements="+props);Application.Quit();
    }
}
}
