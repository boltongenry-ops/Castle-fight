using System.Collections;
using System.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    IEnumerator ExerciseReforgedRoster(string folder) {
        NewMatch();testHoldAuthority=true;match.Gold[0]=100000;match.Wood[0]=100000;match.Gold[1]=0;options.hdTextures=true;
        foreach(int i in match.BaseKinds){int n=System.Array.IndexOf(match.BaseKinds,i);QaBuild(i,2+n%6*4,15+n/6*4);}
        RenderMatch();Check(huts.Values.All(v=>v.reforged),"all eleven starting buildings instantiate Reforged geometry");
        controls.FocusWorld(World(new Vector2(-151,39)),97);FocusBuilder();controls.FocusWorld(World(new Vector2(-151,39)),97);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"20-reforged-buildings");
        int index=0;foreach(var l in match.Lines.Where(l=>l.Producer)){var p=new Vector2(-188+(index%6)*9,42+index/6*9);var u=match.Spawn(index%2,match.Kind(l.building),p);u.onRoad=true;u.pose="Stand";u.cooldown=1000;index++;}
        RenderMatch();foreach(var model in units.Values){Check(model.reforged,"Reforged unit geometry "+model.raw);Check(model.clips.Any(c=>c.name.StartsWith("Walk"))&&model.clips.Any(c=>c.name.StartsWith("Attack"))&&model.clips.Any(c=>c.name.StartsWith("Death")),"Reforged movement combat death clips "+model.raw);}
        controls.FocusWorld(World(new Vector2(-166,46)),47);builderSelected=false;selectedUnit=match.Units[0].id;yield return new WaitForSecondsRealtime(.4f);yield return Shot(folder,"21-reforged-units");
        foreach(var u in match.Units){u.pose="Attack";u.attackSerial++;}RenderMatch();yield return new WaitForSecondsRealtime(.25f);yield return Shot(folder,"22-reforged-attack");
        foreach(var u in match.Units){u.hp=0;u.pose="Death";}RenderMatch();yield return new WaitForSecondsRealtime(1);yield return Shot(folder,"23-reforged-death");
        Check(units.Values.All(v=>v.GetComponentsInChildren<SkinnedMeshRenderer>().All(s=>s.sharedMesh.vertexCount>500)),"Reforged assets contain actual higher-detail meshes");
        Debug.Log("CF_REFORGED_PLAYER_PASS units="+units.Count+" buildings="+huts.Count+" textures=true animations=true");
    }
}
}
