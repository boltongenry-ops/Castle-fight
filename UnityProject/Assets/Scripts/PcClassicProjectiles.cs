using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    AssetBundle classicProjectileBundle;JObject classicProjectileIndex;readonly Dictionary<string,GameObject> classicProjectilePrefabs=new Dictionary<string,GameObject>();
    IEnumerator LoadClassicProjectiles(){
        if(MobileContent.Active){yield return MobileContent.Current.EnsurePrefix("ClassicMissiles","Классические снаряды");var text=ContentResources.Load<TextAsset>("ClassicMissiles/index");if(text)classicProjectileIndex=JObject.Parse(text.text);}
        else {string path=MobileContent.Media("Frontend/classic-missiles.windows");if(File.Exists(path)){var operation=AssetBundle.LoadFromFileAsync(path);yield return operation;classicProjectileBundle=operation.assetBundle;if(classicProjectileBundle)classicProjectileIndex=JObject.Parse(classicProjectileBundle.LoadAsset<TextAsset>("Assets/ClassicProjectiles/runtime.json").text);}}
        if(classicProjectileIndex!=null)Debug.Log("CF_CLASSIC_PROJECTILES_READY weapons="+((JObject)classicProjectileIndex["weapons"]).Count+" paths="+((JObject)classicProjectileIndex["paths"]).Count);
    }
    static string ProjectilePath(string value){value=(value??"").Replace('/','\\').ToLowerInvariant();return value.EndsWith(".mdl")||value.EndsWith(".mdx")?value.Substring(0,value.Length-4):value;}
    GameObject ClassicProjectile(string key,int team){
        if(string.IsNullOrEmpty(key)||classicProjectileIndex==null)return null;string id=key+"-t"+team;
        if(classicProjectilePrefabs.TryGetValue(id,out var result))return result;
        result=MobileContent.Active?ContentResources.Load<GameObject>("ClassicMissiles/"+id):classicProjectileBundle?classicProjectileBundle.LoadAsset<GameObject>("Assets/ClassicProjectiles/Prefabs/"+id+".prefab"):null;
        if(result)classicProjectilePrefabs[id]=result;return result;
    }
    GameObject ClassicWeaponMissile(HumanMatch.Shot shot){
        if(classicProjectileIndex==null||shot.weapon==null||shot.line<0||shot.line>=match.Lines.Length||!string.IsNullOrEmpty(shot.chaosSpell))return null;
        string missile=shot.weapon.missile??"";
        // Barrage is ordinary weapon damage with different native missile art.
        // Resolve its original art through the same billboard/Death adapter.
        if(missile=="elven:A015")return ClassicProjectile((string)classicProjectileIndex["paths"]?[ProjectilePath((string)match.Abilities["A015"]?["fields"]?["amat"])],shot.team);
        if(missile.Contains(":")&&!missile.StartsWith("res:",StringComparison.Ordinal))return null;
        string raw=shot.sourceId>=0?match.Units.FirstOrDefault(u=>u.id==shot.sourceId) is HumanMatch.Unit unit?match.Lines[unit.kind].unit:null:null;
        var line=match.Lines[shot.line];if(raw==null)raw=shot.sourceId<0||string.IsNullOrEmpty(line.unit)||line.buildingWeapons.Contains(shot.weapon)||match.Huts.Any(h=>h.id==shot.sourceId)?line.building:line.unit;
        var list=raw==line.building?line.buildingWeapons:line.weapons;int slot=list==null?-1:Array.IndexOf(list,shot.weapon);
        string key=(string)classicProjectileIndex["weapons"]?[raw+"_"+(slot>=0?slot+1:1)];
        if(key==null)key=(string)classicProjectileIndex["paths"]?[ProjectilePath(shot.weapon.missile)];return ClassicProjectile(key,shot.team);
    }
    GameObject ClassicOnlineMissile(JObject row){
        string path=(string)row["path"]??"";string key=(string)classicProjectileIndex?["paths"]?[ProjectilePath(path)];
        if(key==null&&path.StartsWith("native:",StringComparison.Ordinal))key=path.Substring(7);
        int team=((int?)row["t"]??0)==networkSlot?0:1;return ClassicProjectile(key,team);
    }
}
}
