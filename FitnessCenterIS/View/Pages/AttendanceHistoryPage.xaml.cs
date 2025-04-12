using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Pages
{
    public partial class AttendanceHistoryPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private ObservableCollection<AttendanceViewModel> _attendances;

        public AttendanceHistoryPage()
        {
            InitializeComponent();
            Loaded += AttendanceHistoryPage_Loaded;
        }

        private void AttendanceHistoryPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAttendanceData();
        }

        private void LoadAttendanceData()
        {
            try
            {
                var query = _dbContext.Attendances
                    .Include(a => a.Clients.Persons)
                    .Include(a => a.Staffs.Persons)
                    .Include(a => a.Lockers)
                    .AsQueryable();

                // Применяем фильтры
                if (StartDatePicker.SelectedDate != null)
                    query = query.Where(a => a.EntryDateTime >= StartDatePicker.SelectedDate);

                if (EndDatePicker.SelectedDate != null)
                    query = query.Where(a => a.EntryDateTime <= EndDatePicker.SelectedDate);

                if (!string.IsNullOrWhiteSpace(SearchTextBox.Text))
                {
                    var searchText = SearchTextBox.Text.ToLower();
                    query = query.Where(a =>
                        (a.Clients != null &&
                        (a.Clients.Persons.Surname + " " + a.Clients.Persons.Name + " " + a.Clients.Persons.MiddleName)
                            .ToLower().Contains(searchText)) ||
                        (a.Staffs != null &&
                        (a.Staffs.Persons.Surname + " " + a.Staffs.Persons.Name + " " + a.Staffs.Persons.MiddleName)
                            .ToLower().Contains(searchText)));
                }

                _attendances = new ObservableCollection<AttendanceViewModel>(
                    query.ToList().Select(a => new AttendanceViewModel(a)));

                AttendanceDataGrid.ItemsSource = _attendances;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadAttendanceData();
        }

        public class AttendanceViewModel
        {
            public string FullName { get; set; }
            public string UserType { get; set; }
            public DateTime? EntryDateTime { get; set; }
            public DateTime? ExitDateTime { get; set; }
            public string LockerNumber { get; set; }

            public AttendanceViewModel(Attendances attendance)
            {
                EntryDateTime = attendance.EntryDateTime;
                ExitDateTime = attendance.ExitDateTime;
                LockerNumber = attendance.Lockers?.KeyNumber?.ToString() ?? "N/A";

                if (attendance.ClientID != null)
                {
                    var client = attendance.Clients;
                    FullName = $"{client.Persons.Surname} {client.Persons.Name} {client.Persons.MiddleName}";
                    UserType = "Клиент";
                }
                else if (attendance.StaffID != null)
                {
                    var staff = attendance.Staffs;
                    FullName = $"{staff.Persons.Surname} {staff.Persons.Name} {staff.Persons.MiddleName}";
                    UserType = "Сотрудник";
                }
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadAttendanceData();
        }
    }
}
