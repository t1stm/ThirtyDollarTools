#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;

namespace ThirtyDollarVisualizer
{
    public struct ShaderData
    {
        public string? FilePath;
        public string Code;
        public ShaderType Type;
    }
    public class Shader
    {
        private int Program;
        private ShaderData[] _sources = Array.Empty<ShaderData>();
        private Dictionary<string, int> _uniformLocations = new();

        private static int CompileShader(ShaderType type, string code)
        {
            const int FAIL = 0;
            var shader = GL.CreateShader(type);
            
            GL.ShaderSource(shader, code);
            GL.CompileShader(shader);
            
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var result);
            if (result != FAIL) return shader;
            
            GL.GetShaderInfoLog(shader, out var message);
            GL.DeleteShader(shader);
            throw new Exception($"\'{type}\' compilation failed with message: \"{message}\"");
        }
        
        public static int CreateShaderProgram(params ShaderData[] shaderFiles)
        {
            
            var program = GL.CreateProgram();
            
            foreach (var shaderFile in shaderFiles)
            {
                var compiledShader = CompileShader(shaderFile.Type, shaderFile.Code);

                GL.AttachShader(program, compiledShader);

                GL.LinkProgram(program);
                GL.ValidateProgram(program);
            
                GL.DeleteShader(compiledShader);
            }
            
            return program;
        }

        public static Shader FromFiles(string vertexShaderPath, string fragmentShaderPath)
        {
            var vertexShader = File.ReadAllText(vertexShaderPath);
            var fragmentShader = File.ReadAllText(fragmentShaderPath);
            var sources = new ShaderData[]
            {
                new()
                {
                    FilePath = vertexShaderPath,
                    Code = vertexShader,
                    Type = ShaderType.VertexShader
                },
                new()
                {
                    FilePath = fragmentShaderPath,
                    Code = fragmentShader,
                    Type = ShaderType.FragmentShader
                }
            };

            var shader = new Shader
            {
                Program = CreateShaderProgram(sources),
                _sources = sources
            };
            
            return shader;
        }

        public void SetUniform4(string name, Color4 color)
        {
            var location = GetUniformLocation(name);
            GL.Uniform4(location, color);
        }

        public int GetUniformLocation(string name)
        {
            bool found;
            int location;
            lock (_uniformLocations)
            {
                found = _uniformLocations.TryGetValue(name, out location);
            }
            if (found) return location;
            location = GL.GetUniformLocation(Program, name);
            if (location == -1) throw new Exception(
                $"Uniform \'{name}\' wasn't found in files \"{_sources.Select(r => $"{r.FilePath} ").ToArray().ToString()?.Trim()}\".");
            lock (_uniformLocations)
            {
                _uniformLocations.Add(name, location);
            }
            return location;
        }
        public void SetUniform4(string name, float r, float g, float b, float a) => SetUniform4(name, new Color4(r, g, b, a));
        public void Bind() => GL.UseProgram(Program);
        public void Unbind() => GL.UseProgram(0);
    }
}