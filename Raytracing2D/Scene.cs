using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Raytracing2D
{
    interface Primitive
    {
        (Vector2, Vector2) GetAABB();
        int GetMaterialId();
        void Move(Vector2 delta);
        bool IsPointInside(Vector2 point);
    }

    struct Circle : Primitive
    {
        public Vector2 center;
        public float radius;
        public int material_id;

        public const int size = sizeof(float) * 4;

        public (Vector2, Vector2) GetAABB()
        {
            var r_vec = new Vector2(radius, radius);
            return (center - r_vec, center + r_vec);
        }

        public int GetMaterialId()
        {
            return material_id;
        }

        public void Move(Vector2 delta)
        {
            center += delta;
        }

        public bool IsPointInside(Vector2 point)
        {
            return (point - center).LengthSquared <= radius * radius;
        }
    }

    struct Triangle : Primitive
    {
        public Vector2 vert_0;
        public Vector2 vert_1;
        public Vector2 vert_2;
        public int material_id;
        float pad0;

        public const int size = sizeof(float) * 8;

        public Triangle(Vector2 v0, Vector2 v1, Vector2 v2, int material_id)
        {
            // store as CW
            var normal_01 = v1 - v0;
            normal_01 = new Vector2(-normal_01.Y, normal_01.X);

            vert_0 = v0;

            if (Vector2.Dot(normal_01, v2 - v0) < 0.0f)
            {
                vert_1 = v1;
                vert_2 = v2;
            }
            else
            {
                vert_1 = v2;
                vert_2 = v1;
            }
            
            this.material_id = material_id;
            pad0 = 0;
        }

        public (Vector2, Vector2) GetAABB()
        {
            Vector2 min = Vector2.ComponentMin(Vector2.ComponentMin(vert_0, vert_1), vert_2);
            Vector2 max = Vector2.ComponentMax(Vector2.ComponentMax(vert_0, vert_1), vert_2);

            return (min, max);
        }

        public int GetMaterialId()
        {
            return material_id;
        }

        public void Move(Vector2 delta)
        {
            vert_0 += delta;
            vert_1 += delta;
            vert_2 += delta;
        }

        public bool IsPointInside(Vector2 point)
        {
            var normal_01 = vert_1 - vert_0;
            normal_01 = new Vector2(-normal_01.Y, normal_01.X);

            if (Vector2.Dot(point - vert_0, normal_01) > 0.0f)
                return false;

            var normal_12 = vert_2 - vert_1;
            normal_12 = new Vector2(-normal_12.Y, normal_12.X);

            if (Vector2.Dot(point - vert_1, normal_12) > 0.0f)
                return false;

            var normal_02 = vert_0 - vert_2;
            normal_02 = new Vector2(-normal_02.Y, normal_02.X);

            if (Vector2.Dot(point - vert_2, normal_02) > 0.0f)
                return false;

            return true;
        }
    }

    public struct Material
    {
        public float emissive;
        public float reflective;
        public float refractive;
        public float diffuse;

        public Vector3 emission_color;
        float pad0;

        public Vector3 diffuse_color;
        float pad1;
        public Vector3 eta;
        float pad2;

        public const int size = sizeof(float) * 16;

        public static Material Default()
        {
            return new Material() { 
                reflective = 0, 
                refractive = 0, 
                emissive = 0, 
                diffuse = 1, 
                diffuse_color = new Vector3(1, 0, 0), 
                emission_color = new Vector3(1, 1, 1), 
                eta = new Vector3(1.0f, 0.0f, 0.0f) 
            };
        }
    }

    public class Scene
    {
        int width = 1024;
        int height = 1024;

        List<Primitive> primitives = new List<Primitive>();
        List<Material> materials = new List<Material>();
        // x - wavelength; yzw - color
        List<Vector4> wavelength_colors = new List<Vector4>();

        int render_shader, compute_shader;
        int materials_ssbo, circles_ssbo, triangles_ssbo, wavelengths_ssbo;
        int image;

        bool dispersion = false;

        Vector3 WavelengthToColor(float wavelength)
        {
            Vector3 color = new Vector3(0.0f);

            float gamma = 0.8f;

            if (wavelength >= 380.0f && wavelength <= 440.0f)
            {
                float attenuation = 0.3f + 0.7f * (wavelength - 380.0f) / (440.0f - 380.0f);
                color.X = MathF.Pow(((-(wavelength - 440.0f) / (440.0f - 380.0f)) * attenuation), gamma);
                color.Z = MathF.Pow((1.0f * attenuation), gamma);
            }
            else if (wavelength >= 440.0f && wavelength <= 490.0f)
            {
                color.Y = MathF.Pow(((wavelength - 440.0f) / (490.0f - 440.0f)), gamma);
                color.Z = 1.0f;
            }
            else if (wavelength >= 490.0f && wavelength <= 510.0f)
            {
                color.Y = 1.0f;
                color.Z = MathF.Pow((-(wavelength - 510.0f) / (510.0f - 490.0f)), gamma);
            }
            else if (wavelength >= 510.0f && wavelength <= 580.0f)
            {
                color.X = MathF.Pow(((wavelength - 510.0f) / (580.0f - 510.0f)), gamma);
                color.Y = 1.0f;
            }
            else if (wavelength >= 580.0f && wavelength <= 645.0f)
            {
                color.X = 1.0f;
                color.Y = MathF.Pow((-(wavelength - 645.0f) / (645.0f - 580.0f)), gamma);
            }
            else if (wavelength >= 645.0f && wavelength <= 750.0f)
            {
                float attenuation = 0.3f + 0.7f * (750.0f - wavelength) / (750.0f - 645.0f);
                color.X = MathF.Pow((1.0f * attenuation), gamma);
            }

            return color;
        }

        public Scene()
        {
            materials.Add(new Material() { reflective = 0, refractive = 0, emissive = 0, diffuse = 1, diffuse_color = new Vector3(1, 0, 0), emission_color = new Vector3(1, 1, 1), eta = new Vector3(1.0f, 8000.0f, 10000.0f) });
            materials.Add(new Material() { reflective = 0, refractive = 0, emissive = 1, diffuse = 0, diffuse_color = new Vector3(1, 0, 0), emission_color = new Vector3(1, 1, 1), eta = new Vector3(1.0f, 0.0f, 0.0f) });

            primitives.Add(new Circle() { center = new Vector2(-0.5f, 0.5f), radius = 0.3f, material_id = 0 });
            primitives.Add(new Circle() { center = new Vector2(0.5f, 0.5f), radius = 0.3f, material_id = 0 });
            primitives.Add(new Circle() { center = new Vector2(-0.5f, -0.5f), radius = 0.3f, material_id = 1 });
            primitives.Add(new Circle() { center = new Vector2(0.5f, -0.5f), radius = 0.3f, material_id = 1 });

            primitives.Add(new Triangle(new Vector2(0.0f, 0.0f), new Vector2(0.5f, 0.5f), new Vector2(1.0f, 0.0f), 0));

            wavelength_colors.Add(new Vector4(500.0f, 1.0f, 1.0f, 1.0f));
        }

        public void Init(int width, int height)
        {
            this.width = width;
            this.height = height;

            render_shader = new ShaderProgram()
                .addFragmentShader("shaders/screen_quad.frag")
                .addVertexShader("shaders/screen_quad.vert")
                .Compile();
            compute_shader = new ShaderProgram()
                .addComputeShader("shaders/compute_light.comp")
                .Compile();

            float[] vertices = {-1, -1, -1, 1, 1, 1, 1, -1 };

            int VAO = GL.GenVertexArray();
            int VBO = GL.GenBuffer();

            GL.BindVertexArray(VAO);
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBO);
                GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            }

            materials_ssbo = GL.GenBuffer();
            UpdateMaterials();

            circles_ssbo = GL.GenBuffer();
            triangles_ssbo = GL.GenBuffer();
            UpdatePrimitives();

            wavelengths_ssbo = GL.GenBuffer();
            UpdateWavelengths();

            UpdateCameraPos();

            image = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, image);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMagFilter.Nearest);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindImageTexture(0, image, 0, false, 0, TextureAccess.ReadWrite, SizedInternalFormat.Rgba32f);
        }

        void UpdateMaterials()
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, materials_ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Material.size * materials.Count, materials.ToArray(), BufferUsageHint.StaticDraw);
        }

        void UpdatePrimitives()
        {
            var triangles = new List<Triangle>();
            var circles = new List<Circle>();
            for (int i = 0; i < primitives.Count; i++)
            {
                if (primitives[i] is Circle circle)
                    circles.Add(circle);
                else if (primitives[i] is Triangle triangle)
                    triangles.Add(triangle);
            }

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, circles_ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, circles.Count * Circle.size, circles.ToArray(), BufferUsageHint.StaticDraw);

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 2, triangles_ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, triangles.Count * Triangle.size, triangles.ToArray(), BufferUsageHint.StaticDraw);
        }

        void UpdateWavelengths()
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 3, wavelengths_ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, wavelength_colors.Count * Vector4.SizeInBytes, wavelength_colors.ToArray(), BufferUsageHint.StaticDraw);
        }

        Vector2 camera_min = new Vector2(-1, -1);
        Vector2 camera_max = new Vector2(1, 1);

        void UpdateCameraPos()
        {
            GL.UseProgram(compute_shader);
            GL.Uniform2(GL.GetUniformLocation(compute_shader, "camera_min"), camera_min);
            GL.Uniform2(GL.GetUniformLocation(compute_shader, "camera_max"), camera_max);
        }

        Vector2 ScreenToWorldPoint(Point screen)
        {
            var scr = new Vector2((float)screen.X, (float)screen.Y);
            var inv_screen_size = new Vector2(1.0f / width, 1.0f / height);
            return scr * inv_screen_size * (camera_max - camera_min) + camera_min;
        }

        Point WorldToScreenPoint(Vector2 world)
        {
            var screen_size = new Vector2(width, height);
            var inv_cam_size = new Vector2(1.0f / (camera_max.X - camera_min.X), 1.0f / (camera_max.Y- camera_min.Y));
            var screen = (world - camera_min) * screen_size * inv_cam_size;
            return new Point(screen.X, screen.Y);
        }

        int selected_primitive_id = -1;

        int PrimitiveIdAtPosition(Vector2 pos)
        {
            for(int i = 0; i < primitives.Count; i++)
            {
                var primitive = primitives[i];

                if (primitive.IsPointInside(pos))
                    return i;
            }

            return -1;
        }

        public bool SelectPrimitive(Point at)
        {
            selected_primitive_id = PrimitiveIdAtPosition(ScreenToWorldPoint(at));
            return selected_primitive_id != -1;
        }

        public Rect? GetSelectedAABB()
        {
            if (selected_primitive_id == -1)
                return null;

            var (min, max) = primitives[selected_primitive_id].GetAABB();
            var aabb_size = WorldToScreenPoint(max) - WorldToScreenPoint(min);
            return new Rect(WorldToScreenPoint(min), new Size(aabb_size.X, aabb_size.Y));
        }

        public void DeleteSelectedPrimitive()
        {
            if (selected_primitive_id == -1)
                return;

            primitives.RemoveAt(selected_primitive_id);

            selected_primitive_id = -1;

            UpdatePrimitives();
            ResetRender();
        }

        public Material GetCurrentSelectedMaterial()
        {
            return materials[primitives[selected_primitive_id].GetMaterialId()];
        }

        public void SetMaterialToSelected(Material material)
        {
            var primitive = primitives[selected_primitive_id];
            materials[primitive.GetMaterialId()] = material;

            UpdateMaterials();
            ResetRender();
        }

        public void MoveSelected(Vector delta)
        {
            var move_delta = ScreenToWorldPoint(new Point(delta.X, delta.Y)) - ScreenToWorldPoint(new Point());
            primitives[selected_primitive_id].Move(move_delta);

            UpdatePrimitives();
            ResetRender();
        }

        public void MoveCamera(Vector delta)
        {
            var move_delta = ScreenToWorldPoint(new Point(delta.X, delta.Y)) - ScreenToWorldPoint(new Point());
            camera_min -= move_delta;
            camera_max -= move_delta;

            UpdateCameraPos();
            ResetRender();
        }

        public void ScaleView(float scale_delta)
        {
            var center = (camera_min + camera_max) * 0.5f;
            camera_min = center + (camera_min - center) * scale_delta;
            camera_max = center + (camera_max - center) * scale_delta;

            UpdateCameraPos();
            ResetRender();
        }

        List<Vector2> primitive_points = new List<Vector2>();
        bool creating_circle = false;
        bool creating_triangle = false;

        public void StartCircleCreate()
        {
            creating_triangle = false;
            creating_circle = true;

            primitive_points.Clear();
        }

        public void StartTriangleCreate()
        {
            creating_circle = false;
            creating_triangle = true;

            primitive_points.Clear();
        }

        public bool AddPrimitivePoint(Point point)
        {
            if (creating_circle)
            {
                primitive_points.Add(ScreenToWorldPoint(point));

                if (primitive_points.Count == 2)
                {
                    creating_circle = false;
                    var r = (primitive_points[1] - primitive_points[0]).Length;

                    materials.Add(Material.Default());
                    primitives.Add(new Circle() { center = primitive_points[0], radius = r, material_id = materials.Count - 1 });

                    primitive_points.Clear();

                    UpdateMaterials();
                    UpdatePrimitives();
                    ResetRender();
                }
            }
            else if (creating_triangle)
            {
                primitive_points.Add(ScreenToWorldPoint(point));

                if(primitive_points.Count == 3)
                {
                    creating_triangle = false;

                    materials.Add(Material.Default());
                    primitives.Add(new Triangle(primitive_points[0], primitive_points[1], primitive_points[2], materials.Count - 1));

                    primitive_points.Clear();

                    UpdateMaterials();
                    UpdatePrimitives();
                    ResetRender();
                }
            }
            else return false;

            return true;
        }

        public List<Point> GetPrimitivePoints()
        {
            var points = new List<Point>(primitive_points.Count);
            foreach (var point in primitive_points)
                points.Add(WorldToScreenPoint(point));
            return points;
        }

        public void AbortPrimitiveCreate()
        {
            creating_circle = false;
            creating_triangle = false;
        }

        public void SetDispersion(bool dispersion)
        {
            if (this.dispersion == dispersion)
                return;

            this.dispersion = dispersion;

            wavelength_colors.Clear();

            if (dispersion)
            {
                float[] wavelengths = new float[41];

                for (int i = 0; i < wavelengths.Length; i++)
                    wavelengths[i] = 380 + 10 * i;

                foreach (var wavelength in wavelengths)
                {
                    var color = WavelengthToColor(wavelength);
                    wavelength_colors.Add(new Vector4(wavelength, color.X, color.Y, color.Z));
                }
            }
            else
            {
                wavelength_colors.Add(new Vector4(500.0f, 1.0f, 1.0f, 1.0f));
            }

            UpdateWavelengths();
            ResetRender();
        }

        void ResetRender()
        {
            iterations = 0;
            GL.ClearTexImage(image, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        }

        Random random = new Random();
        int iterations = 0;

        public void Render()
        {
            GL.UseProgram(compute_shader);
            GL.Uniform1(GL.GetUniformLocation(compute_shader, "random_seed"), (float)random.NextDouble());
            GL.DispatchCompute(width / 32, height / 32, 1);
            iterations++;

            GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

            GL.UseProgram(render_shader);
            GL.Uniform1(GL.GetUniformLocation(render_shader, "iterations"), (float)iterations);
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4);
        }
    }
}
