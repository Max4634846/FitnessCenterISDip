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
using System.Data.Entity;

namespace FitnessCenterIS.View.Windows
{
    public partial class QRCodeWindow : Window
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private BarcodeReader barcodeReader;
        private List<ClientsCollection> _clients;
        private bool _isScanning = false;
        private MenuWindow _menuWindow;
        private string lastQRCode = string.Empty;
        private bool isScanFinished = false;
        private int _currentUserRole; // Роль текущего пользователя
        private int _currentUserId; // ID текущего пользователя

        public event Action<string> QRCodeScanned;

        public QRCodeWindow(List<ClientsCollection> clients, MenuWindow menuWindow, int currentUserRole, int currentUserId = 0)
        {
            InitializeComponent();
            barcodeReader = new BarcodeReader();
            _clients = clients;
            _menuWindow = menuWindow;
            _currentUserRole = currentUserRole; // Сохраняем роль пользователя
            _currentUserId = currentUserId; // Сохраняем ID пользователя
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                {
                    MessageBox.Show("Камера не найдена.");
                    return;
                }

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

                        // Если QR-код распознан, проверяем разрешения и затем ищем в базе данных
                        Dispatcher.Invoke(() =>
                        {
                            using (var context = new BDFitnessClubDipEntities())
                            {
                                // Проверяем, принадлежит ли карта администратору
                                var restrictedStaff = context.Staffs
                                    .Include(s => s.Persons)
                                    .Include(s => s.Roles)
                                    .FirstOrDefault(s => s.Persons.NumberCard == result.Text &&
                                                (s.Roles.Name == "Администратор стойки" || s.Roles.Name == "Системный администратор"));

                                // Если карта принадлежит администратору и текущий пользователь - Администратор стойки
                                if (restrictedStaff != null && IsCurrentUserAdminDesk())
                                {
                                    MessageBox.Show("У вас недостаточно прав для просмотра профиля этого сотрудника.",
                                        "Ограничение доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    lastQRCode = ""; // Сброс QR-кода для повторной попытки
                                    return;
                                }

                                // Если прошли проверку, выполняем стандартный поиск
                                var client = context.Clients
                                    .Include(c => c.Persons)
                                    .FirstOrDefault(c => c.Persons.NumberCard == result.Text);

                                var staff = context.Staffs
                                    .Include(s => s.Persons)
                                    .FirstOrDefault(s => s.Persons.NumberCard == result.Text);

                                if (client != null)
                                {
                                    ProfileClient profileClientPage = new ProfileClient(client.ClientID);
                                    _menuWindow.MainFrame.Navigate(profileClientPage);
                                    Closed += Window_Closed;
                                    Close();
                                }
                                else if (staff != null)
                                {
                                    ProfileStaff profileStaffPage = new ProfileStaff(staff.StaffID);
                                    _menuWindow.MainFrame.Navigate(profileStaffPage);
                                    Closed += Window_Closed;
                                    Close();
                                }
                                else
                                {
                                    MessageBox.Show("Профиль с данным номером карты не найден.");
                                    lastQRCode = ""; // Сброс последнего QR-кода для повторной попытки
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

        // Проверка, является ли текущий пользователь Администратором стойки
        private bool IsCurrentUserAdminDesk()
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                // Получаем ID роли Администратора стойки
                var adminDeskRole = context.Roles.FirstOrDefault(r => r.Name == "Администратор стойки");
                if (adminDeskRole == null)
                    return false;

                return _currentUserRole == adminDeskRole.RoleID;
            }
        }

        // Метод проверки ограничения доступа
        private bool IsAdminRoleRestricted(int currentUserRoleId, int targetUserRoleId)
        {
            // Получаем ID ролей из базы данных, если они еще не известны
            int adminDeskRoleId = GetRoleId("Администратор стойки");
            int sysAdminRoleId = GetRoleId("Системный администратор");

            // Если текущий пользователь - Администратор стойки, а целевой сотрудник - Админ стойки или Системный админ
            return currentUserRoleId == adminDeskRoleId &&
                   (targetUserRoleId == adminDeskRoleId || targetUserRoleId == sysAdminRoleId);
        }

        // Получение ID роли по названию
        private int GetRoleId(string roleName)
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                var role = context.Roles.FirstOrDefault(r => r.Name == roleName);
                return role?.RoleID ?? 0;
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