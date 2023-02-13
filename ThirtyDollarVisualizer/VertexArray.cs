using OpenTK.Graphics.OpenGL;

namespace ThirtyDollarVisualizer
{
    public class VertexArray<T> where T : struct
    {
        private int _vao;
        
        public VertexArray()
        {
            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);
        }

        public void AddBuffer(VertexBuffer<T> vb, VertexBufferLayout layout)
        {
            Bind();
            vb.Bind();
            var elements = layout.GetElements();
            var offset = 0;
            for (var i = 0; i < elements.Count; i++)
            {
                var el = elements[i];
                GL.EnableVertexAttribArray(i); 
                GL.VertexAttribPointer(i, el.Count, el.Type, el.Normalized, layout.GetStride(), offset);
                offset += el.Count * el.Type.GetSize();
            }
        }

        public void Bind() => GL.BindVertexArray(_vao);

        public void Unbind() => GL.BindVertexArray(0);

        ~VertexArray() => GL.DeleteVertexArray(_vao);
    }
}