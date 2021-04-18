using OpenTK.Graphics.OpenGL4;
using System.IO;

namespace Raytracing2D
{
    class ShaderProgram
    {
        int shader_program;

        public ShaderProgram()
        {
            shader_program = GL.CreateProgram();
        }

        public int Compile()
        {
            GL.LinkProgram(shader_program);

            string log = GL.GetProgramInfoLog(shader_program);
            //if (log != "")
                //throw new System.Exception(log);

            return shader_program;
        }

        public ShaderProgram addFragmentShader(string source)
        {
            addShader(source, ShaderType.FragmentShader);
            return this;
        }

        public ShaderProgram addVertexShader(string source)
        {
            addShader(source, ShaderType.VertexShader);
            return this;
        }

        public ShaderProgram addGeometryShader(string source)
        {
            addShader(source, ShaderType.GeometryShader);
            return this;
        }

        public ShaderProgram addComputeShader(string source)
        {
            addShader(source, ShaderType.ComputeShader);
            return this;
        }

        public ShaderProgram addTessControlShader(string source)
        {
            addShader(source, ShaderType.TessControlShader);
            return this;
        }

        public ShaderProgram addTessEvaluationShader(string source)
        {
            addShader(source, ShaderType.TessEvaluationShader);
            return this;
        }

        void addShader(string source, ShaderType type)
        {
            string code = new StreamReader(source).ReadToEnd();
            int shader = GL.CreateShader(type);
            GL.ShaderSource(shader, code);
            GL.CompileShader(shader);

            string log = GL.GetShaderInfoLog(shader);
            if (log != "")
                throw new System.Exception(log);

            GL.AttachShader(shader_program, shader);
        }
    }
}
