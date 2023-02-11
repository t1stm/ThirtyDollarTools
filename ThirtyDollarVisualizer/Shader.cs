using System;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer
{
    public static class Shader
    {
        private static int Compile(ShaderType type, string code)
        {
            const int FAIL = 0;
            var shader = GL.CreateShader(type);
            
            GL.ShaderSource(shader, code);
            GL.CompileShader(shader);
            
            GL.GetShader(shader, ShaderParameter.CompileStatus, out var result);
            if (result != FAIL) return shader;
            
            GL.GetShaderInfoLog(shader, out var message);
            Console.WriteLine($"\'{type}\' compilation failed with message: \"{message}\"");
            GL.DeleteShader(shader);
            
            return 0;
        }
        
        public static int Create(string vertexShader, string fragmentShader)
        {
            var program = GL.CreateProgram();

            var vertex = Compile(ShaderType.VertexShader, vertexShader);
            var fragment = Compile(ShaderType.FragmentShader, fragmentShader);
            
            GL.AttachShader(program, vertex);
            GL.AttachShader(program, fragment);
            
            GL.LinkProgram(program);
            GL.ValidateProgram(program);
            
            GL.DeleteShader(vertex);
            GL.DeleteShader(fragment);
            
            return program;
        }

        public static int FromFiles(string vertexShaderPath, string fragmentShaderPath)
        {
            var vertexShader = File.ReadAllText(vertexShaderPath);
            var fragmentShader = File.ReadAllText(fragmentShaderPath);
            
            return Create(vertexShader, fragmentShader);
        }
    }
}