using OpenTK.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Raytracing2D
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.KeyDown += (s, e) => {
                if (primitive_selected && e.Key == Key.Delete)
                {
                    scene.DeleteSelectedPrimitive();
                    Unselect();
                }
            };

            var settings = new GLWpfControlSettings
            {
                MajorVersion = 4,
                MinorVersion = 3
            };
            RenderRegion.Start(settings);
        }

        Scene scene = new Scene();
        bool initialized = false;

        bool primitive_selected = false;

        void Redraw(TimeSpan delta)
        {
            if (!initialized)
            {
                initialized = true;
                scene.Init(1024, 1024);
            }

            scene.Render();
        }

        private void RenderRegionMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
                return;

            var click_pos = e.GetPosition(RenderRegion);
            click_pos.Y = RenderRegion.ActualHeight - click_pos.Y;

            if(scene.AddPrimitivePoint(click_pos))
            {
                UpdatePrimitiveCreationPoints();
            } 
            else if (scene.SelectPrimitive(click_pos))
            {
                var rect = (Rect)scene.GetSelectedAABB();

                Selection.Width = rect.Width;
                Selection.Height = rect.Height;
                Canvas.SetLeft(Selection, rect.X);
                Canvas.SetBottom(Selection, rect.Y);

                // to avoid restarting render due to value change
                primitive_selected = false;

                var material = scene.GetCurrentSelectedMaterial();
                DiffuseSlider.Value = material.diffuse;
                ReflectiveSlider.Value = material.reflective;
                RefractiveSlider.Value = material.refractive;
                EmissiveSlider.Value = material.emissive;
                DiffusePicker.SelectedColor = Color.FromRgb((byte)(material.diffuse_color.X * 255.0f), (byte)(material.diffuse_color.Y * 255.0f), (byte)(material.diffuse_color.Z * 255.0f));
                EmissionPicker.SelectedColor = Color.FromRgb((byte)(material.emission_color.X * 255.0f), (byte)(material.emission_color.Y * 255.0f), (byte)(material.emission_color.Z * 255.0f));
                EtaInputA.Value = material.eta.X;
                EtaInputB.Value = material.eta.Y;
                EtaInputC.Value = material.eta.Z;

                Selection.Visibility = Visibility.Visible;
                primitive_selected = true;

                Keyboard.Focus(RenderRegion);
            }
            else
                Unselect();

            prev_mouse_pos = click_pos;
        }

        Point prev_mouse_pos;

        List<Ellipse> primitive_points = new List<Ellipse>();

        void UpdatePrimitiveCreationPoints()
        {
            var points = scene.GetPrimitivePoints();

            while(primitive_points.Count < points.Count)
            {
                var ellipse = new Ellipse();
                ellipse.Width = 10;
                ellipse.Height = 10;
                ellipse.Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                ellipse.IsHitTestVisible = false;
                primitive_points.Add(ellipse);

                RenderRegionCanvas.Children.Add(ellipse);
            }
                
            for(int i = 0; i < primitive_points.Count; i++)
            {
                if(i < points.Count)
                {
                    Canvas.SetLeft(primitive_points[i], points[i].X - 5);
                    Canvas.SetBottom(primitive_points[i], points[i].Y - 5);
                    primitive_points[i].Visibility = Visibility.Visible;
                }
                else
                {
                    primitive_points[i].Visibility = Visibility.Hidden;
                }
            }
        }

        void Unselect()
        {
            Selection.Visibility = Visibility.Hidden;
            primitive_selected = false;
        }

        private void RenderRegionMouseMove(object sender, MouseEventArgs e)
        {
            var mouse_pos = e.GetPosition(RenderRegion);
            mouse_pos.Y = RenderRegion.ActualHeight - mouse_pos.Y;

            if (e.LeftButton == MouseButtonState.Pressed && primitive_selected)
            {// Move primitive.
                scene.MoveSelected(mouse_pos - prev_mouse_pos);

                if (scene.GetSelectedAABB() is Rect rect)
                {
                    Selection.Width = rect.Width;
                    Selection.Height = rect.Height;
                    Canvas.SetLeft(Selection, rect.X);
                    Canvas.SetBottom(Selection, rect.Y);
                }
            }
            else if(e.RightButton == MouseButtonState.Pressed)
            {// Move camera.
                Unselect();
                scene.MoveCamera(mouse_pos - prev_mouse_pos);
                UpdatePrimitiveCreationPoints();
            }

            prev_mouse_pos = mouse_pos;
        }

        private void DiffuseChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                material.diffuse = (float)e.NewValue;
                scene.SetMaterialToSelected(material);
            }
        }

        private void ReflectiveChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                material.reflective = (float)e.NewValue;
                scene.SetMaterialToSelected(material);
            }
        }

        private void RefractiveChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                material.refractive = (float)e.NewValue;
                scene.SetMaterialToSelected(material);
            }
        }

        private void EmissiveChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                material.emissive = (float)e.NewValue;
                scene.SetMaterialToSelected(material);
            }
        }

        private void RenderRegionMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Unselect();
            scene.ScaleView(1.0f - e.Delta / 1000.0f);
            UpdatePrimitiveCreationPoints();
        }

        private void DiffuseColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                var color = (Color)e.NewValue;
                material.diffuse_color = new OpenTK.Mathematics.Vector3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
                scene.SetMaterialToSelected(material);
            }
        }

        private void EmissionColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                var color = (Color)e.NewValue;
                material.emission_color = new OpenTK.Mathematics.Vector3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
                scene.SetMaterialToSelected(material);
            }
        }

        private void EtaChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if(primitive_selected)
            {
                var material = scene.GetCurrentSelectedMaterial();
                var a = EtaInputA.Value != null ? (float)(double)EtaInputA.Value : 0.0f;
                var b = EtaInputB.Value != null ? (float)(double)EtaInputB.Value : 0.0f;
                var c = EtaInputC.Value != null ? (float)(double)EtaInputC.Value : 0.0f;
                material.eta = new OpenTK.Mathematics.Vector3(a, b, c);
                scene.SetMaterialToSelected(material);
            }
        }

        private void AddSphere(object sender, RoutedEventArgs e)
        {
            scene.StartCircleCreate();
        }

        private void AddTriangle(object sender, RoutedEventArgs e)
        {
            scene.StartTriangleCreate();
        }

        private void MonochromeRays(object sender, RoutedEventArgs e)
        {
            scene.SetDispersion(!(bool)MonochromeRaysCheckBox.IsChecked);
        }
    }
}
