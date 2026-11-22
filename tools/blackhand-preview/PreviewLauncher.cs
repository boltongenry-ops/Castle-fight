using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using CastleFightLauncher;

static class PreviewLauncher {
    const string Tag = "0.43.0-blackhand";
    const string BaseVersion = "0.42.2";

    [STAThread]
    public static int Main(string[] args) {
        bool verify = args.Length==1 && args[0]=="--verify";
        try {
            var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CastleFight");
            string parent = Path.Combine(home, "Preview", Tag);
            string game = Path.Combine(parent, "Game");
            Directory.CreateDirectory(parent);
            using (var gate = new FileStream(Path.Combine(parent, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            using (var manifestStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("baseline"))
            using (var reader = new StreamReader(manifestStream))
            using (var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload"))
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read)) {
                var target = new UpdateCore(game);
                var baseline = new UpdateCore(Path.Combine(home, "Game"));
                var version = baseline.Parse(reader.ReadToEnd());
                if(version.version!=BaseVersion)throw new InvalidDataException("Неверная базовая версия.");
                var patch = ReadPatch(archive,target);
                string stamp = Path.Combine(game, ".preview");
                bool installed = File.Exists(stamp) && File.ReadAllText(stamp)==Tag &&
                    File.Exists(target.Target(version.windows.entryPoint));
                if(!installed) {
                    if(!File.Exists(baseline.Target(version.windows.entryPoint)))
                        throw new InvalidOperationException("Сначала установите Castle Fight 0.42.2 через обычный лаунчер.");
                    string stage = Path.Combine(parent,"build-"+Guid.NewGuid().ToString("N"));
                    var prepared = new UpdateCore(stage);
                    Directory.CreateDirectory(stage);
                    try {
                        foreach(var row in version.windows.files) {
                            if(patch.ContainsKey(row.path))continue;
                            string source = baseline.Target(row.path);
                            string destination = prepared.Target(row.path);
                            if(!File.Exists(source)||new FileInfo(source).Length!=row.size)
                                throw new InvalidDataException("В базовой установке отсутствует файл "+row.path+". Обновите её обычным лаунчером.");
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            File.Copy(source,destination);
                            if(UpdateCore.Hash(destination)!=row.sha256)
                                throw new InvalidDataException("Отличается файл "+row.path+". Установите через лаунчер версию "+BaseVersion+".");
                        }
                        foreach(var item in patch) {
                            string destination=prepared.Target(item.Key);
                            Directory.CreateDirectory(Path.GetDirectoryName(destination));
                            item.Value.ExtractToFile(destination);
                        }
                        File.WriteAllText(Path.Combine(stage,".preview"),Tag);
                        Directory.Move(stage,game);
                    } catch {
                        if(Directory.Exists(stage))Directory.Delete(stage,true);
                        throw;
                    }
                }
                if(verify) {
                    foreach(var item in patch) {
                        string file=target.Target(item.Key);
                        using(var data=item.Value.Open())
                        using(var expected=System.Security.Cryptography.SHA256.Create())
                            if(BitConverter.ToString(expected.ComputeHash(data)).Replace("-","").ToLowerInvariant()!=UpdateCore.Hash(file))
                                throw new InvalidDataException("Проверка файла "+item.Key);
                    }
                    return 0;
                }
                var launch = new ProcessStartInfo(target.Target(version.windows.entryPoint)) {
                    WorkingDirectory=game,
                    Arguments="--pc-settings-file=\""+Path.Combine(parent,"settings.json")+"\" --pc-profile-file=\""+
                        Path.Combine(parent,"profile.json")+"\""
                };
                Process.Start(launch);
            }
            return 0;
        } catch(Exception error) {
            if(verify)Console.Error.WriteLine(error);
            else MessageBox.Show(error.Message,"Castle Fight · Чёрная Рука",MessageBoxButtons.OK,MessageBoxIcon.Error);
            return 1;
        }
    }

    static Dictionary<string,ZipArchiveEntry> ReadPatch(ZipArchive archive,UpdateCore target) {
        var files=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in archive.Entries) {
            if(file.FullName!="CastleFightMap_Data/Managed/Assembly-CSharp.dll" &&
               !file.FullName.StartsWith("CastleFightMap_Data/StreamingAssets/Frontend/BlackHand/",StringComparison.Ordinal))
                throw new InvalidDataException("Файл вне набора Чёрной Руки.");
            target.Target(file.FullName);
            files.Add(file.FullName,file);
        }
        return files;
    }
}
