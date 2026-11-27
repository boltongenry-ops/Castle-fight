using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CastleFight {
public sealed partial class PcGame {
    PcProgressionStore progression;bool progressShop,challengeSettled;Vector2 progressionScroll;string purchaseConfirm;
    JObject activeChallenge;int challengeReward;int[] savedChallengeSetup;
    GameObject builderCosmetic;Material builderCosmeticMaterial;
    static readonly string[] ProgressRaces={"Human Alliance","Orcish Horde","Naga Faction","Nature Force","Chaos Legions","Northern Realms","Mech Coalition","Undead Forces","Corrupted Conglomerate","Elementals' Universe","Night Elfs","Elven Union","Ultimate Builder","Nerubian Swarm","Pandaren Order","Centaur Khanates","Pepe Brotherhood"};
    void LoadProgression(){
        var asset=ContentResources.Load<TextAsset>("PcProgression");
        if(!asset)throw new InvalidOperationException("Missing progression catalog");
        var data=JObject.Parse(asset.text);
        ChallengeCatalog034.Extend(data);
        ChallengeCampaign038.Extend(data);
        data["mythic"]=new JArray(WinterTrialDefinition(),ChoirDefinition(),HordeDefinition(),ProtocolDefinition(),AbyssDefinition(),QuackDefinition());
        var items=(JArray)data["items"];
        items.Add(new JObject{["id"]=MemePackId,["kind"]="stickers",["title"]="Рофлопак · местные легенды",["description"]="4 стикера: наги, гриб, вышки и Терминатор. Для личного чата и сетевого боя.",["cost"]=300});
        WorkshopCatalog038.Extend(items);
        progression=new PcProgressionStore(data,BattleLab.Arg("--pc-profile-file",Path.Combine(Application.persistentDataPath,"pc-profile-v1.json")));
    }
    void OpenProgression(bool shop){
        progressShop=shop;progressionScroll=Vector2.zero;purchaseConfirm=null;page=Page.Progression;
        mythicTab=false;mythicSelection=null;
        Workshop.dragFinger=-1;Workshop.dragged=false;
        if(shop)OpenWorkshop();
    }
    void BeginChallenge(JObject row,int chosenRace=-1){
        if(row==null)return;EndChallenge();
        int player=chosenRace>=0?chosenRace:Array.IndexOf(ProgressRaces,(string)row["race"]);
        int enemy=Array.IndexOf(ProgressRaces,(string)row["opponent"]);
        if(!RaceStaged(player)||!RaceStaged(enemy)){Notice("Раса пока недоступна");return;}
        savedChallengeSetup=new[]{raceChoice,botRaceChoice,matchMode,roundsChoice,startGold,difficulty,aspectsOn?1:0};
        activeChallenge=(JObject)row.DeepClone();raceChoice=player;botRaceChoice=enemy;matchMode=0;roundsChoice=1;startGold=(int?)row["start_gold"]??500;difficulty=Mathf.Clamp((int?)row["difficulty"]??1,0,2);aspectsOn=false;
        AccountHub.challengeOwner=CloudProgression?AccountHub.owner:"";
        BeginOfflineVersus();Debug.Log("CF_PC_CHALLENGE_START id="+row["id"]+" rules="+row.ToString(Newtonsoft.Json.Formatting.None));
    }
    void EndChallenge(){if(savedChallengeSetup!=null){raceChoice=savedChallengeSetup[0];botRaceChoice=savedChallengeSetup[1];matchMode=savedChallengeSetup[2];roundsChoice=savedChallengeSetup[3];startGold=savedChallengeSetup[4];difficulty=savedChallengeSetup[5];aspectsOn=savedChallengeSetup[6]==1;savedChallengeSetup=null;}activeChallenge=null;challengeSettled=false;challengeReward=0;}
    void SettleChallenge(){
        if(activeChallenge==null||challengeSettled||onlineMatch)return;
        StopBossSoundtrack();
        if(match.ChallengeVictoryEligible)try{
            string id=(string)activeChallenge["id"];
            if(string.IsNullOrEmpty(AccountHub.challengeOwner))challengeReward=progression.Complete(id,match.Elapsed);
            else AccountChallengeVictory(id,match.Elapsed);
            ShareChallengeVictory(id);
        }catch(IOException error){Notice("Не удалось сохранить награду: "+error.Message);return;}
        challengeSettled=true;CaptureMythicScore();Debug.Log("CF_PC_CHALLENGE_RESULT id="+activeChallenge["id"]+" eligible="+match.ChallengeVictoryEligible+" reward="+challengeReward);
    }
    Color CosmeticColor(JObject row){return row!=null&&ColorUtility.TryParseHtmlString("#"+(string)row["color"],out var color)?color:new Color(.38f,.45f,.43f);}
    Texture2D ProfileAvatar(){
        var identity=networkProfile??(HasEndpointToken?Avatars.ownIdentity:null);
        string avatarItem=identity!=null&&!HasNetworkProfile?(string)identity["equipped"]??"":AccountEquipment("avatar");
        var row=progression.Item(avatarItem);
        if(row!=null)return ContentResources.Load<Texture2D>("RaceIcon-"+(string)row["asset"]);
        var image=AccountAvatar(identity);
        return image?image:unitIcons.FirstOrDefault();
    }
    void DrawProfileBadge(Rect rect){var frame=CosmeticColor(progression.Item(AccountEquipment("frame")));Fill(rect,new Color(.035f,.05f,.055f));var icon=ProfileAvatar();if(icon)GUI.DrawTexture(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),icon,ScaleMode.ScaleToFit);Border(rect,frame,3);}
    void DrawProgression(){
        EnsureRaceStyles();if(progressShop){DrawWorkshop();return;}
        var all=(JArray)progression.Catalog["challenges"];
        var state=Workshop;
        AtlasSurface(new Rect(20,18,1240,684));
        DrawProfileBadge(new Rect(42,34,64,64));Text(126,30,740,49,"Испытания и награды",atlasTitle);
        int experience=all.OfType<JObject>().Where(row=>AccountCompleted((string)row["id"])).Sum(ChallengeCatalog034.Experience);
        int level=ChallengeCatalog034.Level(experience);
        Text(128,77,895,27,"Ур. "+level+" · "+ChallengeCatalog034.Title(level)+"  |  "+experience+" / "+ChallengeCatalog034.Threshold(level+1)+" опыта  |  "+all.Count(row=>AccountCompleted((string)row["id"]))+" / "+all.Count+" пройдено  |  "+AccountCoins+" жетонов",small);
        if(Button("progress.back",new Rect(1074,38,164,42),MythicRacePicker?"← Назад":"← В меню")){
            if(MythicRacePicker)mythicSelection=null;else MainMenu();
        }
        if(HasNetworkProfile)DrawFriendsShortcut(new Rect(1011,38,49,42));
        if(Button("progress.challenges",new Rect(42,116,208,42),"Все испытания",!state.siegeOnly&&!mythicTab)){
            state.siegeOnly=mythicTab=false;mythicSelection=null;progressionScroll=Vector2.zero;
        }
        if(Button("progress.siege",new Rect(260,116,180,42),"Осадные",state.siegeOnly&&!mythicTab)){
            state.siegeOnly=true;mythicTab=false;mythicSelection=null;progressionScroll=Vector2.zero;
        }
        if(Button("progress.mythic",new Rect(450,116,205,42),"Эпохальные",mythicTab)){
            mythicTab=true;mythicSelection=null;progressionScroll=Vector2.zero;mythicBoardPage=0;
            StartCoroutine(LoadMythicLeaderboard());
        }
        if(Button("progress.shop",new Rect(665,116,180,42),"Магазин")){OpenProgression(true);return;}
        if(MythicRacePicker){DrawMythicRacePicker();return;}
        if(mythicTab){DrawMythicChallenges();return;}
        var rows=all.OfType<JObject>().Where(row=>!state.siegeOnly||ChallengeCampaign038.IsSiege(row)).ToArray();
        const int cardHeight=174;
        state.maxChallengeScroll=Mathf.Max(0,rows.Length*(cardHeight+10)-502);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!state.dragged;
        progressionScroll=GUI.BeginScrollView(new Rect(40,176,1200,502),progressionScroll,new Rect(0,0,1174,rows.Length*(cardHeight+10)));
        for(int i=0;i<rows.Length;i++){
            var row=rows[i];string id=(string)row["id"];float y=i*(cardHeight+10);
            AtlasTile(new Rect(0,y,1170,cardHeight),false,false);
            AtlasScene(3,new Rect(12,y+13,102,146));
            Fill(new Rect(12,y+13,102,146),new Color(.033f,.042f,.023f,.5f));
            int a=Array.IndexOf(ProgressRaces,(string)row["race"]),b=Array.IndexOf(ProgressRaces,(string)row["opponent"]);var first=RaceEmblem(a);var second=RaceEmblem(b);
            if(first)GUI.DrawTexture(new Rect(18,y+30,52,52),first,ScaleMode.ScaleToFit);
            if(second)GUI.DrawTexture(new Rect(58,y+87,52,52),second,ScaleMode.ScaleToFit);
            Text(128,y+12,790,34,(string)row["title"],raceTitleStyle);Text(128,y+50,816,76,(string)row["description"],new GUIStyle(label){fontSize=18,wordWrap=true});
            bool done=AccountCompleted(id);
            float best=AccountBest(id);
            string summary=done?"Пройдено"+(best>0?" · лучший результат "+TimeSpan.FromSeconds(best).ToString(@"mm\:ss"):""):
                "Награда: "+row["reward"]+" жетонов · "+ChallengeCatalog034.Experience(row)+" опыта · "+((int?)row["difficulty"]==2?"сложный":"обычный")+" бот";
            Text(128,y+139,800,25,summary,small);
            if(Button("challenge."+id,new Rect(978,y+48,172,48),done?"Повторить":"Начать",!done)){
                BeginChallenge(row);
                break;
            }
        }
        GUI.EndScrollView();
        GUI.enabled=enabled;
    }
    void DrawChallengeHud(){
        if(activeChallenge==null)return;
        if(match.QuackActive){DrawQuackHud();return;}
        if(match.LastProtocol){DrawProtocolHud();return;}
        if(match.AbyssTrialActive){DrawAbyssHud();return;}
        if(match.CursedHorde){DrawHordeHud();return;}
        if(match.FuneralChoir){DrawChoirHud();return;}
        if(match.EternalWinter){DrawWinterHud();return;}
        var strip=new Rect(210,36,661,55);
        Fill(strip,new Color(.015f,.025f,.03f,.89f));
        string titleText=(string)activeChallenge["title"];
        float survival=match.ChallengeSurvivalTime;
        float timeLimit=survival>0?survival:HumanMatch.F(activeChallenge,"max_seconds");
        if(timeLimit>0){
            string remaining=TimeSpan.FromSeconds(Mathf.Max(0,timeLimit-match.Elapsed)).ToString(@"mm\:ss");
            titleText+=(survival>0?" · удержать ещё ":" · до срока ")+remaining;
        }
        var details=new System.Collections.Generic.List<string>();
        if(activeChallenge["max_buildings"]!=null)details.Add("постройки "+match.Huts.Count(h=>h.team==0)+"/"+activeChallenge["max_buildings"]);
        if(activeChallenge["max_producers"]!=null)details.Add("производителей ≤ "+activeChallenge["max_producers"]);
        if(activeChallenge["min_upgraded_buildings"]!=null)details.Add("улучшено "+match.ChallengeUpgradedBuildings+"/"+activeChallenge["min_upgraded_buildings"]);
        if(activeChallenge["min_distinct_producers"]!=null)details.Add("ветки "+match.ChallengeProducerFamilies+"/"+activeChallenge["min_distinct_producers"]);
        float health=HumanMatch.F(activeChallenge,"min_castle_fraction");
        if(health>0)details.Add("замок ≥ "+Mathf.RoundToInt(health*100)+"%");
        if(details.Count==0&&ChallengeCampaign038.IsSiege(activeChallenge))
            details.Add("предел HP "+match.CastleHealthLimit(0).ToString("F0")+" / "+match.CastleHealthLimit(1).ToString("F0"));
        Text(221,39,630,27,titleText,small);
        if(details.Count>0)Text(221,62,630,26,string.Join(" · ",details),small);
    }
    void DrawChallengeResult(){if(activeChallenge==null)return;string result=!match.ChallengeVictoryEligible?"Условия испытания не выполнены":CurrentMythic?"Испытание пройдено":challengeReward>0?"+"+challengeReward+" жетонов · +"+ChallengeCatalog034.Experience(activeChallenge)+" опыта · ур. "+progression.MasteryLevel:"Испытание пройдено · награда уже получена";if(!challengeSettled&&match.ChallengeVictoryEligible){if(Button("challenge.retry-save",new Rect(450,345,380,32),"Повторить сохранение"))SettleChallenge();}else Text(450,345,380,35,result,small);
        if(CurrentMythic&&match.ChallengeVictoryEligible){
            if(mythicPlace>0)Text(450,512,380,30,"Ваше место · "+mythicPlace,small);
            else if(pendingMythicScore!=null&&LobbyButton("mythic.score.open",new Rect(450,509,380,39),mythicScoreSending?"Сохраняем…":"Записать в таблицу"))SubmitPendingMythicScore();
        }
    }
    void RenderBuilderCosmetic(){
        string id=progression==null?"":AccountEquipment("aura");
        if(onlineMatch||match==null||!builders[0]){
            ClearWorkshopBattle();if(builderCosmetic)builderCosmetic.SetActive(false);return;
        }
        UpdateWorkshopBattle(builders[0],id);
        if(id!="aura_jade"&&id!="aura_flame"){if(builderCosmetic)builderCosmetic.SetActive(false);return;}
        if(!builderCosmetic){builderCosmetic=Ring("Local cosmetic builder aura",visuals.transform);if(!builderCosmeticMaterial)builderCosmeticMaterial=new Material(overlayMaterial);builderCosmetic.GetComponent<Renderer>().sharedMaterial=builderCosmeticMaterial;}
        builderCosmetic.SetActive(true);var color=CosmeticColor(progression.Item(id));builderCosmeticMaterial.color=new Color(color.r,color.g,color.b,.7f);builderCosmetic.transform.position=builders[0].transform.position+Vector3.up*.13f;builderCosmetic.transform.localScale=Vector3.one*(1.15f+Mathf.Sin(Time.unscaledTime*2)*.04f);UpdateRichAura(id);
    }
}
}
