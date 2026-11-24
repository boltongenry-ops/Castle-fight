using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace CastleFight {
// Local PC progression. It never impersonates the production account wallet.
public sealed class PcProgressionStore {
    public readonly JObject Catalog;readonly string path;JObject state;
    static JObject Empty()=>new JObject{["schema"]=1,["coins"]=0,["completed"]=new JArray(),["unlocked"]=new JArray(),["equipped"]=new JObject(),["best"]=new JObject()};
    public PcProgressionStore(JObject catalog,string file){Catalog=catalog;path=file;state=Empty();
        foreach(string candidate in new[]{path,path+".bak"})try{if(!File.Exists(candidate))continue;var saved=JObject.Parse(File.ReadAllText(candidate));if((int?)saved["schema"]!=1||saved["coins"]?.Type!=JTokenType.Integer||(int)saved["coins"]<0||!(saved["completed"] is JArray)||!(saved["unlocked"] is JArray)||!(saved["equipped"] is JObject)||!(saved["best"] is JObject))continue;state=saved;break;}catch(IOException){}catch(Newtonsoft.Json.JsonException){}
    }
    public int Coins=>(int)state["coins"];
    public int CompletedCount=>((JArray)Catalog["challenges"]).OfType<JObject>().Count(r=>Completed((string)r["id"]));
    public int Experience=>((JArray)Catalog["challenges"]).OfType<JObject>().Where(r=>Completed((string)r["id"])).Sum(ChallengeCatalog034.Experience);
    public int MasteryLevel=>ChallengeCatalog034.Level(Experience);
    public int NextLevelExperience=>ChallengeCatalog034.Threshold(MasteryLevel+1);
    public string MasteryTitle=>ChallengeCatalog034.Title(MasteryLevel);
    public JObject Challenge(string id)=>((JArray)Catalog["challenges"]).OfType<JObject>().FirstOrDefault(v=>(string)v["id"]==id)
        ??(Catalog["mythic"] as JArray)?.OfType<JObject>().FirstOrDefault(v=>(string)v["id"]==id);
    public JObject Item(string id)=>((JArray)Catalog["items"]).OfType<JObject>().FirstOrDefault(v=>(string)v["id"]==id);
    public bool Completed(string id)=>((JArray)state["completed"]).Any(v=>(string)v==id);
    public bool Owned(string id)=>((JArray)state["unlocked"]).Any(v=>(string)v==id);
    public string Equipped(string kind)=>(string)state["equipped"][kind]??"";
    public float Best(string id)=>(float?)state["best"][id]??0;
    void Commit(JObject next){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+".tmp";File.WriteAllText(temp,next.ToString());if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);state=next;}
    public int Complete(string id,float seconds){var row=Challenge(id);if(row==null||seconds<0||float.IsNaN(seconds)||float.IsInfinity(seconds))return 0;var next=(JObject)state.DeepClone();int reward=Completed(id)?0:(int)row["reward"];if(!Completed(id))((JArray)next["completed"]).Add(id);next["coins"]=checked(Coins+reward);float best=Best(id);if(best<=0||seconds<best)next["best"][id]=seconds;Commit(next);return reward;}
    public bool Buy(string id,out string error){error="";var row=Item(id);if(row==null){error="Неизвестный товар";return false;}if(Owned(id)){error="Уже куплено";return false;}int cost=(int)row["cost"];if(Coins<cost){error="Не хватает жетонов: нужно "+cost;return false;}var next=(JObject)state.DeepClone();next["coins"]=Coins-cost;((JArray)next["unlocked"]).Add(id);Commit(next);return true;}
    public bool Equip(string id,bool equip){var row=Item(id);if(row==null||!Owned(id))return false;var next=(JObject)state.DeepClone();string kind=(string)row["kind"];if(equip)next["equipped"][kind]=id;else if(Equipped(kind)==id)((JObject)next["equipped"]).Remove(kind);Commit(next);return true;}
}
}
