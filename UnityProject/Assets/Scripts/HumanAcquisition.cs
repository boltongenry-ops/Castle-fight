namespace CastleFight {
public sealed partial class HumanMatch {
    // Effective acquisition ranges in Warcraft units from War3Patch and CF240_BASE_NERUB.w3x.
    // Authored catalogs can override the range in Unity units. Missing source entries fall back to the existing rule.
    internal static float AcquisitionRange(string raw, Newtonsoft.Json.Linq.JObject stats,float fallback) {
        float explicitRange=F(stats,"acquisition_range",-1);
        if(explicitRange>=0)return explicitRange;
        int native=raw switch {
            "Hapm" or "Harf" or "Hart" or "Hdgo" or "Hhkl" or "Hlgr" or "Hmbr" or "Hmgd" or "Hmkg"
                or "Hpal" or "Hpb1" or "Hpb2" or "Huth" or "Naka" or "Nal2" or "Nal3" or "Nalc" or "Nalm"
                or "Nbbc" or "Nbst" or "Nkjx" or "Nklj" or "Nmag" or "Nman" or "Npbm" or "Npld" or "Nplh"
                or "Nrob" or "Nsjs" or "Ntin" or "Obla" or "Ocb2" or "Ocbh" or "Ogrh" or "Opgh" or "Orex"
                or "Osam" or "Otcc" or "Otch" or "Uanb" or "Ubal" or "Ucrl" or "Udea" or "Udre" or "Udth"
                or "Uear" or "Umal" or "Utic" or "Uvar" or "Uvng" or "e00D" or "e00F" or "eaoe" or "eaom"
                or "eaow" or "edcm" or "eden" or "edoc" or "efon" or "ehip" or "ehpr" or "eilw" or "emtg"
                or "ensh" or "esen" or "espv" or "etoa" or "etoe" or "etol" or "even" or "h00C" or "h00E"
                or "h00O" or "h00P" or "h017" or "h018" or "h019" or "h01A" or "h033" or "h051" or "h06P"
                or "h075" or "h089" or "h09W" or "h0DF" or "hPAD" or "hPBD" or "hUAv" or "hbew" or "hcth"
                or "hgry" or "hgyr" or "hhdl" or "hhes" or "hkni" or "hmil" or "hmtt" or "hpea" or "hrtt"
                or "hvlt" or "hwat" or "hwt2" or "hwt3" or "n00F" or "n00G" or "n016" or "n017" or "n018"
                or "n01S" or "nadk" or "nadr" or "nahy" or "nalb" or "nanc" or "nano" or "nanw" or "narg"
                or "nass" or "nban" or "nbda" or "nbdk" or "nbdm" or "nbdo" or "nbds" or "nbdw" or "nbee"
                or "nbld" or "nbnb" or "nbsp" or "nbwm" or "nbzd" or "nbzk" or "ncaw" or "ncb0" or "ncb1"
                or "ncb2" or "ncb3" or "ncb4" or "ncb5" or "ncb6" or "ncb7" or "ncb8" or "ncb9" or "ncba"
                or "ncbb" or "ncbc" or "ncbd" or "ncbe" or "ncbf" or "ncen" or "ncer" or "ncfs" or "ncg1"
                or "ncg2" or "ncg3" or "ncgb" or "nchg" or "nchr" or "ncim" or "nckb" or "ncnk" or "ncnt"
                or "ncop" or "ncp2" or "ncp3" or "ncpn" or "ncrb" or "nct1" or "nct2" or "ncta" or "ncte"
                or "nctl" or "ndch" or "nder" or "ndh0" or "ndh1" or "ndh2" or "ndh3" or "ndh4" or "ndmu"
                or "ndog" or "ndqn" or "ndqs" or "ndqt" or "ndr1" or "ndr2" or "ndr3" or "ndrd" or "ndrf"
                or "ndrg" or "ndrj" or "ndrk" or "ndrl" or "ndrn" or "ndro" or "ndrp" or "ndrr" or "ndrt"
                or "ndru" or "ndrv" or "ndrw" or "ndrz" or "ndsa" or "ndtb" or "ndtt" or "ndtw" or "ndwm"
                or "nech" or "necr" or "negz" or "nelb" or "nele" or "nenc" or "nenf" or "nenp" or "nepl"
                or "nfbr" or "nfel" or "nfgb" or "nfgl" or "nfgt" or "nfgu" or "nfh0" or "nfh1" or "nfod"
                or "nfoh" or "nfor" or "nfot" or "nfov" or "nfpc" or "nfpl" or "nfpt" or "nfpu" or "nfr1"
                or "nfr2" or "nfra" or "nfrb" or "nfrg" or "nfrl" or "nfro" or "nfrp" or "nftb" or "nftk"
                or "nftr" or "ngad" or "ngbl" or "ngdk" or "nggr" or "ngh1" or "ngh2" or "ngir" or "ngme"
                or "ngnb" or "ngnh" or "ngni" or "ngno" or "ngns" or "ngnv" or "ngnw" or "ngol" or "ngrd"
                or "ngrk" or "ngsp" or "ngst" or "ngt2" or "ngwr" or "ngz1" or "ngz2" or "ngz3" or "ngz4"
                or "ngza" or "ngzc" or "ngzd" or "nhew" or "nhmc" or "nhns" or "nhrh" or "nhrq" or "nhrr"
                or "nhyd" or "nhyh" or "ninf" or "nits" or "nitt" or "nitw" or "njg1" or "njga" or "njgb"
                or "njks" or "nkob" or "nkol" or "nkot" or "nlkl" or "nlpd" or "nlpr" or "nlps" or "nlrv"
                or "nlsn" or "nlv1" or "nlv2" or "nlv3" or "nmam" or "nmcf" or "nmdr" or "nmer" or "nmfs"
                or "nmg0" or "nmg1" or "nmgd" or "nmgr" or "nmgw" or "nmh0" or "nmh1" or "nmit" or "nmmu"
                or "nmoo" or "nmpe" or "nmpg" or "nmr0" or "nmr2" or "nmr3" or "nmr4" or "nmr5" or "nmr6"
                or "nmr7" or "nmr8" or "nmr9" or "nmra" or "nmrb" or "nmrc" or "nmrd" or "nmre" or "nmrf"
                or "nmrk" or "nmrl" or "nmrm" or "nmrr" or "nmrv" or "nmsh" or "nmsn" or "nmtw" or "nmyr"
                or "nmys" or "nndk" or "nndr" or "nnmg" or "nnrg" or "nnrs" or "nnwa" or "nnws" or "nnzg"
                or "noga" or "nogl" or "nogm" or "nogn" or "nogo" or "nogr" or "nomg" or "now2" or "now3"
                or "nowb" or "nowe" or "nowk" or "nowl" or "npfl" or "npfm" or "npig" or "nplb" or "nplg"
                or "npn1" or "npn3" or "npn4" or "npn6" or "npng" or "npnw" or "nqbh" or "nrac" or "nrat"
                or "nrdr" or "nrmf" or "nrog" or "nrvd" or "nrvf" or "nrvi" or "nrvl" or "nrvs" or "nrwm"
                or "nrzb" or "nrzg" or "nrzm" or "nrzs" or "nsbm" or "nsbs" or "nsc2" or "nsc3" or "nscb"
                or "nsce" or "nsea" or "nser" or "nsgb" or "nsgg" or "nsgh" or "nsgn" or "nsgt" or "nsha"
                or "nshe" or "nshf" or "nshp" or "nshw" or "nske" or "nskg" or "nskk" or "nsko" or "nslh"
                or "nsll" or "nslm" or "nsln" or "nslr" or "nslv" or "nsno" or "nsns" or "nsoc" or "nsog"
                or "nspb" or "nspd" or "nspg" or "nspp" or "nspr" or "nsqa" or "nsqe" or "nsqo" or "nsqt"
                or "nsra" or "nsrh" or "nsrv" or "nsth" or "nstl" or "nstw" or "nsty" or "nsw1" or "nsw2"
                or "nsw3" or "ntav" or "nten" or "nth0" or "nth1" or "nthl" or "nthr" or "ntkc" or "ntkf"
                or "ntkw" or "ntn2" or "ntnt" or "ntrd" or "ntrg" or "ntrs" or "ntrt" or "ntrv" or "ntt2"
                or "ntws" or "nubk" or "nubr" or "nubw" or "nvde" or "nvdg" or "nvdl" or "nvdw" or "nvil"
                or "nvk2" or "nvl2" or "nvlk" or "nvlw" or "nvul" or "nwat" or "nwe2" or "nwe3" or "nwen"
                or "nwgt" or "nwld" or "nwlg" or "nwlt" or "nwna" or "nwnr" or "nwns" or "nwrg" or "nwwd"
                or "nwwf" or "nwwg" or "nwzd" or "nzep" or "nzom" or "ogrk" or "ogru" or "ohun" or "okod"
                or "omtg" or "onzg" or "opeo" or "orai" or "ospw" or "osw1" or "osw2" or "osw3" or "otau"
                or "otbk" or "ovln" or "owar" or "ownr" or "u004" or "u006" or "u008" or "u00C" or "u00E"
                or "u00F" or "uabc" or "uabo" or "uaco" or "uaod" or "uarb" or "ubdd" or "ubdr" or "ubon"
                or "ubsp" or "ucrm" or "ucs1" or "ucs2" or "ucs3" or "ucsB" or "ucsC" or "ufro" or "ugar"
                or "ugho" or "ugol" or "ugrm" or "ugrv" or "uplg" or "ushd" or "ushp" or "uske" or "uswb"
                or "utom" or "zshv" or "zzrg" => 500,
            "Efur" or "Ekee" or "Ekgg" or "Emfr" or "Emns" or "Hamg" or "Hant" or "Hblm" or "Hgam"
                or "Hjai" or "Hkal" or "Hvsh" or "Nfir" or "Nngs" or "Odrt" or "Ofar" or "Orkn" or "Oshd"
                or "Othr" or "Uclc" or "Uktl" or "Ulic" or "Uwar" or "edtm" or "efdr" or "eshd" or "h00D"
                or "h01G" or "h03P" or "h03R" or "h04X" or "h05A" or "h05L" or "h06V" or "h06W" or "h06X"
                or "h08U" or "hdhw" or "hmpr" or "hphx" or "nadw" or "nane" or "nba2" or "nbal" or "nbdr"
                or "nbel" or "nbrg" or "nbzw" or "ncea" or "nchp" or "nchw" or "ncks" or "ndgt" or "ndqp"
                or "ndqv" or "ndrh" or "ndrm" or "ndrs" or "ndt1" or "ndt2" or "ndth" or "ndtp" or "ndtr"
                or "negf" or "negt" or "nemi" or "nerd" or "ners" or "nerw" or "net1" or "net2" or "nfa1"
                or "nfa2" or "nfac" or "nfpe" or "nfps" or "nfre" or "nfrs" or "nfsh" or "nfsp" or "nft1"
                or "nft2" or "nftt" or "ngna" or "ngrw" or "nhar" or "nhdc" or "nhfp" or "nhhr" or "nhrw"
                or "nina" or "ninm" or "nith" or "nitp" or "nitr" or "nkog" or "nlds" or "nltc" or "nltl"
                or "nmbg" or "nmdm" or "nmed" or "nmsc" or "nnht" or "npn2" or "npn5" or "nqb1" or "nqb2"
                or "nqb3" or "nqb4" or "nrdk" or "nrel" or "nrzt" or "nsat" or "nsca" or "nsel" or "nska"
                or "nskf" or "nskm" or "nslf" or "nsrn" or "nsrw" or "nssp" or "nsts" or "ntkh" or "ntks"
                or "ntrh" or "ntt1" or "ntx2" or "nw2w" or "nwiz" or "nws1" or "nwzg" or "nwzr" or "odkt"
                or "odoc" or "oshm" or "osp1" or "osp2" or "osp3" or "osp4" or "ovlj" or "tec1" or "tec2"
                or "tec3" or "uktn" or "unec" or "uobs" or "zcso" or "zhyd" or "zjug" or "zmar" or "zsmc" => 600,
            "ninc" => 625,
            "Edem" or "Edmm" or "Eevi" or "Eevm" or "Eidm" or "Eill" or "Eilm" or "Emoo" or "Etyr"
                or "Ewar" or "Ewrd" or "Hvwd" or "Nbrn" or "Usyl" or "edes" or "hdes" or "nanb" or "nanm"
                or "nfgo" or "nssn" or "nwgs" or "odes" or "owyv" or "udes" => 650,
            "edot" or "etrp" or "h014" or "hgtw" or "hsor" or "hspt" or "n02J" or "ncap" or "negm"
                or "nehy" or "nhym" or "nnsu" or "nnsw" or "nntg" or "nnwl" or "nnwq" or "nnwr" or "nsnp"
                or "ocbw" or "otrb" or "uban" or "ucry" or "uskm" => 700,
            "e000" or "e001" or "e003" or "e004" or "e006" or "e007" or "e009" or "e00A" or "e00B"
                or "e00C" or "e00J" or "earc" or "edry" or "h00U" or "h00W" or "h016" or "h01U" or "h01V"
                or "h02W" or "h02X" or "h02Y" or "h02Z" or "h030" or "h034" or "h03A" or "h03B" or "h03C"
                or "h03H" or "h03N" or "h03V" or "h04I" or "h05C" or "h05H" or "h068" or "h06A" or "h06N"
                or "h06R" or "h06S" or "h06T" or "h074" or "h07B" or "h07C" or "h098" or "h099" or "h09M"
                or "h09Q" or "h0A2" or "h0DA" or "h0DB" or "h0DC" or "hatw" or "hctw" or "hfoo" or "hrif"
                or "n000" or "n001" or "n002" or "n003" or "n004" or "n005" or "n006" or "n007" or "n008"
                or "n009" or "n00A" or "n00B" or "n00C" or "n00D" or "n00E" or "n00H" or "n00I" or "n00J"
                or "n00K" or "n00L" or "n00S" or "n00T" or "n00U" or "n00V" or "n00W" or "n00X" or "n00Y"
                or "n00Z" or "n010" or "n011" or "n012" or "n013" or "n014" or "n015" or "n019" or "n01A"
                or "n01B" or "n01C" or "n01D" or "n01E" or "n01F" or "n01G" or "n01H" or "n01I" or "n01J"
                or "n01K" or "n01L" or "n01M" or "n01N" or "n01O" or "n01P" or "n01Q" or "n01R" or "n01T"
                or "n01V" or "n01W" or "n01X" or "n01Y" or "n01Z" or "n020" or "n021" or "n022" or "n023"
                or "n024" or "n026" or "n027" or "n028" or "n029" or "n02A" or "n02B" or "n02C" or "n02D"
                or "n02G" or "n02I" or "n02K" or "n02L" or "nN01" or "nN02" or "nN03" or "nN04" or "nN05"
                or "nN06" or "nN07" or "nN08" or "nN09" or "nN0A" or "nN0B" or "nN0C" or "nN0D" or "nZW0"
                or "nZW1" or "nZW2" or "nZW3" or "nZW4" or "nZW5" or "nbt1" or "nbt2" or "nhea" or "ntka"
                or "ntkt" or "o002" or "o003" or "o004" or "o005" or "o006" or "o007" or "o008" or "o009"
                or "o00A" or "o00B" or "o00C" or "o00D" or "o00E" or "o00F" or "u000" or "u001" or "u002"
                or "u003" or "u005" or "u007" or "u009" or "u00A" or "u00B" or "u00D" or "unp1" or "unp2"
                or "z000" or "z001" or "z002" or "z003" => 800,
            "h073" => 850,
            "ebsh" or "echm" or "h01O" or "hbsh" or "nhyc" or "o000" or "ojgn" or "owtw" or "uloc"
                or "uubs" or "uzg1" or "uzg2" => 900,
            "Ecen" or "e00G" or "h006" or "h03Q" or "h061" or "h076" or "h0DD" or "hNT1" or "hZW6"
                or "otbr" => 1000,
            "ebal" or "ncat" or "ocat" or "umtw" => 1150,
            "e002" or "e005" or "hmtm" or "o001" => 1200,
            "h06Q" => 1400,
            "Ogld" => 1500,
            "n01U" => 4500,
            "h06B" => 5000,
            "h01M" or "h01W" or "h01X" or "h02A" or "h02C" or "h04K" or "h04R" or "h04T" or "h05Z"
                or "h060" or "h062" or "h06E" or "h06F" or "hNC1" => 20000,
            "h00A" or "h02N" or "h02O" or "h02R" or "h03L" or "h03O" or "h03X" or "h03Y" or "h03Z"
                or "h040" or "h041" or "h042" or "h044" or "h045" or "h046" or "h047" or "h048" or "h04A"
                or "h04C" or "h04D" or "h05I" or "h05R" or "h06J" or "h06K" or "h06U" or "h07H" or "h07I"
                or "h08P" or "hNL1" or "hPB0" or "hPB1" or "hPB2" or "hPB3" or "hPB4" or "hPB5" or "hPB6"
                or "hPB8" or "hPB9" or "hPBA" or "hPBB" or "hPBC" => 25000,
            "h04G" or "h04H" => 30000,
            "h001" or "h00I" or "h00M" or "h00N" or "h00Z" or "h010" or "h01E" or "h01F" or "h01P"
                or "h02D" or "h056" or "h05G" or "h063" or "h06C" or "h06H" or "h072" or "h0DE" or "hNB1" => 99999,
            _ => -1
        };
        return native>=0?native/30f:fallback;
    }
}
}
