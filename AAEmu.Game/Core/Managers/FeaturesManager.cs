using System;
using System.Collections.Generic;
using System.IO;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Game.Models.Game.Features;
using AAEmu.Game.Models.Json;

using NLog;

namespace AAEmu.Game.Core.Managers
{
    public class FeaturesManager : Singleton<FeaturesManager>
    {
        public static FeatureSet Fsets;
        private static Logger _log = LogManager.GetCurrentClassLogger();

        public void Initialize()
        {
            _log.Info("Initializing Features from json...");

            Load();

            var featsOn = string.Empty;
            foreach (var fObj in Enum.GetValues(typeof(Feature)))
            {
                var f = (Feature)fObj;
                if (FeaturesManager.Fsets.Check(f))
                    featsOn += f.ToString() + "  ";
            }
            _log.Info("Enabled Features: {0}", featsOn);

            return;
            //_log.Info("Initializing Features ...");
            //Fsets = new FeatureSet();

            //Fsets.PlayerLevelLimit = 55;
            //Fsets.MateLevelLimit = 50;

            //// Allow House sales
            //Fsets.Set(Feature.houseSale, true);

            //// Disables Auction Button
            //// Fsets.Set(Feature.hudAuctionButton, false);

            //// Enable the Nations UI menu
            //Fsets.Set(Feature.nations, true);

            //// Enables family invites
            //Fsets.Set(Feature.allowFamilyChanges, true);

            //// Disables Dwarf/Warborn character creation (0.5 only)
            //Fsets.Set(Feature.dwarfWarborn, false);

            //// Debug convenience flags, disables most of the sensitive operation stuff to do easier testing
            //Fsets.Set(Feature.sensitiveOpeartion, false);
            //Fsets.Set(Feature.secondpass, false);
            //Fsets.Set(Feature.ingameshopSecondpass, false);
            //Fsets.Set(Feature.itemSecure, false);

            //// Use gold instead of tax certificates to pay house tax
            //// Fsets.Set(Feature.taxItem, false); 

            //// Enable the Custom UI (Addons) button and menu
            //Fsets.Set(Feature.customUiButton, true);

            //// The following flags are set in our default, but have unknown behaviour. Disabling them seems to have no impact on gameplay
            ///*
            //Fsets.Set(Feature.flag_2_0, false);
            //Fsets.Set(Feature.flag_2_1, false);
            //Fsets.Set(Feature.flag_2_2, false);
            //Fsets.Set(Feature.flag_2_3, false);

            //Fsets.Set(Feature.flag_3_0, false);
            //Fsets.Set(Feature.flag_3_1, false);
            //Fsets.Set(Feature.flag_3_2, false);
            //Fsets.Set(Feature.flag_3_3, false);

            //Fsets.Set(Feature.flag_4_0, false);
            //Fsets.Set(Feature.flag_4_3, false);
            //Fsets.Set(Feature.flag_4_5, false);

            //Fsets.Set(Feature.flag_6_1, false);
            //*/


            //var featsOn = string.Empty;
            //foreach (var fObj in Enum.GetValues(typeof(Feature)))
            //{
            //    var f = (Feature)fObj;
            //    if (FeaturesManager.Fsets.Check(f))
            //        featsOn += f.ToString() + "  ";
            //}
            //_log.Info("Enabled Features: {0}",featsOn);
        }

        private void Load()
        {
            _log.Info("Loading FSets...");

            var jsonFileName = Path.Combine(FileManager.AppPath, "Data", "fsets.json");

            if (!File.Exists(jsonFileName))
            {
                _log.Info($"fsets.json  is missing...");
            }
            else
            {
                var contents = FileManager.GetFileContents(jsonFileName);

                if (string.IsNullOrWhiteSpace(contents))
                    _log.Warn($"File {jsonFileName} is empty.");
                else
                {
                    if (JsonHelper.TryDeserializeObject(contents, out List<JsonFSets> JsonFSets, out _))
                    {
                        Fsets = new FeatureSet();

                        foreach (var jsonFSets in JsonFSets)
                        {
                            Fsets.Set(jsonFSets.Feature, jsonFSets.Enabled);
                        }
                    }
                    else
                        throw new Exception($"FeaturesManager: Parse {jsonFileName} file");
                }
            }

            _log.Info("Loaded FSets...");
        }

    }
}
