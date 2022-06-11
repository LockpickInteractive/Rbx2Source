﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using Rbx2Source.Animating;
using Rbx2Source.Geometry;
using Rbx2Source.QuakeC;
using Rbx2Source.StudioMdl;
using Rbx2Source.Textures;
using Rbx2Source.Web;

using RobloxFiles;
using RobloxFiles.Enums;
using RobloxFiles.DataTypes;
using System.Diagnostics.Contracts;

namespace Rbx2Source.Assembler
{
    public enum Limb { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg, Unknown }
    
    public class CharacterAssembler : IAssembler<UserAvatar>
    {
        public static bool DEBUG_RAPID_ASSEMBLY = false;

        public static BodyPart? GetLimb(BasePart part)
        {
            Contract.Requires(part != null);

            var invariant = StringComparison.InvariantCulture;
            string name = part.Name;

            if (name == "Head")
                return BodyPart.Head;
            else if (name.EndsWith("Torso", invariant))
                return BodyPart.Torso;

            string limbName;

            if (name.StartsWith("Left", invariant))
                limbName = "Left";
            else if (name.StartsWith("Right", invariant))
                limbName = "Right";
            else
                return null;

            if (name.EndsWith("Arm", invariant) || name.EndsWith("Hand", invariant))
                limbName += "Arm";
            else if (name.EndsWith("Leg", invariant) || name.EndsWith("Foot", invariant))
                limbName += "Leg";

            if (!Enum.TryParse(limbName, out BodyPart bodyPart))
                return null;

            return bodyPart;
        }

        public static List<Attachment> FindOtherAttachments(Attachment a, Instance bin)
        {
            Contract.Requires(a != null && bin != null);
            List<Attachment> result = new List<Attachment>();

            foreach (Instance child in bin.GetChildren())
            {
                Attachment b = child.FindFirstChild<Attachment>(a.Name);

                if (b != null && a != b)
                {
                    result.Add(b);
                }
            }

            return result;
        }

        private static void GenerateBones(BoneAssemblePrep prep, Attachment[] queue)
        {
            if (queue.Length == 0)
                return;

            Instance bin = queue[0].Parent.Parent;

            foreach (Attachment a0 in queue)
            {
                List<Attachment> a1s = FindOtherAttachments(a0, bin);

                foreach (Attachment a1 in a1s)
                {
                    if (a1 != null && !prep.Completed.Contains(a1))
                    {
                        BasePart part0 = (BasePart)a0.Parent;
                        BasePart part1 = (BasePart)a1.Parent;

                        bool isRigAttachment = a0.Name.EndsWith("RigAttachment", StringComparison.InvariantCulture);

                        if (isRigAttachment || prep.AllowNonRigs)
                        {
                            StudioBone bone = new StudioBone(part1.Name, part0, part1)
                            {
                                C0 = a0.CFrame,
                                C1 = a1.CFrame,
                            };

                            bone.IsAvatarBone = !prep.AllowNonRigs;
                            prep.Bones.Add(bone);

                            Node node = bone.Node;
                            node.NodeIndex = prep.Bones.IndexOf(bone);
                            prep.Nodes.Add(node);

                            if (!prep.Completed.Contains(a0))
                                prep.Completed.Add(a0);

                            prep.Completed.Add(a1);

                            if (!prep.AllowNonRigs)
                            {
                                GenerateBones(prep, part1.GetChildrenOfType<Attachment>());
                            }
                        }
                        else // We'll deal with Accessory attachments afterwards.
                        {
                            prep.NonRigs.Add(a0);
                        }
                    }
                }
            }
        }

        public static void ApplyBoneCFrames(BasePart part)
        {
            Contract.Requires(part != null);

            foreach (StudioBone bone in part.GetChildrenOfType<StudioBone>())
            {
                BasePart part0 = bone.Part0;
                BasePart part1 = bone.Part1;

                part1.CFrame = part0.CFrame * bone.C0 * bone.C1.Inverse();

                if (part0 != part1)
                {
                    ApplyBoneCFrames(part1);
                }
            }
        }

        public static BoneKeyframe AssembleBones(StudioMdlWriter meshBuilder, BasePart rootPart)
        {
            Contract.Requires(meshBuilder != null);
            Contract.Requires(rootPart != null);

            Rbx2Source.Print("Building Skeleton...");
            BoneKeyframe kf = new BoneKeyframe();

            List<StudioBone> bones = kf.Bones;
            List<Node> nodes = meshBuilder.Nodes;

            StudioBone rootBone = new StudioBone(rootPart.Name, rootPart)
            {
                C0 = new CFrame(),
                IsAvatarBone = true
            };

            Node rootNode = rootBone.Node;
            rootNode.NodeIndex = 0;

            bones.Add(rootBone);
            nodes.Add(rootNode);

            // Assemble the base rig.
            BoneAssemblePrep prep = new BoneAssemblePrep(ref bones, ref nodes);
            GenerateBones(prep, rootPart.GetChildrenOfType<Attachment>());

            // Assemble the accessories.
            prep.AllowNonRigs = true;
            GenerateBones(prep, prep.NonRigs.ToArray());

            // Apply the rig cframe data.
            ApplyBoneCFrames(rootPart);
            meshBuilder.Skeleton.Add(kf);

            return kf;
        }

        public static void PrepareAccessory(Instance asset, Folder assembly)
        {
            Contract.Requires(asset != null && assembly != null);

            if (DEBUG_RAPID_ASSEMBLY)
            {
                asset.Destroy();
                return;
            }

            BasePart handle = asset.FindFirstChild<BasePart>("Handle");

            if (handle != null)
            {
                handle.Name = FileUtility.MakeNameWindowsSafe(asset.Name);
                handle.CFrame = new CFrame();
                handle.Parent = assembly;
                
                if (asset is Accessory) // Make sure the attachment is in the Handle
                {
                    Attachment accAtt = asset.FindFirstChildOfClass<Attachment>();

                    if (accAtt == null)
                        return;

                    accAtt.Parent = handle;
                }
            }
        }

        public static void OverwriteHead(DataModelMesh mesh, BasePart head)
        {
            Contract.Requires(mesh != null && head != null);
            DataModelMesh currentMesh = head.FindFirstChild<DataModelMesh>("Mesh");

            if (currentMesh != null)
                currentMesh.Destroy();

            mesh.Name = "Mesh";
            mesh.Parent = head;

            // Apply Rthro adjustments
            Vector3Value[] overrides = mesh.GetChildrenOfType<Vector3Value>();

            foreach (Vector3Value overrider in overrides)
            {
                Attachment attachment = head.FindFirstChild<Attachment>(overrider.Name);
                if (attachment != null)
                {
                    CFrame cf = attachment.CFrame;
                    attachment.CFrame = new CFrame(overrider.Value) * (cf - cf.Position);
                }
            }

            // Move any extra instances into the Head.
            var extraInstances = mesh.GetChildren()
                .Except(overrides)
                .ToList();

            extraInstances.ForEach((inst) => inst.Parent = head);
        }

        public static void BuildAvatarGeometry(StudioMdlWriter meshBuilder, StudioBone bone)
        {
            Contract.Requires(meshBuilder != null && bone != null);
            
            string task = "BuildGeometry_" + bone.Node.Name;
            Rbx2Source.ScheduleTasks(task);

            Node node = bone.Node;
            BasePart part = bone.Part1;

            bool isAvatarLimb = bone.IsAvatarBone;
            string matName = part.Name;

            if (isAvatarLimb)
            {
                BodyPart? limb = GetLimb(part);

                if (!limb.HasValue)
                    throw new ArgumentException("Provided StudioBone did not point to a limb correctly.");

                matName = Rbx2Source.GetEnumName(limb.Value);
            }

            var material = new ValveMaterial() { UseAvatarMap = isAvatarLimb };
            
            Rbx2Source.Print("Building Geometry for {0}", part.Name);
            Rbx2Source.IncrementStack();

            Mesh geometry = Mesh.BakePart(part, material);
            meshBuilder.Materials[matName] = material;

            int faceStride;

            if (geometry.HasLODs)
                faceStride = geometry.LODs[1];
            else
                faceStride = geometry.NumFaces;

            for (int i = 0; i < faceStride; i++)
            {
                Triangle tri = new Triangle()
                {
                    Node = node,
                    FaceIndex = i,
                    Mesh = geometry,
                    Material = matName
                };
                
                meshBuilder.Triangles.Add(tri);
            }

            Rbx2Source.DecrementStack();
            Rbx2Source.MarkTaskCompleted(task);
        }

        public static Folder AppendCharacterAssets(UserAvatar avatar, string avatarType, string context = "CHARACTER")
        {
            Contract.Requires(avatar != null);
            Rbx2Source.PrintHeader("GATHERING " + context + " ASSETS");

            Folder characterAssets = new Folder();
            AssetInfo[] assets = avatar.Assets;

            foreach (AssetInfo info in assets)
            {
                long id = info.Id;
                Asset asset = Asset.Get(id);

                Instance import = asset.OpenAsModel();
                Folder typeSpecific = import.FindFirstChild<Folder>(avatarType);

                if (typeSpecific != null)
                    import = typeSpecific;

                var children = import
                    .GetChildren()
                    .ToList();

                children.ForEach((obj) => obj.Parent = characterAssets);
            }

            return characterAssets;
        }

        public static Folder AppendCollisionAssets(UserAvatar avatar, string avatarType)
        {
            Folder collisionAssets;

            if (DEBUG_RAPID_ASSEMBLY)
                collisionAssets = new Folder();
            else
                collisionAssets = AppendCharacterAssets(avatar, avatarType, "COLLISION");

            // Replace the head mesh with a low-poly head
            DataModelMesh oldHeadMesh = collisionAssets.FindFirstChildOfClass<DataModelMesh>();

            if (oldHeadMesh != null)
                oldHeadMesh.Destroy();
            
            _ = new SpecialMesh()
            {
                MeshId = "rbxassetid://582002794",
                MeshType = MeshType.FileMesh,
                Scale = new Vector3(1, 1, 1),
                Offset = new Vector3(),
                Parent = collisionAssets
            };

            return collisionAssets;
        }

        public static Asset GetAvatarFace(Folder characterAssets)
        {
            // Check if this avatar is using an Rthro head with a texture overlay.
            Contract.Requires(characterAssets != null);
            Folder assembly = characterAssets.FindFirstChild<Folder>("ASSEMBLY");

            if (assembly != null)
            {
                BasePart head = assembly.FindFirstChild<BasePart>("Head");

                if (head != null)
                {
                    SpecialMesh headMesh = head.FindFirstChildOfClass<SpecialMesh>();

                    if (headMesh != null && headMesh.TextureId != null)
                    {
                        string textureId = headMesh.TextureId;

                        if (textureId.Length > 0)
                        {
                            // One last check to make sure this is *probably* an Rthro head.
                            // The reason this check is necessary is due to the iBot Head, which has a texture and allows a face to be drawn on it.
                            // I suspect Roblox will expand this behavior later, so I need to keep an eye on it.
                            StringValue scaleType = head.FindFirstChild<StringValue>("AvatarPartScaleType");

                            if (scaleType != null && scaleType.Value != "Classic")
                            {
                                return Asset.GetByAssetId(headMesh.TextureId);
                            }
                        }
                    }
                }
            }

            // Fall back to normal behavior.
            Decal face = characterAssets.FindFirstChild<Decal>("face");
            Asset result;

            if (face != null && face.Texture != "rbxasset://textures/face.png")
                result = Asset.GetByAssetId(face.Texture);
            else
                result = Asset.FromResource("Images/face.png");

            return result;
        }

        public static float ComputeFloorLevel(Folder assembly)
        {
            Contract.Requires(assembly != null);

            BasePart lowest = null;
            float lowestY = float.MaxValue;

            foreach (BasePart part in assembly.GetChildrenOfType<BasePart>())
            {
                float y = part.Position.Y;

                if (y < lowestY)
                {
                    lowest = part;
                    lowestY = y;
                }
            }

            return (lowestY - (lowest.Size.Y / 2f)) * Rbx2Source.MODEL_SCALE;
        }

        public AssemblerData Assemble(UserAvatar avatar)
        {
            Contract.Requires(avatar != null);

            UserInfo userInfo = avatar.UserInfo;
            string userName = FileUtility.MakeNameWindowsSafe(userInfo.Username);

            string appData = Environment.GetEnvironmentVariable("LocalAppData");
            string rbx2Src = Path.Combine(appData, "Rbx2Source");
            string avatars = Path.Combine(rbx2Src, "Avatars");
            string userBin = Path.Combine(avatars, userName);

            string modelDir = Path.Combine(userBin, "Model");
            string anim8Dir = Path.Combine(modelDir, "Animations");
            string texturesDir = Path.Combine(userBin, "Textures");
            string materialsDir = Path.Combine(userBin, "Materials");

            FileUtility.InitiateEmptyDirectories(modelDir, anim8Dir, texturesDir, materialsDir);

            AvatarType avatarType = avatar.PlayerAvatarType;
            ICharacterAssembler assembler;

            if (avatarType == AvatarType.R15)
                assembler = new R15CharacterAssembler();
            else
                assembler = new R6CharacterAssembler();

            string compileDir = "roblox_avatars/" + userName;

            string avatarTypeName = Rbx2Source.GetEnumName(avatarType);
            Folder characterAssets = AppendCharacterAssets(avatar, avatarTypeName);
            
            Rbx2Source.ScheduleTasks
            (
                "BuildCharacter",
                "BuildCollisionModel", 
                "BuildAnimations",
                "BuildTextures",
                "BuildMaterials", 
                "BuildCompilerScript"
            );

            Rbx2Source.PrintHeader("BUILDING CHARACTER MODEL");
            #region Build Character Model
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            StudioMdlWriter writer = assembler.AssembleModel(characterAssets, avatar.Scales, DEBUG_RAPID_ASSEMBLY);

            string studioMdl = writer.BuildFile();
            string modelPath = Path.Combine(modelDir, "CharacterModel.smd");
            FileUtility.WriteFile(modelPath, studioMdl);

            string staticPose = writer.BuildFile(false);
            string refPath = Path.Combine(modelDir, "ReferencePos.smd");
            FileUtility.WriteFile(refPath, staticPose);

            Rbx2Source.MarkTaskCompleted("BuildCharacter");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            Rbx2Source.PrintHeader("BUILDING COLLISION MODEL");
            #region Build Character Collisions
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            Folder collisionAssets = AppendCollisionAssets(avatar, avatarTypeName);
            StudioMdlWriter collisionWriter = assembler.AssembleModel(collisionAssets, avatar.Scales, true);

            string collisionModel = collisionWriter.BuildFile();
            string cmodelPath = Path.Combine(modelDir, "CollisionModel.smd");
            FileUtility.WriteFile(cmodelPath, collisionModel); 

            byte[] collisionJoints = assembler.CollisionModelScript;
            string cjointsPath = Path.Combine(modelDir, "CollisionJoints.qc");

            FileUtility.WriteFile(cjointsPath, collisionJoints);
            Rbx2Source.MarkTaskCompleted("BuildCollisionModel");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            Rbx2Source.PrintHeader("BUILDING CHARACTER ANIMATIONS");
            #region Build Character Animations
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            var animIds = assembler.CollectAnimationIds(avatar);
            var compileAnims = new Dictionary<string, Asset>();
                
            if (animIds.Count > 0)
            {
                Rbx2Source.Print("Collecting Animations...");
                Rbx2Source.IncrementStack();

                Action<string, Asset> collectAnimation = (animName, animAsset) =>
                {
                    if (!compileAnims.ContainsKey(animName))
                    {
                        Rbx2Source.Print("Collected animation {0} with id {1}", animName, animAsset.Id);
                        compileAnims.Add(animName, animAsset);
                    }
                };

                foreach (string animName in animIds.Keys)
                {
                    var animId = animIds[animName];
                    var animAsset = animId.GetAsset();
                    var import = animAsset.OpenAsModel();

                    if (animId.AnimationType == AnimationType.R15AnimFolder)
                    {
                        Folder r15Anim = import.FindFirstChild<Folder>("R15Anim");

                        if (r15Anim != null)
                        {
                            foreach (Instance animDef in r15Anim.GetChildren())
                            {
                                if (animDef.Name == "idle")
                                {
                                    var anims = animDef.GetChildrenOfType<Animation>();

                                    if (anims.Length == 2)
                                    {
                                        var getLookAnim = anims.OrderBy((anim) =>
                                        {
                                            var weight = anim.FindFirstChild<NumberValue>("Weight");

                                            if (weight != null)
                                                return weight.Value;

                                            return 0.0;
                                        });

                                        var lookAnim = getLookAnim.First();
                                        lookAnim.Destroy();

                                        Asset lookAsset = Asset.GetByAssetId(lookAnim.AnimationId);
                                        collectAnimation("Idle2", lookAsset);
                                    }
                                }

                                Animation compileAnim = animDef.FindFirstChildOfClass<Animation>();

                                if (compileAnim != null)
                                {
                                    Asset compileAsset = Asset.GetByAssetId(compileAnim.AnimationId);
                                    string compileName = animName;

                                    if (animDef.Name == "pose")
                                        compileName = "Pose";

                                    collectAnimation(compileName, compileAsset);
                                }
                            }
                        }
                    }
                    else
                    {
                        collectAnimation(animName, animAsset);
                    }
                }

                Rbx2Source.DecrementStack();
            }
            else
            {
                Rbx2Source.Print("No animations found :(");
            }

            if (compileAnims.Count > 0)
            {
                Rbx2Source.Print("Assembling Animations...");
                Rbx2Source.IncrementStack();

                foreach (string animName in compileAnims.Keys)
                {
                    Rbx2Source.Print("Building Animation {0}...", animName);

                    Asset animAsset = compileAnims[animName];
                    var import = animAsset.OpenAsModel();
                    
                    var sequence = import.FindFirstChildOfClass<KeyframeSequence>();
                    sequence.Name = animName;

                    var avatarTypeRef = new StringValue()
                    {
                        Value = $"{avatarType}",
                        Name = "AvatarType",
                        Parent = sequence
                    };

                    string animation = AnimationBuilder.Assemble(sequence, writer.Skeleton[0].Bones);
                    string animPath = Path.Combine(anim8Dir, animName + ".smd");

                    FileUtility.WriteFile(animPath, animation);
                }

                Rbx2Source.DecrementStack();
            }

            Rbx2Source.MarkTaskCompleted("BuildAnimations");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            Rbx2Source.PrintHeader("BUILDING CHARACTER TEXTURES");
            #region Build Character Textures
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            var materials = writer.Materials;
            TextureBindings textures;

            if (DEBUG_RAPID_ASSEMBLY)
            {
                textures = new TextureBindings();
                materials.Clear();
            }
            else
            {
                TextureCompositor texCompositor = assembler.ComposeTextureMap(characterAssets, avatar.BodyColors);
                textures = assembler.BindTextures(texCompositor, materials);
            }

            var images = textures.Images;
            textures.MaterialDirectory = compileDir;

            foreach (string imageName in images.Keys)
            {
                Rbx2Source.Print("Writing Image {0}.png", imageName);

                Image image = images[imageName];
                string imagePath = Path.Combine(texturesDir, imageName + ".png");

                try
                {
                    image.Save(imagePath, ImageFormat.Png);
                }
                catch
                {
                    Rbx2Source.Print("IMAGE {0}.png FAILED TO SAVE!", imageName);
                }

                FileUtility.LockFile(imagePath);
            }

            CompositData.FreeAllocatedTextures();
            Rbx2Source.MarkTaskCompleted("BuildTextures");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            Rbx2Source.PrintHeader("WRITING MATERIAL FILES");
            #region Write Material Files
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            var matLinks = textures.MatLinks;

            foreach (string mtlName in matLinks.Keys)
            {
                Rbx2Source.Print("Building VMT {0}.vmt", mtlName);

                string targetVtf = matLinks[mtlName];
                string vmtPath = Path.Combine(materialsDir, mtlName + ".vmt");

                ValveMaterial mtl = materials[mtlName];
                mtl.SetVmtField("basetexture", "models/" + compileDir + "/" + targetVtf);
                mtl.WriteVmtFile(vmtPath);
            }

            Rbx2Source.MarkTaskCompleted("BuildMaterials");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            Rbx2Source.PrintHeader("WRITING COMPILER SCRIPT");
            #region Write Compiler Script
            ///////////////////////////////////////////////////////////////////////////////////////////////////////

            string modelName = compileDir + ".mdl";
            QuakeCWriter qc = new QuakeCWriter();

            qc.Add("body", userName, "CharacterModel.smd");
            qc.Add("modelname", modelName);
            qc.Add("upaxis", "y");

            // Compute the floor level of the avatar.
            Folder assembly = characterAssets.FindFirstChild<Folder>("ASSEMBLY");

            if (assembly != null)
            {
                float floor = ComputeFloorLevel(assembly);
                string origin = "0 " + floor.ToInvariantString() + " 0";
                qc.Add("origin", origin);
            }

            qc.Add("cdmaterials", "models/" + compileDir);
            qc.Add("surfaceprop", "flesh");
            qc.Add("include", "CollisionJoints.qc");

            QuakeCItem refAnim = qc.Add("sequence", "reference", "ReferencePos.smd");
            refAnim.AddSubItem("fps", 1);
            refAnim.AddSubItem("loop");

            foreach (string animName in compileAnims.Keys)
            {
                QuakeCItem sequence = qc.Add("sequence", animName.ToLowerInvariant(), "Animations/" + animName + ".smd");
                sequence.AddSubItem("fps", AnimationBuilder.FrameRate);

                if (avatarType == AvatarType.R6)
                    sequence.AddSubItem("delta");

                sequence.AddSubItem("loop");
            }

            string qcFile = qc.ToString();
            string qcPath = Path.Combine(modelDir, "Compile.qc");

            FileUtility.WriteFile(qcPath, qcFile);
            Rbx2Source.MarkTaskCompleted("BuildCompilerScript");

            ///////////////////////////////////////////////////////////////////////////////////////////////////////
            #endregion

            AssemblerData data = new AssemblerData()
            {
                ModelData = writer,
                ModelName = modelName,
                TextureData = textures,
                CompilerScript = qcPath,
                
                RootDirectory = userBin,
                CompileDirectory = compileDir,
                TextureDirectory = texturesDir,
                MaterialDirectory = materialsDir,
            };

            return data;
        }
    }
}
