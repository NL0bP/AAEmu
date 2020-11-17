using System.Collections.Generic;

using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Housing;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils.DB;

using NLog;

namespace AAEmu.Game.Core.Managers.UnitManagers
{
    public class DoodadManager : Singleton<DoodadManager>
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        private Dictionary<uint, DoodadTemplate> _templates;
        private Dictionary<uint, List<DoodadFunc>> _funcs;
        private Dictionary<uint, List<DoodadFunc>> _phaseFuncs;
        private Dictionary<string, Dictionary<uint, DoodadFuncTemplate>> _funcTemplates;

        public bool Exist(uint templateId)
        {
            return _templates.ContainsKey(templateId);
        }

        public DoodadTemplate GetTemplate(uint id)
        {
            return Exist(id) ? _templates[id] : null;
        }

        public void Load()
        {
            _templates = new Dictionary<uint, DoodadTemplate>();
            _funcs = new Dictionary<uint, List<DoodadFunc>>();
            _phaseFuncs = new Dictionary<uint, List<DoodadFunc>>();
            _funcTemplates = new Dictionary<string, Dictionary<uint, DoodadFuncTemplate>>();
            foreach (var type in Helpers.GetTypesInNamespace("AAEmu.Game.Models.Game.DoodadObj.Funcs"))
            {
                if (type.BaseType == typeof(DoodadFuncTemplate))
                {
                    _funcTemplates.Add(type.Name, new Dictionary<uint, DoodadFuncTemplate>());
                }
            }
            using (var connection = SQLite.CreateConnection())
            {
                if (connection == null)
                {
                    return;
                }
                _log.Info("Loading doodad templates...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * from doodad_almighties";
                    command.Prepare();
                    using (var sqliteDataReader = command.ExecuteReader())
                    using (var reader = new SQLiteWrapperReader(sqliteDataReader))
                    {
                        while (reader.Read())
                        {
                            var template = new DoodadTemplate
                            {
                                Id = reader.GetUInt32("id"),
                                Childable = reader.GetBoolean("childable", true),
                                ClimateId = reader.GetUInt32("climate_id", 0),
                                CollideShip = reader.GetBoolean("collide_ship", true),
                                CollideVehicle = reader.GetBoolean("collide_vehicle", true),
                                //DdeleteWhenNotExistCreator = reader.GetBoolean("delete_when_not_exist_creator", true),
                                DespawnOnCollision = reader.GetBoolean("despawn_on_collision", true),
                                FactionId = reader.GetUInt32("faction_id"),
                                ForceTodTopPriority = reader.GetBoolean("force_tod_top_priority", true),
                                ForceUpAction = reader.GetBoolean("force_up_action", true),
                                GroupId = reader.GetUInt32("group_id"),
                                GrowthTime = reader.GetInt32("growth_time", 0),
                                //LoadModelFromWorld = reader.GetBoolean("load_model_from_world", true),
                                //MarkModel = reader.GetString("mark_model"),
                                MaxTime = reader.GetInt32("max_time", 0),
                                MgmtSpawn = reader.GetBoolean("mgmt_spawn", true),
                                MinTime = reader.GetInt32("min_time", 0),
                                //Model = reader.GetString("model"),
                                ModelKindId = reader.GetUInt32("model_kind_id"),
                                //Name = reader.GetString("name"),
                                NoCollision = reader.GetBoolean("no_collision", true),
                                OnceOneInteraction = reader.GetBoolean("once_one_interaction", true),
                                OnceOneMan = reader.GetBoolean("once_one_man", true),
                                Parentable = reader.GetBoolean("parentable", true),
                                //PassThroughOuterside = reader.GetBoolean("pass_through_outerside", true),
                                //PassUpdateDist = reader.GetBoolean("pass_update_dist", true),
                                Percent = reader.GetInt32("percent", 0),
                                //PlaceAreaKindId = reader.GetInt32("place_area_kind_id", 0),
                                //ResetData = reader.GetBoolean("reset_data", true),
                                RestrictZoneId = reader.IsDBNull("restrict_zone_id") ? 0 : reader.GetUInt32("restrict_zone_id"),
                                SaveIndun = reader.GetBoolean("save_indun", true),
                                //ShowMinimap = reader.GetBoolean("show_minimap", true),
                                //ShowName = reader.GetBoolean("show_name", true),
                                SimRadius = reader.GetInt32("sim_radius", 0),
                                //SpawnFxGroupId = reader.GetInt32("spawn_fx_group_id", 0),
                                //SystemDoodad = reader.GetBoolean("system_doodad", true),
                                TargetDecalSize = reader.GetFloat("target_decal_size", 0),
                                UseCreatorFaction = reader.GetBoolean("use_creator_faction", true),
                                UseTargetDecal = reader.GetBoolean("use_target_decal", true),
                                UseTargetHighlight = reader.GetBoolean("use_target_highlight", true),
                                UseTargetSilhouette = reader.GetBoolean("use_target_silhouette", true),
                                Name = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_almighties", "name", reader.GetUInt32("id"))
                            };
                            _templates.Add(template.Id, template);
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_groups";
                    command.Prepare();
                    using (var sqliteDataReader = command.ExecuteReader())
                    using (var reader = new SQLiteWrapperReader(sqliteDataReader))
                    {
                        while (reader.Read())
                        {
                            var templateId = reader.GetUInt32("doodad_almighty_id");
                            if (_templates.ContainsKey(templateId))
                            {
                                var funcGroups = new DoodadFuncGroups
                                {
                                    Id = reader.GetUInt32("id"),
                                    GroupKindId = (DoodadFuncGroups.DoodadFuncGroupKind)reader.GetUInt32("doodad_func_group_kind_id"),
                                    SoundId = reader.IsDBNull("sound_id") ? 0 : reader.GetUInt32("sound_id")
                                };
                                funcGroups.Name = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_func_groups", "name", funcGroups.Id);
                                funcGroups.PhaseMsg = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_func_groups", "phase_msg", funcGroups.Id);
                                _templates[templateId].FuncGroups.Add(funcGroups);
                            }
                        }
                    }
                }

                _log.Info("Loaded {0} doodad templates", _templates.Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_funcs";
                    command.Prepare();
                    using (var sqliteDataReader = command.ExecuteReader())
                    using (var reader = new SQLiteWrapperReader(sqliteDataReader))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFunc
                            {
                                GroupId = reader.GetUInt32("doodad_func_group_id"),
                                FuncId = reader.GetUInt32("actual_func_id"),
                                FuncType = reader.GetString("actual_func_type"),
                                NextPhase = reader.GetInt32("next_phase", -1),
                                SoundId = reader.IsDBNull("sound_id") ? 0 : reader.GetUInt32("sound_id"),
                                SkillId = reader.GetUInt32("func_skill_id", 0),
                                PermId = reader.GetUInt32("perm_id"),
                                Count = reader.GetInt32("act_count", 0)
                            };
                            // TODO next_phase = 0?
                            List<DoodadFunc> list;
                            if (_funcs.ContainsKey(func.GroupId))
                            {
                                list = _funcs[func.GroupId];
                            }
                            else
                            {
                                list = new List<DoodadFunc>();
                                _funcs.Add(func.GroupId, list);
                            }

                            list.Add(func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcs", _funcs.Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_phase_funcs";
                    command.Prepare();
                    using (var sqliteDataReader = command.ExecuteReader())
                    using (var reader = new SQLiteWrapperReader(sqliteDataReader))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFunc
                            {
                                GroupId = reader.GetUInt32("doodad_func_group_id"),
                                FuncId = reader.GetUInt32("actual_func_id"),
                                FuncType = reader.GetString("actual_func_type")
                            };
                            List<DoodadFunc> list;
                            if (_phaseFuncs.ContainsKey(func.GroupId))
                            {
                                list = _phaseFuncs[func.GroupId];
                            }
                            else
                            {
                                list = new List<DoodadFunc>();
                                _phaseFuncs.Add(func.GroupId, list);
                            }

                            list.Add(func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad phaseFuncs", _phaseFuncs.Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_animates";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncAnimate
                            {
                                Id = reader.GetUInt32("id"),
                                Name = reader.GetString("name"),
                                PlayOnce = reader.GetBoolean("play_once", true)
                            };
                            _funcTemplates["DoodadFuncAnimate"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncAnimate\"]", _funcTemplates["DoodadFuncAnimate"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_area_triggers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncAreaTrigger
                            {
                                Id = reader.GetUInt32("id"),
                                NpcId = reader.GetUInt32("npc_id", 0),
                                IsEnter = reader.GetBoolean("is_enter", true)
                            };
                            _funcTemplates["DoodadFuncAreaTrigger"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncAreaTrigger\"]", _funcTemplates["DoodadFuncAreaTrigger"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_attachments";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncAttachment
                            {
                                Id = reader.GetUInt32("id"),
                                AttachPointId = reader.GetByte("attach_point_id"),
                                Space = reader.GetInt32("space"),
                                BondKindId = reader.GetByte("bond_kind_id")
                            };
                            _funcTemplates["DoodadFuncAttachment"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncAttachment\"]", _funcTemplates["DoodadFuncAttachment"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_bindings";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBinding
                            {
                                Id = reader.GetUInt32("id"),
                                DistrictId = reader.GetUInt32("district_id")
                            };
                            _funcTemplates["DoodadFuncBinding"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBinding\"]", _funcTemplates["DoodadFuncBinding"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_bubbles";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBubble
                            {
                                Id = reader.GetUInt32("id"),
                                BubbleId = reader.GetUInt32("bubble_id")
                            };
                            _funcTemplates["DoodadFuncBubble"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBubble\"]", _funcTemplates["DoodadFuncBubble"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_buffs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBuff
                            {
                                Id = reader.GetUInt32("id"),
                                BuffId = reader.GetUInt32("buff_id"),
                                Radius = reader.GetFloat("radius"),
                                Count = reader.GetInt32("count"),
                                PermId = reader.GetUInt32("perm_id"),
                                RelationshipId = reader.GetUInt32("relationship_id")
                            };
                            _funcTemplates["DoodadFuncBuff"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBuff\"]", _funcTemplates["DoodadFuncBuff"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_butchers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncButcher
                            {
                                Id = reader.GetUInt32("id"),
                                CorpseModel = reader.GetString("corpse_model")
                            };
                            _funcTemplates["DoodadFuncButcher"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncButcher\"]", _funcTemplates["DoodadFuncButcher"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_buy_fish_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBuyFishItem
                            {
                                Id = reader.GetUInt32("id"),
                                DoodadFuncBuyFishId = reader.GetUInt32("doodad_func_buy_fish_id"),
                                ItemId = reader.GetUInt32("item_id")
                            };
                            _funcTemplates["DoodadFuncBuyFishItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBuyFishItem\"]", _funcTemplates["DoodadFuncBuyFishItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_buy_fish_models";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBuyFishModel
                            {
                                Id = reader.GetUInt32("id"),
                                Name = reader.GetString("name")
                            };
                            _funcTemplates["DoodadFuncBuyFishModel"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBuyFishModel\"]", _funcTemplates["DoodadFuncBuyFishModel"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_buy_fishes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncBuyFish
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id", 0)
                            };
                            _funcTemplates["DoodadFuncBuyFish"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncBuyFish\"]", _funcTemplates["DoodadFuncBuyFish"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_catches";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCatch
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCatch"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCatch\"]", _funcTemplates["DoodadFuncCatch"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_cereal_harvests";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCerealHarvest
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCerealHarvest"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCerealHarvest\"]", _funcTemplates["DoodadFuncCerealHarvest"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_cleanup_logic_links";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCleanupLogicLink
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCleanupLogicLink"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCleanupLogicLink\"]", _funcTemplates["DoodadFuncCleanupLogicLink"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_climate_reacts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncClimateReact
                            {
                                Id = reader.GetUInt32("id"),
                                NextPhase = reader.GetUInt32("next_phase")
                            };
                            _funcTemplates["DoodadFuncClimateReact"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncClimateReact\"]", _funcTemplates["DoodadFuncClimateReact"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_climbs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncClimb
                            {
                                Id = reader.GetUInt32("id"),
                                ClimbTypeId = reader.GetUInt32("climb_type_id"),
                                AllowHorizontalMultiHanger = reader.GetBoolean("allow_horizontal_multi_hanger", true)
                            };
                            _funcTemplates["DoodadFuncClimb"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncClimb\"]", _funcTemplates["DoodadFuncClimb"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_clout_effects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCloutEffect
                            {
                                Id = reader.GetUInt32("id"),
                                FuncCloutId = reader.GetUInt32("doodad_func_clout_id"),
                                EffectId = reader.GetUInt32("effect_id")
                            };
                            _funcTemplates["DoodadFuncCloutEffect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCloutEffect\"]", _funcTemplates["DoodadFuncCloutEffect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_clouts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncClout
                            {
                                Id = reader.GetUInt32("id"),
                                Duration = reader.GetInt32("duration"),
                                Tick = reader.GetInt32("tick"),
                                TargetRelationId = reader.GetUInt32("target_relation_id"),
                                BuffId = reader.GetUInt32("buff_id", 0),
                                ProjectileId = reader.GetUInt32("projectile_id", 0),
                                ShowToFriendlyOnly = reader.GetBoolean("show_to_friendly_only", true),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0
                                    ? reader.GetUInt32("next_phase")
                                    : 0,
                                AoeShapeId = reader.GetUInt32("aoe_shape_id"),
                                TargetBuffTagId = reader.GetUInt32("target_buff_tag_id", 0),
                                TargetNoBuffTagId = reader.GetUInt32("target_no_buff_tag_id", 0),
                                UseOriginSource = reader.GetBoolean("use_origin_source", true)
                            };
                            _funcTemplates["DoodadFuncClout"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncClout\"]", _funcTemplates["DoodadFuncClout"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_coffer_perms";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCofferPerm
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCofferPerm"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCofferPerm\"]", _funcTemplates["DoodadFuncCofferPerm"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_coffers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCoffer
                            {
                                Id = reader.GetUInt32("id"),
                                Capacity = reader.GetInt32("capacity")
                            };
                            _funcTemplates["DoodadFuncCoffer"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCoffer\"]", _funcTemplates["DoodadFuncCoffer"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_conditional_uses";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConditionalUse
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id", 0),
                                FakeSkillId = reader.GetUInt32("fake_skill_id", 0),
                                QuestId = reader.GetUInt32("quest_id", 0),
                                QuestTriggerPhase = reader.GetUInt32("quest_trigger_phase", 0),
                                ItemId = reader.GetUInt32("item_id", 0),
                                ItemTriggerPhase = reader.GetUInt32("item_trigger_phase", 0)
                            };
                            _funcTemplates["DoodadFuncConditionalUse"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConditionalUse\"]", _funcTemplates["DoodadFuncConditionalUse"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_consume_changer_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConsumeChangerItem
                            {
                                Id = reader.GetUInt32("id"),
                                DoodadFuncConsumeChangerId = reader.GetUInt32("doodad_func_consume_changer_id"),
                                ItemId = reader.GetUInt32("item_id")
                            };
                            _funcTemplates["DoodadFuncConsumeChangerItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConsumeChangerItem\"]", _funcTemplates["DoodadFuncConsumeChangerItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_consume_changer_model_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConsumeChangerModelItem
                            {
                                Id = reader.GetUInt32("id"),
                                DoodadFuncConsumeChangerModelId =
                                    reader.GetUInt32("doodad_func_consume_changer_model_id"),
                                ItemId = reader.GetUInt32("item_id")
                            };
                            _funcTemplates["DoodadFuncConsumeChangerModelItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConsumeChangerModelItem\"]", _funcTemplates["DoodadFuncConsumeChangerModelItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_consume_changer_models";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConsumeChangerModel
                            {
                                Id = reader.GetUInt32("id"),
                                Name = reader.GetString("name")
                            };
                            _funcTemplates["DoodadFuncConsumeChangerModel"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConsumeChangerModel\"]", _funcTemplates["DoodadFuncConsumeChangerModel"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_consume_changers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConsumeChanger
                            {
                                Id = reader.GetUInt32("id"),
                                SlotId = reader.GetUInt32("slot_id"),
                                Count = reader.GetInt32("count")
                            };
                            _funcTemplates["DoodadFuncConsumeChanger"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConsumeChanger\"]", _funcTemplates["DoodadFuncConsumeChanger"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_consume_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConsumeItem
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id"),
                                Count = reader.GetInt32("count")
                            };
                            _funcTemplates["DoodadFuncConsumeItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConsumeItem\"]", _funcTemplates["DoodadFuncConsumeItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_convert_fish_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConvertFishItem
                            {
                                Id = reader.GetUInt32("id"),
                                DoodadFuncConvertFishId = reader.GetUInt32("doodad_func_convert_fish_id"),
                                ItemId = reader.GetUInt32("item_id"),
                                LootPackId = reader.GetUInt32("loot_pack_id")
                            };
                            _funcTemplates["DoodadFuncConvertFishItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConvertFishItem\"]", _funcTemplates["DoodadFuncConvertFishItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_convert_fishes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncConvertFish
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncConvertFish"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncConvertFish\"]", _funcTemplates["DoodadFuncConvertFish"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_acts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftAct
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCraftAct"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftAct\"]", _funcTemplates["DoodadFuncCraftAct"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_cancels";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftCancel
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCraftCancel"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftCancel\"]", _funcTemplates["DoodadFuncCraftCancel"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_directs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftDirect
                            {
                                Id = reader.GetUInt32("id"),
                                NextPhase = reader.GetUInt32("next_phase")
                            };
                            _funcTemplates["DoodadFuncCraftDirect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftDirect\"]", _funcTemplates["DoodadFuncCraftDirect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_get_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftGetItem
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCraftGetItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftGetItem\"]", _funcTemplates["DoodadFuncCraftGetItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_infos";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftInfo
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCraftInfo"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftInfo\"]", _funcTemplates["DoodadFuncCraftInfo"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_packs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftPack
                            {
                                Id = reader.GetUInt32("id"),
                                CraftPackId = reader.GetUInt32("craft_pack_id")
                            };
                            _funcTemplates["DoodadFuncCraftPack"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftPack\"]", _funcTemplates["DoodadFuncCraftPack"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_start_crafts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftStartCraft
                            {
                                Id = reader.GetUInt32("id"),
                                DoodadFuncCraftStartId = reader.GetUInt32("doodad_func_craft_start_id"),
                                CraftId = reader.GetUInt32("craft_id")
                            };
                            _funcTemplates["DoodadFuncCraftStartCraft"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftStartCraft\"]", _funcTemplates["DoodadFuncCraftStartCraft"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_craft_starts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCraftStart
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCraftStart"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCraftStart\"]", _funcTemplates["DoodadFuncCraftStart"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_crop_harvests";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCropHarvest
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCropHarvest"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCropHarvest\"]", _funcTemplates["DoodadFuncCropHarvest"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_crystal_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCrystalCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCrystalCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCrystalCollect\"]", _funcTemplates["DoodadFuncCrystalCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_cutdownings";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCutdowning
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCutdowning"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCutdowning\"]", _funcTemplates["DoodadFuncCutdowning"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_cutdowns";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncCutdown
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncCutdown"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncCutdown\"]", _funcTemplates["DoodadFuncCutdown"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_dairy_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncDairyCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncDairyCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncDairyCollect\"]", _funcTemplates["DoodadFuncDairyCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_declare_sieges";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncDeclareSiege
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncDeclareSiege"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncDeclareSiege\"]", _funcTemplates["DoodadFuncDeclareSiege"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_digs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncDig
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncDig"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncDig\"]", _funcTemplates["DoodadFuncDig"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_dig_terrains";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncDigTerrain
                            {
                                Id = reader.GetUInt32("id"),
                                Radius = reader.GetInt32("radius"),
                                Life = reader.GetInt32("life")
                            };
                            _funcTemplates["DoodadFuncDigTerrain"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncDigTerrain\"]", _funcTemplates["DoodadFuncDigTerrain"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_dyeingredient_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncDyeingredientCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncDyeingredientCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncDyeingredientCollect\"]", _funcTemplates["DoodadFuncDyeingredientCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_enter_instances";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncEnterInstance
                            {
                                Id = reader.GetUInt32("id"),
                                ZoneId = reader.GetUInt32("zone_id"),
                                ItemId = reader.GetUInt32("item_id", 0)
                            };
                            _funcTemplates["DoodadFuncEnterInstance"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncEnterInstance\"]", _funcTemplates["DoodadFuncEnterInstance"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_enter_sys_instances";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncEnterSysInstance
                            {
                                Id = reader.GetUInt32("id"),
                                ZoneId = reader.GetUInt32("zone_id"),
                                FactionId = reader.GetUInt32("faction_id", 0)
                            };
                            _funcTemplates["DoodadFuncEnterSysInstance"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncEnterSysInstance\"]", _funcTemplates["DoodadFuncEnterSysInstance"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_evidence_item_loots";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncEvidenceItemLoot
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id"),
                                CrimeValue = reader.GetInt32("crime_value"),
                                CrimeKindId = reader.GetUInt32("crime_kind_id")
                            };
                            _funcTemplates["DoodadFuncEvidenceItemLoot"].Add(func.Id, func);
                        }
                    }
                }

                // TODO doodad_func_exchange_items( id INT, doodad_func_exchange_id INT, item_id INT, loot_pack_id INT )

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncEvidenceItemLoot\"]", _funcTemplates["DoodadFuncEvidenceItemLoot"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_exchanges";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncExchange
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncExchange"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncExchange\"]", _funcTemplates["DoodadFuncExchange"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_exit_induns";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncExitIndun
                            {
                                Id = reader.GetUInt32("id"),
                                ReturnPointId = reader.GetUInt32("return_point_id", 0)
                            };
                            _funcTemplates["DoodadFuncExitIndun"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncExitIndun\"]", _funcTemplates["DoodadFuncExitIndun"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_fake_uses";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFakeUse
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id", 0),
                                FakeSkillId = reader.GetUInt32("fake_skill_id", 0),
                                TargetParent = reader.GetBoolean("target_parent", true)
                            };
                            _funcTemplates["DoodadFuncFakeUse"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFakeUse\"]", _funcTemplates["DoodadFuncFakeUse"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_feeds";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFeed
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id"),
                                Count = reader.GetInt32("count")
                            };
                            _funcTemplates["DoodadFuncFeed"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFeed\"]", _funcTemplates["DoodadFuncFeed"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_fiber_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFiberCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncFiberCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFiberCollect\"]", _funcTemplates["DoodadFuncFiberCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_finals";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFinal
                            {
                                Id = reader.GetUInt32("id"),
                                After = reader.GetInt32("after", 0),
                                Respawn = reader.GetBoolean("respawn", true),
                                MinTime = reader.GetInt32("min_time", 0),
                                MaxTime = reader.GetInt32("max_time", 0),
                                ShowTip = reader.GetBoolean("show_tip", true),
                                ShowEndTime = reader.GetBoolean("show_end_time", true)
                            };
                            func.Tip = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_func_finals", "tip", func.Id);

                            _funcTemplates["DoodadFuncFinal"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFinal\"]", _funcTemplates["DoodadFuncFinal"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_fish_schools";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFishSchool
                            {
                                Id = reader.GetUInt32("id"),
                                NpcSpawnerId = reader.GetUInt32("npc_spawner_id")
                            };
                            _funcTemplates["DoodadFuncFishSchool"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFishSchool\"]", _funcTemplates["DoodadFuncFishSchool"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_fruit_picks";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncFruitPick
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncFruitPick"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncFruitPick\"]", _funcTemplates["DoodadFuncFruitPick"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_gass_extracts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncGassExtract
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncGassExtract"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncGassExtract\"]", _funcTemplates["DoodadFuncGassExtract"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_growths";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncGrowth
                            {
                                Id = reader.GetUInt32("id"),
                                Delay = reader.GetInt32("delay"),
                                StartScale = reader.GetInt32("start_scale"),
                                EndScale = reader.GetInt32("end_scale"),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0
                            };
                            _funcTemplates["DoodadFuncGrowth"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncGrowth\"]", _funcTemplates["DoodadFuncGrowth"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_harvests";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncHarvest
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncHarvest"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncHarvest\"]", _funcTemplates["DoodadFuncHarvest"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_house_farms";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncHouseFarm
                            {
                                Id = reader.GetUInt32("id"),
                                ItemCategoryId = reader.GetUInt32("item_category_id")
                            };
                            _funcTemplates["DoodadFuncHouseFarm"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncHouseFarm\"]", _funcTemplates["DoodadFuncHouseFarm"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_housing_areas";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncHousingArea
                            {
                                Id = reader.GetUInt32("id"),
                                FactionId = reader.GetUInt32("faction_id"),
                                Radius = reader.GetInt32("radius")
                            };
                            _funcTemplates["DoodadFuncHousingArea"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncHousingArea\"]", _funcTemplates["DoodadFuncHousingArea"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_hungers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncHunger
                            {
                                Id = (uint)reader.GetInt32("id"),
                                HungryTerm = reader.GetInt32("hungry_term"),
                                FullStep = reader.GetInt32("full_step"),
                                PhaseChangeLimit = reader.GetInt32("phase_change_limit"),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0
                            };
                            _funcTemplates["DoodadFuncHunger"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncHunger\"]", _funcTemplates["DoodadFuncHunger"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_insert_counters";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncInsertCounter
                            {
                                Id = reader.GetUInt32("id"),
                                Count = reader.GetInt32("count"),
                                ItemId = reader.GetUInt32("item_id"),
                                ItemCount = reader.GetInt32("item_count")
                            };
                            _funcTemplates["DoodadFuncInsertCounter"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncInsertCounter\"]", _funcTemplates["DoodadFuncInsertCounter"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_logics";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncLogic
                            {
                                Id = reader.GetUInt32("id"),
                                OperationId = reader.GetUInt32("operation_id"),
                                DelayId = reader.GetUInt32("delay_id")
                            };
                            _funcTemplates["DoodadFuncLogic"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncLogic\"]", _funcTemplates["DoodadFuncLogic"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_logic_family_providers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncLogicFamilyProvider
                            {
                                Id = reader.GetUInt32("id"),
                                FamilyId = reader.GetUInt32("family_id")
                            };
                            _funcTemplates["DoodadFuncLogicFamilyProvider"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncLogicFamilyProvider\"]", _funcTemplates["DoodadFuncLogicFamilyProvider"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_logic_family_subscribers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncLogicFamilySubscriber
                            {
                                Id = reader.GetUInt32("id"),
                                FamilyId = reader.GetUInt32("family_id")
                            };
                            _funcTemplates["DoodadFuncLogicFamilySubscriber"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncLogicFamilySubscriber\"]", _funcTemplates["DoodadFuncLogicFamilySubscriber"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_loot_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncLootItem
                            {
                                Id = reader.GetUInt32("id"),
                                WorldInteractionId = reader.GetUInt32("wi_id"),
                                ItemId = reader.GetUInt32("item_id"),
                                CountMin = reader.GetInt32("count_min"),
                                CountMax = reader.GetInt32("count_max"),
                                Percent = reader.GetInt32("percent"),
                                RemainTime = reader.GetInt32("remain_time"),
                                GroupId = reader.GetUInt32("group_id")
                            };
                            _funcTemplates["DoodadFuncLootItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncLootItem\"]", _funcTemplates["DoodadFuncLootItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_loot_packs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncLootPack
                            {
                                Id = reader.GetUInt32("id"),
                                LootPackId = reader.GetUInt32("loot_pack_id")
                            };
                            _funcTemplates["DoodadFuncLootPack"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncLootPack\"]", _funcTemplates["DoodadFuncLootPack"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_machine_parts_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncMachinePartsCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncMachinePartsCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncMachinePartsCollect\"]", _funcTemplates["DoodadFuncMachinePartsCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_medicalingredient_mines";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncMedicalingredientMine
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncMedicalingredientMine"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncMedicalingredientMine\"]", _funcTemplates["DoodadFuncMedicalingredientMine"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_mows";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncMow
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncMow"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncMow\"]", _funcTemplates["DoodadFuncMow"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_mark_pos_to_maps";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviMarkPosToMap
                            {
                                Id = reader.GetUInt32("id"),
                                X = reader.GetInt32("x"),
                                Y = reader.GetInt32("y"),
                                Z = reader.GetInt32("z")
                            };
                            _funcTemplates["DoodadFuncNaviMarkPosToMap"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviMarkPosToMap\"]", _funcTemplates["DoodadFuncNaviMarkPosToMap"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_namings";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviNaming
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncNaviNaming"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviNaming\"]", _funcTemplates["DoodadFuncNaviNaming"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_open_bounties";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviOpenBounty
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncNaviOpenBounty"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviOpenBounty\"]", _funcTemplates["DoodadFuncNaviOpenBounty"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_open_mailboxes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviOpenMailbox
                            {
                                Id = reader.GetUInt32("id"),
                                Duration = reader.GetInt32("duration")
                            };
                            _funcTemplates["DoodadFuncNaviOpenMailbox"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviOpenMailbox\"]", _funcTemplates["DoodadFuncNaviOpenMailbox"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_open_portals";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviOpenPortal
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncNaviOpenPortal"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviOpenPortal\"]", _funcTemplates["DoodadFuncNaviOpenPortal"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_remove_timers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviRemoveTimer
                            {
                                Id = reader.GetUInt32("id"),
                                After = reader.GetInt32("after")
                            };
                            _funcTemplates["DoodadFuncNaviRemoveTimer"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviRemoveTimer\"]", _funcTemplates["DoodadFuncNaviRemoveTimer"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_removes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviRemove
                            {
                                Id = reader.GetUInt32("id"),
                                ReqLaborPower = reader.GetInt32("req_lp")
                            };
                            _funcTemplates["DoodadFuncNaviRemove"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviRemove\"]", _funcTemplates["DoodadFuncNaviRemove"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_navi_teleports";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncNaviTeleport
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncNaviTeleport"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncNaviTeleport\"]", _funcTemplates["DoodadFuncNaviTeleport"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_open_farm_infos";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncOpenFarmInfo
                            {
                                Id = reader.GetUInt32("id"),
                                FarmId = reader.GetUInt32("farm_id")
                            };
                            _funcTemplates["DoodadFuncOpenFarmInfo"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncOpenFarmInfo\"]", _funcTemplates["DoodadFuncOpenFarmInfo"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_open_papers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncOpenPaper
                            {
                                Id = reader.GetUInt32("id"),
                                BookPageId = reader.GetUInt32("book_page_id", 0),
                                BookId = reader.GetUInt32("book_id", 0)
                            };
                            _funcTemplates["DoodadFuncOpenPaper"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncOpenPaper\"]", _funcTemplates["DoodadFuncOpenPaper"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_ore_mines";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncOreMine
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncOreMine"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncOreMine\"]", _funcTemplates["DoodadFuncOreMine"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_parent_infos";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncParentInfo
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncParentInfo"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncParentInfo\"]", _funcTemplates["DoodadFuncParentInfo"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_parrots";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncParrot
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncParrot"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncParrot\"]", _funcTemplates["DoodadFuncParrot"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_plant_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPlantCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncPlantCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPlantCollect\"]", _funcTemplates["DoodadFuncPlantCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_play_flow_graphs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPlayFlowGraph
                            {
                                Id = reader.GetUInt32("id"),
                                EventOnPhaseChangeId = reader.GetUInt32("event_on_phase_change_id"),
                                EventOnVisibleId = reader.GetUInt32("event_on_visible_id")
                            };
                            _funcTemplates["DoodadFuncPlayFlowGraph"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPlayFlowGraph\"]", _funcTemplates["DoodadFuncPlayFlowGraph"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_pulse_triggers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPulseTrigger
                            {
                                Id = reader.GetUInt32("id"),
                                Flag = reader.GetBoolean("flag", true),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0
                            };
                            _funcTemplates["DoodadFuncPulseTrigger"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPulseTrigger\"]", _funcTemplates["DoodadFuncPulseTrigger"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_pulses";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPulse
                            {
                                Id = reader.GetUInt32("id"),
                                Flag = reader.GetBoolean("flag", true)
                            };
                            _funcTemplates["DoodadFuncPulse"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPulse\"]", _funcTemplates["DoodadFuncPulse"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_purchases";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPurchase
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id", 0),
                                Count = reader.GetInt32("count"),
                                CoinItemId = reader.GetUInt32("coin_item_id", 0),
                                CoinCount = reader.GetInt32("coin_count", 0),
                                CurrencyId = reader.GetUInt32("currency_id")
                            };
                            _funcTemplates["DoodadFuncPurchase"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPurchase\"]", _funcTemplates["DoodadFuncPurchase"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_puzzle_ins";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPuzzleIn
                            {
                                Id = reader.GetUInt32("id"),
                                GroupId = reader.GetUInt32("group_id"),
                                Ratio = reader.GetFloat("ratio")
                            };
                            _funcTemplates["DoodadFuncPuzzleIn"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPuzzleIn\"]", _funcTemplates["DoodadFuncPuzzleIn"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_puzzle_outs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPuzzleOut
                            {
                                Id = reader.GetUInt32("id"),
                                GroupId = reader.GetUInt32("group_id"),
                                Ratio = reader.GetFloat("ratio"),
                                Anim = reader.GetString("anim"),
                                ProjectileId = reader.GetUInt32("projectile_id", 0),
                                ProjectileDelay = reader.GetInt32("projectile_delay"),
                                LootPackId = reader.GetUInt32("loot_pack_id", 0),
                                Delay = reader.GetInt32("delay"),
                                NextPhase = reader.GetUInt32("next_phase")
                            };
                            _funcTemplates["DoodadFuncPuzzleOut"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPuzzleOut\"]", _funcTemplates["DoodadFuncPuzzleOut"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_puzzle_rolls";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncPuzzleRoll
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id"),
                                Count = reader.GetInt32("count")
                            };
                            _funcTemplates["DoodadFuncPuzzleRoll"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncPuzzleRoll\"]", _funcTemplates["DoodadFuncPuzzleRoll"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_quests";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncQuest
                            {
                                Id = reader.GetUInt32("id"),
                                QuestKindId = reader.GetUInt32("quest_kind_id"),
                                QuestId = reader.GetUInt32("quest_id")
                            };
                            _funcTemplates["DoodadFuncQuest"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncQuest\"]", _funcTemplates["DoodadFuncQuest"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_ratio_changes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRatioChange
                            {
                                Id = reader.GetUInt32("id"),
                                Ratio = reader.GetInt32("ratio"),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0
                            };
                            _funcTemplates["DoodadFuncRatioChange"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRatioChange\"]", _funcTemplates["DoodadFuncRatioChange"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_ratio_respawns";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRatioRespawn
                            {
                                Id = reader.GetUInt32("id"),
                                Ratio = reader.GetInt32("ratio"),
                                SpawnDoodadId = reader.GetUInt32("spawn_doodad_id")
                            };
                            _funcTemplates["DoodadFuncRatioRespawn"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRatioRespawn\"]", _funcTemplates["DoodadFuncRatioRespawn"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_recover_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRecoverItem
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncRecoverItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRecoverItem\"]", _funcTemplates["DoodadFuncRecoverItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_remove_instances";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRemoveInstance
                            {
                                Id = reader.GetUInt32("id"),
                                ZoneId = reader.GetUInt32("zone_id")
                            };
                            _funcTemplates["DoodadFuncRemoveInstance"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRemoveInstance\"]", _funcTemplates["DoodadFuncRemoveInstance"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_remove_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRemoveItem
                            {
                                Id = reader.GetUInt32("id"),
                                ItemId = reader.GetUInt32("item_id"),
                                Count = reader.GetInt32("count")
                            };
                            _funcTemplates["DoodadFuncRemoveItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRemoveItem\"]", _funcTemplates["DoodadFuncRemoveItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_renew_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRenewItem
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id")
                            };
                            _funcTemplates["DoodadFuncRenewItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRenewItem\"]", _funcTemplates["DoodadFuncRenewItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_require_items";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRequireItem
                            {
                                Id = reader.GetUInt32("id"),
                                WorldInteractionId = reader.GetUInt32("wi_id"),
                                ItemId = reader.GetUInt32("item_id")
                            };
                            _funcTemplates["DoodadFuncRequireItem"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRequireItem\"]", _funcTemplates["DoodadFuncRequireItem"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_require_quests";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRequireQuest
                            {
                                Id = reader.GetUInt32("id"),
                                WorldInteractionId = reader.GetUInt32("wi_id"),
                                QuestId = reader.GetUInt32("quest_id")
                            };
                            _funcTemplates["DoodadFuncRequireQuest"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRequireQuest\"]", _funcTemplates["DoodadFuncRequireQuest"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_respawns";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRespawn
                            {
                                Id = reader.GetUInt32("id"),
                                MinTime = reader.GetInt32("min_time"),
                                MaxTime = reader.GetInt32("max_time")
                            };
                            _funcTemplates["DoodadFuncRespawn"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRespawn\"]", _funcTemplates["DoodadFuncRespawn"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_rock_mines";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncRockMine
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncRockMine"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncRockMine\"]", _funcTemplates["DoodadFuncRockMine"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_seed_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSeedCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncSeedCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSeedCollect\"]", _funcTemplates["DoodadFuncSeedCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_shears";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncShear
                            {
                                Id = reader.GetUInt32("id"),
                                ShearTypeId = reader.GetUInt32("shear_type_id"),
                                ShearTerm = reader.GetInt32("shear_term")
                            };
                            _funcTemplates["DoodadFuncShear"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncShear\"]", _funcTemplates["DoodadFuncShear"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_siege_periods";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSiegePeriod
                            {
                                Id = reader.GetUInt32("id"),
                                SiegePeriodId = reader.GetUInt32("siege_period_id"),
                                NextPhase = reader.GetUInt32("next_phase"),
                                Defense = reader.GetBoolean("defense", true)
                            };
                            _funcTemplates["DoodadFuncSiegePeriod"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSiegePeriod\"]", _funcTemplates["DoodadFuncSiegePeriod"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_signs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSign
                            {
                                Id = reader.GetUInt32("id"),
                                PickNum = reader.GetInt32("pick_num")
                            };
                            func.Name = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_func_signs", "name", func.Id);
                            _funcTemplates["DoodadFuncSign"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSign\"]", _funcTemplates["DoodadFuncSign"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_skill_hits";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSkillHit
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id")
                            };
                            _funcTemplates["DoodadFuncSkillHit"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSkillHit\"]", _funcTemplates["DoodadFuncSkillHit"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_skin_offs";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSkinOff
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncSkinOff"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSkinOff\"]", _funcTemplates["DoodadFuncSkinOff"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_soil_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSoilCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncSoilCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSoilCollect\"]", _funcTemplates["DoodadFuncSoilCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_spawn_gimmicks";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSpawnGimmick
                            {
                                Id = reader.GetUInt32("id"),
                                GimmickId = reader.GetUInt32("gimmick_id"),
                                FactionId = reader.GetUInt32("faction_id"),
                                Scale = reader.GetFloat("scale"),
                                OffsetX = reader.GetFloat("offset_x"),
                                OffsetY = reader.GetFloat("offset_y"),
                                OffsetZ = reader.GetFloat("offset_z"),
                                VelocityX = reader.GetFloat("velocity_x"),
                                VelocityY = reader.GetFloat("velocity_y"),
                                VelocityZ = reader.GetFloat("velocity_z"),
                                AngleX = reader.GetFloat("angle_x"),
                                AngleY = reader.GetFloat("angle_y"),
                                AngleZ = reader.GetFloat("angle_z"),
                                NextPhase = reader.GetUInt32("next_phase")
                            };
                            _funcTemplates["DoodadFuncSpawnGimmick"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSpawnGimmick\"]", _funcTemplates["DoodadFuncSpawnGimmick"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_spawn_mgmts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSpawnMgmt
                            {
                                Id = reader.GetUInt32("id"),
                                GroupId = reader.GetUInt32("group_id"),
                                Spawn = reader.GetBoolean("spawn", true),
                                ZoneId = reader.GetUInt32("zone_id")
                            };
                            _funcTemplates["DoodadFuncSpawnMgmt"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSpawnMgmt\"]", _funcTemplates["DoodadFuncSpawnMgmt"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_spice_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncSpiceCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncSpiceCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncSpiceCollect\"]", _funcTemplates["DoodadFuncSpiceCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_stamp_makers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncStampMaker
                            {
                                Id = reader.GetUInt32("id"),
                                ConsumeMoney = reader.GetInt32("consume_money"),
                                ItemId = reader.GetUInt32("item_id"),
                                ConsumeItemId = reader.GetUInt32("consume_item_id"),
                                ConsumeCount = reader.GetInt32("consume_count")
                            };
                            _funcTemplates["DoodadFuncStampMaker"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncStampMaker\"]", _funcTemplates["DoodadFuncStampMaker"].Count);

                // TODO 1.2                
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_store_uis";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncStoreUi
                            {
                                Id = reader.GetUInt32("id"),
                                MerchantPackId = reader.GetUInt32("merchant_pack_id")
                            };
                            _funcTemplates["DoodadFuncStoreUi"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncStoreUi\"]", _funcTemplates["DoodadFuncStoreUi"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_timers";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncTimer
                            {
                                Id = reader.GetUInt32("id"),
                                Delay = reader.GetInt32("delay"),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0,
                                KeepRequester = reader.GetBoolean("keep_requester", true),
                                ShowTip = reader.GetBoolean("show_tip", true),
                                ShowEndTime = reader.GetBoolean("show_end_time", true),
                            };
                            func.Tip = LocalizationManager.Instance.GetEnglishLocalizedText("doodad_func_timers", "tip", func.Id);
                            _funcTemplates["DoodadFuncTimer"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncTimer\"]", _funcTemplates["DoodadFuncTimer"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_tods";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncTod
                            {
                                Id = reader.GetUInt32("id"),
                                Tod = reader.GetInt32("tod"),
                                NextPhase = reader.GetInt32("next_phase", -1) >= 0 ? reader.GetUInt32("next_phase") : 0
                            };
                            _funcTemplates["DoodadFuncTod"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncTod\"]", _funcTemplates["DoodadFuncTod"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_tree_byproducts_collects";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncTreeByProductsCollect
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncTreeByProductsCollect"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncTreeByProductsCollect\"]", _funcTemplates["DoodadFuncTreeByProductsCollect"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_ucc_imprints";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncUccImprint
                            {
                                Id = reader.GetUInt32("id")
                            };
                            _funcTemplates["DoodadFuncUccImprint"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncUccImprint\"]", _funcTemplates["DoodadFuncUccImprint"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_uses";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncUse
                            {
                                Id = reader.GetUInt32("id"),
                                SkillId = reader.GetUInt32("skill_id", 0)
                            };
                            _funcTemplates["DoodadFuncUse"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncUse\"]", _funcTemplates["DoodadFuncUse"].Count);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_water_volumes";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncWaterVolume
                            {
                                Id = reader.GetUInt32("id"),
                                LevelChange = reader.GetFloat("levelChange"),
                                Duration = reader.GetFloat("duration")
                            };
                            _funcTemplates["DoodadFuncWaterVolume"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncWaterVolume\"]", _funcTemplates["DoodadFuncWaterVolume"].Count);

                // TODO 1.2                
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM doodad_func_zone_reacts";
                    command.Prepare();
                    using (var reader = new SQLiteWrapperReader(command.ExecuteReader()))
                    {
                        while (reader.Read())
                        {
                            var func = new DoodadFuncZoneReact
                            {
                                Id = reader.GetUInt32("id"),
                                ZoneGroupId = reader.GetUInt32("zone_group_id"),
                                NextPhase = reader.GetUInt32("next_phase")
                            };

                            _funcTemplates["DoodadFuncZoneReact"].Add(func.Id, func);
                        }
                    }
                }

                _log.Info("Loaded {0} doodad funcTemplates[\"DoodadFuncZoneReact\"]", _funcTemplates["DoodadFuncZoneReact"].Count);
            }
        }

        public Doodad Create(uint bcId, uint id, Unit unit = null)
        {
            if (!_templates.ContainsKey(id))
            {
                return null;
            }

            var template = _templates[id];
            var doodad = new Doodad
            {
                ObjId = bcId > 0 ? bcId : ObjectIdManager.Instance.GetNextId(),
                TemplateId = template.Id,
                Template = template,
                OwnerObjId = unit?.ObjId ?? 0,
                OwnerId = 0
            };

            if (unit is Character character)
            {
                doodad.OwnerId = character.Id;
                doodad.OwnerType = DoodadOwnerType.Character;
            }

            if (unit is House house)
            {
                doodad.OwnerObjId = 0;
                doodad.ParentObjId = house.ObjId;
                doodad.OwnerId = house.OwnerId;
                doodad.OwnerType = DoodadOwnerType.Housing;
                doodad.DbHouseId = house.Id;
            }

            doodad.FuncGroupId = doodad.GetGroupId(); // TODO look, using doodadFuncId
            return doodad;
        }

        public DoodadFunc GetFunc(uint funcGroupId, uint skillId)
        {
            if (!_funcs.ContainsKey(funcGroupId))
            {
                return null;
            }

            foreach (var func in _funcs[funcGroupId])
            {
                if (func.SkillId == skillId)
                {
                    return func;
                }
            }

            foreach (var func in _funcs[funcGroupId])
            {
                if (func.SkillId == 0)
                {
                    return func;
                }
            }

            return null;
        }

        public DoodadFunc[] GetPhaseFunc(uint funcGroupId)
        {
            if (_phaseFuncs.ContainsKey(funcGroupId))
            {
                return _phaseFuncs[funcGroupId].ToArray();
            }

            return new DoodadFunc[0];
        }

        public DoodadFuncTemplate GetFuncTemplate(uint funcId, string funcType)
        {
            if (!_funcTemplates.ContainsKey(funcType))
            {
                return null;
            }

            var funcs = _funcTemplates[funcType];
            if (funcs.ContainsKey(funcId))
            {
                return funcs[funcId];
            }

            return null;
        }
    }
}
