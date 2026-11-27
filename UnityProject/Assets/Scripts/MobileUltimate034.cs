using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
// Runtime shell keeps catalog row offsets, but never loads the monolithic race-12 asset.
// Loading that ScriptableObject materializes every race through serialized references.
public static class MobileUltimate034 {
    public sealed class State {public JObject art=JObject.Parse(UltimateAssetMap034.Json);public string[][] draw;public PcRacePack shell;}
    static readonly ConditionalWeakTable<PcGame,State> states=new ConditionalWeakTable<PcGame,State>();
    public static State For(PcGame game)=>states.GetValue(game,_=>new State());
    public static PcRacePack Shell(PcGame game){var s=For(game);if(s.shell)return s.shell;var p=ScriptableObject.CreateInstance<PcRacePack>();p.name="Ultimate selected models";
        int n=((JArray)s.art["buildingIcons"]).Count;p.catalog=ContentResources.Load<TextAsset>("MobileCatalogs/12");p.units=new GameObject[n*2];p.buildings=new GameObject[n*2];p.builders=new GameObject[2];p.buildingIcons=new Texture2D[n];p.hdUnits=p.hdBuildings=p.hdBuilders=Array.Empty<GameObject>();p.abilityIds=HumanMatch.Strings(s.art["abilityIds"]);p.abilityIcons=new Texture2D[p.abilityIds.Length];s.shell=p;return p;}
    public static string Path(PcGame game,string group,int index=-1){var a=For(game).art[group];return index<0?(string)a:(string)a[index];}
}
public sealed partial class PcGame {
    IEnumerator PrepareUltimateShell034(){
        if(ultimatePack)yield break;var p=MobileUltimate034.Shell(this);ultimatePack=p;
        for(int t=0;t<2;t++){int side=t;yield return MobileContent.Current.LoadNamedAsync<GameObject>(MobileUltimate034.Path(this,"builders",t),v=>p.builders[side]=v);}
        yield return MobileContent.Current.LoadNamedAsync<Texture2D>(MobileUltimate034.Path(this,"builderIcon"),v=>p.builderIcon=v);
        yield return MobileContent.Current.LoadNamedAsync<Texture2D>(MobileUltimate034.Path(this,"buildIcon"),v=>p.buildIcon=v);
    }
    IEnumerator PrepareUltimateDraw034(int a,int b,JArray server=null,int viewerSlot=-1){
        if(viewerSlot<0)viewerSlot=networkSlot;
        var s=MobileUltimate034.For(this);s.draw=null;if(a!=12&&b!=12)yield break;
        var canonical=JObject.Parse(RaceCatalog(12));var draw=new UltimateLoadout((JArray)canonical["ultimateSlots"],n=>UnityEngine.Random.Range(0,n));
        s.draw=new[]{Array.Empty<string>(),Array.Empty<string>()};
        for(int t=0;t<2;t++){if((t==0?a:b)!=12)continue;
            var ids=server!=null?HumanMatch.Strings(server[t==0?viewerSlot:1-viewerSlot]):draw.Draw();
            if(!draw.Valid(new JArray(ids)))throw new InvalidOperationException("Invalid Ultimate round assets");s.draw[t]=ids;
            var playable=draw.Playable(canonical,ids);int row=0;
            foreach(JObject line in (JArray)playable["lines"]){int i=row++;if((bool?)line["allowed"]!=true)continue;int index=t*(ultimatePack.units.Length/2)+i;
                foreach(bool building in new[]{true,false}){var arr=building?ultimatePack.buildings:ultimatePack.units;if(arr[index])continue;string path=MobileUltimate034.Path(this,building?"buildings":"units",index);if(path.Length>0)yield return MobileContent.Current.LoadNamedAsync<GameObject>(path,v=>arr[index]=v);}
                if(!ultimatePack.buildingIcons[i])yield return MobileContent.Current.LoadNamedAsync<Texture2D>(MobileUltimate034.Path(this,"buildingIcons",i),v=>ultimatePack.buildingIcons[i]=v);
            }
        }
        Debug.Log("CF_MOBILE_ULTIMATE_SELECTED models="+ultimatePack.Prefabs.Count()+" all="+(ultimatePack.units.Length+ultimatePack.buildings.Length));
    }
    GameObject UltimateModel034(int team,int row,bool building){var p=ultimatePack;var arr=building?p.buildings:p.units;int i=team*(arr.Length/2)+row;if(!arr[i]){string path=MobileUltimate034.Path(this,building?"buildings":"units",i);if(path.Length>0)arr[i]=MobileContent.Current.LoadNamed<GameObject>(path);}return arr[i];}
    Texture2D UltimateIcon034(int row){if(!ultimatePack.buildingIcons[row])ultimatePack.buildingIcons[row]=MobileContent.Current.LoadNamed<Texture2D>(MobileUltimate034.Path(this,"buildingIcons",row));return ultimatePack.buildingIcons[row];}
    Texture2D UltimateAbility034(string id){if(!MobileContent.Active||!ultimatePack)return null;int i=Array.IndexOf(ultimatePack.abilityIds,id);if(i<0)return null;if(!ultimatePack.abilityIcons[i])ultimatePack.abilityIcons[i]=MobileContent.Current.LoadNamed<Texture2D>(MobileUltimate034.Path(this,"abilityIcons",i));return ultimatePack.abilityIcons[i];}
}
}
