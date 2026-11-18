using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    AssetBundle desktopMenuBuilders;
    AsyncOperation raceAssetSweep;
    static readonly string[] DesktopRaceCatalogs={"human","orcs","naga","nature","chaos","northern","mech","undead","corrupted","elemental","night","elven","ultimate","nerubian","pandaren","centaur"};

    IEnumerator OpenDesktopMenuBuilders(){
        if(MobileContent.Active)yield break;
        string path=MobileContent.Media("Frontend/race-builders.windows");
        if(!File.Exists(path))yield break;
        var request=AssetBundle.LoadFromFileAsync(path);
        yield return request;
        desktopMenuBuilders=request.assetBundle;
        if(!desktopMenuBuilders)Debug.LogWarning("CF_DESKTOP_MENU_BUILDERS_UNAVAILABLE");
    }

    void ApplyRaceMaterialDefaults(int race){
        switch(race){
        case 6:ResolveMechDefaultColor();break;
        case 7:ResolveUndeadDefaultColors();break;
        case 8:ResolveCorruptedDefaultColors();break;
        case 9:ResolveElementalDefaultColors();break;
        case 10:ResolveNightDefaultColors();break;
        case 11:ResolveElvenDefaultColors();break;
        case 12:case 13:case 14:ResolveFinalDefaultColors();break;
        }
    }

    void LoadDesktopRace(int race,PcRacePack pack=null){
        if(race==0||Pack(race))return;
        pack=pack?pack:ContentResources.Load<PcRacePack>(MobileContent.RaceResources[race]);
        if(!pack)throw new InvalidDataException("Не удалось загрузить ресурсы расы "+RaceTitle(race));
        AssignPack(race,pack);
        ApplyRaceMaterialDefaults(race);
    }

    IEnumerator PrepareDesktopMatch(int player,int opponent,int generation){
        ClearMatch();
        if(DiscardRaceAssets(player,opponent))SweepRaceAssets();
        if(raceAssetSweep!=null&&!raceAssetSweep.isDone)yield return raceAssetSweep;
        foreach(int race in player==opponent?new[]{player}:new[]{player,opponent}){
            if(generation!=versusSerial)yield break;
            if(race==0||Pack(race))continue;
            if(race==15){LoadDesktopRace(race);continue;}
            var request=Resources.LoadAsync<PcRacePack>(MobileContent.RaceResources[race]);
            yield return request;
            if(generation!=versusSerial){SweepRaceAssets();yield break;}
            LoadDesktopRace(race,request.asset as PcRacePack);
        }
    }

    bool DiscardRaceAssets(int keepFirst=-1,int keepSecond=-1){
        if(testing)return false;
        bool released=false;
        for(int race=1;race<MobileContent.RaceResources.Length;race++){
            if(race==keepFirst||race==keepSecond||!Pack(race))continue;
            AssignPack(race,null);
            released=true;
        }
        return released;
    }

    void SweepRaceAssets(){
        if(raceAssetSweep==null||raceAssetSweep.isDone)raceAssetSweep=Resources.UnloadUnusedAssets();
    }

    void ReleaseIdleRaceAssets(bool hadContent){
        if(testing)return;
        bool released=DiscardRaceAssets();
        if(!released&&!hadContent)return;
        ClearPortraits();
        if(MobileContent.Active&&!MobileContent.Current.Busy)MobileContent.Current.ReleaseRaceBundles();
        SweepRaceAssets();
    }

    void ClearRacePrefabCaches(){
        HdModels.ClearPrefabs();
        northernFxPrefabs.Clear();mechFxPrefabs.Clear();undeadFxPrefabs.Clear();corruptedFxPrefabs.Clear();
        elementalFxPrefabs.Clear();nightFxPrefabs.Clear();elvenFxPrefabs.Clear();finalFxPrefabs.Clear();
        classicProjectilePrefabs.Clear();portraitRaw=null;
    }

    void ClearPortraits(){
        if(portraitCamera)portraitCamera.enabled=false;
        if(portraitModel){portraitModel.gameObject.SetActive(false);Destroy(portraitModel.gameObject);}
        if(menuPortraitModel){menuPortraitModel.gameObject.SetActive(false);Destroy(menuPortraitModel.gameObject);}
        portraitModel=menuPortraitModel=null;portraitRaw=menuPortraitRaw=null;
    }

    void PreserveAssetAuditForHistoricalTests(){
        for(int race=1;race<MobileContent.RaceResources.Length;race++)LoadDesktopRace(race);
        AuditMaterials();AuditUndeadParticles();AuditCorruptedParticles();AuditElementalParticles();AuditNightParticles();AuditElvenParticles();AuditFinalParticles();
    }
}
}
