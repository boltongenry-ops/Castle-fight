using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    bool polishLoadingPreview;
    IEnumerator ExerciseFrontend022(){
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"polish022"));Directory.CreateDirectory(folder);MainMenu();yield return new WaitForSecondsRealtime(.5f);
        Check(classicProjectileIndex!=null,"Supplemental classic projectile bundle loaded in actual player");
        var keys=((JObject)classicProjectileIndex["weapons"]).Properties().Select(p=>(string)p.Value).Distinct().ToArray();Check(keys.Length>=69,"Full original weapon art catalog, including particle-only projectiles");
        foreach(string key in keys)for(int team=0;team<2;team++){
            var prefab=ClassicProjectile(key,team);Check(prefab&&prefab.GetComponent<ClassicProjectileModel>()&&prefab.GetComponentsInChildren<Renderer>().All(renderer=>renderer.sharedMaterials.All(mat=>mat&&mat.shader&&mat.mainTexture)),"Native model/material references "+key+" team"+team);
            var instance=Instantiate(prefab);instance.transform.position=new Vector3(0,-10000,0);yield return null;Check(instance.GetComponent<ClassicProjectileModel>().key==key,"Runtime native animation/particle initialization "+key+" team"+team);Destroy(instance);
        }
        networkName="";profileName="";profilePassword="";page=Page.Network;showNetworkProfile=true;yield return new WaitForSecondsRealtime(.3f);Check(networkName==""&&profileName=="","Empty guest/login values survive UI frames without restoring the default");yield return Shot(folder,"01-empty-login");showNetworkProfile=false;MainMenu();
        foreach(string key in new[]{"strategy","naga","ggwp","mushroom"}){
            yield return PlayMemeAudio(key);Check(memeAudio.TryGetValue(key,out var clip)&&clip&&clip.length>.1f&&clip.length<15,"Decode supplied MP3 voice for "+key);
            if(clip){var samples=new float[Mathf.Min(clip.samples*clip.channels,44100*4)];bool read=clip.GetData(samples,0);Check(read&&samples.Any(v=>Mathf.Abs(v)>.0001f),"Nonzero recorded voice samples "+key);}
        }
        LoadStickerCatalog();Check(stickerPacks.SelectMany(p=>p.items).Where(item=>item.id.StartsWith("local_meme_")).All(item=>item.sound=="@meme/"+item.id.Substring(11)),"All four sticker IDs map to their own supplied phrase");if(stickerAudio)stickerAudio.Stop();
        MainMenu();polishLoadingPreview=true;yield return Shot(folder,"02-resource-loading");yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"03-resource-loading-hammer");polishLoadingPreview=false;
        aspectsOn=false;matchMode=0;raceChoice=0;botRaceChoice=1;roundsChoice=1;NewMatch();testHoldAuthority=true;RenderMatch();
        var paths=((JObject)classicProjectileIndex["paths"]).Properties().Where(p=>p.Name.StartsWith("res:")&&p.Name.Contains("models_effects")).Take(9).ToArray();
        int oldSlot=networkSlot;networkSlot=0;var rows=new JArray();
        for(int i=0;i<paths.Length;i++)rows.Add(new JObject{["id"]=700000+i,["k"]="missile",["path"]=paths[i].Name,["p"]=new JArray(-157+i%3*9,World(new Vector2(-157+i%3*9,24+i/3*9)).y+4,24+i/3*9),["t"]=0,["yaw"]=1.57f,["pitch"]=0,["s"]=new JArray(2,2,2)});
        DrawOnlineProjectiles(rows);Check(onlineProjectiles.Count==paths.Length&&onlineProjectiles.Values.All(obj=>obj.GetComponent<ClassicProjectileModel>()),"Network snapshot uses distinct native prefabs instead of generic spheres");Check(onlineProjectiles.Values.Select(obj=>obj.GetComponent<ClassicProjectileModel>().key).Distinct().Count()>4,"Network weapon visuals remain distinct");
        controls.FocusWorld(World(new Vector2(-148,33)),46);yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"04-network-native-projectiles");DrawOnlineProjectiles(new JArray());UpdateOnlineProjectiles(.3f);Check(onlineProjectiles.Count==0,"Expired network projectile models retire after buffered flight");networkSlot=oldSlot;
        var line=match.Lines.Select((value,i)=>new{value,i}).First(v=>v.value.unit!=""&&v.value.weapons.Any(w=>w.type!="normal"));var weapon=line.value.weapons.First(w=>w.type!="normal");
        var unit=match.Spawn(0,line.i,new Vector2(-148,29));var shot=new HumanMatch.Shot{id=900001,sourceId=unit.id,line=line.i,team=0,weapon=weapon,start=unit.position,end=unit.position+Vector2.right*8,duration=1,age=.4f,kind=weapon.type=="instant"?1:3,startHeight=2,endHeight=2};var native=NativeMissilePrefab(shot);Check(native&&native.GetComponent<ClassicProjectileModel>(),"Offline classic weapon resolves source-specific art with HD disabled");var customWeapon=weapon.Copy();customWeapon.missile="corrupted:A0CG";var special=new HumanMatch.Shot{line=line.i,sourceId=unit.id,weapon=customWeapon};Check(ClassicWeaponMissile(special)==null,"Native special ability projectiles keep their dedicated visual overrides");float damage=weapon.damage,speed=weapon.speed;var view=new GameObject("QA shot").AddComponent<PcShotView>();view.Setup(shot.kind,shellMaterial,tracerMaterial,impactMaterial);view.Draw(shot,World,0,false,native);Check(view.NativeObject&&weapon.damage==damage&&weapon.speed==speed,"Visual replacement preserves authority weapon damage and speed");Destroy(view.gameObject);
        MainMenu();var result=new JObject{["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["androidDeviceTest"]=false};File.WriteAllText(Path.Combine(folder,"polish022-verification.json"),result.ToString());Debug.Log("CF_POLISH022_TARGETED_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);Application.Quit(testFailures==0?0:1);
    }
}
}
