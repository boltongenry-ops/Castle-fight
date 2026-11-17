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
    static readonly string[] ProgressRaces={"Human Alliance","Orcish Horde","Naga Faction","Nature Force","Chaos Legions","Northern Realms","Mech Coalition","Undead Forces","Corrupted Conglomerate","Elementals' Universe","Night Elfs","Elven Union","Ultimate Builder","Nerubian Swarm","Pandaren Order","Centaur Khanates"};
    void LoadProgression(){var asset=ContentResources.Load<TextAsset>("PcProgression");if(!asset)throw new InvalidOperationException("Missing progression catalog");var data=JObject.Parse(asset.text);ChallengeCatalog034.Extend(data);((JArray)data["items"]).Add(new JObject{["id"]=MemePackId,["kind"]="stickers",["title"]="Рофлопак · местные легенды",["description"]="4 стикера: наги, гриб, вышки и Терминатор. Для личного чата и сетевого боя.",["cost"]=300});progression=new PcProgressionStore(data,BattleLab.Arg("--pc-profile-file",Path.Combine(Application.persistentDataPath,"pc-profile-v1.json")));}
    void OpenProgression(bool shop){progressShop=shop;progressionScroll=Vector2.zero;purchaseConfirm=null;page=Page.Progression;}
    void BeginChallenge(JObject row){
        if(row==null)return;EndChallenge();
        int player=Array.IndexOf(ProgressRaces,(string)row["race"]),enemy=Array.IndexOf(ProgressRaces,(string)row["opponent"]);
        if(!RaceStaged(player)||!RaceStaged(enemy)){Notice("Раса пока недоступна");return;}
        savedChallengeSetup=new[]{raceChoice,botRaceChoice,matchMode,roundsChoice,startGold,difficulty,aspectsOn?1:0};
        activeChallenge=(JObject)row.DeepClone();raceChoice=player;botRaceChoice=enemy;matchMode=0;roundsChoice=1;startGold=(int?)row["start_gold"]??500;difficulty=Mathf.Clamp((int?)row["difficulty"]??1,0,2);aspectsOn=false;
        BeginOfflineVersus();Debug.Log("CF_PC_CHALLENGE_START id="+row["id"]+" rules="+row.ToString(Newtonsoft.Json.Formatting.None));
    }
    void EndChallenge(){if(savedChallengeSetup!=null){raceChoice=savedChallengeSetup[0];botRaceChoice=savedChallengeSetup[1];matchMode=savedChallengeSetup[2];roundsChoice=savedChallengeSetup[3];startGold=savedChallengeSetup[4];difficulty=savedChallengeSetup[5];aspectsOn=savedChallengeSetup[6]==1;savedChallengeSetup=null;}activeChallenge=null;challengeSettled=false;challengeReward=0;}
    void SettleChallenge(){
        if(activeChallenge==null||challengeSettled||onlineMatch)return;
        if(match.ChallengeVictoryEligible)try{challengeReward=progression.Complete((string)activeChallenge["id"],match.Elapsed);}catch(IOException error){Notice("Не удалось сохранить награду: "+error.Message);return;}
        challengeSettled=true;Debug.Log("CF_PC_CHALLENGE_RESULT id="+activeChallenge["id"]+" eligible="+match.ChallengeVictoryEligible+" reward="+challengeReward);
    }
    Color CosmeticColor(JObject row){return row!=null&&ColorUtility.TryParseHtmlString("#"+(string)row["color"],out var color)?color:new Color(.38f,.45f,.43f);}
    Texture2D ProfileAvatar(){var row=progression.Item(progression.Equipped("avatar"));return row!=null?ContentResources.Load<Texture2D>("RaceIcon-"+(string)row["asset"]):unitIcons.FirstOrDefault();}
    void DrawProfileBadge(Rect rect){var frame=CosmeticColor(progression.Item(progression.Equipped("frame")));Fill(rect,new Color(.035f,.05f,.055f));var icon=ProfileAvatar();if(icon)GUI.DrawTexture(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),icon,ScaleMode.ScaleToFit);Border(rect,frame,3);}
    void DrawProgression(){
        EnsureRaceStyles();Fill(new Rect(20,18,1240,684),new Color(.025f,.04f,.045f,.96f));Border(new Rect(20,18,1240,684),new Color(.36f,.31f,.22f));
        DrawProfileBadge(new Rect(42,34,64,64));Text(126,32,740,44,progressShop?"Магазин оформления":"Испытания и награды",heading);
        Text(128,78,850,24,"Ур. "+progression.MasteryLevel+" · "+progression.MasteryTitle+"  |  "+progression.Experience+" / "+progression.NextLevelExperience+" опыта  |  "+progression.CompletedCount+" / 30 пройдено  |  "+progression.Coins+" жетонов",small);
        if(Button("progress.back",new Rect(1074,38,164,42),"← В меню"))MainMenu();
        if(Button("progress.challenges",new Rect(42,116,270,42),"Испытания",!progressShop))OpenProgression(false);
        if(Button("progress.shop",new Rect(322,116,270,42),"Магазин",progressShop))OpenProgression(true);
        Text(616,126,610,28,progressShop?"Оформление — с ботом; стикеры — и в сетевом бою.":"Прогресс мастерства на этом устройстве · награды один раз",small);
        var rows=(JArray)progression.Catalog[progressShop?"items":"challenges"];float cardHeight=progressShop?204:174;
        progressionScroll=GUI.BeginScrollView(new Rect(40,176,1200,502),progressionScroll,new Rect(0,0,1174,progressShop?Mathf.Ceil(rows.Count/2f)*(cardHeight+12):rows.Count*(cardHeight+10)));
        for(int i=0;i<rows.Count;i++){
            var row=(JObject)rows[i];string id=(string)row["id"];float x=progressShop?(i%2)*590:0,y=progressShop?(i/2)*(cardHeight+12):i*(cardHeight+10),width=progressShop?578:1170;
            Fill(new Rect(x,y,width,cardHeight),new Color(.055f,.08f,.085f));Border(new Rect(x,y,width,cardHeight),new Color(.26f,.30f,.28f));
            if(progressShop){
                string kind=(string)row["kind"];bool owned=progression.Owned(id),equipped=kind!="stickers"&&progression.Equipped(kind)==id;Color tint=CosmeticColor(row);
                if(kind=="stickers"){LoadStickerCatalog();var item=stickerPacks.Last().items[0];StickerImage(new Rect(x+8,y+10,110,118),item);if(Button("progress.preview.meme",new Rect(x+12,y+118,104,28),"Смотреть")){stickerPack=stickerPacks.Count-1;stickerSelected=0;showStickerPicker=true;}}
                else if(kind=="avatar"){var icon=ContentResources.Load<Texture2D>("RaceIcon-"+(string)row["asset"]);if(icon)GUI.DrawTexture(new Rect(x+18,y+22,88,88),icon,ScaleMode.ScaleToFit);}
                else if(kind=="frame"){DrawProfileBadge(new Rect(x+22,y+25,78,78));Border(new Rect(x+22,y+25,78,78),tint,4);}
                else{for(int ring=0;ring<5;ring++)Border(new Rect(x+21+ring*6,y+26+ring*6,82-ring*12,82-ring*12),new Color(tint.r,tint.g,tint.b,1-ring*.13f),2);}
                Text(x+126,y+16,430,35,(string)row["title"],raceTitleStyle);Text(x+126,y+58,425,72,(string)row["description"],new GUIStyle(label){fontSize=16});
                Text(x+18,y+155,285,28,equipped?"Надето":owned?"В коллекции":row["cost"]+" жетонов",small);
                string caption=kind=="stickers"&&owned?"Открыть набор":equipped?"Снять":owned?"Надеть":purchaseConfirm==id?"Подтвердить":"Купить";
                GUI.enabled=owned||progression.Coins>=(int)row["cost"];
                if(Button("progress.item."+id,new Rect(x+348,y+144,208,43),caption,!owned))try{
                    if(owned){if(kind=="stickers"){LoadStickerCatalog();stickerPack=stickerPacks.Count-1;stickerSelected=0;showStickerPicker=true;}else progression.Equip(id,!equipped);purchaseConfirm=null;}
                    else if(purchaseConfirm!=id)purchaseConfirm=id;
                    else{if(progression.Buy(id,out string error)){Notice("Куплено: "+row["title"]);purchaseConfirm=null;}else Notice(error);}
                }catch(IOException error){Notice("Не удалось сохранить покупку: "+error.Message);}GUI.enabled=true;
            }else{
                int a=Array.IndexOf(ProgressRaces,(string)row["race"]),b=Array.IndexOf(ProgressRaces,(string)row["opponent"]);var first=RaceEmblem(a);var second=RaceEmblem(b);
                if(first)GUI.DrawTexture(new Rect(16,y+18,52,52),first,ScaleMode.ScaleToFit);if(second)GUI.DrawTexture(new Rect(58,y+61,52,52),second,ScaleMode.ScaleToFit);
                Text(128,y+12,790,34,(string)row["title"],raceTitleStyle);Text(128,y+50,816,76,(string)row["description"],new GUIStyle(label){fontSize=18,wordWrap=true});
                bool done=progression.Completed(id);string reward=done?"Пройдено · лучший результат "+TimeSpan.FromSeconds(progression.Best(id)).ToString(@"mm\:ss"):"Награда: "+row["reward"]+" жетонов · "+ChallengeCatalog034.Experience(row)+" опыта · "+((int?)row["difficulty"]==2?"сложный":"обычный")+" бот";
                Text(128,y+139,800,25,reward,small);if(Button("challenge."+id,new Rect(978,y+48,172,48),done?"Повторить":"Начать",!done)){BeginChallenge(row);break;}
            }
        }
        GUI.EndScrollView();
    }
    void DrawChallengeHud(){if(activeChallenge==null)return;Fill(new Rect(190,36,680,44),new Color(.015f,.025f,.03f,.86f));string cap=activeChallenge["max_buildings"]!=null?" · постройки "+match.Huts.Count(h=>h.team==0)+"/"+activeChallenge["max_buildings"]:"";string goals=activeChallenge["min_upgraded_buildings"]!=null?" · улучшено "+match.ChallengeUpgradedBuildings+"/"+activeChallenge["min_upgraded_buildings"]:activeChallenge["min_distinct_producers"]!=null?" · ветки "+match.ChallengeProducerFamilies+"/"+activeChallenge["min_distinct_producers"]:"";string clock=activeChallenge["max_seconds"]!=null?" · осталось "+TimeSpan.FromSeconds(Mathf.Max(0,HumanMatch.F(activeChallenge,"max_seconds")-match.Elapsed)).ToString(@"mm\:ss"):"";Text(206,44,650,28,(string)activeChallenge["title"]+cap+goals+clock,small);}
    void DrawChallengeResult(){if(activeChallenge==null)return;string result=!match.ChallengeVictoryEligible?"Условия испытания не выполнены":challengeReward>0?"+"+challengeReward+" жетонов · +"+ChallengeCatalog034.Experience(activeChallenge)+" опыта · ур. "+progression.MasteryLevel:"Испытание пройдено · награда уже получена";if(!challengeSettled&&match.ChallengeVictoryEligible){result="Награда ещё не сохранена";if(Button("challenge.retry-save",new Rect(450,345,380,32),"Повторить сохранение"))SettleChallenge();}else Text(450,345,380,35,result,small);}
    void RenderBuilderCosmetic(){
        string id=progression?.Equipped("aura")??"";
        if(onlineMatch||match==null||id==""||!builders[0]){if(builderCosmetic)builderCosmetic.SetActive(false);return;}
        if(!builderCosmetic){builderCosmetic=Ring("Local cosmetic builder aura",visuals.transform);if(!builderCosmeticMaterial)builderCosmeticMaterial=new Material(overlayMaterial);builderCosmetic.GetComponent<Renderer>().sharedMaterial=builderCosmeticMaterial;}
        builderCosmetic.SetActive(true);var color=CosmeticColor(progression.Item(id));builderCosmeticMaterial.color=new Color(color.r,color.g,color.b,.7f);builderCosmetic.transform.position=builders[0].transform.position+Vector3.up*.13f;builderCosmetic.transform.localScale=Vector3.one*(1.15f+Mathf.Sin(Time.unscaledTime*2)*.04f);UpdateRichAura(id);
    }
}
}
