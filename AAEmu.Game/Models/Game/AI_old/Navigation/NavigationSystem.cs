using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace AAEmu.Game.Models.Game.AI_old.Navigation
{
    [Serializable]
    public class NavSystem
    {
        public Dictionary<(int, string), List<Vector3>> NavigationSystem = new Dictionary<(int, string), List<Vector3>>();

        public NavSystem()
        {
            NavigationSystem = new Dictionary<(int, string), List<Vector3>>();
        }
    }

    public class NavigationSystem
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static bool ReadFromFile(string fileName)
        {
            var ns = new NavSystem();
            var fileLoaded = false;
            //g:\Games\Archeage1.2\game_0\main_world\paths\119_034\areasmission0.bai";
            //g:\Games\Archeage1.2\game_0\main_world\paths\119_027\areasmission0.bai
            //var path = @"f:\Games\AA-dedicated-server-0.5\ArcheAge\game\worlds\arche_mall_world\paths\013_018\areasmission0.bai";
            var path = fileName;

            using (var file = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                var index = 0;
                string AreaName;
                var volumeId = 0;
                var usedVolumesCount = 0u;
                var volumeAreaNameSize = 0u;
                var verticesCount = 0u;

                var nConfigurationVersion = file.ReadUInt32();

                do
                {
                    usedVolumesCount = file.ReadUInt32();
                    for (var idx = 0; idx < usedVolumesCount; ++idx)
                    {
                        // Loading boundary volumes, their ID's and names
                        volumeAreaNameSize = file.ReadUInt32();
                        AreaName = Encoding.Default.GetString(file.ReadBytes((int)volumeAreaNameSize));

                        if (volumeAreaNameSize == 0x13)
                        {
                            // неизвестный заголовок перед зоной
                            var unk1 = file.ReadUInt32(); // 0
                            var unk2 = file.ReadUInt32(); // 0
                            var unk3 = file.ReadByte();   // 2
                            var unk4 = file.ReadUInt32(); // 0
                            var unk5 = file.ReadUInt32(); // 0
                            var unk6 = file.ReadSingle(); // 0
                            var unk7 = file.ReadSingle(); // 0
                            var unk8 = file.ReadSingle(); // 0
                            var unk9 = file.ReadSingle(); // 0
                            var unk10 = file.ReadUInt32(); // 0
                            var unk11 = file.ReadByte();   // 0
                        }
                        verticesCount = file.ReadUInt32();

                        var vtx = new List<Vector3>();
                        for (var vtxIdx = 0; vtxIdx < verticesCount; ++vtxIdx)
                        {
                            vtx.Add(new Vector3(file.ReadSingle(), file.ReadSingle(), file.ReadSingle()));
                        }
                        ns.NavigationSystem.TryAdd((index, AreaName), vtx);
                    }
                    volumeId++;
                    fileLoaded = true;
                    index++;
                } while (usedVolumesCount > 0);

                // сохраним полученную информацию в файл .json
                StopProcessing(ns);
            }

            return fileLoaded;
        }

        public static void StopProcessing(NavSystem ns)
        {
            var json = JsonConvert.SerializeObject(ns, Formatting.Indented);
            Commons.IO.FileManager.SaveFile(json, string.Format("{0}areasmission0.json", Commons.IO.FileManager.AppPath));
        }

    }
}
