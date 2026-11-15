using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CastleFight {
// A race owns its catalog and presentation assets; the match authority consumes catalog rules.
public sealed class PcRacePack : ScriptableObject {
    public TextAsset catalog;
    public GameObject[] units,buildings,builders,hdUnits,hdBuildings,hdBuilders;
    public Texture2D[] buildingIcons;public Texture2D builderIcon,buildIcon;
    public string[] abilityIds;public Texture2D[] abilityIcons;
    public IEnumerable<GameObject> Prefabs=>units.Concat(buildings).Concat(builders).Concat(hdUnits).Concat(hdBuildings).Concat(hdBuilders).Where(p=>p);
}
}
