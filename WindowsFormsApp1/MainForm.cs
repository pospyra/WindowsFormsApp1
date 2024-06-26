﻿using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace WindowsFormsApp1
{
    public partial class MainForm : Form
    {
        private Bitmap bitmap;
        private PictureBox pictureBox;
        private List<System.Drawing.Point> linePoints;
        private bool isDrawing;
        private Mat currentFrame;
        private Timer cameraTimer;
        private OpenCvSharp.VideoCapture videoCapture;
        private Button buttonLoadImage;
        private Button buttonOpenDeviceProcessing;
        private Button buttonStartCamera;
        private Button stopVideoButton;
        private Button buttonSaveResults;
        private SplitContainer splitContainer;
        private Label labelResult;
        private Chart chart;
        private bool isContrastCalculated;
        private bool isGraphDisplayed;
        private bool isCameraActive;

        public MainForm()
        {
            InitializeComponent();
            InitializeForm();
        }

        private void InitializeForm()
        {
            this.Width = 1000;
            this.Height = 800;

            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };

            Controls.Add(splitContainer);

            pictureBox = new PictureBox
            {
                Name = "pictureBox",
                SizeMode = PictureBoxSizeMode.Zoom,
                AllowDrop = true,
                BackColor = Color.WhiteSmoke,
                Anchor = AnchorStyles.None
            };

            pictureBox.Width = 500;
            pictureBox.Height = 400;
            pictureBox.BackColor = Color.Transparent;

            pictureBox.Location = new System.Drawing.Point(
                (splitContainer.Panel1.Width - pictureBox.Width) / 2,
                (splitContainer.Panel1.Height - pictureBox.Height) / 2
            );
            splitContainer.Panel1.Controls.Add(pictureBox);

            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.DragEnter += PictureBox_DragEnter;
            pictureBox.DragDrop += PictureBox_DragDrop;

            buttonOpenDeviceProcessing = new Button
            {
                Text = "Настройки",
                Dock = DockStyle.Top
            };
            buttonOpenDeviceProcessing.Click += ButtonOpenDeviceProcessing_Click;
            buttonOpenDeviceProcessing.Enabled = false;
            Controls.Add(buttonOpenDeviceProcessing);

            buttonLoadImage = new Button
            {
                Name = "buttonLoadImage",
                Text = "Загрузить картинку",
                Dock = DockStyle.Top
            };
            buttonLoadImage.Click += ButtonLoadImage_Click;
            Controls.Add(buttonLoadImage);

            buttonStartCamera = new Button
            {
                Name = "buttonStartCamera",
                Text = "Включить камеру",
                Dock = DockStyle.Top
            };
            buttonStartCamera.Click += ButtonStartCamera_Click;
            Controls.Add(buttonStartCamera);

            stopVideoButton = new Button
            {
                Name = "StopVideoButton",
                Text = "Захватить кадр",
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 0)
            };
            stopVideoButton.Click += StopVideoButton_Click;
            stopVideoButton.Enabled = false;
            Controls.Add(stopVideoButton);

            buttonSaveResults = new Button
            {
                Name = "buttonSaveResults",
                Text = "Сохранить результат",
                Dock = DockStyle.Top,
                Margin = new Padding(0, 5, 0, 0)
            };
            buttonSaveResults.Click += ButtonSaveResults_Click;
            buttonSaveResults.Enabled = false;
            Controls.Add(buttonSaveResults);

            labelResult = new Label
            {
                Name = "labelResult",
                Dock = DockStyle.Bottom,
                Font = new Font("Arial", 18, FontStyle.Regular),
                AutoSize = true
            };
            Controls.Add(labelResult);

            isCameraActive = false;
        }

        public void ApplySettings(double hue, double saturation, bool whiteBalance, double brightness, double contrast, double exposure, double sharpness, double gamma, double backlightCompensation)
        {
            if (bitmap == null)
            {
                return;
            }

            Mat img = BitmapConverter.ToMat(bitmap);
            Mat modifiedImg = img.Clone();

            ImageEditor editor = new ImageEditor();

            editor.ApplyColorSettings(ref modifiedImg, hue, saturation, whiteBalance);
            editor.ApplyExposureSettings(ref modifiedImg, (int)(brightness), contrast, exposure);
            editor.ApplyImageSettings(ref modifiedImg, sharpness, gamma, backlightCompensation);

            Bitmap modifiedBitmap = BitmapConverter.ToBitmap(modifiedImg);

            img.Dispose();
            modifiedImg.Dispose();

            pictureBox.Image = modifiedBitmap;
        }

        private void ButtonOpenDeviceProcessing_Click(object sender, EventArgs e)
        {
            isDrawing = false;
            linePoints = null;
            pictureBox.Refresh();

            if (chart != null)
            {
                chart.Series.Clear();
            }
            DeviceProcessing deviceProcessing = new DeviceProcessing(this);
            deviceProcessing.ShowDialog();
        }

        private void ButtonLoadImage_Click(object sender, EventArgs e)
        {
            isCameraActive = false;

            if (cameraTimer != null)
            {
                cameraTimer.Stop();
                cameraTimer.Dispose();
                cameraTimer = null;
            }

            pictureBox.Image = null;
            if (chart != null)
            {
                chart.Series.Clear();
            }

            labelResult.Text = string.Empty;

            if (bitmap != null)
            {
                bitmap.Dispose();
                bitmap = null;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png",
                Title = "Выбрать файл изображения"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                bitmap = new Bitmap(openFileDialog.FileName);
                pictureBox.Image = bitmap;

                stopVideoButton.Enabled = false;
                buttonSaveResults.Enabled = false;
            }

            if (pictureBox.Image != null)
            {
                buttonOpenDeviceProcessing.Enabled = true;
            }

            if (linePoints == null || linePoints.Count < 2)
            {

                if (chart != null)
                {
                    chart.Series.Clear();
                }
            }
        }

        private void ButtonStartCamera_Click(object sender, EventArgs e)
        {
            pictureBox.Image = null;
            labelResult.Text = string.Empty;

            if (cameraTimer != null || videoCapture != null)
            {
                CameraUtility.Instance.ReleaseCameraAndTimer(cameraTimer, videoCapture);
            }

            if (chart != null)
            {
                chart.Series.Clear();
            }

            buttonStartCamera.Enabled = false;
            stopVideoButton.Enabled = false;
            buttonOpenDeviceProcessing.Enabled = false;

            using (var deviceSettingForm = new DeviceSettingForm())
            {
                if (deviceSettingForm.ShowDialog() == DialogResult.OK)
                {
                    var selectedCamera = deviceSettingForm.SelectedCamera;
                    var selectedFormat = deviceSettingForm.SelectedFormat;
                    var selectedFPS = deviceSettingForm.SelectedFPS;

                    InitializeCamera(selectedCamera, selectedFormat, selectedFPS);
                }
                else
                {
                    buttonStartCamera.Enabled = true;
                }
            }
        }

        private void ButtonDisplayGraph()
        {
            if (bitmap == null)
            {
                MessageBox.Show("Сначала требуется загрузить изображение");
                return;
            }

            if (linePoints == null || linePoints.Count < 2)
            {
                MessageBox.Show("Сначала требуется выбрать участок на изображении");
                return;
            }

            chart = new Chart
            {
                Size = new System.Drawing.Size(splitContainer.Panel2.Width, splitContainer.Panel2.Height),
                Dock = DockStyle.Fill
            };

            ChartArea chartArea = new ChartArea();
            chart.ChartAreas.Add(chartArea);

            Series series = new Series
            {
                ChartType = SeriesChartType.Line,
                Color = Color.Blue
            };

            foreach (System.Drawing.Point point in linePoints)
            {
                (double transformedX, double transformedY) = TransformPointToImage(point);

                Color pixelColor = bitmap.GetPixel((int)transformedX, (int)transformedY);
                double brightness = pixelColor.GetBrightness();

                series.Points.AddXY(point.X, brightness);
            }

            chart.Series.Add(series);

            splitContainer.Panel2.Controls.Clear();
            splitContainer.Panel2.Controls.Add(chart);

            isGraphDisplayed = true;

            buttonSaveResults.Enabled = isGraphDisplayed && isContrastCalculated;
        }

        private void CalculateContrast()
        {
            if (bitmap == null)
            {
                MessageBox.Show("Сначала требуется загрузить изображение");
                return;
            }

            if (linePoints == null || linePoints.Count < 2)
            {
                MessageBox.Show("Сначала требуется выбрать участок на изображении");
                return;
            }

            double maxBrightness = double.MinValue;
            double minBrightness = double.MaxValue;

            foreach (System.Drawing.Point point in linePoints)
            {
                (double transformedX, double transformedY) = TransformPointToImage(point);

                Color pixelColor = bitmap.GetPixel((int)transformedX, (int)transformedY);
                double brightness = pixelColor.GetBrightness();

                maxBrightness = Math.Max(maxBrightness, brightness);
                minBrightness = Math.Min(minBrightness, brightness);
            }

            double contrast = (maxBrightness - minBrightness) / maxBrightness;

            labelResult.Text = $"Контраст вдоль линии: {contrast:F3}";

            isContrastCalculated = true;

            buttonSaveResults.Enabled = isGraphDisplayed && isContrastCalculated;
        }

        private void ButtonSaveResults_Click(object sender, EventArgs e)
        {
            if (pictureBox.Image == null || splitContainer.Panel2.Controls.OfType<Chart>().FirstOrDefault() == null)
            {
                MessageBox.Show("Сначала необходимо загрузить изображение и отобразить график");
                return;
            }

            if (linePoints == null || linePoints.Count == 0)
            {
                MessageBox.Show("Сначала необходимо выбрать участок на изображении для линии.");
                return;
            }

            if (!isContrastCalculated)
            {
                return;
            }

            Chart chart = splitContainer.Panel2.Controls.OfType<Chart>().FirstOrDefault();

            int chartWidth = chart.Width;
            int chartHeight = chart.Height;

            int pictureBoxWidth = pictureBox.Width;
            int pictureBoxHeight = pictureBox.Height;

            int textHeight = 30;
            int spacing = 20;

            int totalWidth = Math.Max(pictureBoxWidth, chartWidth);

            int totalHeight = pictureBoxHeight + chartHeight + textHeight * 3 + spacing * 4;

            Bitmap resultBitmap = new Bitmap(totalWidth, totalHeight);

            using (Graphics g = Graphics.FromImage(resultBitmap))
            {
                int offsetX = (totalWidth - pictureBoxWidth) / 2;
                int offsetY = 0;

                Bitmap pictureBoxBitmap = new Bitmap(pictureBoxWidth, pictureBoxHeight);
                pictureBox.DrawToBitmap(pictureBoxBitmap, new Rectangle(0, 0, pictureBoxWidth, pictureBoxHeight));
                g.DrawImage(pictureBoxBitmap, offsetX, offsetY);

                using (Pen pen = new Pen(Color.Red, 2))
                {
                    var translatedPoints = linePoints
                        .Select(p => new System.Drawing.Point((int)(p.X + offsetX), (int)(p.Y + offsetY)))
                        .ToArray();

                    g.DrawLines(pen, translatedPoints);
                }

                int chartOffsetX = (totalWidth - chartWidth) / 2;
                int chartOffsetY = pictureBoxHeight + spacing;

                Bitmap chartBitmap = new Bitmap(chartWidth, chartHeight);
                chart.DrawToBitmap(chartBitmap, new Rectangle(0, 0, chartWidth, chartHeight));
                g.DrawImage(chartBitmap, chartOffsetX, chartOffsetY);

                Font font = new Font("Arial", 20, FontStyle.Bold);
                Brush brush = Brushes.Black;
                string contrastText = labelResult.Text;
                string dateText = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");

                int textYPosition = pictureBoxHeight + chartHeight + spacing * 3;

                g.DrawString(contrastText, font, brush, 10, textYPosition);
                g.DrawString(dateText, font, brush, 10, textYPosition + textHeight);
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png",
                Title = "Save results"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                resultBitmap.Save(saveFileDialog.FileName);
                MessageBox.Show("Результаты сохранены.");
            }
        }


        private void StopVideoButton_Click(object sender, EventArgs e)
        {
            StopVideoAndCaptureImage();

            if (pictureBox.Image != null)
            {
                buttonOpenDeviceProcessing.Enabled = true;
                isCameraActive = false;
            }
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (isCameraActive)
            {
                return; 
            }

            if (e.Button == MouseButtons.Left && (bitmap != null || currentFrame != null))
            {
                isDrawing = true;
                linePoints = new List<System.Drawing.Point> { e.Location };
                pictureBox.Refresh();
            }
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (isCameraActive)
            {
                return;
            }

            if (isDrawing && (bitmap != null || currentFrame != null))
            {
                (double transformedX, double transformedY) = TransformPointToImage(e.Location);

                if (transformedX < 0 || transformedX >= bitmap.Width || transformedY < 0 || transformedY >= bitmap.Height)
                {
                    isDrawing = false;
                    linePoints = null;
                    pictureBox.Refresh();
                    labelResult.Text = string.Empty;
                    if (chart != null)
                    {
                        chart.Series.Clear();
                    }
                    
                    MessageBox.Show("Вы вышли за границы изображения. Линия была обнулена.");
                    return;
                }

                linePoints.Add(e.Location);

                using (Graphics g = pictureBox.CreateGraphics())
                {
                    Pen pen = new Pen(Color.Red, 2);
                    g.DrawLine(pen, linePoints[linePoints.Count - 2], e.Location);
                }
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (isCameraActive)
            {
                return;
            }

            if (isDrawing)
            {
                isDrawing = false;
                linePoints.Add(e.Location);

                ButtonDisplayGraph();

                CalculateContrast();
            }
        }

        private void PictureBox_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void PictureBox_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0)
                {
                    string filePath = files[0];
                    bitmap = new Bitmap(filePath);
                    pictureBox.Image = bitmap;

                    stopVideoButton.Enabled = false;
                }
            }
        }

        private void CameraTimer_Tick(object sender, EventArgs e)
        {
            currentFrame = new Mat();
            videoCapture.Read(currentFrame);

            if (!currentFrame.Empty())
            {
                bitmap = BitmapConverter.ToBitmap(currentFrame);
                pictureBox.Image = bitmap;
            }
        }

        private void InitializeCamera(string cameraName, string videoFormat, int fps)
        {
            isCameraActive = true;

            BackgroundWorker bgWorker = new BackgroundWorker();

            bgWorker.DoWork += (sender, e) =>
            {
                int cameraIndex = GetCameraIndex(cameraName);

                SetLoadingImage();

                videoCapture = new VideoCapture(cameraIndex);


                if (!videoCapture.IsOpened())
                {
                    Invoke(new Action(() => MessageBox.Show($"Камера '{cameraName}' не найдена. Пожалуйста, убедитесь, что камера подключена и попробуйте снова.")));
                    Invoke(new Action(() => buttonStartCamera.Enabled = true));
                    return;
                }

                videoCapture.Fps = fps;

                if (!string.IsNullOrEmpty(videoFormat) && videoFormat.Length >= 4)
                {
                    videoCapture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC(videoFormat[0], videoFormat[1], videoFormat[2], videoFormat[3]));
                }

                Invoke(new Action(() =>
                {
                    cameraTimer = new Timer
                    {
                        Interval = 1000 / fps
                    };
                    cameraTimer.Tick += CameraTimer_Tick;
                    cameraTimer.Start();

                    buttonStartCamera.Enabled = true;
                    stopVideoButton.Enabled = true;
                    buttonOpenDeviceProcessing.Enabled = true;

                    // Очищаем текущий вид PictureBox
                    pictureBox.Image = null;
                }));
            };

            bgWorker.RunWorkerAsync();
        }

        private void SetLoadingImage()
        {
            string loadingImagePath = Path.Combine(Directory.GetCurrentDirectory(), "Files", "loading.gif");

            if (File.Exists(loadingImagePath))
            {
                Invoke(new Action(() => pictureBox.Image = Image.FromFile(loadingImagePath)));
            }
            else
            {
                Invoke(new Action(() => MessageBox.Show("Изображение loading.gif не найдено.")));
            }
        }

        private int GetCameraIndex(string cameraName)
        {
            var availableCameras = CameraUtility.Instance.GetAvailableCameras();

            foreach (var camera in availableCameras)
            {
                if (string.Equals(camera.Value, cameraName, StringComparison.OrdinalIgnoreCase))
                {
                    return camera.Key;
                }
            }

            throw new ArgumentException($"Камера с именем '{cameraName}' не найдена.");
        }

        private (double transformedX, double transformedY) TransformPointToImage(System.Drawing.Point point)
        {
            double pictureBoxWidth = pictureBox.Width;
            double pictureBoxHeight = pictureBox.Height;

            double imageWidth = bitmap.Width;
            double imageHeight = bitmap.Height;

            // Вычисление коэффициентов масштабирования
            double scaleX = pictureBoxWidth / imageWidth;
            double scaleY = pictureBoxHeight / imageHeight;

            double scale = Math.Min(scaleX, scaleY);

            // Фактические размеры изображения внутри PictureBox
            double actualImageWidth = imageWidth * scale;
            double actualImageHeight = imageHeight * scale;

            // Начальные координаты (верхний левый угол) изображения внутри PictureBox
            double startX = (pictureBoxWidth - actualImageWidth) / 2.0;
            double startY = (pictureBoxHeight - actualImageHeight) / 2.0;

            // Преобразование координат из PictureBox в исходное изображение
            double transformedX = (point.X - startX) / scale;
            double transformedY = (point.Y - startY) / scale;

            return (transformedX, transformedY);
        }

        private void StopVideoAndCaptureImage()
        {
            CameraUtility.Instance.ReleaseCameraAndTimer(cameraTimer, videoCapture);

            if (currentFrame != null)
            {
                bitmap = BitmapConverter.ToBitmap(currentFrame);

                pictureBox.Image = bitmap;

                currentFrame.Dispose();
                currentFrame = null;

                stopVideoButton.Enabled = false;
            }
            else
            {
                MessageBox.Show("Не удалось захватить изображение или инициализировать таймер.");
            }
        }
    }
}
