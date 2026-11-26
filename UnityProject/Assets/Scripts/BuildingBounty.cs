namespace CastleFight {
public sealed partial class HumanMatch {
    // Warcraft object data: ubba + ubdi for structures with one-sided dice. Also includes later map additions.
    static int OriginalBuildingBounty(string building)=>building switch {
        "h000" => 20,
        "h029" => 22,
        "h049" => 23,
        "h002" or "h01I" or "h01R" or "hNP1" or "hNV1" or "pA01" or "pA02" or "pB01" or "pB02" or
        "pF01" or "pG01" or "pH01" or "pH02" or "pK01" or "pL01" or "pM01" or "pM02" or "pS01" or
        "pS02" or "pT01" or "pT02" or "pW01" or "pW02" => 24,
        "h04V" => 25,
        "h003" => 28,
        "h00Q" or "h00S" or "hNP2" or "hNV2" => 30,
        "h01D" or "h01P" or "h03U" or "h04A" or "h05J" => 32,
        "h02B" or "h06I" => 34,
        "h00J" or "h020" or "h03K" => 35,
        "h05X" or "h08X" => 36,
        "h006" or "h03X" or "hNT1" or "hNT2" => 37,
        "h047" or "h04L" or "h06J" or "h088" or "hNS1" or "hNW1" => 38,
        "h001" or "h00Y" or "h012" or "h02H" or "h05I" or "h05M" or "h07M" or "hNA1" => 40,
        "h00T" => 41,
        "h004" or "h01K" or "h02U" or "h048" => 42,
        "o000" => 43,
        "h013" or "h01H" or "h024" or "h02P" or "h042" or "h04Z" or "h05T" or "h071" or "h07H" => 44,
        "h010" or "h08P" => 45,
        "h01C" or "h01N" or "h01Y" or "h045" or "h07O" => 46,
        "h005" or "h04O" => 47,
        "h00Z" or "h040" or "h04F" or "h04S" => 48,
        "h00F" or "h00G" or "h00R" or "h015" or "h01O" or "h02C" or "h02R" or "h037" or "h04W" or
        "h069" or "hNO1" or "hNV3" or "n01J" => 50,
        "h00B" or "h023" or "h02K" or "h04R" or "h060" or "h073" or "n02D" => 52,
        "h02N" or "h05V" or "hNL1" => 54,
        "h00V" or "h039" or "h03T" => 55,
        "h01B" or "h025" or "h03I" or "h05E" => 56,
        "h011" or "h03Q" => 57,
        "h01L" => 58,
        "h00K" or "h04K" or "h04Q" or "h056" or "h05L" or "h061" or "h076" or "h0A1" => 60,
        "h00M" or "h014" or "h027" or "h06Y" => 62,
        "h05D" => 64,
        "h03J" => 65,
        "h05R" => 66,
        "h09J" => 67,
        "h01F" or "h05Z" or "h09L" or "h09T" => 68,
        "h009" or "h01M" or "h01Z" or "h021" or "h026" or "h02A" or "h03D" or "h03G" or "h062" or
        "h06O" or "h070" or "hNC1" => 70,
        "h00A" or "h00I" or "h035" or "h036" or "h092" or "h09P" or "h09X" => 72,
        "h03Z" or "h04C" => 73,
        "h01T" => 74,
        "h05U" or "h07L" or "hNA2" => 75,
        "h02I" or "h04B" or "h04N" or "h08Y" or "h09B" or "h09I" or "hNS2" or "hNW2" => 76,
        "h01J" or "h03S" or "h04M" => 80,
        "h03F" => 83,
        "h03M" => 85,
        "h031" or "h032" or "h04P" or "h07D" => 90,
        "h046" => 92,
        "h07N" => 94,
        "h02O" => 95,
        "h041" or "h044" => 97,
        "h054" => 98,
        "h007" or "h008" or "h01S" or "h03L" or "h043" or "h04T" => 100,
        "h028" => 101,
        "h03W" or "h04D" or "h04U" => 104,
        "h01W" or "h038" => 105,
        "h09H" => 106,
        "h022" => 108,
        "h03Y" => 109,
        "h05G" => 111,
        "h03O" => 120,
        "h072" => 121,
        "h01Q" or "h059" or "h07I" or "h100" => 130,
        "h02D" or "hNB1" => 136,
        "h01X" or "h063" => 140,
        "h06L" => 141,
        "h00N" or "h03E" or "h055" or "h09Y" => 150,
        "h06D" or "h06Z" => 151,
        "h097" => 154,
        "h04E" => 156,
        "h00X" => 160,
        "h01E" => 170,
        "h05F" => 180,
        _=>-1
    };

    int BuildingBounty(Hut hut){
        int original=OriginalBuildingBounty(Lines[hut.kind].building);
        // Authored buildings use a fifth of the gold invested.
        return original>=0?original:hut.spent/5;
    }
}
}
