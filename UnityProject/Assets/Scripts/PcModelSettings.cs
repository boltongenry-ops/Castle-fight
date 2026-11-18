using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame {
    ReforgedModelPack HdModels=>ReforgedModelPack.For(this);

    void LoadModelPreference(string json){
        HdModels.SetCache(settingsPath);
        try{var style=JObject.Parse(json)["modelStyle"];HdModels.Select(style?.Type==JTokenType.String&&(string)style=="reforged");}
        catch(Newtonsoft.Json.JsonException){HdModels.Select(false);}
    }

    void DrawModelSettings(){
        var models=HdModels;
        Box(new Rect(240,66,800,588));
        Text(286,93,710,50,"Модели",heading);Accent(286,153,708);
        bool interactive=GUI.enabled;GUI.enabled=interactive&&!models.Busy;
        if(Button("models.classic",new Rect(286,179,330,52),"Классические",!models.Draft))models.Draft=false;
        if(Button("models.reforged",new Rect(644,179,350,52),"Reforged",models.Draft))models.Draft=true;
        GUI.enabled=interactive;

        Text(286,259,708,56,models.Draft?"HD-модели и иконки бойцов и строителей.":"Классический стиль доступен для всех рас.",label);
        string detail=models.Draft?"12 моделей кентавров и пандаренов и часть построек пока в классическом стиле.":"Можно включить Reforged и скачать модели здесь в любое время.";
        Text(286,316,708,64,detail,small);
        if(models.Draft){
            if(models.Total>0){
                var bar=new Rect(287,407,707,14);
                Fill(bar,new Color(.105f,.13f,.13f));Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01((float)models.Bytes/models.Total),bar.height),new Color(.71f,.54f,.27f));
                Text(287,428,707,27,(models.Bytes/1048576f).ToString("F1")+" / "+(models.Total/1048576f).ToString("F1")+" МБ",small);
            }
            string status=models.Status!=""?models.Status:models.Installed?"Пакет скачан":"Пакет ещё не скачан";
            Text(286,466,708,36,status,small);
            if(models.Busy){if(Button("models.stop",new Rect(644,514,350,48),"Остановить"))models.Cancel();}
            else if(!models.Installed&&!models.Active&&Button("models.download",new Rect(644,514,350,48),"Скачать модели Reforged",true))
                StartCoroutine(models.Download(true));
        }
        GUI.enabled=interactive&&!models.Busy;
        if(Button("models.back",new Rect(286,586,260,47),"← К настройкам"))models.Panel=false;
        GUI.enabled=interactive;
    }
}
}
