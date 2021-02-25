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

        public ShaderProgram(string vsPath, string fsPath)
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

            programName = GL.CreateProgram();
            GL.AttachShader(programName, vsName);
            GL.AttachShader(programName, fsName);
            GL.LinkProgram(programName);

            GL.GetProgram(programName, GetProgramParameterName.LinkStatus, out var status);
            if (status == 0)
                throw new InvalidOperationException($"Linking errors for '{vsPath}' and '{fsPath}':\n\n{GL.GetProgramInfoLog(programName)}");

            GL.DeleteShader(vsName);
            GL.DeleteShader(fsName);

            GL.GetProgram(programName, GetProgramParameterName.ActiveUniformMaxLength, out var activeUniformMaxLength);
            GL.GetProgram(programName, GetProgramParameterName.ActiveUniforms, out var uniformCount);
            for (int i = 0; i < uniformCount; ++i)
            {
                GL.GetActiveUniformName(programName, i, Math.Max(1, activeUniformMaxLength), out _, out var name);
                int location = GL.GetUniformLocation(programName, name);

                if (location >= 0)
                    attributeLocations[name] = location;
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

        public ShaderProgram(string path) : this(path + ".vert", path + ".frag") { }

        public void Use() => GL.UseProgram(programName);

        public void Set(string name, Matrix4x4 mat) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], 1, true, ref mat.M11);
        public void Set(string name, ref Matrix4x4 mat) => GL.ProgramUniformMatrix4(programName, attributeLocations[name], 1, false, ref mat.M11);

        public void Set(string name, Vector3 vec) => GL.ProgramUniform3(programName, attributeLocations[name], 1, ref vec.X);
        public void Set(string name, ref Vector3 vec) => GL.ProgramUniform3(programName, attributeLocations[name], 1, ref vec.X);

        public void Set(string name, int val) => GL.ProgramUniform1(programName, attributeLocations[name], val);
        public void Set(string name, float val) => GL.ProgramUniform1(programName, attributeLocations[name], val);

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
