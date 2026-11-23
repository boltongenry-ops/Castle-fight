using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame : MonoBehaviour {
    public GameObject[] unitModels,buildingModels,builderModels,castleObjects;public Material overlayMaterial;
    string Version=>MobileContent.Active?Application.version:"0.44.1";
    public GameObject[] reforgedUnits,reforgedBuildings,reforgedBuilders,reforgedCastles;
    readonly PcModel[] castleViews=new PcModel[2];
    GameObject[] UnitModels=>OrcsInMatch?(options.hdTextures?orcPack.hdUnits:orcPack.units):options.hdTextures&&reforgedUnits!=null&&reforgedUnits.Length>0?reforgedUnits:unitModels;
    GameObject[] BuildingModels=>OrcsInMatch?(options.hdTextures?orcPack.hdBuildings:orcPack.buildings):options.hdTextures&&reforgedBuildings!=null&&reforgedBuildings.Length>0?reforgedBuildings:buildingModels;
    GameObject[] BuilderModels=>Pack(matchRace)?Pack(matchRace).builders:builderModels;
    public Material shellMaterial,tracerMaterial,impactMaterial;
    public Font warcraftFont;
    public Texture2D[] itemIcons;public string[] itemIds;public Texture2D[] buildingIcons,unitIcons,abilityIcons,extraCommandIcons;public string[] abilityIds,extraCommandIds;
    public Texture2D[] hudTiles,commandIcons,minimapIcons;public Texture2D goldIcon,woodIcon,armyIcon,timeIndicator,minimapTexture,buttonUp,buttonDown,buttonHighlight;
    public TextAsset catalog,terrainBytes,terrainMetadata,waterMetadata;public NativeMapCamera controls;
    enum Page { Main,Settings,Setup,Match,Pause,Result,Viewer,Leave,Help,Teams,Network,Progression,Guide }
    Page page=Page.Main,settingsReturn=Page.Main;
    [Serializable] public sealed class Options {public int resolution=0,fps=60;public bool fullscreen,vsync;public bool hdTextures=false,fogOfWar=true;public bool softFog {get=>FogOption034.Get(this);set=>FogOption034.Set(this,value);}public float volume=.8f,musicVolume=.7f,cameraSpeed=1;public int quality=1;}
    Options options=new Options(),draft;string settingsPath,notice="";float noticeLeft,accumulator,screenDeadline;
    int difficulty=1,startGold=250,chosen=-1,roundsChoice=1;PcSeries series;HumanMatch match;NativeMapTerrain terrain;
    readonly Dictionary<int,PcModel> units=new Dictionary<int,PcModel>(),huts=new Dictionary<int,PcModel>();
    readonly Dictionary<int,int> unitViewTeams=new Dictionary<int,int>();
    readonly Dictionary<int,PcShotView> projectiles=new Dictionary<int,PcShotView>();
    GameObject visuals,preview;Material previewMaterial;Vector2Int hover;bool hoverValid,confirmDisplay;
    readonly PcModel[] builders=new PcModel[2];GameObject selectedRing,moveMarker;Mesh ringMesh;
    bool builderSelected=true,buildMenu,moveOrder;
    GameObject localGrid;Mesh gridMesh;Material gridMaterial;Vector2Int gridCenter=new Vector2Int(-99,-99);Vector2Int? testPreviewCell;
    readonly string[] sizes={"1280 × 720","1600 × 900","1920 × 1080","2560 × 1440","3840 × 2160"};
    readonly int[] widths={1280,1600,1920,2560,3840},heights={720,900,1080,1440,2160};
    GUIStyle title,heading,label,small,button,primary,menuButton,menuPrimary,menuInput,menuArrow;Texture2D panel,normal,hot,active;float guiScale=1;Vector2 guiOrigin;
    string pendingTestAction;bool testing;int testFailures;JArray testChecks=new JArray();
    public bool InMatch=>page==Page.Match;
    IEnumerator Start() {
        StartFrontend();LoadLauncherSession();LoadProgression();StartLoadingArt();
        yield return null;
        yield return CentaurContent.Open();
        yield return PepeContent.Open();
        yield return OpenDesktopMenuBuilders();
        terrain=new NativeMapTerrain(terrainBytes.bytes,JObject.Parse(terrainMetadata.text),(JObject)JObject.Parse(waterMetadata.text)["water"]);
        settingsPath=BattleLab.Arg("--pc-settings-file",Path.Combine(Application.persistentDataPath,"pc-settings.json"));
        string testMode=BattleLab.Arg("--pc-test");testing=testMode=="battle-help"||testMode=="pepe"||testMode=="hud-themes"||testMode=="art040"||testMode=="models039"||testMode=="workshop038"||testMode=="spell-audit036"||testMode=="optimization035"||testMode=="challenge034"||testMode=="nerubian033"||testMode=="nerubian032"||testMode=="structure031"||testMode=="combat030"||testMode=="centaur028"||testMode=="bot027"||testMode=="units026"||testMode=="cards025"||testMode=="motion024"||testMode=="network023"||testMode=="polish022"||testMode=="recovery"||testMode=="social"||testMode=="guide"||testMode=="fog"||testMode=="frontend"||testMode=="ultimate"||testMode=="nerubian"||testMode=="pandaren"||testMode=="elven"||testMode=="polish"||testMode=="night"||testMode=="elemental"||testMode=="corrupted"||testMode=="undead"||testMode=="mech"||testMode=="northern"||testMode=="1"||testMode=="naga"||testMode=="nature"||testMode=="classic"||testMode=="menu"||testMode=="chaos";
        LoadSettings();if(MapArt.Preferred)yield return MapArt.Apply(controls);if(HdModels.Preferred)StartCoroutine(HdModels.OpenCached());LoadMatchRules();Apply(options,false);controls.HideToolbar=true;controls.TouchOverUi=TouchOverUi;controls.TouchPanAllowed=()=>chosen<0&&selectedItem<0&&string.IsNullOrEmpty(targetedSpell)&&!moveOrder;
        controls.AllowGridToggle=false;controls.buildGrid.SetActive(false);
        if(testing&&!MobileContent.Active&&testMode!="hud-themes"&&testMode!="workshop038"&&testMode!="models039"&&testMode!="art040"&&testMode!="pepe"&&testMode!="battle-help")PreserveAssetAuditForHistoricalTests();
        yield return LoadClassicProjectiles();
        yield return LoadSpellArt036();
        if(MobileContent.Active){yield return LoadMobileFrontendArt();yield return MobileContent.Current.EnsureMedia("Frontend/startup_intro.mp4");}
        if(IntroWanted())StartIntro(BattleLab.Arg("--pc-intro-shot"));else BeginMenuMusic(0);
        if(BattleLab.Arg("--map-shot")!=""||BattleLab.Arg("--map-view")!="")page=Page.Viewer;
        else controls.Focus("castle");
        string mode=BattleLab.Arg("--pc-mode");
        if(mode=="setup")page=Page.Setup;
        if(mode=="settings")OpenSettings();
        if(mode=="network")page=Page.Network;
        if(mode=="match")NewMatch();
        Debug.Log("CF_PC_READY page="+page+" build="+Version);
        StartCoroutine(CompleteStartupLoading());if(!MobileContent.Active)StartCoroutine(LoadMobileFrontendArt());
        if(!testing){StartCoroutine(CheckGameRelease(true));if(!string.IsNullOrEmpty(networkSessionToken))BeginDirectory();}
        if(testing)StartCoroutine(testMode=="battle-help"?ExerciseBattleHelp():testMode=="pepe"?ExercisePepe():testMode=="hud-themes"?ExerciseHudThemes():testMode=="art040"?ExerciseReforgedArt040():testMode=="models039"?ExerciseModels039():testMode=="workshop038"?ExerciseWorkshop038():testMode=="spell-audit036"?ExerciseSpellAudit036():testMode=="optimization035"?ExerciseOptimization035():testMode=="challenge034"?ExerciseChallenge034():testMode=="nerubian033"?ExerciseNerubian033():testMode=="nerubian032"?ExerciseNerubian032():testMode=="structure031"?ExerciseStructure031():testMode=="combat030"?ExerciseCombat030():testMode=="centaur028"?ExerciseCentaur028():testMode=="bot027"?ExerciseBot027():testMode=="units026"?ExerciseUnits026():testMode=="cards025"?ExerciseCards025():testMode=="motion024"?ExerciseProjectileMotion024():testMode=="network023"?ExerciseNetwork023():testMode=="polish022"?ExerciseFrontend022():testMode=="recovery"?ExerciseRecovery():testMode=="social"?ExerciseSocial():testMode=="guide"?ExerciseGuide():testMode=="fog"?ExerciseFog():testMode=="frontend"?ExerciseFrontend():testMode=="ultimate"||testMode=="nerubian"||testMode=="pandaren"?ExerciseFinalRaceOnly(testMode):testMode=="elven"?ExerciseElvenOnly():testMode=="polish"?ExercisePolishOnly():testMode=="night"?ExerciseNightOnly():testMode=="elemental"?ExerciseElementalOnly():testMode=="corrupted"?ExerciseCorruptedOnly():testMode=="undead"?ExerciseUndeadOnly():testMode=="mech"?ExerciseMechOnly():testMode=="northern"?ExerciseNorthernOnly():testMode=="chaos"?ExerciseChaosOnly():testMode=="classic"?ExerciseClassicOnly():testMode=="menu"?ExerciseMenu():testMode=="naga"?ExerciseNagaOnly():testMode=="nature"?ExerciseNatureOnly():Exercise());
#if CF_ONLINE_QA
        if(BattleLab.Arg("--pc-online-qa")!="")StartCoroutine(ExerciseOnlineMatch());
#endif
        string mapCheck=BattleLab.Arg("--pc-map-art-check");if(mapCheck!="")StartCoroutine(VerifyMapArt(mapCheck));
        string startupShot=BattleLab.Arg("--pc-startup-shot");if(startupShot!="")StartCoroutine(CaptureStartup(startupShot));
    }
    IEnumerator CaptureStartup(string path){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);Debug.Log("CF_PC_STARTUP_CAPTURE page="+page);}
    void LoadSettings() {
        try {if(File.Exists(settingsPath)){string json=File.ReadAllText(settingsPath);options=JsonUtility.FromJson<Options>(json)??new Options();if(!json.Contains("\"fogOfWar\""))options.fogOfWar=true;}}
        catch(Exception e){Debug.LogWarning("Settings reset: "+e.Message);options=new Options();}
        options.hdTextures=false;
        LoadModelPreference(File.Exists(settingsPath)?File.ReadAllText(settingsPath):"{}");
        if(File.Exists(settingsPath)&&!File.ReadAllText(settingsPath).Contains("\"musicVolume\""))options.musicVolume=.7f;
        // Soft fog is opt-in: older settings files keep the original opaque shroud.
        if(File.Exists(settingsPath))try{options.softFog=(bool?)JObject.Parse(File.ReadAllText(settingsPath))["softFog"]??false;}catch(Exception){options.softFog=false;}
        HudThemePreference.For(options).Choice=0;
        if(File.Exists(settingsPath))try{
            var choice=JObject.Parse(File.ReadAllText(settingsPath))["hudTheme"];
            HudThemePreference.For(options).Choice=HudTheme.Parse(choice?.Type==JTokenType.String?(string)choice:null);
        }catch(Newtonsoft.Json.JsonException){}
        options.resolution=Mathf.Clamp(options.resolution,0,sizes.Length-1);options.quality=Mathf.Clamp(options.quality,0,2);
        if(options.fps!=0&&options.fps!=30&&options.fps!=60&&options.fps!=90&&options.fps!=120)options.fps=60;
        options.volume=Mathf.Clamp01(options.volume);options.musicVolume=Mathf.Clamp01(options.musicVolume);options.cameraSpeed=Mathf.Clamp(options.cameraSpeed,.5f,2);
        Debug.Log("CF_PC_SETTINGS_LOADED fps="+options.fps+" resolution="+options.resolution+" fullscreen="+options.fullscreen);
    }
    void Apply(Options value,bool display) {
        value.hdTextures=false;
        Shader.SetGlobalFloat("_HumanHD",0);Application.targetFrameRate=value.fps==0?-1:value.fps;QualitySettings.vSyncCount=value.vsync?1:0;
        QualitySettings.antiAliasing=value.quality==0?0:value.quality==1?2:4;QualitySettings.globalTextureMipmapLimit=value.quality==0?1:0;
        if(match!=null&&renderedHd!=value.hdTextures){ResetModelViews();renderedHd=value.hdTextures;}
        AudioListener.volume=value.volume;controls.CameraSpeed=value.cameraSpeed;
        if(Application.isMobilePlatform){Screen.fullScreen=true;return;}
        if(display)Screen.SetResolution(widths[value.resolution],heights[value.resolution],value.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
        else if(!testing&&BattleLab.Arg("-screen-width")=="")Screen.SetResolution(widths[value.resolution],heights[value.resolution],value.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed);
    }
    Options Copy(Options value){
        var result=JsonUtility.FromJson<Options>(JsonUtility.ToJson(value));result.softFog=value.softFog;
        HudThemePreference.For(result).Choice=HudThemePreference.For(value).Choice;
        return result;
    }
    void OpenSettings(){settingsReturn=page;draft=Copy(options);MapArt.Draft=MapArt.Preferred;MapArt.Panel=false;HdModels.Draft=HdModels.Preferred;HdModels.Panel=false;page=Page.Settings;}
    void SaveSettings() {
        options=Copy(draft);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(settingsPath)));
        string temp=settingsPath+".tmp";var saved=JObject.Parse(JsonUtility.ToJson(options,true));saved["softFog"]=options.softFog;saved["modelStyle"]=HdModels.Draft?"reforged":"classic";
        saved["mapStyle"]=MapArt.Draft?"reforged":"classic";saved["hudTheme"]=HudTheme.Keys[HudThemeIndex];File.WriteAllText(temp,saved.ToString());
        if(File.Exists(settingsPath))File.Replace(temp,settingsPath,null);else File.Move(temp,settingsPath);
        MapArt.Preferred=MapArt.Draft;StartCoroutine(MapArt.Apply(controls));HdModels.Select(HdModels.Draft);if(HdModels.Preferred)StartCoroutine(HdModels.OpenCached());else ReleaseOptionalVortex();
        Apply(options,false);confirmDisplay=false;page=settingsReturn;Notice("Настройки сохранены");
    }
    void PreviewSettings() {
        bool changed=!Application.isMobilePlatform&&(draft.resolution!=options.resolution||draft.fullscreen!=options.fullscreen);
        if(changed){Apply(draft,true);confirmDisplay=true;screenDeadline=Time.unscaledTime+15;}
        else SaveSettings();
    }
    void RevertDisplay(){Apply(options,true);confirmDisplay=false;}
    void Notice(string text){notice=text;noticeLeft=4;}
    void NewMatch() {if(activeChallenge==null)SaveMatchRules();series=new PcSeries(roundsChoice);StartRound();}
    void StartRound() {
        ClearMatch();
        // Godot match_state.choose_races: random mode redraws both sides every round.
        if(matchMode==1&&!mobileRoundPrepared){raceChoice=RandomStagedRace(-1);botRaceChoice=RandomStagedRace(raceChoice);}mobileRoundPrepared=false;
        matchRace=raceChoice;matchBotRace=matchMode==2?raceChoice:botRaceChoice<0?matchRace:botRaceChoice;
        if(!MobileContent.Active){LoadDesktopRace(matchRace);LoadDesktopRace(matchBotRace);}
        PrepareRaceIndices();
        var first=JObject.Parse(RaceCatalog(matchRace));var second=matchRace==matchBotRace?first:JObject.Parse(RaceCatalog(matchBotRace));
        ApplyUltimateRound(ref first,ref second);
        bool aspectRound=aspectsOn&&AspectCount(matchRace)==2&&AspectCount(matchBotRace)==2;
        match=new HumanMatch(first,startGold,difficulty,-1,-1,second);
        if(activeChallenge!=null)match.BeginChallengeRules(activeChallenge);
        challengeReward=0;challengeSettled=false;AspectBeginRound(aspectRound);
        page=Page.Match;chosen=-1;accumulator=0;
        visuals=new GameObject("Offline "+RaceEnglish+" 1v1");
        visuals.AddComponent<BattleAudio>().Bind(match,World,FogVisible);
        constructionPage=0;builderSelected=true;selectedCastle=false;selectedItem=-1;selectedHut=selectedUnit=-1;targetedSpell=null;buildMenu=false;moveOrder=false;
        foreach(var c in castleObjects)c.SetActive(true);
        controls.Focus("west");controls.buildGrid.SetActive(false);Notice("Рабочий выбран.");
        Debug.Log("CF_PC_MATCH_START race="+RaceEnglish+" botRace="+BotRaceName+" gold="+startGold+" difficulty="+difficulty+" aspects="+(aspectRound?1:0)+" mode="+matchMode);
    }

    void ClearMatch() {
        ClearWorkshopBattle();
        ClearOnlineProjectiles();
        aspectInfoOpen=false;skillCardUnit=-1;skillCardPage=0;
        if(visuals){visuals.SetActive(false);Destroy(visuals);}units.Clear();huts.Clear();projectiles.Clear();match=null;chosen=-1;
        nativeFlames.Clear();
        if(preview)Destroy(preview);if(previewMaterial)Destroy(previewMaterial);preview=null;previewMaterial=null;
        Art036.casts.Clear();Art036.hutTeams.Clear();Art036.unitKinds.Clear();ClearHumanEffects();ClearNorthernViews();ClearMechViews();ClearUndeadViews();ClearCorruptedViews();ClearElementalViews();ClearNightViews();ClearElvenViews();ClearFinalViews();
        ClearPortraits();ClearRacePrefabCaches();
        for(int i=0;i<2;i++){builders[i]=null;castleViews[i]=null;}selectedRing=null;moveMarker=null;
        localGrid=null;if(gridMesh)Destroy(gridMesh);if(gridMaterial)Destroy(gridMaterial);gridMesh=null;gridMaterial=null;gridCenter=new Vector2Int(-99,-99);
        controls.buildGrid.SetActive(false);foreach(var c in castleObjects)c.SetActive(true);
    }
    void MainMenu(){bool hadContent=match!=null||page==Page.Guide;noticeLeft=0;CloseVersus();CloseGuide();EndChallenge();if(onlineMatch){onlineMatch=false;onlineStarted=false;onlineReplica=null;StopDirectory();}ClearMatch();ReleaseIdleRaceAssets(hadContent);series=null;page=Page.Main;controls.Focus("castle");}
    Vector3 World(Vector2 point)=>terrain.Position(point.x*30,point.y*30,true);
    void Select(int line){if(!builderSelected)return;selectedItem=-1;buildMenu=true;chosen=line;moveOrder=false;Notice("Приказ рабочему: "+match.Lines[line].name+(Application.isMobilePlatform?". Коснитесь места постройки.":". ЛКМ — место · ПКМ — отмена."));}
    bool Place(int x,int y){
        if(chosen<0)return false;
        var line=match.Lines[chosen];
        if(onlineMatch){
            if(!HumanMatch.LegalCell(0,x,y)){Notice("Выберите клетку своей базы");return false;}
            if(match.Huts.Any(h=>h.team==0&&h.x==x&&h.y==y)){Notice("Клетка занята");return false;}
            if(!NetworkCommand(PcServerReplica.Build(line.building,HumanMatch.Plot(0,x,y))))return false;
            Notice("Приказ на строительство отправлен");
        }else{
            if(!match.TryBuild(0,chosen,x,y,out string message)){Notice(message);return false;}
            Notice("Строится: "+line.name);
            WorkshopBuildingPlaced(HumanMatch.Plot(0,x,y));
        }
        chosen=-1;buildMenu=false;hoverValid=false;
        if(preview)preview.SetActive(false);
        if(localGrid)localGrid.SetActive(false);
        return true;
    }
    void Tick(float dt) {match.Step(dt);if(match.Finished&&page==Page.Match){series.FinishRound(match.Winner);SettleChallenge();page=Page.Result;chosen=-1;controls.buildGrid.SetActive(false);Debug.Log("CF_PC_MATCH_END winner="+match.Winner+" time="+match.Elapsed);}}
    void Update() {
        if(MobileContent.Active&&MobileContent.Current.Busy){controls.InputBlocked=true;if(network!=null)PollLobby();UpdateVoiceControls();return;}
        if(terrain==null)return;
        if(introPlaying){UpdateIntro();return;}
        UpdateGuiMetrics();UpdateFrontend();UpdateVoiceControls();UpdateFog();UpdateGuide();if(Application.isMobilePlatform){UpdateTouchScroll();}
        noticeLeft-=Time.unscaledDeltaTime;
        if(versusActive){controls.InputBlocked=true;controls.EdgePan=false;return;}
        controls.GameplayViewport(match!=null&&page!=Page.Viewer);
        if(helpMatch!=null&&!HelpOpen)CloseBattleHelp();
        controls.InputBlocked=HelpOpen||NetworkBlocked||aspectWaiting||(page!=Page.Match&&page!=Page.Viewer);
        controls.ReserveBuildKeys=page==Page.Match;
        controls.EdgePan=!HelpOpen&&!NetworkBlocked&&!Application.isMobilePlatform&&!testing&&page==Page.Match&&!aspectWaiting&&!selectionPending&&!showBattleChat&&!showVoicePanel&&!showStickerPicker;
        controls.buildGrid.SetActive(false);
        if(confirmDisplay&&Time.unscaledTime>=screenDeadline)RevertDisplay();
        if(Input.GetKeyDown(KeyCode.Escape)&&!NetworkBlocked) {
            if(HelpOpen)CloseBattleHelp();
            else if(aspectWaiting){if(aspectInfoOpen)aspectInfoOpen=false;}
            else if(aspectInfoOpen)aspectInfoOpen=false;
            else if(confirmDisplay)RevertDisplay();
            else if(showVoicePanel)showVoicePanel=false;
            else if(onlineMatch&&showStickerPicker)showStickerPicker=false;
            else if(onlineMatch&&showBattleChat)showBattleChat=false;
            else if(page==Page.Match&&selectedItem>=0)selectedItem=-1;
            else if(page==Page.Match&&selectedCastle)FocusBuilder();
            else if(page==Page.Match&&moveOrder)moveOrder=false;
            else if(page==Page.Match&&!string.IsNullOrEmpty(targetedSpell))targetedSpell=null;
            else if(page==Page.Match&&chosen>=0)chosen=-1;
            else if(page==Page.Match&&buildMenu)buildMenu=false;
            else if(page==Page.Match)page=Page.Pause;
            else if(page==Page.Pause||page==Page.Leave||page==Page.Help||page==Page.Teams)page=Page.Match;
            else if(page==Page.Settings&&HdModels.Panel){if(HdModels.Busy)HdModels.Cancel();else HdModels.Panel=false;}
            else if(page==Page.Settings){page=settingsReturn;}
            else if(page==Page.Network&&showStickerPicker)showStickerPicker=false;
            else if(page==Page.Network&&showNetworkProfile){profilePassword="";showNetworkProfile=false;}
            else if(page==Page.Network&&!string.IsNullOrEmpty(socialViewed))socialViewed="";
            else if(page==Page.Network&&socialTab==2&&!string.IsNullOrEmpty(socialPeer))socialPeer="";
            else if(page==Page.Network&&socialTab>0)OpenSocialTab(0);
            else if(page==Page.Network){StopDirectory();page=Page.Main;}
            else if(page==Page.Guide)GuideBack();
            else if(page==Page.Setup||page==Page.Viewer||page==Page.Progression)MainMenu();
        }
        if(match!=null&&page!=Page.Result&&!aspectWaiting&&!NetworkBlocked) {
            if(Input.GetKeyDown(KeyCode.F10))page=page==Page.Pause?Page.Match:Page.Pause;
            if(Input.GetKeyDown(KeyCode.F9)&&page==Page.Match&&!showBattleChat&&!showStickerPicker&&!showVoicePanel){if(HelpOpen)CloseBattleHelp();else OpenBattleHelp();}
            if(Input.GetKeyDown(KeyCode.F11))page=page==Page.Teams?Page.Match:Page.Teams;
            if(Input.GetKeyDown(KeyCode.F12)){if(onlineMatch){showBattleChat=!showBattleChat;showStickerPicker=showVoicePanel=false;}else Notice("Чат доступен в сетевой игре");}
            if(onlineMatch&&Input.GetKeyDown(KeyCode.F8)){showStickerPicker=!showStickerPicker;showBattleChat=showVoicePanel=false;}
            if(onlineMatch&&Input.GetKeyDown(KeyCode.F7)){showVoicePanel=!showVoicePanel;showBattleChat=showStickerPicker=false;}
        }
        if(aspectWaiting)AspectTick(Time.unscaledDeltaTime);
        if(page!=Page.Match){aspectWaiting=false;selectionPending=selectionDragging=false;}
        if(page==Page.Match&&!aspectWaiting&&!NetworkBlocked) {
            if(!onlineMatch){if(!testing||!testHoldAuthority)accumulator+=Mathf.Min(Time.unscaledDeltaTime,.2f);
            while(accumulator>=.05f&&page==Page.Match){Tick(.05f);accumulator-=.05f;}}
            if(HelpOpen){
                if(preview)preview.SetActive(false);if(localGrid)localGrid.SetActive(false);
                if(Application.isMobilePlatform)foreach(var finger in Input.touches)
                    if(finger.phase==TouchPhase.Moved&&HelpTextArea.Contains(GuiPoint(finger.position)))helpScroll.y=Mathf.Max(0,helpScroll.y+finger.deltaPosition.y/guiScale);
            }else if(page==Page.Match){
            if(Input.GetKeyDown(KeyCode.F1))FocusBuilder();
            if(Input.GetKeyDown(KeyCode.F2))SelectCastle();
            if(!testing&&(!onlineMatch||onlineStarted)&&!showBattleChat&&!showVoicePanel&&!showStickerPicker)HandleCommandKeys();
            if(!testing&&(!onlineMatch||onlineStarted)){if(Application.isMobilePlatform)HandleTouchWorld();else HandleBuilderMouse();}
            if(!onlineMatch||onlineStarted)UpdatePlacement();
            }
        } else {if(preview)preview.SetActive(false);if(localGrid)localGrid.SetActive(false);}
        if(match!=null)RenderMatch();
    }
    void UpdatePlacement() {
        hoverValid=false;if(chosen<0){if(preview)preview.SetActive(false);if(localGrid)localGrid.SetActive(false);return;}
        if(!preview) {
            preview=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.name="Build placement";Destroy(preview.GetComponent<Collider>());
            previewMaterial=new Material(overlayMaterial);preview.GetComponent<Renderer>().sharedMaterial=previewMaterial;
            preview.transform.localScale=new Vector3(HumanMatch.Cell,.25f,HumanMatch.Cell);
        }
        if(testing&&testPreviewCell.HasValue){int tx=testPreviewCell.Value.x,ty=testPreviewCell.Value.y;PreviewPlot(tx,ty,World(HumanMatch.Plot(0,tx,ty)));return;}
        if(Application.isMobilePlatform&&Input.touchCount==0){preview.SetActive(false);if(localGrid)localGrid.SetActive(false);return;}bool ui=MouseOverHud();
        preview.SetActive(!ui);if(localGrid)localGrid.SetActive(!ui);if(ui)return;
        if(!PointerOnTerrain(out Vector3 hit))return;
        float wx=hit.x*30,wy=-hit.z*30;
        int x=Mathf.RoundToInt((wx+6080)/128),y;
        if(wy<=-448)y=Mathf.RoundToInt((wy+1920)/128);
        else if(wy>=448)y=12+Mathf.RoundToInt((wy-512)/128);
        else y=-1;
        PreviewPlot(x,y,hit);
        if(!testing&&!Application.isMobilePlatform&&Input.GetMouseButtonDown(0))Place(x,y);
    }
    void PreviewPlot(int x,int y,Vector3 hit) {
        preview.SetActive(true);hover=new Vector2Int(x,y);
        hoverValid=HumanMatch.LegalCell(0,x,y)&&!match.Huts.Any(h=>h.team==0&&h.x==x&&h.y==y)&&match.PurchaseError(0,chosen)=="";
        Vector2 point=HumanMatch.LegalCell(0,x,y)?HumanMatch.Plot(0,x,y):new Vector2(hit.x,-hit.z);
        preview.transform.position=World(point)+Vector3.up*.4f;previewMaterial.color=hoverValid?new Color(.3f,1,.55f,.6f):new Color(1,.2f,.12f,.6f);
        LocalGrid(x,y);
    }
    bool renderedHd;
    void ResetModelViews(){foreach(var v in units.Values.Concat(huts.Values).Concat(builders).Concat(castleViews))if(v){v.gameObject.SetActive(false);Destroy(v.gameObject);}units.Clear();unitViewTeams.Clear();huts.Clear();effectAnchors.Clear();orcDecorAnchors.Clear();for(int i=0;i<2;i++){builders[i]=castleViews[i]=null;}portraitRaw=null;}
    void RenderMatch() {
        var art036=Art036;
        ObserveSelectedSpells();
        UpdateOnlineProjectiles(Time.unscaledDeltaTime);
        UpdateFog();
        if(renderedHd!=options.hdTextures||HdModels.RenderRevision!=HdModels.Revision){ResetModelViews();renderedHd=options.hdTextures;HdModels.RenderRevision=HdModels.Revision;}
        for(int t=0;t<2;t++){
            var prefab=HdModels.Model("hcas",t);
            castleObjects[t].SetActive(!prefab);
            if(prefab&&!castleViews[t]){
                castleViews[t]=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();
                castleViews[t].transform.position=World(HumanMatch.CastlePosition(t));
                castleViews[t].transform.rotation=Quaternion.Euler(0,t==0?0:180,0);
            }
            if(castleViews[t])castleViews[t].Play("Stand");
        }
        float alpha=onlineMatch?Mathf.Clamp01((Time.unscaledTime-onlineSnapshotAt)/.1f):page==Page.Match&&(!testing||!testHoldAuthority)?Mathf.Clamp01(accumulator/.05f):1;
        for(int team=0;team<2;team++) {
            bool born=!builders[team];if(born)builders[team]=Instantiate(RaceBuilder(team),visuals.transform).GetComponent<PcModel>();
            var builder=match.Builders[team];var model=builders[team];model.transform.position=World(Vector2.Lerp(builder.previous,builder.position,alpha));
            model.Face(builder.direction,born);model.Play(builder.pose,builder.pose=="Walk"?match.BuilderWalkRate:1);
        }
        if(!selectedRing)selectedRing=Ring("Selected worker",visuals.transform);
        UpdateSelectionRing();
        foreach(var h in match.Huts) {
            if(huts.TryGetValue(h.id,out var previousView)&&(previousView.raw!=match.Lines[h.kind].building||!art036.hutTeams.TryGetValue(h.id,out int previousTeam)||previousTeam!=h.team)){previousView.gameObject.SetActive(false);Destroy(previousView.gameObject);huts.Remove(h.id);orcDecorAnchors.Remove(h.id);}
            if(!huts.TryGetValue(h.id,out var view)){var prefab=RaceModel(h.team,h.kind,true);if(!prefab)continue;view=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();huts.Add(h.id,view);art036.hutTeams[h.id]=h.team;view.transform.position=World(h.position);view.transform.rotation=Quaternion.Euler(0,h.team==0?0:180,0);}
            view.Play(match.NorthernChannel(h)||match.MechBuildingChannel(h)?"Death":h.attackPoseLeft>0?"Attack":"Stand",1,h.attackSerial);
            view.transform.localScale=Vector3.one*(h.buildLeft>0?.5f+.5f*(1-h.buildLeft/match.Lines[h.kind].buildSeconds):1);
        }
        foreach(int id in huts.Keys.Where(id=>!match.Huts.Any(h=>h.id==id)).ToArray()){Destroy(huts[id].gameObject);huts.Remove(id);art036.hutTeams.Remove(id);}
        foreach(var u in match.Units) {
            if(u.hp<=0&&u.deadAge>3){if(units.TryGetValue(u.id,out var consumed))consumed.gameObject.SetActive(false);continue;}
            if(match.Hidden(u)&&!match.MechMine(u)){if(units.TryGetValue(u.id,out var hidden))hidden.gameObject.SetActive(false);continue;}
            if(units.TryGetValue(u.id,out var oldView)&&(!art036.unitKinds.TryGetValue(u.id,out int oldKind)||oldKind!=u.kind||unitViewTeams.TryGetValue(u.id,out int oldTeam)&&oldTeam!=u.team)){oldView.gameObject.SetActive(false);Destroy(oldView.gameObject);units.Remove(u.id);unitViewTeams.Remove(u.id);effectAnchors.Remove(u.id);}
            bool born=!units.TryGetValue(u.id,out var view);if(born){var prefab=RaceModel(u.team,u.kind,false);if(!prefab)continue;view=Instantiate(prefab,visuals.transform).GetComponent<PcModel>();units.Add(u.id,view);unitViewTeams[u.id]=u.team;art036.unitKinds[u.id]=u.kind;}
            view.transform.position=World(Vector2.Lerp(u.previous,u.position,alpha))+Vector3.up*match.Height(u);
            view.gameObject.SetActive((!match.MechMine(u)||u.team==0||u.mechMineAge<2)&&CanSeeUnit(u));if(u.hp>0)view.Face(u.direction,born);
            string pose=u.abilities.Contains("A03G")&&(u.pose=="Walk"||u.pose=="Attack"||u.pose=="Stand")?u.pose+" Defend":u.pose;
            if(u.hp>0&&(u.pendingSpell=="A0AE"||u.corruptedCastPause>0))pose="Death";if(u.hp>0&&u.elementalPoseLeft>0)pose=u.elementalPose;view.Play(pose,u.pose=="Walk"?match.Lines[u.kind].walkRate*match.MoveSpeed(u)/Mathf.Max(.01f,match.Lines[u.kind].speed):u.pose=="Attack"?match.AttackSpeed(u):1,u.attackSerial);
        }
        foreach(int id in units.Keys.Where(id=>!match.Units.Any(u=>u.id==id)).ToArray()){Destroy(units[id].gameObject);units.Remove(id);unitViewTeams.Remove(id);effectAnchors.Remove(id);art036.unitKinds.Remove(id);}
        Func<HumanMatch.Shot,PcShotView,bool> drawHdShot=HdArt040?DrawHdShot040:null;
        foreach(var shot in match.Shots) {
            if(!projectiles.TryGetValue(shot.id,out var view)){view=new GameObject("Projectile "+shot.id).AddComponent<PcShotView>();view.transform.SetParent(visuals.transform);view.Setup(shot.kind,shellMaterial,tracerMaterial,impactMaterial);projectiles.Add(shot.id,view);}
            var hd=HdArt040?HdGeometry040(ClassicWeaponKey(shot)??HdMissileKey040(shot.weapon?.missile),shot.team):null;
            view.Draw(shot,World,(1-alpha)*.05f,options.hdTextures&&ReplaceMissile(shot),hd?hd:NativeMissilePrefab(shot),drawHdShot);
        }
        Art036.rendered.Clear();
        HdModels.CurrentEffects.Clear();
        RenderHdPulses040();
        RenderHdActorArt040();
        RenderWispBursts();
        RenderClassicSpells036();
        RenderNativeFlame();RenderOrcArmorAuras();
        RenderReforgedEffects();RenderNorthernEffects(alpha);RenderMechEffects();RenderUndeadEffects();RenderCorruptedEffects();RenderElementalEffects(alpha);RenderNightEffects();RenderElvenEffects();RenderFinalEffects();RenderPepeSkills();RenderHumanEffects();RenderBuilderCosmetic();ApplyFogViews();DrawOptionalVortices();
        SweepHdEffects040();
        foreach(int id in projectiles.Keys.Where(id=>!match.Shots.Any(s=>s.id==id)).ToArray()){projectiles[id].Finish();Destroy(projectiles[id].gameObject);projectiles.Remove(id);}
    }
    void FocusBuilder(){ClearGroupSelection();selectedCastle=false;selectedItem=-1;selectedHut=selectedUnit=-1;builderSelected=true;controls.FocusWorld(World(match.Builders[0].position),40);Notice("Рабочий выбран.");}
    bool PointerOnTerrain(out Vector3 hit) {
        Ray ray=Camera.main.ScreenPointToRay(ActivePointer);float surface=terrain.Height(-4500,0,true);hit=Vector3.zero;
        for(int i=0;i<8;i++){var plane=new Plane(Vector3.up,new Vector3(0,surface,0));if(!plane.Raycast(ray,out float distance))return false;hit=ray.GetPoint(distance);surface=terrain.Height(hit.x*30,-hit.z*30,true);}
        return NativeMapTerrain.Contains(hit.x*30,-hit.z*30);
    }
    bool HudContains(Vector2 p)=>p.x<0||p.x>1280||p.y<28||
        (HudThemeIndex==0?p.y>=520:ThemedHudBottomContains(p))||
        new Rect(5,31,52,52).Contains(p)||new Rect(546,0,184,66).Contains(p)&&HudThemeIndex!=0||
        new Rect(546,0,144,66).Contains(p)||new Rect(948,31,327,scoreExpanded?112:29).Contains(p)||
        (aspectInfoOpen||!string.IsNullOrEmpty(spellDetailsId))&&PassiveAspectPanel.Contains(p);
    bool MouseOverHud() {
        if(onlineMatch&&(showBattleChat||showVoicePanel||showStickerPicker||!onlineStarted))return true;
        return TouchOverUi(ActivePointer);
    }
    void HandleBuilderMouse() {
        HandleSelectionGesture();
        if(MouseOverHud())return;
        if(Input.GetMouseButtonDown(0)&&chosen<0) {
            if(selectedItem>=0&&PointerOnTerrain(out Vector3 itemPoint)){ExecuteItem(new Vector2(itemPoint.x,-itemPoint.z));return;}
            if(!string.IsNullOrEmpty(targetedSpell)&&PointerOnTerrain(out Vector3 spellPoint)){ExecuteBuilderSpell(new Vector2(spellPoint.x,-spellPoint.z));return;}
            if(moveOrder&&PointerOnTerrain(out Vector3 destination)){OrderBuilder(new Vector2(destination.x,-destination.z));moveOrder=false;return;}
            // Selection is resolved on release, allowing a drag rectangle.
        }
        if(!Input.GetMouseButtonDown(1))return;
        if(selectedItem>=0){selectedItem=-1;return;}
        if(chosen>=0){chosen=-1;return;}
        if(!string.IsNullOrEmpty(targetedSpell)){targetedSpell=null;return;}
        if(!builderSelected||!PointerOnTerrain(out Vector3 hit))return;
        Vector2 point=new Vector2(hit.x,-hit.z);
        var unfinished=match.Huts.FirstOrDefault(h=>h.team==0&&h.buildLeft>0&&Vector2.Distance(h.position,point)<3);
        if(unfinished!=null){match.ResumeBuilding(0,unfinished.id);Notice("Рабочий продолжит постройку");}
        else OrderBuilder(point);
    }
    void OrderBuilder(Vector2 point){if(onlineMatch){try{NetworkCommand(PcServerReplica.Move(point));}catch(ArgumentOutOfRangeException){Notice("Точка вне карты");return;}}if(onlineMatch||match.MoveBuilder(0,point)){selectedItem=-1;moveOrder=false;Notice("Рабочий: идти к указанному месту");if(!moveMarker)moveMarker=Ring("Move order",visuals.transform);moveMarker.SetActive(true);moveMarker.transform.position=World(point)+Vector3.up*.12f;}}
    void StopBuilder(){selectedItem=-1;chosen=-1;moveOrder=false;if(onlineMatch)NetworkCommand(PcServerReplica.Command("stop"));else match.MoveBuilder(0,match.Builders[0].position);if(moveMarker)moveMarker.SetActive(false);targetedSpell=null;Notice("Рабочий остановлен");}
    void LocalGrid(int x,int y) {
        if(!HumanMatch.LegalCell(0,x,y)){if(localGrid)localGrid.SetActive(false);return;}
        if(!localGrid) {
            localGrid=new GameObject("Nearby friendly build cells");localGrid.transform.SetParent(visuals.transform);gridMesh=new Mesh();gridMaterial=new Material(overlayMaterial);gridMaterial.color=new Color(.4f,.65f,.4f,.45f);
            localGrid.AddComponent<MeshFilter>().sharedMesh=gridMesh;localGrid.AddComponent<MeshRenderer>().sharedMaterial=gridMaterial;
        }
        localGrid.SetActive(true);if(gridCenter==new Vector2Int(x,y))return;gridCenter=new Vector2Int(x,y);
        var points=new List<Vector3>();
        for(int a=x-3;a<=x+3;a++)for(int b=y-3;b<=y+3;b++)if(HumanMatch.LegalCell(0,a,b)) {
            var p=HumanMatch.Plot(0,a,b);float h=HumanMatch.Cell/2;
            var corners=new[]{p+new Vector2(-h,-h),p+new Vector2(h,-h),p+new Vector2(h,h),p+new Vector2(-h,h)};
            for(int i=0;i<4;i++){points.Add(World(corners[i])+Vector3.up*.1f);points.Add(World(corners[(i+1)%4])+Vector3.up*.1f);}
        }
        gridMesh.Clear();gridMesh.SetVertices(points);gridMesh.SetIndices(Enumerable.Range(0,points.Count).ToArray(),MeshTopology.Lines,0);
    }
    GameObject Ring(string name,Transform parent) {
        if(!ringMesh) {
            ringMesh=new Mesh {name="Worker selection ring"};var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;foreach(float radius in new[]{.92f,1.04f})v.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            for(int i=0;i<48;i++){int p=i*2;t.AddRange(new[]{p,p+2,p+1,p+1,p+2,p+3});}ringMesh.SetVertices(v);ringMesh.SetTriangles(t,0);ringMesh.RecalculateNormals();
        }
        var obj=new GameObject(name);obj.transform.SetParent(parent);obj.AddComponent<MeshFilter>().sharedMesh=ringMesh;obj.AddComponent<MeshRenderer>().sharedMaterial=overlayMaterial;return obj;
    }
    void AuditMaterials() {
        int count=0,additive=0,alpha=0;
        foreach(var prefab in unitModels.Concat(buildingModels).Concat(builderModels).Concat(reforgedUnits??new GameObject[0]).Concat(reforgedBuildings??new GameObject[0]).Concat(reforgedBuilders??new GameObject[0]).Concat(reforgedCastles??new GameObject[0]).Concat(orcPack?orcPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(nagaPack?nagaPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(naturePack?naturePack.Prefabs:Enumerable.Empty<GameObject>()).Concat(chaosPack?chaosPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(northernPack?northernPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("NorthernFx")).Concat(mechPack?mechPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("MechFx")).Concat(undeadPack?undeadPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("UndeadFx")).Concat(ultimatePack?ultimatePack.Prefabs:Enumerable.Empty<GameObject>()).Concat(nerubianPack?nerubianPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(pandarenPack?pandarenPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("FinalFx")).Concat(elvenPack?elvenPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("ElvenFx")).Concat(nightPack?nightPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("NightFx")).Concat(elementalPack?elementalPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("ElementalFx")).Concat(corruptedPack?corruptedPack.Prefabs:Enumerable.Empty<GameObject>()).Concat(ContentResources.LoadAll<GameObject>("CorruptedFx")).Where(p=>p))foreach(var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())foreach(var m in r.sharedMaterials) {
            if(!m||!m.shader||!m.shader.isSupported||(m.shader.name!="CastleFight/NativeUnit"&&m.shader.name!="CastleFight/ReforgedUnit")||!m.mainTexture)throw new InvalidOperationException("Invalid player material: "+prefab.name+" / "+(m?m.name:"null"));count++;
            if(m.GetInt("_DstBlend")!=0){if(m.GetInt("_ZWrite")!=0)throw new InvalidOperationException("Transparent native material writes depth: "+m.name);if(m.GetInt("_DstBlend")==1)additive++;else alpha++;}
        }
        if(!overlayMaterial||!overlayMaterial.shader.isSupported)throw new InvalidOperationException("Invalid placement shader");
        Debug.Log("CF_PC_MATERIAL_AUDIT_PASS materials="+count+" textured=true shaderSupported=true");
        Debug.Log("CF_PC_BLEND_AUDIT_PASS additive="+additive+" alpha="+alpha);
        Debug.Log("CF_CLASSIC_MATERIAL_AUDIT_PASS races=15 reforged=false");
        if(ultimatePack)Debug.Log("CF_ULTIMATE_MATERIAL_AUDIT_PASS prefabs="+ultimatePack.Prefabs.Count());if(nerubianPack)Debug.Log("CF_NERUBIAN_MATERIAL_AUDIT_PASS prefabs="+nerubianPack.Prefabs.Count());if(pandarenPack)Debug.Log("CF_PANDAREN_MATERIAL_AUDIT_PASS prefabs="+pandarenPack.Prefabs.Count());if(orcPack)Debug.Log("CF_ORC_MATERIAL_AUDIT_PASS prefabs="+orcPack.Prefabs.Count());if(nagaPack)Debug.Log("CF_NAGA_MATERIAL_AUDIT_PASS prefabs="+nagaPack.Prefabs.Count());if(naturePack)Debug.Log("CF_NATURE_MATERIAL_AUDIT_PASS prefabs="+naturePack.Prefabs.Count());if(chaosPack)Debug.Log("CF_CHAOS_MATERIAL_AUDIT_PASS prefabs="+chaosPack.Prefabs.Count());if(northernPack)Debug.Log("CF_NORTHERN_MATERIAL_AUDIT_PASS prefabs="+northernPack.Prefabs.Count());if(mechPack)Debug.Log("CF_MECH_MATERIAL_AUDIT_PASS prefabs="+mechPack.Prefabs.Count());if(undeadPack)Debug.Log("CF_UNDEAD_MATERIAL_AUDIT_PASS prefabs="+undeadPack.Prefabs.Count());if(elvenPack)Debug.Log("CF_ELVEN_MATERIAL_AUDIT_PASS prefabs="+elvenPack.Prefabs.Count());if(nightPack)Debug.Log("CF_NIGHT_MATERIAL_AUDIT_PASS prefabs="+nightPack.Prefabs.Count());if(elementalPack)Debug.Log("CF_ELEMENTAL_MATERIAL_AUDIT_PASS prefabs="+elementalPack.Prefabs.Count());if(corruptedPack)Debug.Log("CF_CORRUPTED_MATERIAL_AUDIT_PASS prefabs="+corruptedPack.Prefabs.Count());
    }
    Texture2D Solid(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}
    void InitGui() {
        if(label!=null)return;
        panel=Solid(new Color(.035f,.045f,.055f,.96f));normal=Solid(new Color(.1f,.13f,.15f,.98f));hot=Solid(new Color(.18f,.23f,.26f));active=Solid(new Color(.29f,.35f,.37f));
        var font=warcraftFont?warcraftFont:Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true,normal={textColor=new Color(.91f,.9f,.84f)}};
        small=new GUIStyle(label){fontSize=14,normal={textColor=new Color(.63f,.69f,.71f)}};
        heading=new GUIStyle(label){fontSize=28,fontStyle=FontStyle.Bold};title=new GUIStyle(heading){fontSize=49};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(18,16,0,0),border=new RectOffset(),normal={background=normal,textColor=new Color(.9f,.92f,.91f)},hover={background=hot,textColor=Color.white},active={background=active,textColor=Color.white},focused={background=hot,textColor=Color.white}};
        primary=new GUIStyle(button){normal={background=Solid(new Color(.74f,.57f,.29f)),textColor=new Color(.08f,.09f,.09f)},hover={background=Solid(new Color(.91f,.72f,.4f)),textColor=Color.black}};
        InitHud();
        // The in-game HUD keeps its Warcraft textures; the PC menus use a separate, restrained skin.
        var ink=Solid(new Color(.075f,.085f,.088f,.98f));var hoverInk=Solid(new Color(.13f,.145f,.145f));var pressedInk=Solid(new Color(.18f,.18f,.16f));
        var bronze=Solid(new Color(.30f,.235f,.15f));var bronzeHover=Solid(new Color(.39f,.30f,.18f));
        menuButton=new GUIStyle(GUI.skin.button){font=font,fontSize=17,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,padding=new RectOffset(16,12,0,0),border=new RectOffset(),normal={background=ink,textColor=new Color(.88f,.87f,.81f)},hover={background=hoverInk,textColor=Color.white},active={background=pressedInk,textColor=Color.white},focused={background=ink,textColor=new Color(.95f,.92f,.82f)}};
        menuPrimary=new GUIStyle(menuButton){normal={background=bronze,textColor=new Color(1f,.92f,.72f)},hover={background=bronzeHover,textColor=Color.white},active={background=pressedInk,textColor=Color.white},focused={background=bronze,textColor=Color.white}};
        menuInput=new GUIStyle(GUI.skin.textField){font=font,fontSize=17,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,10,0,0),border=new RectOffset(),normal={background=ink,textColor=new Color(.95f,.93f,.86f)},focused={background=hoverInk,textColor=Color.white},hover={background=hoverInk,textColor=Color.white}};
        menuArrow=new GUIStyle(label){fontSize=19,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.78f,.66f,.44f)}};
    }
    void Box(Rect rect){if(page==Page.Match){StoneFrame(rect);return;}Fill(rect,new Color(.025f,.033f,.038f,.97f));Border(rect,new Color(.33f,.30f,.24f,.9f));Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,2),new Color(.62f,.49f,.30f,.85f));}
    void Text(float x,float y,float w,float h,string value,GUIStyle style=null)=>GUI.Label(new Rect(x,y,w,h),value,style??label);
    void Accent(float x,float y,float w){var before=GUI.color;GUI.color=new Color(.8f,.64f,.36f);GUI.DrawTexture(new Rect(x,y,w,2),Texture2D.whiteTexture);GUI.color=before;}
    bool Button(string id,Rect rect,string text,bool prominent=false,GUIStyle custom=null) {
        bool menu=custom==null&&page!=Page.Match;
        GUI.SetNextControlName(id);bool clicked=GUI.Button(rect,text,custom??(menu?(prominent?menuPrimary:menuButton):(prominent?primary:button)));
        if(testing)clicked=false;
        if(pendingTestAction==id&&Event.current.type==EventType.Repaint){pendingTestAction=null;clicked=true;}
        if(menu){bool hovered=GUI.enabled&&rect.Contains(Event.current.mousePosition);Border(rect,hovered?new Color(.75f,.62f,.40f):prominent?new Color(.60f,.48f,.31f):new Color(.26f,.28f,.27f),1);if(prominent)Fill(new Rect(rect.x,rect.y,3,rect.height),new Color(.76f,.62f,.40f));}
        else if(custom==null&&GUI.enabled&&rect.Contains(Event.current.mousePosition))Border(rect,new Color(.85f,.68f,.3f),1);
        return clicked;
    }
    void OnGUI() {
        if(polishLoadingPreview){ContentLoadingScreen.Draw(loadingBackground,loadingHammer,warcraftFont,"Все ресурсы · скачивание\n284 / 3012 МБ",.37f,"",true);return;}
        if(MobileContent.Active&&MobileContent.Current.Busy)return;
        if(terrain==null){DrawLoadingScreen();return;}InitGui();UpdateGuiMetrics();if(introPlaying){DrawIntroScreen();return;}
        if(WorkshopVisible)DrawWorkshopBleed();
        else if(page==Page.Main||page==Page.Setup||page==Page.Progression||page==Page.Network||(page==Page.Settings&&settingsReturn==Page.Main))DrawFrontendBleed();
        if(match!=null&&page!=Page.Viewer)DrawWorldFog();
        Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3(guiOrigin.x,guiOrigin.y,0),Quaternion.identity,Vector3.one*guiScale);
        if(showUpdatePanel){DrawGameUpdates();GUI.matrix=previous;return;}
        if(showVoicePanel){DrawVoicePanel();DrawNetworkRecovery();GUI.matrix=previous;return;}
        if(showStickerPicker&&(page==Page.Network||page==Page.Match||page==Page.Progression)){DrawStickerPicker();DrawNetworkRecovery();GUI.matrix=previous;return;}
        if(showBattleChat&&onlineMatch&&page==Page.Match){DrawBattleChatPanel();DrawNetworkRecovery();GUI.matrix=previous;return;}
        bool updateModal=showUpdatePanel;if(updateModal)GUI.enabled=false;
        if(page==Page.Main)DrawMain();else if(page==Page.Setup)DrawSetup();else if(page==Page.Settings)DrawSettings();else if(page==Page.Network)DrawNetwork();
        else if(page==Page.Guide)DrawGuide();else if(page==Page.Progression)DrawProgression();else if(page==Page.Match){if(HelpOpen)DrawBattleHelp();else{DrawMatch();DrawChallengeHud();}}else if(page==Page.Viewer) {
            Box(new Rect(16,16,1248,56));if(Button("viewer.back",new Rect(26,24,180,40),"← Главное меню"))MainMenu();
            string[] ids={"west","river","east","top"},texts={"Моя база","Река","Восток","Вся карта"};
            for(int i=0;i<4;i++)if(Button("view."+ids[i],new Rect(220+i*160,24,150,40),texts[i]))controls.Focus(ids[i]);
        } else if(page==Page.Help||page==Page.Teams)DrawInfoPanel();
        else if(page==Page.Pause||page==Page.Leave||page==Page.Result)DrawOverlay();
        if(showStickerPicker&&page==Page.Progression)DrawStickerPicker();
        if(versusActive)DrawVersus();
        DrawNetworkRecovery();
        if(!versusActive&&noticeLeft>0&&page!=Page.Match)Text(460,672,790,34,notice,small);
        if(testing&&combat030Gallery)DrawCombat030Gallery();
        if(updateModal)GUI.enabled=true;DrawGameUpdates();
        DismissBuildMenuOnEmptyClick();
        GUI.matrix=previous;
    }
    void DrawMain() {
        DrawMenuBackground();
        Fill(new Rect(0,0,470,720),new Color(.012f,.018f,.023f,.93f));Fill(new Rect(469,0,1,720),new Color(.53f,.43f,.29f,.55f));
        DrawCastleLogo(new Rect(27,10,408,245));
        var descriptionStyle=new GUIStyle(label){fontSize=17,padding=new RectOffset(0,0,0,0)};
        const string description="Постройте армию. Удержите рубеж.\nРазрушьте замок противника.";
        float descriptionHeight=descriptionStyle.CalcHeight(new GUIContent(description),360);
        Text(48,307-18-descriptionHeight,360,descriptionHeight,description,descriptionStyle);
        if(Button("main.play",new Rect(48,307,360,49),"Играть с ботом",true))page=Page.Setup;
        if(Button("main.network",new Rect(48,363,360,49),"Сетевая игра"))OpenNetwork();
        if(Button("main.challenges",new Rect(48,419,360,49),"Испытания и магазин"))OpenProgression(false);
        if(Button("main.guide",new Rect(48,475,360,49),"Учебник рас"))OpenGuide();
        if(Button("main.settings",new Rect(48,541,174,43),"Настройки"))OpenSettings();
        if(Button("main.viewer",new Rect(234,541,174,43),"Карта")){page=Page.Viewer;controls.Focus("west");}
        if(Button("main.intro",new Rect(48,596,174,43),"Смотреть ролик"))StartIntro(null);
        if(Button("main.quit",new Rect(234,596,174,43),"Выход"))Application.Quit();
        Text(48,664,360,28,"CASTLE FIGHT  ·  "+(Application.isMobilePlatform?"ANDROID":"WINDOWS")+"  ·  "+Version,small);
    }
    void DrawSetup() {
        DrawRaceSelect();
    }
    void DrawSettings() {
        if(settingsReturn==Page.Main)DrawMenuBackground();
        if(showResourceDownloads){DrawResourceDownloads();return;}
        if(HdModels.Panel){DrawModelSettings();return;}
        if(ThemesOpen){DrawThemeSettings();return;}
        Box(new Rect(250,24,780,672));Text(288,48,680,43,SettingsLayout035.Mobile?"Настройки Android":"Настройки ПК",heading);Accent(288,101,700);
        GUI.enabled=!confirmDisplay&&!SettingsLayout035.Mobile;
        Text(290,120,310,31,"Разрешение",label);if(Button("settings.resolution",SettingsLayout035.Control(0),SettingsLayout035.Mobile?"По экрану устройства":sizes[draft.resolution]))draft.resolution=(draft.resolution+1)%sizes.Length;
        Text(290,164,310,31,"Режим экрана",label);if(Button("settings.fullscreen",SettingsLayout035.Control(1),SettingsLayout035.Mobile?"На весь экран":draft.fullscreen?"Без рамки на весь экран":"В окне"))draft.fullscreen=!draft.fullscreen;GUI.enabled=!confirmDisplay;
        Text(290,208,310,31,"Качество изображения",label);if(Button("settings.quality",SettingsLayout035.Control(2),new[]{"Низкое","Среднее","Высокое"}[draft.quality]))draft.quality=(draft.quality+1)%3;
        Text(290,252,310,31,"Ограничение кадров",label);if(Button("settings.fps",SettingsLayout035.Control(3),draft.fps==0?"Без ограничения":draft.fps+" FPS"))draft.fps=draft.fps==30?60:draft.fps==60?90:draft.fps==90?120:draft.fps==120?0:30;
        Text(290,296,310,31,"Вертикальная синхронизация",label);if(Button("settings.vsync",SettingsLayout035.Control(4),draft.vsync?"Включена":"Выключена"))draft.vsync=!draft.vsync;
        Text(290,340,310,31,"Общая громкость",label);draft.volume=GUI.HorizontalSlider(new Rect(620,SettingsLayout035.Mobile?341:349,250,SettingsLayout035.Mobile?38:22),draft.volume,0,1);Text(895,340,80,28,(draft.volume*100).ToString("F0")+"%",small);
        Text(290,384,310,31,"Музыка в меню",label);draft.musicVolume=GUI.HorizontalSlider(new Rect(620,SettingsLayout035.Mobile?385:393,250,SettingsLayout035.Mobile?38:22),draft.musicVolume,0,1);Text(895,384,80,28,(draft.musicVolume*100).ToString("F0")+"%",small);
        Text(290,428,310,31,"Скорость камеры",label);draft.cameraSpeed=GUI.HorizontalSlider(new Rect(620,SettingsLayout035.Mobile?429:437,250,SettingsLayout035.Mobile?38:22),draft.cameraSpeed,.5f,2);Text(895,428,80,28,draft.cameraSpeed.ToString("F1")+"×",small);
        Text(290,472,310,31,"Туман войны",label);GUI.enabled=!confirmDisplay&&activeChallenge==null;if(Button("settings.fog",SettingsLayout035.Control(8),activeChallenge!=null?"Включён в испытании":draft.fogOfWar?"Включён":"Выключен"))draft.fogOfWar=!draft.fogOfWar;GUI.enabled=!confirmDisplay;
        Text(290,516,310,31,"Простое затемнение",label);GUI.enabled=!confirmDisplay;if(Button("settings.softfog",SettingsLayout035.Control(9),draft.softFog?"Включено  [x]":"Выключено  [ ]"))draft.softFog=!draft.softFog;GUI.enabled=!confirmDisplay;
        if(Button("settings.theme",new Rect(290,565,189,38),"Тема интерфейса"))ThemesOpen=true;
        if(Button("settings.models",new Rect(491,565,160,38),"Модели и карта"))HdModels.Panel=true;
        if(MobileContent.Active||SettingsLayout035.PreviewMobile){if(Button("settings.downloads",SettingsLayout035.Downloads,"Ресурсы игры"))showResourceDownloads=true;}
        if(Button("settings.updates",SettingsLayout035.Updates,"Обновления")){showUpdatePanel=true;StartCoroutine(CheckGameRelease());}
        if(Button("settings.cancel",SettingsLayout035.Cancel,"Отмена")){if(confirmDisplay)RevertDisplay();page=settingsReturn;}
        if(Button("settings.apply",SettingsLayout035.Apply,"Применить",true))PreviewSettings();
        GUI.enabled=true;
        if(showResourceDownloads)DrawResourceDownloads();
        if(confirmDisplay) {
            Box(new Rect(360,230,560,220));Text(390,257,500,69,"Сохранить новый режим экрана?\nОткат через "+Mathf.CeilToInt(screenDeadline-Time.unscaledTime)+" сек.",label);
            if(Button("settings.keep",new Rect(390,362,230,48),"Сохранить",true))SaveSettings();
            if(Button("settings.revert",new Rect(650,362,230,48),"Вернуть"))RevertDisplay();
        }
    }
    void DrawMatch() {
        bool interactive=GUI.enabled;
        if(aspectWaiting)GUI.enabled=false;
        DrawWarcraftHud();
        GUI.enabled=interactive;
        if(GUI.enabled)DrawNetworkHudOverlay();
        DrawAspectUi();
        DrawSpellDetails();
    }
    void DrawOverlay() {
        if(page==Page.Result&&onlineMatch){DrawOnlineResult();return;}
        GUI.enabled=false;if(match!=null)DrawMatch();GUI.enabled=true;Box(new Rect(0,0,1280,720));Box(new Rect(410,145,460,445));Accent(450,187,50);
        string caption=page==Page.Result?(match.Winner==0?"Победа":match.Winner==1?"Поражение":"Ничья"):page==Page.Leave?"Завершить матч?":"Пауза";
        Text(450,215,380,60,caption,heading);
        if(page==Page.Pause) {
            if(Button("pause.resume",new Rect(450,300,380,52),"Продолжить",true))page=Page.Match;
            if(Button("pause.settings",new Rect(450,369,380,48),"Настройки"))OpenSettings();
            if(Button("pause.leave",new Rect(450,434,380,48),"Сдаться"))page=Page.Leave;
        } else if(page==Page.Leave) {
            Text(450,293,380,50,"Текущий раунд будет завершён.",label);
            if(Button("leave.confirm",new Rect(450,375,380,48),"Да, сдаться")){if(onlineMatch)NetworkReturnToMenu();else{match.Surrender(0);series.FinishRound(match.Winner);SettleChallenge();page=Page.Result;}}
            if(Button("leave.cancel",new Rect(450,440,380,48),"Вернуться в матч",true))page=Page.Match;
        } else {
            Text(450,290,380,75,(series.Finished?"Серия завершена":"Раунд "+series.Round+" / "+series.Rounds)+" · счёт "+series.Wins[0]+":"+series.Wins[1]+"\nВремя: "+TimeSpan.FromSeconds(match.Elapsed).ToString(@"mm\:ss")+" · Погибло: "+match.Kills,label);
            DrawChallengeResult();GUI.enabled=!onlineMatch||!onlineDecision;
            if(Button("result.again",new Rect(450,385,380,48),onlineMatch&&onlineDecision?"Ожидаем соперника":series.Finished?"Реванш":series.Draw?"Переиграть раунд":"Следующий раунд",true)){if(onlineMatch){onlineDecision=true;SendNetwork(new JObject { ["type"]="next_round" });}else if(series.Finished)BeginOfflineVersus();else if(series.Advance())BeginOfflineVersus(true);}
            if(Button("result.menu",new Rect(450,450,380,48),onlineMatch?"Вернуться в комнату":"Главное меню")){if(onlineMatch){onlineDecision=true;SendNetwork(new JObject { ["type"]="return_lobby" });}else MainMenu();}GUI.enabled=true;
        }
    }
    void OnDestroy(){MapArt.Abort();MapArt.Restore();HdModels.Cancel();HdModels.Select(false);if(ownChatBubble)Destroy(ownChatBubble);if(peerChatBubble)Destroy(peerChatBubble);if(chatToolbarIcon)Destroy(chatToolbarIcon);if(micToolbarIcon)Destroy(micToolbarIcon);CloseVersus();if(resultEmblem)Destroy(resultEmblem);if(rankSheet)Destroy(rankSheet);if(castleLogo)Destroy(castleLogo);DisposeVoice();DisposeFrontend();DisposeWorkshop();DisposeRichAura();DisposeFog();DisposeGuide();if(Art036.bundle)Art036.bundle.Unload(false);if(builderCosmeticMaterial)Destroy(builderCosmeticMaterial);DisposeReforgedFx();if(flameMaterial)Destroy(flameMaterial);if(wispMaterial)Destroy(wispMaterial);if(wispMeshMaterial)Destroy(wispMeshMaterial);if(armorAuraMaterial)Destroy(armorAuraMaterial);if(orcRuneMaterial)Destroy(orcRuneMaterial);DestroyHud();foreach(var t in new[]{panel,normal,hot,active})if(t)Destroy(t);if(ringMesh)Destroy(ringMesh);}

    // Exercises the same GUI button branches as a click, using the rendered native player.
    IEnumerator Action(string id) {Debug.Log("CF_QA_ACTION "+id);pendingTestAction=id;float until=Time.realtimeSinceStartup+3;while(pendingTestAction!=null&&Time.realtimeSinceStartup<until)yield return null;Check(pendingTestAction==null,"button "+id);yield return null;}
    void Check(bool passed,string description){testChecks.Add(new JObject {["check"]=description,["passed"]=passed});if(!passed){testFailures++;Debug.LogError("CF_PC_TEST_FAIL "+description);}}
    IEnumerator Shot(string folder,string name) {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return null;}
    IEnumerator Exercise() {
        string folder=BattleLab.Arg("--pc-results",Path.Combine(Application.persistentDataPath,"pc-validation"));Directory.CreateDirectory(folder);
        yield return Shot(folder,"01-menu");
        Check(menuArtwork!=null&&menuAudio!=null&&menuAudio.clip!=null,"PC menu artwork and Godot music are loaded");
        yield return Action("main.network");Check(page==Page.Network,"network directory opens");yield return Shot(folder,"01b-network");
        string testDirectory=BattleLab.Arg("--pc-directory-url");
        if(testDirectory!="") {directoryEndpoint=testDirectory;BeginDirectory();float directoryDeadline=Time.realtimeSinceStartup+15;while(directoryLoading&&Time.realtimeSinceStartup<directoryDeadline)yield return null;Check(!directoryLoading&&directoryStatus.StartsWith("Сервер ответил"),"directory request succeeds against test server");if(directoryReady)yield return ExerciseNetworkLobby(folder);}
        yield return Action("network.back");Check(page==Page.Main,"network directory returns to menu");
        yield return Action("main.settings");yield return Action("settings.fps");int savedFps=draft.fps;
        yield return Shot(folder,"02-settings");yield return Action("settings.apply");Check(page==Page.Main,"settings return to main");
        LoadSettings();Check(options.fps==savedFps,"settings persist after disk reload");
        yield return Action("main.settings");int oldResolution=options.resolution;
        yield return Action("settings.resolution");yield return Action("settings.apply");Check(confirmDisplay,"display change asks to keep or revert");
        yield return Action("settings.revert");Check(!confirmDisplay&&options.resolution==oldResolution,"display revert preserves saved resolution");yield return Action("settings.cancel");
        yield return Action("main.play");yield return Action("setup.human");yield return Shot(folder,"03-race-selection");
        yield return Action("setup.race-pandaren");Check(raceChoice==0,"the fifteen-race screen keeps unported races locked");
        yield return Action("setup.rules");Check(aspectsOn&&AspectCount(0)==2,"battle rules enable the human aspects");yield return Action("setup.rules");Check(!aspectsOn,"battle rules can be switched back to classic");
        startGold=1000;yield return Action("setup.start");Check(page==Page.Match&&match.Huts.Count==0,"clean new human match");
        Check(!controls.buildGrid.activeSelf,"global grid stays hidden");
        controls.Focus("river");var oldCamera=controls.transform.position;builderSelected=false;selectedCastle=true;
        yield return Action("hud.builder");Check(builderSelected&&!selectedCastle&&Vector3.Distance(oldCamera,controls.transform.position)>50&&HudContains(new Vector2(25,50))&&CommandIcon(0),"Human builder shortcut selects worker focuses camera and blocks map input");
        yield return Shot(folder,"84-human-builder-shortcut");
        Check(HudContains(new Vector2(1000,100))&&HudContains(new Vector2(640,50))&&!HudContains(new Vector2(640,510)),"HUD input respects scoreboard clock and playable area");
        yield return Action("match.help");Check(HelpOpen,"F9 race reference opens over live match");yield return Action("info.close");
        yield return Action("match.teams");Check(page==Page.Teams,"F11 team panel opens");yield return Action("info.close");
        Vector2 workerStart=match.Builders[0].position;Check(match.MoveBuilder(0,workerStart+new Vector2(0,5)),"worker accepts a move order");
        for(int i=0;i<20;i++)Tick(.05f);RenderMatch();Check(Vector2.Distance(match.Builders[0].position,workerStart)>4,"worker walks to order");
        FocusBuilder();yield return Shot(folder,"08-builder");
        yield return SampleMotion(folder);
        yield return Action("builder.build");
        yield return Action("build.0");testPreviewCell=new Vector2Int(7,15);controls.FocusWorld(World(HumanMatch.Plot(0,7,15)),45);
        yield return new WaitForSecondsRealtime(.15f);Check(localGrid&&localGrid.activeSelf&&gridMesh.vertexCount<=49*8,"only nearby friendly construction cells are visible");
        yield return Shot(folder,"09-local-grid");testPreviewCell=null;
        yield return Action("build.0");Check(Place(7,15),"place barracks");float beforeGold=match.Gold[0];Check(!Place(7,15)&&match.Gold[0]==beforeGold,"occupied cell does not spend gold");Check(!Place(-1,0),"outside cell rejected");
        yield return Action("build.1");Check(Place(9,15),"place sniper nest");yield return Action("build.2");Check(Place(11,15),"place workshop");
        for(int i=0;i<1400;i++)Tick(.05f);RenderMatch();Check(match.Spawned>0&&match.Huts.Any(h=>h.team==1),"both sides produce; bot builds");
        chosen=-1;controls.Focus("west");yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"04-buildings");
        controls.Focus("river");yield return new WaitForSecondsRealtime(.3f);yield return Shot(folder,"05-battle");
        Check(match.Kills>0,"automatic combat causes deaths");
        for(int i=0;i<2400&&!match.Finished&&!match.Shots.Any(s=>s.kind==2&&!s.hit&&s.duration-s.age>.25f);i++)Tick(.05f);
        RenderMatch();var shell=match.Shots.FirstOrDefault(s=>s.kind==2&&!s.hit&&s.duration-s.age>.2f);
        Check(shell!=null&&match.ShellsFired>0&&match.RifleShots>0,"both rifle tracers and mortar shells are active in the match");
        if(shell!=null){
            for(int i=0;i<100&&shell.age<shell.duration*.5f;i++)Tick(.05f);
            RenderMatch();controls.FocusWorld(PcShotView.Position(shell,.5f,World),30);
            Check(shell.age>shell.duration*.4f&&!shell.hit,"capture shell while airborne");
            yield return Shot(folder,"10-mortar-flight");        }
        for(int i=0;i<100&&!match.Finished&&match.ShellImpacts==0;i++)Tick(.05f);
        Check(match.ShellImpacts>0,"shell reaches impact in playable match");
        controls.Focus("river");
        yield return Action("match.pause");float pausedAt=match.Elapsed;yield return new WaitForSecondsRealtime(.2f);Check(match.Elapsed==pausedAt,"pause freezes authority");
        yield return Shot(folder,"06-pause");yield return Action("pause.resume");Check(page==Page.Match,"resume match");
        yield return Action("match.pause");yield return Action("pause.leave");yield return Action("leave.confirm");Check(page==Page.Result&&match.Winner==1,"surrender produces result");
        yield return Shot(folder,"07-result");yield return Action("result.again");Check(match.Elapsed<1&&match.Huts.Count==0&&match.Units.Count==0,"rematch clears all entities");
        yield return ExerciseHumanRoster(folder);
        yield return ExerciseFoundation(folder);
        yield return ExerciseCastleItems(folder);
        yield return ExerciseHudStats(folder);
        yield return ExerciseSeries(folder);
        yield return ExerciseMixedRaces(folder);
        yield return ExerciseReforgedFx(folder);
        yield return ExerciseOrcs(folder);
        yield return ExerciseNaga(folder);
        MainMenu();yield return Action("main.viewer");Check(page==Page.Viewer,"map viewer opens");yield return Action("viewer.back");Check(page==Page.Main&&match==null,"return from viewer");
        var report=new JObject {["status"]=testFailures==0?"PASS":"FAIL",["checks"]=testChecks,["failures"]=testFailures,["build"]=Version,["reforgedHdImported"]=true,["experimentalHdTextures"]=false,["shopItems"]=9,["buildings"]=16,["baseBuildings"]=11,["unitTypes"]=11,["workerControl"]=true,["nativeMaterialsAudited"]=true,["referenceConsoleHud"]=true,["interpolatedMovement"]=true,["rangedProjectiles"]=true};
        File.WriteAllText(Path.Combine(folder,"ui-verification.json"),report.ToString());Debug.Log("CF_PC_UI_TEST_"+(testFailures==0?"PASS":"FAIL")+" checks="+testChecks.Count);
        Application.Quit(testFailures==0?0:1);
    }
}
}
