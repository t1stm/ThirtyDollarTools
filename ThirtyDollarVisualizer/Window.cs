using System;
using OpenTK.Graphics.OpenGL;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ThirtyDollarVisualizer
{
    public class Window : GameWindow
    {
        public Window(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings) : base(gameWindowSettings, nativeWindowSettings)
        {
        }
        
        private int _vbo;
        private int _vao;

        protected override void OnLoad()
        {
            GL.ClearColor(.0f, .0f, .0f,1.0f);
            const int VERTEX_COUNT = 2;
            float[] positions = 
            {
                -0.5f, -0.5f,
                0.0f, 0.5f,
                0.5f, -0.5f
            };
            
            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, sizeof(float) * positions.Length, positions, BufferUsageHint.StaticDraw);

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);
            
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, sizeof(float) * VERTEX_COUNT,0);
            GL.EnableVertexAttribArray(0);

            var shader = Shader.FromFiles("./Assets/shader.vert", "./Assets/shader.frag");
            GL.UseProgram(shader);
            
            base.OnLoad();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            
            GL.BindVertexArray(_vao);
            
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            
            SwapBuffers();
            //base.OnRenderFrame(args);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            var mouse = MouseState;
            
            base.OnUpdateFrame(args);
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
            base.OnResize(e);
        }

        protected override void OnUnload()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
            
            base.OnUnload();
        }
    }
}