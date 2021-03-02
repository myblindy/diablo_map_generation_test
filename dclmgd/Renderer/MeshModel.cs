﻿using Assimp;
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
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;

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

        struct PerMeshData
        {
            public readonly VertexArrayObject<Vertex, ushort> vao;
            public readonly Texture2D diffuseTexture, normalTexture;
            public readonly ShaderProgram lightProgram, shadowProgram;
            public readonly Matrix4x4 modelTransform;

            public PerMeshData(VertexArrayObject<Vertex, ushort> vao, Texture2D diffuseTexture, Texture2D normalTexture, ShaderProgram lightProgram, ShaderProgram shadowProgram, Matrix4x4 modelTransform) =>
                (this.vao, this.diffuseTexture, this.normalTexture, this.lightProgram, this.shadowProgram, this.modelTransform) =
                    (vao, diffuseTexture, normalTexture, lightProgram, shadowProgram, modelTransform);
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

        public MeshModel(string path)
        {
            var scene = assimpContext.ImportFile(Path.Combine(path, "model.dae"), PostProcessSteps.FlipUVs | PostProcessSteps.OptimizeGraph | PostProcessSteps.OptimizeMeshes
                /*| PostProcessSteps.RemoveRedundantMaterials*/ | PostProcessSteps.Triangulate | PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.LimitBoneWeights
                /*| PostProcessSteps.MakeLeftHanded*/ | PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.CalculateTangentSpace);

            // calculate the per-mesh transforms
            var transformsDictionary = new Dictionary<Mesh, Matrix4x4>();

            void visitTransforms(Node node, Matrix4x4 mat)
            {
                mat = node.Transform.ToNumerics() * mat;
                foreach (var meshIndex in node.MeshIndices)
                    transformsDictionary[scene.Meshes[meshIndex]] = mat;
                foreach (var child in node.Children)
                    visitTransforms(child, mat);
            }
            visitTransforms(scene.RootNode, Matrix4x4.Identity);

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
                    .SelectMany((bone, BoneIdx) => bone.VertexWeights.Select(vw => (BoneIdx, vw.VertexID, vw.Weight)))
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
                    modelTransform: transformsDictionary[mesh]);
            }
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
                }

                perMeshDataItem.vao.Draw(PrimitiveType.Triangles);
            }
        }
    }
}
