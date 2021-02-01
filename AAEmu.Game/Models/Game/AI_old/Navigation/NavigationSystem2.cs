//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Numerics;
//using NLog;

//namespace AAEmu.Game.Models.Game.AI.Navigation
//{
//    enum eBAINavigationFileVersion : ushort
//    {
//        CURRENT,
//        FIRST_COMPATIBLE
//    }

//    public class NavigationSystem
//    {
//        private static Logger _log = LogManager.GetCurrentClassLogger();

//        public static NavigationSystem CreateInstance()
//        {
//            return new NavigationSystem();
//        }

//        public bool ReadFromFile(string fileName, bool bAfterExporting)
//        {
//            //MEMSTAT_CONTEXT(EMemStatContextType.Other, "Navigation Meshes (Read File)");

//            var fileLoaded = false;
//            int m_configurationVersion = 0;

//            //m_pEditorBackgroundUpdate.Pause(true);
//            //m_volumesManager.ClearLoadedAreas();
//            //CCryFile file = new CCryFile();

//            // чтение из файла
//            var path = @"C:\SomeDir2";
//            using (var file = new BinaryReader(File.Open(path, FileMode.Open)))
//            {
//                var fileVersionCompatible = true;

//                var nFileVersion = file.ReadUInt16();

//                //Verify version of exported file in first place
//                if (nFileVersion < (ushort)eBAINavigationFileVersion.FIRST_COMPATIBLE)
//                {
//                    _log.Warn("Wrong BAI file version (found %d expected at least %d)!! Regenerate Navigation data in the editor.", nFileVersion, eBAINavigationFileVersion.FIRST_COMPATIBLE);
//                    fileVersionCompatible = false;
//                }
//                else
//                {
//                    uint nConfigurationVersion = file.ReadUInt32();

//                    //if (nConfigurationVersion != m_configurationVersion)
//                    //{
//                    //    _log.Warn("Navigation.xml config version mismatch (found %d expected %d)!! Regenerate Navigation data in the editor.", nConfigurationVersion, m_configurationVersion);

//                    //    // In the launcher we still read the navigation data even if the configuration file
//                    //    // contains different version than the exported one
//                    //    if (gEnv.IsEditor())
//                    //    {
//                    //        fileVersionCompatible = false;
//                    //    }
//                    //}
//                    //else
//                    //{
//                    //    uint useGUID = file.ReadUInt32();
//                    //    if (useGUID != BAI_NAVIGATION_GUID_FLAG)
//                    //    {
//                    //        _log.Warn("Navigation GUID config mismatch (found %d expected %d)!! Regenerate Navigation data in the editor.", useGUID, BAI_NAVIGATION_GUID_FLAG);
//                    //        fileVersionCompatible = false;
//                    //    }
//                    //}
//                }

//                if (fileVersionCompatible)
//                {
//                    {
//                        // Loading boundary volumes, their ID's and names
//                        var volumeVerticesBuffer = new List<Vector3>();
//                        var volumeAreaNameBuffer = new List<char>();
//                        string volumeAreaName;

//                        var usedVolumesCount = file.ReadUInt32();

//                        var vtx = new Dictionary<int, Vector3>();
//                        for (uint idx = 0; idx < usedVolumesCount; ++idx)
//                        {
//                            // Read volume data
//                            var volumeId = file.ReadUInt32();
//                            float volumeHeight = file.ReadUInt32();
//                            var verticesCount = file.ReadUInt32();

//                            //volumeVerticesBuffer.Resize(verticesCount);
//                            for (var vtxIdx = 0; vtxIdx < verticesCount; ++vtxIdx)
//                            {
//                                vtx[vtxIdx] = new Vector3(file.ReadSingle(), file.ReadSingle(), file.ReadSingle());
//                            }

//                            //var volumeAreaNameSize = file.ReadUInt32();

//                            //if (volumeAreaNameSize > 0)
//                            {
//                                //volumeAreaNameBuffer.Resize(volumeAreaNameSize, '\0');
//                                //file.ReadType(volumeAreaNameBuffer[0], volumeAreaNameSize);
//                                //volumeAreaName.assign(volumeAreaNameBuffer[0], (volumeAreaNameBuffer[volumeAreaNameBuffer.Count - 1]) + 1);
//                                volumeAreaName = file.ReadString();
//                            }
//                            else
//                            {
//                                volumeAreaName = "";
//                            }

//                            // Create volume
//                            if (volumeId == NavigationVolumeID())
//                            {
//                                _log.Warn("NavigationSystem::ReadFromFile: file contains invalid Navigation Volume ID");
//                                continue;
//                            }

//                            if (m_volumes.validate(volumeId))
//                            {
//                                _log.Warn("NavigationSystem::ReadFromFile: Navigation Volume with volumeId=%u (name '%s') is already registered", (uint)volumeId, volumeAreaName);
//                                continue;
//                            }

//                            CreateVolume(volumeVerticesBuffer[0], verticesCount, volumeHeight, volumeId);
//                            m_volumesManager.RegisterAreaFromLoadedData(volumeAreaName, volumeId);
//                        }
//                    }

//                    {
//                        var markupVerticesBuffer = new List<Vector3>();

//                        uint markupsCount;
//                        uint markupsCapacity;
//                        markupsCount = file.ReadUInt32();
//                        markupsCapacity = file.ReadUInt32();

//                        if (markupsCapacity > m_markupVolumes.capacity())
//                        {
//                            m_markupVolumes.grow(markupsCapacity - m_markupVolumes.capacity());
//                        }

//                        if (markupsCapacity > m_markupsData.capacity())
//                        {
//                            m_markupsData.grow(markupsCapacity - m_markupsData.capacity());
//                        }

//                        for (uint idx = 0; idx < markupsCount; ++idx)
//                        {
//                            NavigationVolumeID markupId = new NavigationVolumeID();
//                            MNM.SMarkupVolumeParams @params = new MNM.SMarkupVolumeParams();
//                            uint verticesCount;
//                            MNM.AreaAnnotation.value_type areaAnnotation = new MNM.AreaAnnotation.value_type();

//                            MNM.Utils.ReadNavigationIdType(file, markupId);

//                            //file.ReadType(@params.height);
//                            params.height = file.ReadUInt32();
//                            areaAnnotation = file.ReadUInt32();
//                            //file.ReadType(@params.bExpandByAgentRadius);
//                            params.bExpandByAgentRadius = file.ReadUInt32();
//                            //file.ReadType(@params.bStoreTriangles);
//                            params.bStoreTriangles = file.ReadUInt32();
//                            verticesCount = file.ReadUInt32();

//                            @params.areaAnnotation = areaAnnotation;

//                            markupVerticesBuffer.Resize(verticesCount);
//                            for (var vtxIdx = 0; vtxIdx < verticesCount; ++vtxIdx)
//                            {
//                                Vector3 vtx = markupVerticesBuffer[vtxIdx];
//                                vtx.X = file.ReadUInt32();
//                                vtx.Y = file.ReadUInt32();
//                                vtx.Z = file.ReadUInt32();
//                            }

//                            //CRY_ASSERT(markupId != NavigationVolumeID(), "Markup volume with invalid id loaded!");
//                            //CRY_ASSERT(!m_markupVolumes.validate(markupId),"Markup volume with the same id was already loaded!");

//                            CreateMarkupVolume(markupId);
//                            SetMarkupVolume(0, markupVerticesBuffer.data(), verticesCount, markupId, @params);
//                        }
//                    }

//                    uint agentsCount;
//                    agentsCount = file.ReadUInt32()
//                    for (uint i = 0; i < agentsCount; ++i)
//                    {
//                        uint nameLength;
//                        nameLength = file.ReadUInt32();
//                        var agentName = new string(new char[nameLength]);
//                        //nameLength = Math.Min(nameLength, (uint)MAX_NAME_LENGTH - 1);
//                        //file.ReadType(agentName, nameLength);
//                        agentName = file.ReadString();
//                        agentName = StringFunctions.ChangeCharacter(agentName, nameLength, '\0');

//                        // Reading total amount of memory used for the current agent
//                        uint totalAgentMemory = 0;
//                        file.ReadType(totalAgentMemory);

//                        size_t fileSeekPositionForNextAgent = file.GetPosition() + totalAgentMemory;
//                        NavigationAgentTypeID agentTypeID = GetAgentTypeID(agentName);
//                        if (agentTypeID == 0)
//                        {
//                            _log.Warn("The agent '%s' doesn't exist between the ones loaded from the Navigation.xml", agentName);
//                            file.Seek(fileSeekPositionForNextAgent, SEEK_SET);
//                            continue;
//                        }

//                        {
//                            // Reading markup volumes
//                            AgentType.MarkupVolumes markups = new AgentType.MarkupVolumes();
//                            uint markupsCount;
//                            file.ReadType(markupsCount);
//                            markups.reserve(markupsCount);
//                            for (uint mIdx = 0; mIdx < markupsCount; ++mIdx)
//                            {
//                                NavigationVolumeID markupId = new NavigationVolumeID();
//                                MNM.Utils.ReadNavigationIdType(file, markupId);
//                                markups.push_back(markupId);
//                            }

//                            m_agentTypes[agentTypeID - 1].markups = markups;
//                        }

//                        // ---------------------------------------------
//                        // Reading navmesh for the different agents type

//                        uint meshesCount = 0;
//                        file.ReadType(meshesCount);

//                        for (uint meshCounter = 0; meshCounter < meshesCount; ++meshCounter)
//                        {
//                            // Reading mesh id
//                            uint meshIDuint32 = 0;
//                            file.ReadType(meshIDuint32);
//                            // Reading mesh name
//                            uint meshNameLength = 0;
//                            file.ReadType(meshNameLength);
//                            var meshName = new string(new char[MAX_NAME_LENGTH]);
//                            meshNameLength = Math.Min(meshNameLength, (uint)MAX_NAME_LENGTH - 1);
//                            file.ReadType(meshName, meshNameLength);
//                            meshName = StringFunctions.ChangeCharacter(meshName, meshNameLength, '\0');

//                            // Reading flags
//                            CEnumFlags<EMeshFlag> meshFlags = new CEnumFlags<EMeshFlag>();
//                            if (nFileVersion >= NavigationSystem.eBAINavigationFileVersion.MESH_FLAGS)
//                            {
//                                uint flagsValue;
//                                file.ReadType(flagsValue);
//                                meshFlags.UnderlyingValue() = flagsValue;
//                            }

//                            // Reading the amount of islands in the mesh
//                            MNM.StaticIslandID totalIslands = 0;
//                            file.ReadType(totalIslands);

//                            // Reading total mesh memory
//                            uint totalMeshMemory = 0;
//                            file.ReadType(totalMeshMemory);

//                            size_t fileSeekPositionForNextMesh = file.GetPosition() + totalMeshMemory;

//                            // Reading mesh boundary
//                            NavigationVolumeID boundaryID = new NavigationVolumeID();
//                            MNM.Utils.ReadNavigationIdType(file, boundaryID);
//                            {
//                                if (m_volumesManager.GetLoadedAreaID(meshName) != boundaryID)
//                                {
//                                    _log.Warn("The NavMesh '%s' (agent = '%s', meshId = %u, boundaryVolumeId = %u) and the loaded corresponding Navigation Area have different IDs. Data might be corrupted.", meshName, agentName, meshIDuint32, (uint)boundaryID);
//                                }

//                                NavigationVolumeID existingAreaId = m_volumesManager.GetAreaID(meshName);
//                                if (existingAreaId != boundaryID)
//                                {
//                                    if (!m_volumesManager.IsAreaPresent(meshName))
//                                    {
//                                        if (!m_volumesManager.IsLoadedAreaPresent(meshName))
//                                        {
//                                            _log.Warn("The NavMesh '%s' (agent = '%s', meshId = %u, boundaryVolumeId = %u) doesn't have a loaded corresponding Navigation Area. Data might be corrupted.", meshName, agentName, meshIDuint32, (uint)boundaryID);
//                                        }
//                                    }
//                                    else
//                                    {
//                                        if (existingAreaId == NavigationVolumeID() && bAfterExporting)
//                                        {
//                                            // Expected situation
//                                        }
//                                        else
//                                        {
//                                            _log.Warn("The NavMesh '%s' (agent = '%s', meshId = %u, boundaryVolumeId = %u) and the existing corresponding Navigation Area have different IDs. Data might be corrupted.", meshName, agentName, meshIDuint32, (uint)boundaryID);
//                                        }
//                                    }
//                                }
//                            }

//                            // Reading mesh exclusion shapes
//                            uint exclusionShapesCount = 0;
//                            file.ReadType(exclusionShapesCount);
//                            AgentType.ExclusionVolumes exclusions = new AgentType.ExclusionVolumes();
//                            exclusions.reserve(exclusionShapesCount);
//                            for (uint exclusionsCounter = 0;
//                                exclusionsCounter < exclusionShapesCount;
//                                ++exclusionsCounter)
//                            {
//                                NavigationVolumeID exclusionId = new NavigationVolumeID();
//                                MNM.Utils.ReadNavigationIdType(file, exclusionId);
//                                // Save the exclusion shape with the read ID
//                                exclusions.push_back(exclusionId);
//                            }

//                            m_agentTypes[agentTypeID - 1].exclusions = exclusions;

//                            NavigationMesh.Markups markups = new NavigationMesh.Markups();
//                            {
//                                // Reading markup volumes
//                                uint markupsCount;
//                                file.ReadType(markupsCount);
//                                markups.reserve(markupsCount);
//                                for (uint mIdx = 0; mIdx < markupsCount; ++mIdx)
//                                {
//                                    NavigationVolumeID markupId = new NavigationVolumeID();
//                                    MNM.Utils.ReadNavigationIdType(file, markupId);
//                                    markups.push_back(markupId);
//                                }
//                            }

//                            // Reading tile count
//                            uint tilesCount = 0;
//                            file.ReadType(tilesCount);

//                            // Reading NavMesh grid params
//                            MNM.CNavMesh.SGridParams @params = new MNM.CNavMesh.SGridParams();
//                            file.ReadType((@params.origin.x));
//                            file.ReadType((@params.origin.y));
//                            file.ReadType((@params.origin.z));
//                            file.ReadType((@params.tileSize.x));
//                            file.ReadType((@params.tileSize.y));
//                            file.ReadType((@params.tileSize.z));
//                            file.ReadType((@params.voxelSize.x));
//                            file.ReadType((@params.voxelSize.y));
//                            file.ReadType((@params.voxelSize.z));
//                            file.ReadType((@params.tileCount));
//                            // If we are full reloading the mnm then we also want to create a new grid with the parameters
//                            // written in the file

//                            SCreateMeshParams createParams = new SCreateMeshParams();
//                            createParams.origin = @params.origin;
//                            createParams.tileSize = @params.tileSize;
//                            createParams.tileCount = tilesCount;

//                            NavigationMeshID newMeshID = new NavigationMeshID(meshIDuint32);
//                            if (!m_meshes.validate(meshIDuint32))
//                            {
//                                newMeshID = CreateMesh(meshName, agentTypeID, createParams, newMeshID);
//                            }

//                            if (newMeshID == 0)
//                            {
//                                _log.Warn("Unable to create mesh '%s'", meshName);
//                                file.Seek(fileSeekPositionForNextMesh, SEEK_SET);
//                                continue;
//                            }

//                            if (newMeshID != meshIDuint32)
//                            {
//                                _log.Warn("The restored mesh has a different ID compared to the saved one.");
//                            }

//                            NavigationMesh mesh = m_meshes[newMeshID];
//                            SetMeshBoundaryVolume(newMeshID, boundaryID);
//                            mesh.flags = meshFlags;
//                            mesh.markups = markups;
//                            mesh.exclusions = exclusions;
//                            mesh.navMesh.GetIslands().SetTotalIslands(totalIslands);

//                            for (uint j = 0; j < tilesCount; ++j)
//                            {
//                                // Reading Tile indexes
//                                ushort x;
//                                ushort y;
//                                ushort z;
//                                uint hashValue;
//                                file.ReadType(x);
//                                file.ReadType(y);
//                                file.ReadType(z);
//                                file.ReadType(hashValue);

//                                // Reading triangles
//                                ushort triangleCount = 0;
//                                file.ReadType(triangleCount);
//                                std::unique_ptr<MNM.Tile.STriangle[]> pTriangles =
//                                    new std::unique_ptr<MNM.Tile.STriangle[]>();
//                                if (triangleCount != 0)
//                                {
//                                    pTriangles.reset(Arrays.InitializeWithDefaultInstances<STriangle>(triangleCount));
//                                    file.ReadType(pTriangles.get(), triangleCount);
//                                }

//                                // Reading Vertices
//                                ushort vertexCount = 0;
//                                file.ReadType(vertexCount);
//                                std::unique_ptr<MNM.Tile.Vertex[]> pVertices = new std::unique_ptr<MNM.Tile.Vertex[]>();
//                                if (vertexCount != 0)
//                                {
//                                    pVertices.reset(Arrays.InitializeWithDefaultInstances<Vertex>(vertexCount));
//                                    file.ReadType(pVertices.get(), vertexCount);
//                                }

//                                // Reading Links
//                                ushort linkCount;
//                                file.ReadType(linkCount);
//                                std::unique_ptr<MNM.Tile.SLink[]> pLinks = new std::unique_ptr<MNM.Tile.SLink[]>();
//                                if (linkCount != 0)
//                                {
//                                    pLinks.reset(Arrays.InitializeWithDefaultInstances<SLink>(linkCount));
//                                    file.ReadType(pLinks.get(), linkCount);
//                                }

//                                // Reading nodes
//                                ushort nodeCount;
//                                file.ReadType(nodeCount);
//                                std::unique_ptr<MNM.Tile.SBVNode[]> pNodes = new std::unique_ptr<MNM.Tile.SBVNode[]>();
//                                if (nodeCount != 0)
//                                {
//                                    pNodes.reset(Arrays.InitializeWithDefaultInstances<SBVNode>(nodeCount));
//                                    file.ReadType(pNodes.get(), nodeCount);
//                                }

//                                // Creating and swapping the tile
//                                MNM.STile tile = new MNM.STile();
//                                tile.SetTriangles(std::move(pTriangles), triangleCount);
//                                tile.SetVertices(std::move(pVertices), vertexCount);
//                                tile.SetLinks(std::move(pLinks), linkCount);
//                                tile.SetNodes(std::move(pNodes), nodeCount);
//                                tile.SetHashValue(hashValue);

//                                mesh.navMesh.SetTile(x, y, z, tile);
//                            }
//                        }
//                    }

//                    m_volumesManager.ValidateAndSanitizeLoadedAreas(this);

//                    const ENavigationEvent navigationEvent = bAfterExporting
//                        ? ENavigationEvent.MeshReloadedAfterExporting
//                        : ENavigationEvent.MeshReloaded;
//                    UpdateAllListeners(navigationEvent);

//                    m_offMeshNavigationManager.OnNavigationLoadedComplete();

//                    //TODO: consider saving island connectivity in the navmesh
//                    ComputeAllIslands();

//                    m_pEditorBackgroundUpdate->Pause(false);

//                    return fileLoaded;
//                }
//            }
//            return fileLoaded;
//        }
//    }
//}
