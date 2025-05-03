using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FitnessCenterIS.View.Pages
{
    public partial class UserPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private ObservableCollection<UsersCollection> _userData;

        public UserPage()
        {
            InitializeComponent();
            LoadUserData();
        }

        // Класс для отображения информации о пользователях в DataGrid
        public class UsersCollection
        {
            public int UserID { get; set; }
            public int? StaffID { get; set; }  // Nullable int
            public string Surname { get; set; }
            public string Name { get; set; }
            public string MiddleName { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
        }

        private void LoadUserData()
        {
            _userData = new ObservableCollection<UsersCollection>(
                _dbContext.Users
                    .Include(u => u.Staffs)
                    .Include(u => u.Staffs.Persons)
                    .Select(u => new UsersCollection
                    {
                        UserID = u.UserID,
                        StaffID = u.StaffID,
                        Surname = u.Staffs.Persons.Surname,
                        Name = u.Staffs.Persons.Name,
                        MiddleName = u.Staffs.Persons.MiddleName,
                        Login = u.Login,
                        Password = u.Password
                    })
            );

            BDUsers.ItemsSource = _userData;
        }

        private void OpenEditWindow_Click(object sender, RoutedEventArgs e)
        {
            if (BDUsers.SelectedItem is UsersCollection selectedUser)
            {
                var window = new AddEditUserWindow(selectedUser.UserID);
                if (window.ShowDialog() == true)
                    LoadUserData();
            }
            else
            {
                MessageBox.Show("Выберите пользователя для редактирования");
            }
        }

        private void AddNewUser_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditUserWindow();
            if (window.ShowDialog() == true)
                LoadUserData();
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            var selectedUser = BDUsers.SelectedItem as UsersCollection;
            if (selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для удаления");
                return;
            }

            if (MessageBox.Show("Удалить выбранного пользователя?", "Подтверждение",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var user = context.Users.Find(selectedUser.UserID);
                    if (user != null)
                    {
                        context.Users.Remove(user);
                        context.SaveChanges();
                        LoadUserData();
                    }
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower();
            var filtered = _userData.Where(u =>
                u.Surname.ToLower().Contains(searchText) ||
                u.Name.ToLower().Contains(searchText) ||
                u.MiddleName.ToLower().Contains(searchText) ||
                u.Login.ToLower().Contains(searchText)
            );
            BDUsers.ItemsSource = new ObservableCollection<UsersCollection>(filtered);
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Проверяем, является ли источник события DataGridRow
            if (sender is DataGridRow row && row.Item is UsersCollection selectedRow)
            {
                OpenUserProfile(selectedRow);
            }
            // Проверяем, если это DataGrid, берем выбранный элемент напрямую
            else if (sender is DataGrid && BDUsers.SelectedItem is UsersCollection selectedGrid)
            {
                OpenUserProfile(selectedGrid);
            }
        }

        // Вспомогательный метод для открытия профиля сотрудника пользователя
        private void OpenUserProfile(UsersCollection user)
        {
            try
            {
                // Проверяем, что StaffID имеет значение, так как это nullable тип
                if (!user.StaffID.HasValue || user.StaffID.Value <= 0)
                {
                    MessageBox.Show("Некорректный ID сотрудника", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int staffId = user.StaffID.Value; // Безопасное получение значения
                // Открываем профиль сотрудника
                ProfileStaff profilePage = new ProfileStaff(staffId);
                this.NavigationService?.Navigate(profilePage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии профиля: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BDUsers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row != null)
            {
                e.Row.MouseDoubleClick += DataGridRow_MouseDoubleClick;
            }
        }

        private void BDUsers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}