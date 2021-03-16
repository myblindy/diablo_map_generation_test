using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace dclmgd.Renderer
{
    class ShaderProgram
    {
        readonly int programName;
        readonly Dictionary<string, int> attributeLocations = new();

        public ShaderProgram(string vsPath, string fsPath, string geomPath = null)
        {
            static int CompileShader(ShaderType type, string path)
            {
                var name = GL.CreateShader(type);
                GL.ShaderSource(name, File.ReadAllText(path));
                GL.CompileShader(name);

                GL.GetShader(name, ShaderParameter.CompileStatus, out var status);
                if (status == 0)
                    throw new InvalidOperationException($"Compilation errors for '{path}':\n\n{GL.GetShaderInfoLog(name)}");

                return name;
            }

            var vsName = CompileShader(ShaderType.VertexShader, Path.Combine("Data", "Shaders", vsPath));
            var fsName = CompileShader(ShaderType.FragmentShader, Path.Combine("Data", "Shaders", fsPath));
            string fullGeomPath = geomPath is null ? null : Path.Combine("Data", "Shaders", geomPath);
            var geomName = fullGeomPath is not null && File.Exists(fullGeomPath) ? CompileShader(ShaderType.GeometryShader, fullGeomPath) : 0;

            programName = GL.CreateProgram();
            GL.AttachShader(programName, vsName);
            GL.AttachShader(programName, fsName);
            if (geomName != 0) GL.AttachShader(programName, geomName);
            GL.LinkProgram(programName);

            GL.GetProgram(programName, GetProgramParameterName.LinkStatus, out var status);
            if (status == 0)
                throw new InvalidOperationException($"Linking errors for '{vsPath}', '{fsPath}' and '{geomPath}':\n\n{GL.GetProgramInfoLog(programName)}");

            GL.DetachShader(programName, vsName);
            GL.DeleteShader(vsName);
            GL.DetachShader(programName, fsName);
            GL.DeleteShader(fsName);
            if (geomName != 0)
            {
                GL.DetachShader(programName, geomName);
                GL.DeleteShader(geomName);
            }

            GL.GetProgram(programName, GetProgramParameterName.ActiveUniformMaxLength, out var activeUniformMaxLength);
            GL.GetProgram(programName, GetProgramParameterName.ActiveUniforms, out var uniformCount);
            for (int i = 0; i < uniformCount; ++i)
            {
                GL.GetActiveUniformName(programName, i, Math.Max(1, activeUniformMaxLength), out _, out var name);

                if (name.EndsWith("[0]"))
                    for (int idx = 0; ; ++idx)
                    {
                        string arrayName = name[..^3] + $"[{idx}]";
                        int location = GL.GetUniformLocation(programName, arrayName);

                        if (location >= 0)
                            attributeLocations[arrayName] = location;
                        else
                            break;
                    }
                else
                {
                    int location = GL.GetUniformLocation(programName, name);

                    if (location >= 0)
                        attributeLocations[name] = location;
                }
            }

            GL.GetProgram(programName, GetProgramParameterName.ActiveUniformBlockMaxNameLength, out var xctiveUniformBlockMaxNameLength);
            GL.GetProgram(programName, GetProgramParameterName.ActiveUniformBlocks, out var uniformBlockCount);
            for (int i = 0; i < uniformBlockCount; ++i)
            {
                GL.GetActiveUniformBlockName(programName, i, Math.Max(1, xctiveUniformBlockMaxNameLength), out _, out var name);
                int location = GL.GetUniformBlockIndex(programName, name);

                if (location >= 0)
                    attributeLocations[name] = location;
            }
        }

        public ShaderProgram(string path) : this(path + ".vert", path + ".frag", path + ".geom") { }

        public void Use() => GL.UseProgram(programName);

        public void Set(string name, Matrix4x4 mat, bool transpose) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], 1, transpose, ref mat.M11);
        public void Set(string name, ref Matrix4x4 mat, bool transpose) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], 1, transpose, ref mat.M11);
        public void Set(string name, Matrix4x4 mat, int count, bool transpose) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], count, transpose, ref mat.M11);
        public void Set(string name, ref Matrix4x4 mat, int count, bool transpose) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], count, transpose, ref mat.M11);

        public void Set(string name, Vector3 vec) => GL.ProgramUniform3(programName, attributeLocations[name], 1, ref vec.X);
        public void Set(string name, ref Vector3 vec) => GL.ProgramUniform3(programName, attributeLocations[name], 1, ref vec.X);

        public void Set(string name, int val) => GL.ProgramUniform1(programName, attributeLocations[name], val);

        public void Set(string name, float val) => GL.ProgramUniform1(programName, attributeLocations[name], val);
        public bool TrySet(string name, float val) { var found = attributeLocations.TryGetValue(name, out var id); if (found) GL.ProgramUniform1(programName, id, val); return found; }

        public void UniformBlockBind(string uniformVariableName, int bindingPoint) =>
            GL.UniformBlockBinding(programName, attributeLocations[uniformVariableName], bindingPoint);
    }

    static class ShaderProgramCache
    {
        static readonly Dictionary<string, ShaderProgram> cache = new();

        public static ShaderProgram Get(string name, Action<ShaderProgram> init = null)
        {
            if (!cache.TryGetValue(name, out var program))
            {
                cache[name] = program = new(name);
                if (init is not null) init(program);
            }
            return program;
        }
    }
}
