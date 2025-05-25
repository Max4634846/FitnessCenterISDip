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
                _currentStaff = context.Staffs
                    .Include("Persons")
                    .Include("Roles")
                    .FirstOrDefault(s => s.StaffID == _staffId);

                if (_currentStaff != null)
                {
                    // Загрузка основной информации
                    StaffFullName.Text = $"{_currentStaff.Persons.Surname} {_currentStaff.Persons.Name} {_currentStaff.Persons.MiddleName}";
                    StaffDateOfBirth.Text = _currentStaff.Persons.DateOfBirth?.ToString("dd.MM.yyyy") ?? "Не указана";
                    StaffGender.Text = _currentStaff.Persons.Gender ?? "Не указан";
                    StaffEmail.Text = _currentStaff.Persons.Email ?? "Не указан";
                    StaffPhoneNumber.Text = _currentStaff.Persons.PhoneNumber ?? "Не указан";
                    StaffAddress.Text = _currentStaff.Persons.Address ?? "Не указан";
                    StaffNotesTextBox.Text = string.IsNullOrEmpty(_currentStaff.Persons.Notes)
                        ? "Заметок о сотруднике нет"
                        : _currentStaff.Persons.Notes;

                    // Информация о работе
                    StaffIDTextBlock.Text = _currentStaff.StaffID.ToString();
                    StaffCardNumber.Text = _currentStaff.Persons.NumberCard ?? "Не выдана";
                    StaffRole.Text = _currentStaff.Roles?.Name ?? "Не указана";
                    StaffRoleDetail.Text = _currentStaff.Roles?.Name ?? "Не указана";
                    StaffRoleDescription.Text = _currentStaff.Roles?.Description ?? "";

                    // Дата найма
                    if (!string.IsNullOrEmpty(_currentStaff.HireDate))
                    {
                        if (DateTime.TryParse(_currentStaff.HireDate, out DateTime hireDate))
                        {
                            StaffHireDate.Text = hireDate.ToString("dd.MM.yyyy");
                            StaffHireDateDetail.Text = hireDate.ToString("dd.MM.yyyy");

                            var workExperience = DateTime.Now - hireDate;
                            var years = (int)(workExperience.TotalDays / 365.25);
                            var months = (int)((workExperience.TotalDays % 365.25) / 30.44);

                            if (years > 0)
                                StaffWorkExperience.Text = $"Стаж: {years} лет {months} месяцев";
                            else
                                StaffWorkExperience.Text = $"Стаж: {months} месяцев";
                        }
                        else
                        {
                            StaffHireDate.Text = _currentStaff.HireDate;
                            StaffHireDateDetail.Text = _currentStaff.HireDate;
                            StaffWorkExperience.Text = "";
                        }
                    }
                    else
                    {
                        StaffHireDate.Text = "Не указана";
                        StaffHireDateDetail.Text = "Не указана";
                        StaffWorkExperience.Text = "";
                    }

                    // Загрузка фотографии
                    LoadStaffImage(_currentStaff.Persons.ImagePerson);

                    // Загрузка QR-кода
                    LoadQRCode(_currentStaff.Persons.QRCode);

                    // Загрузка закрепленных услуг
                    LoadStaffServices(context);

                    // Загрузка истории посещений
                    LoadVisitHistory(context);
                }
                else
                {
                    MessageBox.Show($"Сотрудник с ID {_staffId} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    NavigationService?.GoBack();
                }
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
                    bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    StaffImage.ImageSource = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                    SetDefaultImage();
                }
            }
            else
            {
                SetDefaultImage();
            }
        }

        private void SetDefaultImage()
        {
            try
            {
                StaffImage.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Resource/NewPerson.jpg"));
            }
            catch
            {
                StaffImage.ImageSource = new BitmapImage(new Uri("C:\\Users\\ultra\\source\\repos\\FitnessCenterIS\\FitnessCenterIS\\Resource\\NewPerson.jpg", UriKind.Relative));
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
                    bitmap.UriSource = new Uri(qrCodePath, UriKind.RelativeOrAbsolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    StaffQRCodeImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки QR-кода: {ex.Message}");
                    GenerateDefaultQRCode();
                }
            }
            else
            {
                GenerateDefaultQRCode();
            }
        }

        private void GenerateDefaultQRCode()
        {
            try
            {
                StaffQRCodeImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resource/default_qrcode.png"));
            }
            catch
            {
                // Создаем простой placeholder для QR-кода
                var rect = new Rectangle
                {
                    Width = 80,
                    Height = 80,
                    Fill = new SolidColorBrush(Colors.LightGray),
                    Stroke = new SolidColorBrush(Colors.Gray),
                    StrokeThickness = 1
                };
            }
        }

        private void LoadStaffServices(BDFitnessClubDipEntities context)
        {
            var staffServices = context.ServiceTrainer
                .Where(st => st.TrainerID == _currentStaff.StaffID)
                .Select(st => new
                {
                    ServiceName = st.Services.Name,
                    ServiceType = st.Services.ServiceTypes.Name,
                    ServiceClassification = st.Services.ServiceClassifications.Name,
                    Price = st.Services.Price,
                    StatusService = st.Services.StatusService,
                    Description = st.Services.Description
                })
                .ToList();

            StaffServicesGrid.ItemsSource = staffServices;
        }

        private void LoadVisitHistory(BDFitnessClubDipEntities context)
        {
            var visits = context.Attendances
                .Where(v => v.StaffID == _currentStaff.StaffID)
                .OrderByDescending(v => v.EntryDateTime)
                .Take(50) // Ограничиваем последними 50 записями
                .Select(v => new
                {
                    v.EntryDateTime,
                    v.ExitDateTime,
                    KeyNumber = v.Lockers != null ? v.Lockers.KeyNumber : "Не использовался",
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

            StaffAttendancesGrid.ItemsSource = visits;
        }
    }
}
