using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    sealed class WorkshopState {
        public readonly Dictionary<string,Texture2D> glyphs=new Dictionary<string,Texture2D>();
        public readonly WorkshopDye038 fittingDye=new WorkshopDye038(),battleDye=new WorkshopDye038();
        public int category,previewRace,dragFinger=-1;
        public float scrollY,maxScroll,maxChallengeScroll;
        public Vector2 dragStart;
        public bool collection,dragged,loading,siegeOnly;
        public string item,status;
        public float statusUntil;
        public Texture2D forge;
        public AssetBundle art;
        public GameObject stage;
        public Camera preview,mainCamera;
        public int mainMask;
        public RenderTexture image;
        public PcModel mannequin,battleBuilder;
        public WorkshopFx038 fittingEffects,battleEffects;
        public int mannequinRace=-1;
        public GUIStyle title,details,description,caption,center;
    }

    static readonly ConditionalWeakTable<PcGame,WorkshopState> workshops038=new ConditionalWeakTable<PcGame,WorkshopState>();
    WorkshopState Workshop=>workshops038.GetValue(this,_=>new WorkshopState());
    static readonly string[] WorkshopCategories={"Ауры","Эффекты","Скины","Профиль","Стикеры"};
    static readonly Rect WorkshopShelf=new Rect(35,173,706,469);
    static readonly Rect WorkshopLook=new Rect(772,132,475,339);
    static readonly Color WorkshopGold=new Color(.88f,.69f,.42f),WorkshopIvory=new Color(.94f,.88f,.76f),WorkshopText=new Color(.72f,.76f,.74f);
    bool WorkshopVisible=>page==Page.Progression&&progressShop;

    void OpenWorkshop(){
        var state=Workshop;
        state.previewRace=raceChoice;state.scrollY=0;state.item=null;state.status=null;
        state.dragFinger=-1;state.dragged=false;purchaseConfirm=null;
        if(!state.forge&&!state.loading)StartCoroutine(LoadWorkshopArt(state));
    }

    IEnumerator LoadWorkshopArt(WorkshopState state){
        state.loading=true;
        if(MobileContent.Active){
            if(MobileContent.Current.Has("Workshop038/Forge")){
                yield return MobileContent.Current.LoadAsync<Texture2D>("Workshop038/Forge",value=>state.forge=value);
            }
        }else{
            string path=MobileContent.Media("Frontend/workshop-038.windows");
            if(File.Exists(path)){
                var bundle=AssetBundle.LoadFromFileAsync(path);yield return bundle;state.art=bundle.assetBundle;
                if(state.art){var texture=state.art.LoadAssetAsync<Texture2D>("forge");yield return texture;state.forge=texture.asset as Texture2D;}
            }
        }
        state.loading=false;
    }

    static bool WorkshopInCategory(JObject item,int category){
        string kind=(string)item["kind"];
        switch(category){
            case 0:return kind=="aura";
            case 1:return kind=="effect";
            case 2:return kind=="skin";
            case 3:return kind=="frame"||kind=="avatar";
            default:return kind=="stickers";
        }
    }

    void WorkshopCategory(int category){
        var state=Workshop;
        state.category=category;
        state.item=null;state.scrollY=0;purchaseConfirm=null;
    }

    List<JObject> WorkshopItems(){
        var state=Workshop;
        return ((JArray)progression.Catalog["items"]).OfType<JObject>()
            .Where(row=>WorkshopInCategory(row,state.category)&&(!state.collection||progression.Owned((string)row["id"]))).ToList();
    }

    void DrawWorkshopBleed(){
        var canvas=new Rect(0,0,Screen.width,Screen.height);
        Fill(canvas,new Color(.044f,.060f,.061f));
        var forge=Workshop.forge;
        if(forge)GUI.DrawTexture(canvas,forge,ScaleMode.ScaleAndCrop,false);
    }

    void DrawWorkshop(){
        var state=Workshop;
        if(state.title==null){
            state.title=new GUIStyle(heading){fontSize=37,normal={textColor=WorkshopIvory}};
            state.details=new GUIStyle(heading){fontSize=24,normal={textColor=WorkshopIvory}};
            state.description=new GUIStyle(label){fontSize=18,wordWrap=true,normal={textColor=WorkshopText}};
            state.caption=new GUIStyle(small){
                fontSize=16,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(0,0,0,0),normal={textColor=WorkshopText}
            };
            state.center=new GUIStyle(state.caption){alignment=TextAnchor.MiddleCenter};
        }
        AtlasSurface(new Rect(19,14,1233,74));
        Text(38,15,590,52,"Мастерская",state.title);
        WorkshopCoin(new Rect(681,26,38,38));
        Text(731,24,189,42,progression.Coins+" жетонов",state.details);
        if(Button("progress.challenges",new Rect(925,21,148,48),"Испытания"))OpenProgression(false);
        if(Button("progress.back",new Rect(1084,21,157,48),"В меню"))MainMenu();

        AtlasSurface(new Rect(23,96,730,605));
        for(int i=0;i<WorkshopCategories.Length;i++){
            var tab=new Rect(35+i*111,108,104,47);
            if(Button("workshop.tab."+i,tab,WorkshopCategories[i],state.category==i))WorkshopCategory(i);
        }
        if(Button("workshop.collection",new Rect(590,108,151,47),state.collection?"Все товары":"Мои покупки",state.collection)){
            state.collection=!state.collection;state.scrollY=0;state.item=null;purchaseConfirm=null;
        }

        var rows=WorkshopItems();
        if(!rows.Any(row=>(string)row["id"]==state.item))state.item=rows.Count==0?null:(string)rows[0]["id"];
        bool enabled=GUI.enabled;
        GUI.enabled=enabled&&!state.dragged;
        float contentHeight=Mathf.Max(WorkshopShelf.height,Mathf.Ceil(rows.Count/3f)*150);
        state.maxScroll=contentHeight-WorkshopShelf.height;
        state.scrollY=Mathf.Clamp(state.scrollY,0,state.maxScroll);
        var scroll=GUI.BeginScrollView(WorkshopShelf,new Vector2(0,state.scrollY),new Rect(0,0,685,contentHeight));
        state.scrollY=scroll.y;
        for(int i=0;i<rows.Count;i++){
            var card=new Rect(4+(i%3)*230,(i/3)*150,218,140);
            DrawWorkshopCard(card,rows[i]);
        }
        if(rows.Count==0)Text(18,71,640,34,"В этой категории пока нет покупок",state.description);
        GUI.EndScrollView();
        GUI.enabled=enabled;
        Text(39,658,670,31,state.collection?"Коллекция  ·  "+rows.Count:rows.Count+" товаров",state.caption);
        DrawWorkshopSelection(progression.Item(state.item));
    }

    void DrawWorkshopCard(Rect card,JObject item){
        var state=Workshop;
        string id=(string)item["id"],kind=(string)item["kind"];
        bool selected=state.item==id,owned=progression.Owned(id),equipped=owned&&progression.Equipped(kind)==id;
        var tint=CosmeticColor(item);
        AtlasTile(card,selected,false);
        Fill(new Rect(card.x+1,card.y+1,card.width-2,73),new Color(tint.r*.16f,tint.g*.16f,tint.b*.19f,.93f));
        Border(card,selected?WorkshopGold:new Color(.25f,.31f,.30f),selected?2:1);
        DrawWorkshopIcon(new Rect(card.x+77,card.y+7,64,64),item);
        Text(card.x+8,card.y+79,card.width-16,28,(string)item["title"],state.center);
        string info=equipped?"Надето":owned?"Куплено":item["cost"]+" жетонов";
        Text(card.x+8,card.y+111,card.width-16,22,info,state.center);
        if(Button("workshop.pick."+id,card,"",false,raceCardStyle)){state.item=id;purchaseConfirm=null;state.status=null;}
    }

    void DrawWorkshopSelection(JObject row){
        var state=Workshop;
        bool fitting=state.category<=2&&row!=null;
        var goldEdge=new Color(.52f,.39f,.23f);
        Fill(new Rect(769,101,481,370),new Color(.037f,.044f,.038f,.22f));
        Border(new Rect(768,100,483,371),goldEdge,1);
        if(fitting&&state.image)GUI.DrawTexture(WorkshopLook,state.image,ScaleMode.ScaleToFit,true);
        if(!fitting&&row!=null){
            Fill(new Rect(915,169,190,190),new Color(.042f,.066f,.07f,.9f));
            Border(new Rect(915,169,190,190),goldEdge,2);
            DrawWorkshopIcon(new Rect(931,185,158,158),row);
            if((string)row["kind"]=="stickers"&&Button("progress.preview.meme",new Rect(900,400,220,46),"Смотреть набор"))
                OpenWorkshopStickers();
        }
        if(fitting){
            Fill(new Rect(793,420,432,42),new Color(.033f,.044f,.043f,.9f));
            var race=state.previewRace;
            if(Button("workshop.race.prev",new Rect(794,420,51,42),"‹"))state.previewRace=(race+15)%16;
            if(Button("workshop.race.next",new Rect(1174,420,51,42),"›"))state.previewRace=(race+1)%16;
            Text(852,427,313,27,RaceTitle(race),state.center);
        }
        var detail=new Rect(768,476,483,225);
        AtlasSurface(detail);
        if(row==null){Text(790,503,425,49,"Выбери товар в этой категории",state.details);return;}
        string id=(string)row["id"],kind=(string)row["kind"];
        bool owned=progression.Owned(id),equipped=owned&&progression.Equipped(kind)==id;
        Text(791,489,430,40,(string)row["title"],state.details);
        Text(791,536,429,71,(string)row["description"],state.description);
        if(fitting)Text(791,599,432,31,"Оформление для боёв с ботом",state.caption);
        if(state.status!=null&&Time.unscaledTime<state.statusUntil)
            Text(791,633,227,58,state.status,state.description);
        else Text(791,643,227,37,equipped?"Надето":owned?"В коллекции":row["cost"]+" жетонов",state.description);
        string action=owned?kind=="stickers"?"Открыть набор":equipped?"Снять":"Надеть":
            purchaseConfirm==id?"Подтвердить":"Купить";
        bool enabled=GUI.enabled;GUI.enabled=enabled&&(owned||progression.Coins>=(int)row["cost"]);
        if(Button("progress.item."+id,new Rect(1026,637,204,47),action,true))WorkshopPurchase(row,equipped);
        GUI.enabled=enabled;
    }

    void WorkshopPurchase(JObject row,bool equipped){
        var state=Workshop;string id=(string)row["id"],kind=(string)row["kind"];
        try{
            if(progression.Owned(id)){
                if(kind=="stickers")OpenWorkshopStickers();
                else progression.Equip(id,!equipped);
                purchaseConfirm=null;state.status=null;
                return;
            }
            if(purchaseConfirm!=id){purchaseConfirm=id;return;}
            if(progression.Buy(id,out string error)){state.status="Куплено";purchaseConfirm=null;}
            else state.status=error;
        }catch(IOException){state.status="Не удалось сохранить";}
        state.statusUntil=Time.unscaledTime+4;
    }

    void OpenWorkshopStickers(){
        LoadStickerCatalog();stickerPack=stickerPacks.Count-1;stickerSelected=0;showStickerPicker=true;
    }

    void WorkshopCoin(Rect frame){
        const string id="@coin";
        var state=Workshop;
        if(!state.glyphs.TryGetValue(id,out var glyph))state.glyphs[id]=glyph=WorkshopGlyph038.Create("coin",0,WorkshopGold,WorkshopIvory);
        GUI.DrawTexture(frame,glyph,ScaleMode.ScaleToFit,true);
    }

    void DrawWorkshopIcon(Rect frame,JObject row){
        string id=(string)row["id"],kind=(string)row["kind"];
        if(kind=="stickers"){
            LoadStickerCatalog();
            if(stickerPacks.Count>0&&stickerPacks.Last().items.Count>0)StickerImage(frame,stickerPacks.Last().items[0]);
            return;
        }
        if(kind=="avatar"){
            var icon=ContentResources.Load<Texture2D>("RaceIcon-"+(string)row["asset"]);
            if(icon)GUI.DrawTexture(frame,icon,ScaleMode.ScaleToFit,true);
            return;
        }
        var state=Workshop;
        if(!state.glyphs.TryGetValue(id,out var image)){
            var finish=WorkshopCatalog038.Find(id);
            state.glyphs[id]=image=WorkshopGlyph038.Create(kind,finish?.pattern??0,finish?.Tint??CosmeticColor(row),finish?.Highlight??WorkshopIvory);
        }
        GUI.DrawTexture(frame,image,ScaleMode.ScaleToFit,true);
    }

    void UpdateProgressionTouch(){
        if(page!=Page.Progression||showStickerPicker||!workshops038.TryGetValue(this,out var state))return;
        var viewport=progressShop?WorkshopShelf:new Rect(40,176,1200,502);
        for(int i=0;i<Input.touchCount;i++){
            var touch=Input.GetTouch(i);
            if(touch.phase==TouchPhase.Began&&state.dragFinger<0){
                state.dragged=false;
                if(viewport.Contains(GuiPoint(touch.position))){state.dragFinger=touch.fingerId;state.dragStart=touch.position;}
            }
            if(touch.fingerId!=state.dragFinger)continue;
            if(touch.phase==TouchPhase.Moved&&(touch.position-state.dragStart).sqrMagnitude>144){
                state.dragged=true;
                float distance=touch.deltaPosition.y/guiScale;
                if(progressShop)state.scrollY=Mathf.Clamp(state.scrollY+distance,0,state.maxScroll);
                else progressionScroll.y=Mathf.Clamp(progressionScroll.y+distance,0,state.maxChallengeScroll);
            }
            if(touch.phase==TouchPhase.Ended||touch.phase==TouchPhase.Canceled)state.dragFinger=-1;
        }
    }

    void UpdateWorkshopPreview(){
        if(!workshops038.TryGetValue(this,out var state))return;
        if(!WorkshopVisible||showStickerPicker||state.category>2||state.item==null){
            if(state.stage)CloseWorkshopPreview(state);
            return;
        }
        if(state.mannequinRace!=state.previewRace||!state.mannequin){
            CloseWorkshopPreview(state);
            OpenWorkshopPreview(state);
        }
        if(!state.mannequin)return;
        var selected=WorkshopCatalog038.Find(state.item);
        var dye=WorkshopCatalog038.Find(state.category==2?state.item:progression.Equipped("skin"));
        var aura=WorkshopCatalog038.Find(state.category==0?state.item:progression.Equipped("aura"));
        var effect=WorkshopCatalog038.Find(state.category==1?state.item:progression.Equipped("effect"));
        bool walking=state.category==1&&selected!=null&&selected.pattern<3;
        state.fittingDye.Apply(state.mannequin,dye);
        state.mannequin.transform.localRotation=Quaternion.Euler(0,167+Mathf.Sin(Time.unscaledTime*.8f)*29,0);
        state.mannequin.Play(walking?"Walk":"Stand");
        state.fittingEffects.Update(state.stage.transform.position,aura,effect,walking);
    }

    void OpenWorkshopPreview(WorkshopState state){
        const int layer=29;
        var prefab=MenuBuilder(state.previewRace);
        if(!prefab)return;
        state.stage=new GameObject("Workshop preview"){layer=layer};
        Transform pivot=state.stage.transform;pivot.position=new Vector3(0,-9000,0);
        state.mannequin=Instantiate(prefab,pivot).GetComponent<PcModel>();
        state.mannequinRace=state.previewRace;
        foreach(var node in state.mannequin.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;
        state.mannequin.Play("Stand");state.mannequin.SamplePose();
        var bounds=state.mannequin.BodyBounds();
        float scale=3.3f/Mathf.Max(1.5f,bounds.size.y);
        state.mannequin.transform.localScale*=scale;
        state.mannequin.transform.position=pivot.position+(pivot.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;
        state.image=new RenderTexture(384,286,16,RenderTextureFormat.ARGB32){name="Workshop fitting room",antiAliasing=1};
        state.image.Create();
        state.preview=new GameObject("Workshop camera"){layer=layer}.AddComponent<Camera>();
        state.preview.transform.SetParent(pivot,false);
        state.preview.cullingMask=1<<layer;state.preview.clearFlags=CameraClearFlags.SolidColor;state.preview.backgroundColor=Color.clear;
        state.preview.orthographic=true;state.preview.orthographicSize=2.56f;
        state.preview.nearClipPlane=.1f;state.preview.farClipPlane=28;
        state.preview.allowHDR=false;state.preview.allowMSAA=false;state.preview.targetTexture=state.image;
        state.preview.transform.localPosition=new Vector3(0,4,-8);state.preview.transform.LookAt(pivot.position+Vector3.up*1.6f);
        var light=new GameObject("Workshop key light"){layer=layer}.AddComponent<Light>();
        light.transform.SetParent(pivot,false);light.transform.localRotation=Quaternion.Euler(48,-28,0);
        light.type=LightType.Directional;light.cullingMask=1<<layer;light.intensity=1.15f;light.shadows=LightShadows.None;
        state.mainCamera=Camera.main;
        if(state.mainCamera){state.mainMask=state.mainCamera.cullingMask;state.mainCamera.cullingMask&=~(1<<layer);}
        state.fittingEffects=new WorkshopFx038(pivot,overlayMaterial,true);
    }

    void CloseWorkshopPreview(WorkshopState state){
        state.fittingDye.Clear();state.fittingEffects?.Dispose();state.fittingEffects=null;
        if(state.preview){state.preview.enabled=false;state.preview.targetTexture=null;}
        if(state.stage){state.stage.SetActive(false);Destroy(state.stage);}
        if(state.image){state.image.Release();Destroy(state.image);}
        if(state.mainCamera)state.mainCamera.cullingMask=state.mainMask;
        state.stage=null;state.mannequin=null;state.image=null;state.preview=null;state.mainCamera=null;state.mannequinRace=-1;
    }

    void UpdateWorkshopBattle(PcModel worker,string auraId){
        string dye=progression.Equipped("skin"),effect=progression.Equipped("effect");
        bool legacyAura=auraId=="aura_jade"||auraId=="aura_flame";
        string customAura=legacyAura?"":auraId;
        if(dye.Length==0&&effect.Length==0&&customAura.Length==0){ClearWorkshopBattle();return;}
        var state=Workshop;
        if(state.battleBuilder!=worker){
            ClearWorkshopBattle();
            state.battleBuilder=worker;
        }
        state.battleDye.Apply(worker,WorkshopCatalog038.Find(dye));
        if(effect.Length==0&&customAura.Length==0){
            state.battleEffects?.Dispose();state.battleEffects=null;
            return;
        }
        if(state.battleEffects==null)state.battleEffects=new WorkshopFx038(visuals.transform,overlayMaterial,false);
        state.battleEffects.Update(worker.transform.position,WorkshopCatalog038.Find(customAura),
            WorkshopCatalog038.Find(effect),match.Builders[0].pose=="Walk");
    }

    void WorkshopBuildingPlaced(Vector2 point){
        if(onlineMatch||!workshops038.TryGetValue(this,out var state)||state.battleEffects==null)return;
        state.battleEffects.BuildFlash(World(point),WorkshopCatalog038.Find(progression.Equipped("effect")));
    }

    void ClearWorkshopBattle(){
        if(!workshops038.TryGetValue(this,out var state))return;
        state.battleEffects?.Dispose();state.battleEffects=null;state.battleDye.Clear();state.battleBuilder=null;
    }

    void DisposeWorkshop(){
        if(!workshops038.TryGetValue(this,out var state))return;
        CloseWorkshopPreview(state);ClearWorkshopBattle();
        foreach(var image in state.glyphs.Values)if(image)Destroy(image);
        state.glyphs.Clear();
        if(state.art)state.art.Unload(true);
    }
}
}
