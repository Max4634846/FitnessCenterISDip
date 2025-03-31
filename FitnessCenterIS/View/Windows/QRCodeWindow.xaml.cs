using System;
using System.Windows;
using ZXing;
using AForge.Video.DirectShow;
using AForge.Video;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Collections.Generic;
using FitnessCenterIS.Model;
using ZXing.Presentation;
using System.Collections.ObjectModel;
using BarcodeReader = ZXing.BarcodeReader;
using FitnessCenterIS.View.Pages;
using System.Windows.Threading;
using System.Data.Entity; // Make sure to include this for database access

namespace FitnessCenterIS.View.Windows
{
    public partial class QRCodeWindow : Window
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private BarcodeReader barcodeReader;
        private List<ClientsCollection> _clients;
        private bool _isScanning = false;
        private MenuWindow _menuWindow; // Добавляем ссылку на MenuWindow
        private string lastQRCode = string.Empty;
        private bool isScanFinished = false;

        public event Action<string> QRCodeScanned; // Изменено: передаем номер карты

        // Изменяем конструктор для приема MenuWindow
        public QRCodeWindow(List<ClientsCollection> clients, MenuWindow menuWindow)
        {
            InitializeComponent();
            barcodeReader = new BarcodeReader();
            _clients = clients;
            _menuWindow = menuWindow;
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем список доступных камер
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    MessageBox.Show("Камера не найдена.");
                    return;
                }

                // Подключаем первую доступную камеру
                videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);
                videoSource.NewFrame += VideoSource_NewFrame;
                videoSource.Start();
                _isScanning = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске камеры: {ex.Message}");
            }
        }
        private void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                // Выполним остановку камеры асинхронно, чтобы не блокировать UI
                System.Threading.Tasks.Task.Run(() =>
                {
                    videoSource.NewFrame -= VideoSource_NewFrame;
                    videoSource.SignalToStop();
                    videoSource.WaitForStop();
                    videoSource = null;
                });
            }
            _isScanning = false;
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                using (Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    // Отображаем видео на экране
                    Dispatcher.Invoke(() =>
                    {
                        CameraPreview.Source = BitmapToImageSource(bitmap);
                    });

                    // Распознаем QR-код
                    var result = barcodeReader.Decode(bitmap);
                    if (result != null && result.Text != lastQRCode) // Проверка на уникальность
                    {
                        lastQRCode = result.Text; // Запоминаем последний QR-код

                        // Если QR-код распознан, ищем клиента в базе данных по номеру карты и открываем его профиль
                        Dispatcher.Invoke(() =>
                        {
                            using (var context = new BDFitnessClubDipEntities())
                            {
                                var client = context.Clients
                                    .Include(c => c.Persons) // Ensure Persons are loaded if needed on ProfileClient
                                    .FirstOrDefault(c => c.NumberCard == result.Text); // Ищем по NumberCard

                                if (client != null)
                                {
                                    // Открываем ProfileClientPage в MainFrame MenuWindow
                                    ProfileClient profileClientPage = new ProfileClient(client.ClientID);
                                    _menuWindow.MainFrame.Navigate(profileClientPage);
                                    Closed += Window_Closed;
                                    Close(); // Close QRCodeWin after navigating
                                }
                                else
                                {
                                    MessageBox.Show("Клиент с данным номером карты не найден."); // Обновленное сообщение
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обработки кадра: {ex.Message}");
            }
        }
        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopScanButton_Click(object sender, RoutedEventArgs e)
        {
            Closed += Window_Closed;
            Close();
        }
    }
}