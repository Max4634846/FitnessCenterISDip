using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for ProfileStaff.xaml
    /// </summary>
    public partial class ProfileStaff : Page
    {
        private int _staffId;
        private Staffs _currentStaff;
        public ProfileStaff(int staffId)
        {
            InitializeComponent();
            _staffId = staffId;
        }

        private void StaffProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStaffData();
        }
        private void LoadStaffData()
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                _currentStaff = context.Staffs.FirstOrDefault(c => c.StaffID == _staffId);
                if (_currentStaff != null)
                {
                    // Загрузка основной информации
                    ClientFullName.Text = $"{_currentStaff.Persons.Surname} {_currentStaff.Persons.Name} {_currentStaff.Persons.MiddleName}";
                    ClientDateOfBirth.Text = _currentStaff.Persons.DateOfBirth?.ToString("dd.MM.yyyy");
                    ClientGender.Text = _currentStaff.Persons.Gender;
                    ClientEmail.Text = _currentStaff.Persons.Email;
                    ClientPhoneNumber.Text = _currentStaff.Persons.PhoneNumber;
                    ClientAddress.Text = _currentStaff.Persons.Address;
                    ClientNotesTextBox.Text = string.IsNullOrEmpty(_currentStaff.Persons.Notes)
                        ? "Заметок о клиенте нет"
                        : _currentStaff.Persons.Notes;

                    // Установка цвета статуса клиента
                    SetStatusColor(_currentStaff.Roles.Name);

                    // Загрузка фотографии
                    LoadStaffImage(_currentStaff.Persons.ImagePerson);

                    // Загрузка QR-кода
                    LoadQRCode(_currentStaff.Persons.QRCode);

                    // Загрузка дополнительной информации о клиенте
                    ClientIDTextBlock.Text = _currentStaff.StaffID.ToString();
                    ClientCardNumber.Text = _currentStaff.Persons.NumberCard;
                    ClientStatus.Text = _currentStaff.Roles.Name;

                    // Загрузка абонементов
                    LoadSeasonTickets(context);

                    // Загрузка истории посещений
                    LoadVisitHistory(context);

                }
                else
                {
                    MessageBox.Show($"Клиент с ID {_staffId} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Возврат на предыдущую страницу
                    NavigationService?.GoBack();
                }
            }
        }
        private void SetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status))
                return;

            switch (status.ToLower())
            {
                case "активен":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ecc71"));
                    break;
                case "заблокирован":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
                    break;
                case "приостановлен":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12"));
                    break;
                default:
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
                    break;
            }
        }
        private void LoadStaffImage(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ClientImage.ImageSource = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                    // Установка изображения по умолчанию
                    ClientImage.ImageSource = new BitmapImage(new Uri("C:\\Users\\ultra\\source\\repos\\FitnessCenterIS\\FitnessCenterIS\\Resource\\NewPerson.jpg", UriKind.Relative));
                }
            }
            else
            {
                // Установка изображения по умолчанию
                ClientImage.ImageSource = new BitmapImage(new Uri("C:\\Users\\ultra\\source\\repos\\FitnessCenterIS\\FitnessCenterIS\\Resource\\NewPerson.jpg", UriKind.Relative));
            }
        }

        private void LoadQRCode(string qrCodePath)
        {
            if (!string.IsNullOrEmpty(qrCodePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(qrCodePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ClientQRCodeImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки QR-кода: {ex.Message}");
                    // Установка QR-кода по умолчанию или генерация нового
                    GenerateQRCode();
                }
            }
            else
            {
                // Генерация QR-кода, если он отсутствует
                GenerateQRCode();
            }
        }
        private void GenerateQRCode()
        {
            ClientQRCodeImage.Source = new BitmapImage(new Uri("/Resources/default_qrcode.png", UriKind.Relative));
        }
        private void LoadSeasonTickets(BDFitnessClubDipEntities context)
        {
            //var seasonTickets = context.Sales
            //    .Where(s => s.Seasontickets != null &&
            //                s.Seasontickets.SeasonticketClients.Any(sc => sc.cl == _currentClient.ClientID))
            //    .Select(s => new
            //    {
            //        s.SaleID,
            //        s.SaleDateTime,
            //        s.RemainingVisits,
            //        s.StatusSale,
            //        s.PriceSold,
            //        s.Seasontickets.Name,
            //        s.StartDateTime,
            //        s.EndDateTime
            //    })
            //    .ToList();

            //ClientSeasonTicketsGrid.ItemsSource = seasonTickets;
        }
        private void LoadVisitHistory(BDFitnessClubDipEntities context)
        {
            var visits = context.Attendances
                .Where(v => v.StaffID == _currentStaff.StaffID)
                .OrderByDescending(v => v.EntryDateTime)
                .Select(v => new
                {
                    v.EntryDateTime,
                    v.ExitDateTime,
                    KeyNumber = v.Lockers != null ? v.Lockers.KeyNumber : null,
                    DurationMinutes = v.ExitDateTime.HasValue && v.EntryDateTime.HasValue
                        ? System.Data.Entity.SqlServer.SqlFunctions.DateDiff("minute", v.EntryDateTime, v.ExitDateTime)
                        : null
                })
                .ToList()
                .Select(v => new
                {
                    v.EntryDateTime,
                    v.ExitDateTime,
                    v.KeyNumber,
                    Duration = v.DurationMinutes.HasValue
                        ? TimeSpan.FromMinutes(v.DurationMinutes.Value)
                        : (TimeSpan?)null,
                    FormattedDuration = v.DurationMinutes.HasValue
                        ? string.Format("{0:hh\\:mm}", TimeSpan.FromMinutes(v.DurationMinutes.Value))
                        : "Не завершено"
                })
                .ToList();

            ClientAttendancesGrid.ItemsSource = visits;
        }

        private void ViewSeasonTicketDetails_Click(object sender, RoutedEventArgs e)
        {

        }

        private void FreezeSeasonTicket_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
