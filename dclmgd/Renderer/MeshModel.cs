using Assimp;
using dclmgd.Support;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using MoreLinq;

namespace dclmgd.Renderer
{
    class MeshModel
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct Vertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 UV;
            public Vector3 Tangent;
            public Vector3 BiTangent;
            public fixed int BoneIds[4];
            public fixed float BoneWeights[4];

            public Vertex(Vector3 position, Vector3 normal, Vector2 uv, Vector3 tangent, Vector3 biTangent, int[] boneIds, float[] boneWeights)
            {
                (Position, Normal, UV, Tangent, BiTangent) = (position, normal, uv, tangent, biTangent);

                if (boneIds is not null)
                {
                    BoneIds[0] = boneIds[0];
                    BoneIds[1] = boneIds[1];
                    BoneIds[2] = boneIds[2];
                    BoneIds[3] = boneIds[3];
                }

                if (boneWeights is not null)
                {
                    BoneWeights[0] = boneWeights[0];
                    BoneWeights[1] = boneWeights[1];
                    BoneWeights[2] = boneWeights[2];
                    BoneWeights[3] = boneWeights[3];
                }
            }
        }

        enum TextureType { Diffuse, Normal }

        struct KeyFrame<TValue>
        {
            public TValue Value { get; set; }
            public TimeSpan Time { get; set; }
        }

        class Bone
        {
            readonly KeyFrame<Vector3>[] positionKeyFrames;
            readonly KeyFrame<Quaternion>[] rotationKeyFrames;
            readonly KeyFrame<Vector3>[] scaleKeyFrames;

            readonly string name;
            public int Id { get; }

            public Bone(int id, NodeAnimationChannel channel)
            {
                (name, Id) = (channel.NodeName, id);

                (positionKeyFrames, rotationKeyFrames, scaleKeyFrames) =
                    (channel.PositionKeys.Select(vk => new KeyFrame<Vector3> { Value = vk.Value.ToNumerics(), Time = TimeSpan.FromSeconds(vk.Time) }).ToArray(channel.PositionKeyCount),
                    channel.RotationKeys.Select(vk => new KeyFrame<Quaternion> { Value = vk.Value.ToNumerics(), Time = TimeSpan.FromSeconds(vk.Time) }).ToArray(channel.RotationKeyCount),
                    channel.ScalingKeys.Select(vk => new KeyFrame<Vector3> { Value = vk.Value.ToNumerics(), Time = TimeSpan.FromSeconds(vk.Time) }).ToArray(channel.ScalingKeyCount));
            }

            public Matrix4x4 this[TimeSpan time]
            {
                get
                {
                    double getScaleFactor(TimeSpan last, TimeSpan next) => (time - last).TotalSeconds / (next - last).TotalSeconds;
                    int getCurrentKeyFrameIndex<T>(IEnumerable<KeyFrame<T>> keyFrames)
                    {
                        int idx = 0;
                        foreach (var kf in keyFrames.Skip(1))
                            if (kf.Time > time)
                                return idx;
                            else
                                ++idx;
                        throw new InvalidOperationException("Invalid timestamp in bone lookup");
                    }
                    Matrix4x4 interpolateCurrentKeyFrame<T>(KeyFrame<T>[] keyFrames, Func<KeyFrame<T>, Matrix4x4> toMatrixSingle, Func<KeyFrame<T>, KeyFrame<T>, double, Matrix4x4> toMatrixMix)
                    {
                        if (keyFrames.Length == 1) return toMatrixSingle(keyFrames[0]);

                        var index = getCurrentKeyFrameIndex(keyFrames);
                        var factor = getScaleFactor(keyFrames[index].Time, keyFrames[index + 1].Time);
                        return toMatrixMix(keyFrames[index], keyFrames[index + 1], factor);
                    }

                    var posMat = interpolateCurrentKeyFrame(positionKeyFrames, kf => Matrix4x4.CreateTranslation(kf.Value), (kf0, kf1, scale) =>
                        Matrix4x4.CreateTranslation(Vector3.Lerp(kf0.Value, kf1.Value, (float)scale)));
                    var rotMat = interpolateCurrentKeyFrame(rotationKeyFrames, kf => Matrix4x4.CreateFromQuaternion(kf.Value), (kf0, kf1, scale) =>
                        Matrix4x4.CreateFromQuaternion(Quaternion.Lerp(kf0.Value, kf1.Value, (float)scale)));
                    var scaleMat = interpolateCurrentKeyFrame(scaleKeyFrames, kf => Matrix4x4.CreateScale(kf.Value), (kf0, kf1, scale) =>
                        Matrix4x4.CreateScale(Vector3.Lerp(kf0.Value, kf1.Value, (float)scale)));

                    return posMat * rotMat * scaleMat;
                }
            }
        }

        struct PerMeshData
        {
            public readonly VertexArrayObject<Vertex, ushort> vao;
            public readonly Texture2D diffuseTexture, normalTexture;
            public readonly ShaderProgram lightProgram, shadowProgram;
            public readonly Matrix4x4 modelTransform;
            public readonly Dictionary<int, Matrix4x4> boneOffsetMatrices;

            public PerMeshData(VertexArrayObject<Vertex, ushort> vao, Texture2D diffuseTexture, Texture2D normalTexture,
                ShaderProgram lightProgram, ShaderProgram shadowProgram, Matrix4x4 modelTransform, Dictionary<int, Matrix4x4> boneOffsetMatrices) =>
                    (this.vao, this.diffuseTexture, this.normalTexture, this.lightProgram, this.shadowProgram, this.modelTransform, this.boneOffsetMatrices) =
                        (vao, diffuseTexture, normalTexture, lightProgram, shadowProgram, modelTransform, boneOffsetMatrices);
        }
        readonly PerMeshData[] perMeshData;

        static readonly AssimpContext assimpContext = new();

        static Texture2D TryLoadTexture(string meshPath, Mesh mesh, TextureType textureType)
        {
            var texturesPath = Path.Combine(meshPath, mesh.Name);
            return tryLoad("png") ?? tryLoad("jpg");

            Texture2D tryLoad(string ext)
            {
                var texturePath = Path.Combine(texturesPath, $"{textureType}.{ext}");
                return File.Exists(texturePath) ? new(texturePath, TextureStorageType.Rgbx, TextureFilteringType.LinearMinLinearMag, TextureClampingType.Repeat) : null;
            }
        }

        class MaterialType
        {
            public float Shininess { get; set; } = 1;
            public float TextureSMultiplier { get; set; } = 1;
            public float TextureTMultiplier { get; set; } = 1;
        }
        readonly MaterialType material;

        class BoneNode
        {
            public Matrix4x4 Transform;
            public int Id;
            public BoneNode[] Children;
        }

        class Animation
        {
            public Bone[] Bones { get; init; }
            public TimeSpan Duration { get; init; }
        }

        readonly Dictionary<string, Animation> animations = new();
        readonly BoneNode rootBoneNode;

        readonly Matrix4x4[] finalBoneMatrices = Enumerable.Range(0, 100).Select(_ => Matrix4x4.Identity).ToArray();

        double currentAnimationSec;
        string currentAnimationName;
        public string CurrentAnimationName { get => currentAnimationName; set => (currentAnimationName, currentAnimationSec) = (value, 0); }

        public MeshModel(string path)
        {
            var scene = assimpContext.ImportFile(Path.Combine(path, "model.dae"), PostProcessSteps.FlipUVs | PostProcessSteps.OptimizeGraph | PostProcessSteps.OptimizeMeshes
                /*| PostProcessSteps.RemoveRedundantMaterials*/ | PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.LimitBoneWeights
                /*| PostProcessSteps.MakeLeftHanded*/ | PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.CalculateTangentSpace);

            Dictionary<string, int> boneIds = new();
            int maxBoneIds = 0;
            foreach (var animation in scene.Animations)
                animations.Add(animation.Name, new()
                {
                    Bones = animation.NodeAnimationChannels
                        .Select(node => new Bone(boneIds.TryGetValue(node.NodeName, out var id) ? id : boneIds[node.NodeName] = maxBoneIds++, node))
                        .ToArray(),
                    Duration = TimeSpan.FromSeconds(animation.DurationInTicks * animation.TicksPerSecond),
                });

            // calculate the per-mesh transforms
            var transformsDictionary = new Dictionary<Mesh, Matrix4x4>();

            BoneNode visitTransforms(Node node, Matrix4x4 mat)
            {
                var boneNode = new BoneNode
                {
                    Children = new BoneNode[node.ChildCount],
                    Id = boneIds.TryGetValue(node.Name, out var id) ? id : -1,
                    Transform = node.Transform.ToNumerics(),
                };

                mat = node.Transform.ToNumerics() * mat;
                foreach (var meshIndex in node.MeshIndices)
                    transformsDictionary[scene.Meshes[meshIndex]] = mat;
                int childIdx = 0;
                foreach (var child in node.Children)
                    boneNode.Children[childIdx++] = visitTransforms(child, mat);

                return boneNode;
            }
            rootBoneNode = visitTransforms(scene.RootNode, Matrix4x4.Identity);

            perMeshData = new PerMeshData[scene.Meshes.Count];

            for (int meshIdx = 0; meshIdx < scene.Meshes.Count; ++meshIdx)
            {
                var mesh = scene.Meshes[meshIdx];
                ref PerMeshData perMeshDataSlot = ref perMeshData[meshIdx];

                // load the material json file, if any
                var materialPath = Path.Combine(path, mesh.Name, "material.json");
                if (File.Exists(materialPath))
                    material = JsonSerializer.Deserialize<MaterialType>(File.ReadAllText(materialPath), new() { PropertyNameCaseInsensitive = true });

                var indices = mesh.GetShortIndices();
                var diffuseTexture = TryLoadTexture(path, mesh, TextureType.Diffuse);
                var normalTexture = TryLoadTexture(path, mesh, TextureType.Normal);

                var vertexBoneInfluence = mesh.Bones
                    .SelectMany(bone => bone.VertexWeights.Select(vw => (BoneIdx: boneIds[bone.Name], vw.VertexID, vw.Weight)))
                    .GroupBy(w => w.VertexID)
                    .Select(vg => (vId: vg.Key, ids: vg.Select(w => w.BoneIdx).ToArray(4, -1), weights: vg.Select(w => w.Weight).ToArray(4, 0)))
                    .ToArraySequentialBy(mesh.VertexCount, w => w.vId, w => (w.ids, w.weights));

                var lightShaderName = normalTexture is null ? mesh.HasBones ? "object-bones" : "object" : mesh.HasBones ? "object-normal-bones" : "object-normal";

                perMeshDataSlot = new(
                    vao: VertexArrayObject<Vertex, ushort>.CreateStatic(
                        mesh.Vertices.Select(v => new Vector3(v.X, v.Y, v.Z))
                            .Zip(mesh.Normals.Select(v => new Vector3(v.X, v.Y, v.Z)), (v, n) => (v, n))
                            .Zip(mesh.TextureCoordinateChannels[0].Select(v => new Vector2(v.X * material.TextureSMultiplier, v.Y * material.TextureTMultiplier)), (w, uv) => (w.v, w.n, uv))
                            .Zip(mesh.Tangents.Select(v => new Vector3(v.X, v.Y, v.Z)), (w, t) => (w.v, w.n, w.uv, t))
                            .Zip(mesh.BiTangents.Select(v => new Vector3(v.X, v.Y, v.Z)), (w, bt) => (w.v, w.n, w.uv, w.t, bt))
                            .Zip(vertexBoneInfluence, (w, bi) => (w.v, w.n, w.uv, w.t, w.bt, bi.ids, bi.weights))
                            .Select(w => new Vertex(w.v, w.n, w.uv, w.t, w.bt, w.ids, w.weights))
                            .ToArray(mesh.Vertices.Count),
                        indices.Cast<ushort>().ToArray(indices.Length)),
                    diffuseTexture,
                    normalTexture,
                    lightProgram: ShaderProgramCache.Get(lightShaderName, shader =>
                    {
                        shader.UniformBlockBind("matrices", 0);
                        shader.Set("material.diffuse", 1);
                        if (normalTexture is not null)
                            shader.Set("material.normal", 2);
                    }),
                    shadowProgram: ShaderProgramCache.Get("object-shadow", shader =>
                    {
                    }),
                    modelTransform: transformsDictionary[mesh],
                    boneOffsetMatrices: mesh.BoneCount == 0 ? null : mesh.Bones.ToDictionary(b => boneIds[b.Name], b => b.OffsetMatrix.ToNumerics()));
            }
        }

        public void Update(double deltaSec)
        {
            if (CurrentAnimationName is null) return;

            var anim = animations[CurrentAnimationName];
            currentAnimationSec = (currentAnimationSec + deltaSec) % anim.Duration.TotalSeconds;

            void calculateBoneTransforms(BoneNode boneNode, Matrix4x4 parentTransform)
            {
                var bone = anim.Bones.FirstOrDefault(b => b.Id == boneNode.Id);
                var nodeTransform = bone?[TimeSpan.FromSeconds(currentAnimationSec)] ?? boneNode.Transform;
                var globalTransform = parentTransform * nodeTransform;
                
            }
            calculateBoneTransforms(rootBoneNode, Matrix4x4.Identity);
        }

        public void Draw(bool shadowPass)
        {
            foreach (var perMeshDataItem in perMeshData)
            {
                if (shadowPass)
                {
                    perMeshDataItem.shadowProgram.Use();
                    perMeshDataItem.shadowProgram.Set("model", perMeshDataItem.modelTransform, true);
                }
                else
                {
                    perMeshDataItem.diffuseTexture?.Bind(1);
                    perMeshDataItem.normalTexture?.Bind(2);
                    perMeshDataItem.lightProgram.Use();
                    perMeshDataItem.lightProgram.Set("model", perMeshDataItem.modelTransform, true);
                    perMeshDataItem.lightProgram.Set("material.shininess", material.Shininess);
                    if (animations.Any())
                        perMeshDataItem.lightProgram.Set("finalBoneMatrices[0]", ref finalBoneMatrices[0], finalBoneMatrices.Length, true);
                }

                perMeshDataItem.vao.Draw(PrimitiveType.Triangles);
            }
        }
    }
}
