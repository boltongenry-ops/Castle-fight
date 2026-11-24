using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CastleFight {
public sealed partial class PcGame
{
    IEnumerator ExerciseCastleThemes()
    {
        string folder = BattleLab.Arg("--pc-results", Path.Combine(Application.persistentDataPath, "castle-themes"));
        string atlases = Path.Combine(folder, "Atlases");
        Directory.CreateDirectory(atlases);
        MainMenu();
        raceChoice = 2;
        botRaceChoice = 5;
        var root = castleObjects[0];
        int descendants = root.GetComponentsInChildren<Transform>(true).Length;
        var reference = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .Select(m => m ? m.mainTexture : null).First(t => t && t.width == 512 && t.height == 512);
        var source = CastleThemes.Readback(reference, false);
        var original = source.GetPixels32();
        Destroy(source);
        NewMatch();
        testHoldAuthority = true;
        options.fogOfWar = false;
        controls.EdgePan = false;
        RenderMatch();
        var hashes = new HashSet<string>();
        using (var sha = SHA256.Create())
        for (int race = 0; race < CastleThemes.RaceCount; race++)
        {
            castleThemes.Apply(root, race, 0, false);
            var painted = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Select(m => m ? m.mainTexture : null).FirstOrDefault(t => t && t.name.StartsWith("Faction atlas " + race + " ·"));
            if (!painted)
            {
                Check(false, "Faction atlas loaded for " + race);
                continue;
            }
            var readable = CastleThemes.Readback(painted, false);
            var pixels = readable.GetPixels32();
            int changed = 0, alpha = 0, roof = 0, door = 0, crest = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                var a = original[i];
                var b = pixels[i];
                if (a.a == b.a) alpha++;
                if (a.a == 0 || (a.r == b.r && a.g == b.g && a.b == b.b)) continue;
                changed++;
                int x = i % 512, y = 511 - i / 512;
                if (x >= 94 && x < 215 && y > 133 && y < 254) roof++;
                if (x >= 114 && x < 166 && y > 61 && y < 117) door++;
                if ((x - 45) * (x - 45) + (y - 75) * (y - 75) < 24 * 24) crest++;
            }
            Check(changed > 170000 && alpha == pixels.Length && roof > 4500 && door > 1900 && crest > 1000,
                "Faction " + race + " repaints masonry, door, roof and crest without modifying alpha");
            var png = readable.EncodeToPNG();
            hashes.Add(System.Convert.ToBase64String(sha.ComputeHash(png)));
            File.WriteAllBytes(Path.Combine(atlases, CastleThemes.RaceName(race) + ".png"), png);
            Destroy(readable);
            yield return null;
        }
        Check(hashes.Count == 18, "Every faction has a unique complete atlas");
        Check(root.GetComponentsInChildren<Transform>(true).Length == descendants, "No standalone castle ornaments were added");

        matchRace = 2;
        RenderMatch();
        controls.FocusWorld(World(HumanMatch.CastlePosition(0)), 42);
        yield return Shot(folder, "01-naga-castle");
        controls.FocusWorld(World(HumanMatch.CastlePosition(1)), 42);
        yield return Shot(folder, "02-northern-castle");
        matchRace = 6;
        RenderMatch();
        controls.FocusWorld(World(HumanMatch.CastlePosition(0)), 42);
        yield return Shot(folder, "03-mechanisms-castle");
        matchRace = 17;
        RenderMatch();
        yield return Shot(folder, "04-black-hand-castle");
        MainMenu();
        yield return null;
        Check(root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
            .Any(m => m && m.mainTexture == reference), "Leaving restores original material");
        File.WriteAllText(Path.Combine(folder, "castle-checks.json"), new JObject
        {
            ["checks"] = testChecks,
            ["failures"] = testFailures
        }.ToString());
        Application.Quit(testFailures == 0 ? 0 : 1);
    }
}
}
